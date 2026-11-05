namespace AngleSharp.Io.Dom
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// What is kept: the databases of all origins, in memory. Whatever changes it does so under
    /// the lock of the database, as the windows sharing it have event loops of their own.
    /// </summary>
    sealed class IdbStorage
    {
        private readonly Dictionary<String, IdbDatabaseData> _databases = new Dictionary<String, IdbDatabaseData>(StringComparer.Ordinal);

        private static String GetKey(String origin, String name) => String.Concat(origin, "\n", name);

        public IdbDatabaseData GetOrCreate(String origin, String name)
        {
            lock (_databases)
            {
                var key = GetKey(origin, name);

                if (!_databases.TryGetValue(key, out var database))
                {
                    database = new IdbDatabaseData(name, origin);
                    _databases[key] = database;
                }

                return database;
            }
        }

        public IdbDatabaseData[] GetAll(String origin)
        {
            lock (_databases)
            {
                var result = new List<IdbDatabaseData>();

                foreach (var database in _databases.Values)
                {
                    if (database.Origin == origin && database.Version > 0)
                    {
                        result.Add(database);
                    }
                }

                return result.ToArray();
            }
        }
    }

    /// <summary>
    /// A database with its connections, its transactions and who waits to connect.
    /// </summary>
    sealed class IdbDatabaseData
    {
        private readonly Queue<KeyValuePair<IdbScheduler, Action<Action>>> _connecting = new Queue<KeyValuePair<IdbScheduler, Action<Action>>>();
        private readonly List<IdbTransaction> _transactions = new List<IdbTransaction>();
        private readonly List<IdbDatabase> _connections = new List<IdbDatabase>();
        private Boolean _isConnecting;
        private Action _whenClosed;

        public IdbDatabaseData(String name, String origin)
        {
            Name = name;
            Origin = origin;
        }

        public String Name { get; }

        public String Origin { get; }

        public Int64 Version { get; set; }

        public Dictionary<String, IdbStoreData> Stores { get; } = new Dictionary<String, IdbStoreData>(StringComparer.Ordinal);

        /// <summary>
        /// Forgets everything. The object stays, as requests waiting for it know it already.
        /// </summary>
        public void Clear()
        {
            lock (this)
            {
                Stores.Clear();
                Version = 0;
            }
        }

        /// <summary>
        /// Opening and deleting are done one after the other for a database.
        /// </summary>
        public void EnqueueConnecting(IdbScheduler scheduler, Action<Action> step)
        {
            lock (this)
            {
                _connecting.Enqueue(new KeyValuePair<IdbScheduler, Action<Action>>(scheduler, step));
                ConnectNext();
            }
        }

        public void Add(IdbDatabase connection)
        {
            lock (this)
            {
                _connections.Add(connection);
            }
        }

        public IdbDatabase[] GetOpenConnections(IdbDatabase except)
        {
            lock (this)
            {
                _connections.RemoveAll(m => m.IsStale);
                return _connections.FindAll(m => !ReferenceEquals(m, except)).ToArray();
            }
        }

        public void Remove(IdbDatabase connection)
        {
            Action whenClosed = null;

            lock (this)
            {
                _connections.Remove(connection);

                if (_whenClosed != null && _connections.Count <= 1)
                {
                    whenClosed = _whenClosed;
                }
            }

            whenClosed?.Invoke();
        }

        /// <summary>
        /// Runs the action once the other connections are gone, or right now if they are.
        /// </summary>
        public void WhenOthersClosed(IdbDatabase except, Action action)
        {
            lock (this)
            {
                if (GetOpenConnections(except).Length > 0)
                {
                    var once = false;
                    _whenClosed = () =>
                    {
                        lock (this)
                        {
                            if (once || GetOpenConnections(except).Length > 0)
                            {
                                return;
                            }

                            once = true;
                            _whenClosed = null;
                        }

                        action.Invoke();
                    };
                    return;
                }
            }

            action.Invoke();
        }

        public void Add(IdbTransaction transaction)
        {
            lock (this)
            {
                _transactions.Add(transaction);
                StartWaitingTransactions();
            }
        }

        public void Remove(IdbTransaction transaction)
        {
            lock (this)
            {
                _transactions.Remove(transaction);
                StartWaitingTransactions();
            }
        }

        //  Each runs when all that came before it and shares a store with it are done, unless
        //  they only read together.
        private void StartWaitingTransactions()
        {
            for (var i = 0; i < _transactions.Count; i++)
            {
                var candidate = _transactions[i];

                if (candidate.IsStarted)
                {
                    continue;
                }

                var isFree = true;

                for (var j = 0; j < i && isFree; j++)
                {
                    isFree = !_transactions[j].ConflictsWith(candidate);
                }

                if (isFree)
                {
                    candidate.Start();
                }
            }
        }

        private void ConnectNext()
        {
            if (!_isConnecting && _connecting.Count > 0)
            {
                var next = _connecting.Dequeue();
                _isConnecting = true;
                next.Key.Post(() => next.Value.Invoke(() =>
                {
                    lock (this)
                    {
                        _isConnecting = false;
                        ConnectNext();
                    }
                }));
            }
        }
    }

    /// <summary>
    /// An object store and its indexes, with the key generator.
    /// </summary>
    sealed class IdbStoreData
    {
        public IdbStoreData(String name, Object keyPath, Boolean autoIncrement)
        {
            Name = name;
            KeyPath = keyPath;
            AutoIncrement = autoIncrement;
        }

        public String Name { get; set; }

        public Object KeyPath { get; }

        public Boolean AutoIncrement { get; }

        public Double Generator { get; set; } = 1;

        public IdbRecordList Records { get; } = new IdbRecordList();

        public Dictionary<String, IdbIndexData> Indexes { get; } = new Dictionary<String, IdbIndexData>(StringComparer.Ordinal);
    }

    /// <summary>
    /// An index, whose records have the primary key of the record of the store.
    /// </summary>
    sealed class IdbIndexData
    {
        public IdbIndexData(String name, Object keyPath, Boolean unique, Boolean multiEntry)
        {
            Name = name;
            KeyPath = keyPath;
            Unique = unique;
            MultiEntry = multiEntry;
        }

        public String Name { get; }

        public Object KeyPath { get; }

        public Boolean Unique { get; }

        public Boolean MultiEntry { get; }

        public IdbRecordList Records { get; } = new IdbRecordList();

        /// <summary>
        /// Gets the keys a value of the store has in this index, none if it has no valid one.
        /// </summary>
        public Object[] GetKeys(Object value)
        {
            if (IdbKeys.TryEvaluate(value, KeyPath, out var result))
            {
                if (MultiEntry && result is Object[] items)
                {
                    var keys = new List<Object>();

                    foreach (var item in items)
                    {
                        if (IdbKeys.TryToKey(item, out var key) && !keys.Exists(m => IdbKeys.Compare(m, key) == 0))
                        {
                            keys.Add(key);
                        }
                    }

                    return keys.ToArray();
                }

                if (IdbKeys.TryToKey(result, out var single))
                {
                    return new[] { single };
                }
            }

            return Array.Empty<Object>();
        }
    }

    /// <summary>
    /// A record: in a store its key is the primary key, in an index it has no value.
    /// </summary>
    sealed class IdbRecord
    {
        public IdbRecord(Object key, Object primaryKey, Object value)
        {
            Key = key;
            PrimaryKey = primaryKey;
            Value = value;
        }

        public Object Key { get; }

        public Object PrimaryKey { get; }

        public Object Value { get; }
    }

    /// <summary>
    /// Records ordered by their key and then their primary key.
    /// </summary>
    sealed class IdbRecordList
    {
        private readonly List<IdbRecord> _records = new List<IdbRecord>();

        public Int32 Count => _records.Count;

        public IdbRecord this[Int32 index] => _records[index];

        /// <summary>
        /// Gets the position of the first record at or after the key (and primary key, if given).
        /// </summary>
        public Int32 FindFirst(Object key, Object primaryKey = null) => Search(key, primaryKey, false);

        /// <summary>
        /// Gets the position of the first record after the key (and primary key, if given).
        /// </summary>
        public Int32 FindAfter(Object key, Object primaryKey = null) => Search(key, primaryKey, true);

        public IdbRecord Find(Object key, Object primaryKey)
        {
            var index = FindFirst(key, primaryKey);
            return index < _records.Count && IsAt(_records[index], key, primaryKey) ? _records[index] : null;
        }

        public void Insert(IdbRecord record) => _records.Insert(FindFirst(record.Key, record.PrimaryKey), record);

        public Boolean Remove(Object key, Object primaryKey)
        {
            var index = FindFirst(key, primaryKey);

            if (index < _records.Count && IsAt(_records[index], key, primaryKey))
            {
                _records.RemoveAt(index);
                return true;
            }

            return false;
        }

        public void Clear() => _records.Clear();

        public IdbRecord[] ToArray() => _records.ToArray();

        /// <summary>
        /// Gets the positions of the records in a range: the first one, and the one after the last.
        /// </summary>
        public void GetBounds(IdbKeyRange range, out Int32 start, out Int32 end)
        {
            start = range?.Lower == null ? 0 : range.IsLowerOpen ? FindAfter(range.Lower) : FindFirst(range.Lower);
            end = range?.Upper == null ? _records.Count : range.IsUpperOpen ? FindFirst(range.Upper) : FindAfter(range.Upper);
        }

        private static Boolean IsAt(IdbRecord record, Object key, Object primaryKey) =>
            IdbKeys.Compare(record.Key, key) == 0 && IdbKeys.Compare(record.PrimaryKey, primaryKey) == 0;

        private Int32 Search(Object key, Object primaryKey, Boolean after)
        {
            var low = 0;
            var high = _records.Count;

            while (low < high)
            {
                var middle = low + (high - low) / 2;
                var record = _records[middle];
                var order = IdbKeys.Compare(record.Key, key);

                if (order == 0 && primaryKey != null)
                {
                    order = IdbKeys.Compare(record.PrimaryKey, primaryKey);
                }

                if (order < 0 || after && order == 0)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }
    }
}
