namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the options of creating an index.
    /// </summary>
    [DomName("IDBIndexParameters")]
    public sealed class IdbIndexParameters
    {
        /// <summary>
        /// Gets or sets if a key can only be there once.
        /// </summary>
        [DomName("unique")]
        public Boolean? Unique { get; set; }

        /// <summary>
        /// Gets or sets if an array gives a record for each of its items.
        /// </summary>
        [DomName("multiEntry")]
        public Boolean? MultiEntry { get; set; }
    }

    /// <summary>
    /// Represents an object store as seen by a transaction.
    /// </summary>
    [DomName("IDBObjectStore")]
    [DomExposed("Window")]
    public sealed class IdbObjectStore
    {
        private const String ConstraintError = "ConstraintError";
        private const String ReadOnlyError = "ReadOnlyError";

        private readonly Dictionary<String, IdbIndex> _indexes = new Dictionary<String, IdbIndex>(StringComparer.Ordinal);
        private readonly IdbStoreData _data;
        private Boolean _isDeleted;

        internal IdbObjectStore(IdbTransaction transaction, IdbStoreData data)
        {
            Transaction = transaction;
            _data = data;
        }

        /// <summary>
        /// Gets the name of the store.
        /// </summary>
        [DomName("name")]
        public String Name => _data.Name;

        /// <summary>
        /// Gets where a value has its key, if it has it.
        /// </summary>
        [DomName("keyPath")]
        public Object KeyPath => _data.KeyPath;

        /// <summary>
        /// Gets if keys are generated.
        /// </summary>
        [DomName("autoIncrement")]
        public Boolean IsAutoIncrement => _data.AutoIncrement;

        /// <summary>
        /// Gets the transaction.
        /// </summary>
        [DomName("transaction")]
        public IdbTransaction Transaction { get; }

        /// <summary>
        /// Gets the names of the indexes, sorted.
        /// </summary>
        [DomName("indexNames")]
        public IStringList IndexNames
        {
            get
            {
                lock (Transaction.Data)
                {
                    return new IdbStringList(_data.Indexes.Keys);
                }
            }
        }

        internal IdbStoreData Data => _data;

        private IWindow Window => Transaction.Window;

        /// <summary>
        /// Requests a record to be added, which fails if its key is taken.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key, unless the value has it or it is generated.</param>
        /// <returns>The request, whose result is the key.</returns>
        [DomName("add")]
        public IdbRequest Add(Object value = null, Object key = null) => Store(value, key, true);

        /// <summary>
        /// Requests a record to be added or replaced.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="key">The key, unless the value has it or it is generated.</param>
        /// <returns>The request, whose result is the key.</returns>
        [DomName("put")]
        public IdbRequest Put(Object value = null, Object key = null) => Store(value, key, false);

        /// <summary>
        /// Requests the records of a key or range to be deleted.
        /// </summary>
        /// <param name="query">The key or range.</param>
        /// <returns>The request.</returns>
        [DomName("delete")]
        public IdbRequest Delete(Object query = null)
        {
            EnsureWritable();
            var range = RequireRange(query);
            return Transaction.Request(this, () =>
            {
                Transaction.AddUndo(RemoveRecords(range));
                return IdbFactory.GetUndefined(Window);
            });
        }

        /// <summary>
        /// Requests all records to be deleted.
        /// </summary>
        /// <returns>The request.</returns>
        [DomName("clear")]
        public IdbRequest Clear()
        {
            EnsureWritable();
            return Transaction.Request(this, () =>
            {
                Transaction.AddUndo(RemoveRecords(null));
                return IdbFactory.GetUndefined(Window);
            });
        }

        /// <summary>
        /// Requests the value of the first record of a key or range.
        /// </summary>
        /// <param name="query">The key or range.</param>
        /// <returns>The request, whose result is the value, if any.</returns>
        [DomName("get")]
        public IdbRequest Get(Object query = null)
        {
            EnsureActive();
            var range = RequireRange(query);
            return Transaction.Request(this, () =>
            {
                _data.Records.GetBounds(range, out var start, out var end);
                return start < end ? _data.Records[start].Value : IdbFactory.GetUndefined(Window);
            });
        }

        /// <summary>
        /// Requests the key of the first record of a key or range.
        /// </summary>
        /// <param name="query">The key or range.</param>
        /// <returns>The request, whose result is the key, if any.</returns>
        [DomName("getKey")]
        public IdbRequest GetKey(Object query = null)
        {
            EnsureActive();
            var range = RequireRange(query);
            return Transaction.Request(this, () =>
            {
                _data.Records.GetBounds(range, out var start, out var end);
                return start < end ? _data.Records[start].Key : IdbFactory.GetUndefined(Window);
            });
        }

        /// <summary>
        /// Requests the values of the records of a key or range.
        /// </summary>
        /// <param name="query">The key or range; by default all.</param>
        /// <param name="count">The most records to get.</param>
        /// <returns>The request, whose result is an array of the values.</returns>
        [DomName("getAll")]
        public IdbRequest GetAll(Object query = null, Double? count = null)
        {
            EnsureActive();
            var range = IdbKeyRange.From(Window, query);
            return Transaction.Request(this, () => Collect(_data.Records, range, count, m => m.Value));
        }

        /// <summary>
        /// Requests the keys of the records of a key or range.
        /// </summary>
        /// <param name="query">The key or range; by default all.</param>
        /// <param name="count">The most records to get.</param>
        /// <returns>The request, whose result is an array of the keys.</returns>
        [DomName("getAllKeys")]
        public IdbRequest GetAllKeys(Object query = null, Double? count = null)
        {
            EnsureActive();
            var range = IdbKeyRange.From(Window, query);
            return Transaction.Request(this, () => Collect(_data.Records, range, count, m => m.Key));
        }

        /// <summary>
        /// Requests the number of the records of a key or range.
        /// </summary>
        /// <param name="query">The key or range; by default all.</param>
        /// <returns>The request, whose result is the number.</returns>
        [DomName("count")]
        public IdbRequest Count(Object query = null)
        {
            EnsureActive();
            var range = IdbKeyRange.From(Window, query);
            return Transaction.Request(this, () =>
            {
                _data.Records.GetBounds(range, out var start, out var end);
                return (Double)Math.Max(end - start, 0);
            });
        }

        /// <summary>
        /// Requests a cursor over the records with their values.
        /// </summary>
        /// <param name="query">The key or range; by default all.</param>
        /// <param name="direction">The direction: next (the default), nextunique, prev or prevunique.</param>
        /// <returns>The request, whose result is the cursor, or null if there are no records.</returns>
        [DomName("openCursor")]
        public IdbRequest OpenCursor(Object query = null, String direction = null) => Open(query, direction, true);

        /// <summary>
        /// Requests a cursor over the keys of the records.
        /// </summary>
        /// <param name="query">The key or range; by default all.</param>
        /// <param name="direction">The direction: next (the default), nextunique, prev or prevunique.</param>
        /// <returns>The request, whose result is the cursor, or null if there are no records.</returns>
        [DomName("openKeyCursor")]
        public IdbRequest OpenKeyCursor(Object query = null, String direction = null) => Open(query, direction, false);

        /// <summary>
        /// Gets an index of the store.
        /// </summary>
        /// <param name="name">The name of the index.</param>
        /// <returns>The index.</returns>
        [DomName("index")]
        public IdbIndex Index(String name)
        {
            if (_isDeleted)
            {
                throw new DomException(DomError.InvalidState);
            }

            if (!_indexes.TryGetValue(name, out var index))
            {
                lock (Transaction.Data)
                {
                    if (!_data.Indexes.TryGetValue(name, out var data))
                    {
                        throw new DomException(DomError.NotFound);
                    }

                    index = new IdbIndex(this, data);
                }

                _indexes[name] = index;
            }

            return index;
        }

        /// <summary>
        /// Creates an index while the version of the database is changing.
        /// </summary>
        /// <param name="name">The name of the index.</param>
        /// <param name="keyPath">Where a value has the key of the index.</param>
        /// <param name="options">The options of the index.</param>
        /// <returns>The index.</returns>
        [DomName("createIndex")]
        public IdbIndex CreateIndex(String name, Object keyPath, IdbIndexParameters options = null)
        {
            EnsureUpgrade();
            var path = IdbKeys.ToKeyPath(IdbFactory.Clone(Window, keyPath));
            var multiEntry = options?.MultiEntry == true;

            if (path == null || multiEntry && path is String[])
            {
                throw new DomException(multiEntry ? DomError.InvalidAccess : DomError.Syntax);
            }

            lock (Transaction.Data)
            {
                if (_data.Indexes.ContainsKey(name))
                {
                    throw new DomException(ConstraintError);
                }

                var index = new IdbIndexData(name, path, options?.Unique == true, multiEntry);
                _data.Indexes[name] = index;
                Transaction.AddUndo(() => _data.Indexes.Remove(name));

                foreach (var record in _data.Records.ToArray())
                {
                    foreach (var key in index.GetKeys(record.Value))
                    {
                        if (index.Unique && index.Records.FindAfter(key) > index.Records.FindFirst(key))
                        {
                            //  the transaction ends for it, but creating the index was alright
                            Transaction.Database.Scheduler.Post(() => Transaction.Fail(new DomException(ConstraintError)));
                        }

                        index.Records.Insert(new IdbRecord(key, record.Key, null));
                    }
                }
            }

            return Index(name);
        }

        /// <summary>
        /// Deletes an index while the version of the database is changing.
        /// </summary>
        /// <param name="name">The name of the index.</param>
        [DomName("deleteIndex")]
        public void DeleteIndex(String name)
        {
            EnsureUpgrade();

            lock (Transaction.Data)
            {
                if (!_data.Indexes.TryGetValue(name, out var index))
                {
                    throw new DomException(DomError.NotFound);
                }

                _data.Indexes.Remove(name);
                _indexes.Remove(name);
                Transaction.AddUndo(() => _data.Indexes[name] = index);
            }
        }

        internal void MarkDeleted() => _isDeleted = true;

        internal void EnsureActive()
        {
            if (_isDeleted)
            {
                throw new DomException(DomError.InvalidState);
            }

            if (!Transaction.IsActive)
            {
                throw new DomException(IdbTransaction.InactiveError);
            }
        }

        internal void EnsureWritable()
        {
            EnsureActive();

            if (!Transaction.IsWritable)
            {
                throw new DomException(ReadOnlyError);
            }
        }

        internal IdbKeyRange RequireRange(Object query) =>
            IdbKeyRange.From(Window, query) ?? throw new DomException(IdbKeys.DataError);

        internal static Object[] Collect(IdbRecordList records, IdbKeyRange range, Double? count, Func<IdbRecord, Object> select)
        {
            records.GetBounds(range, out var start, out var end);
            var total = Math.Max(end - start, 0);

            if (count.HasValue && count.Value > 0 && count.Value < total)
            {
                total = (Int32)count.Value;
            }

            var result = new Object[total];

            for (var i = 0; i < total; i++)
            {
                result[i] = select.Invoke(records[start + i]);
            }

            return result;
        }

        /// <summary>
        /// Stores a value under a key the way a request or a cursor wants it.
        /// </summary>
        internal Object Write(Object value, Object key, Boolean noOverwrite)
        {
            var oldGenerator = _data.Generator;

            if (key == null)
            {
                if (_data.Generator > 9007199254740992.0)
                {
                    throw new DomException(ConstraintError);
                }

                key = _data.Generator;
                _data.Generator += 1;

                if (_data.KeyPath is String path)
                {
                    IdbKeys.Inject(value, path, key);
                }
            }
            else if (_data.AutoIncrement && key is Double number && number >= _data.Generator)
            {
                _data.Generator = Math.Min(Math.Floor(number) + 1, 9007199254740993.0);
            }

            try
            {
                var existing = _data.Records.Find(key, key);

                if (existing != null && noOverwrite)
                {
                    throw new DomException(ConstraintError);
                }

                var added = new List<KeyValuePair<IdbIndexData, Object>>();

                foreach (var index in _data.Indexes.Values)
                {
                    foreach (var indexKey in index.GetKeys(value))
                    {
                        if (index.Unique)
                        {
                            index.Records.GetBounds(IdbKeyRange.Only(null, indexKey), out var start, out var end);

                            for (var i = start; i < end; i++)
                            {
                                if (IdbKeys.Compare(index.Records[i].PrimaryKey, key) != 0)
                                {
                                    throw new DomException(ConstraintError);
                                }
                            }
                        }

                        added.Add(new KeyValuePair<IdbIndexData, Object>(index, indexKey));
                    }
                }

                var restore = existing != null ? RemoveRecord(existing) : null;
                _data.Records.Insert(new IdbRecord(key, key, value));

                foreach (var entry in added)
                {
                    entry.Key.Records.Insert(new IdbRecord(entry.Value, key, null));
                }

                var storedKey = key;
                Transaction.AddUndo(() =>
                {
                    _data.Generator = oldGenerator;
                    _data.Records.Remove(storedKey, storedKey);

                    foreach (var entry in added)
                    {
                        entry.Key.Records.Remove(entry.Value, storedKey);
                    }

                    restore?.Invoke();
                });
                return key;
            }
            catch (DomException)
            {
                _data.Generator = oldGenerator;
                throw;
            }
        }

        /// <summary>
        /// Removes the records of a range, giving what brings them back.
        /// </summary>
        internal Action RemoveRecords(IdbKeyRange range)
        {
            _data.Records.GetBounds(range, out var start, out var end);
            var restore = new List<Action>();

            for (var i = end - 1; i >= start; i--)
            {
                restore.Add(RemoveRecord(_data.Records[i]));
            }

            return () => restore.ForEach(m => m.Invoke());
        }

        private Action RemoveRecord(IdbRecord record)
        {
            _data.Records.Remove(record.Key, record.Key);
            var removed = new List<KeyValuePair<IdbIndexData, Object>>();

            foreach (var index in _data.Indexes.Values)
            {
                foreach (var indexKey in index.GetKeys(record.Value))
                {
                    if (index.Records.Remove(indexKey, record.Key))
                    {
                        removed.Add(new KeyValuePair<IdbIndexData, Object>(index, indexKey));
                    }
                }
            }

            return () =>
            {
                _data.Records.Insert(record);

                foreach (var entry in removed)
                {
                    entry.Key.Records.Insert(new IdbRecord(entry.Value, record.Key, null));
                }
            };
        }

        private IdbRequest Store(Object value, Object key, Boolean noOverwrite)
        {
            EnsureWritable();
            var hasKey = key != null;
            var clone = IdbFactory.Clone(Window, value);
            var given = hasKey ? IdbKeys.ToKey(IdbFactory.Clone(Window, key)) : null;

            if (_data.KeyPath != null)
            {
                if (hasKey)
                {
                    throw new DomException(IdbKeys.DataError);
                }

                if (IdbKeys.TryEvaluate(clone, _data.KeyPath, out var found))
                {
                    given = IdbKeys.ToKey(found);
                }
                else if (!_data.AutoIncrement)
                {
                    throw new DomException(IdbKeys.DataError);
                }
                else if (!(clone is IDictionary<String, Object>))
                {
                    throw new DomException(IdbKeys.DataError);
                }
            }
            else if (!hasKey && !_data.AutoIncrement)
            {
                throw new DomException(IdbKeys.DataError);
            }

            return Transaction.Request(this, () => Write(clone, given, noOverwrite));
        }

        private IdbRequest Open(Object query, String direction, Boolean withValue)
        {
            EnsureActive();
            var range = IdbKeyRange.From(Window, query);
            return IdbCursor.Open(this, null, range, direction, withValue);
        }

        private void EnsureUpgrade()
        {
            if (Transaction.Mode != IdbTransaction.VersionChange || _isDeleted)
            {
                throw new DomException(DomError.InvalidState);
            }

            if (!Transaction.IsActive)
            {
                throw new DomException(IdbTransaction.InactiveError);
            }
        }
    }

    /// <summary>
    /// Represents an index of an object store as seen by a transaction.
    /// </summary>
    [DomName("IDBIndex")]
    [DomExposed("Window")]
    public sealed class IdbIndex
    {
        private readonly IdbIndexData _data;

        internal IdbIndex(IdbObjectStore store, IdbIndexData data)
        {
            ObjectStore = store;
            _data = data;
        }

        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        [DomName("name")]
        public String Name => _data.Name;

        /// <summary>
        /// Gets the object store of the index.
        /// </summary>
        [DomName("objectStore")]
        public IdbObjectStore ObjectStore { get; }

        /// <summary>
        /// Gets where a value has the key of the index.
        /// </summary>
        [DomName("keyPath")]
        public Object KeyPath => _data.KeyPath;

        /// <summary>
        /// Gets if a key can only be there once.
        /// </summary>
        [DomName("unique")]
        public Boolean IsUnique => _data.Unique;

        /// <summary>
        /// Gets if an array gives a record for each of its items.
        /// </summary>
        [DomName("multiEntry")]
        public Boolean IsMultiEntry => _data.MultiEntry;

        internal IdbIndexData Data => _data;

        private IdbTransaction Transaction => ObjectStore.Transaction;

        private IWindow Window => Transaction.Window;

        /// <summary>
        /// Requests the value of the first record of a key or range.
        /// </summary>
        /// <param name="query">The key or range.</param>
        /// <returns>The request, whose result is the value, if any.</returns>
        [DomName("get")]
        public IdbRequest Get(Object query = null)
        {
            ObjectStore.EnsureActive();
            var range = ObjectStore.RequireRange(query);
            return Transaction.Request(this, () =>
            {
                _data.Records.GetBounds(range, out var start, out var end);
                return start < end ? GetValue(_data.Records[start]) : IdbFactory.GetUndefined(Window);
            });
        }

        /// <summary>
        /// Requests the primary key of the first record of a key or range.
        /// </summary>
        /// <param name="query">The key or range.</param>
        /// <returns>The request, whose result is the primary key, if any.</returns>
        [DomName("getKey")]
        public IdbRequest GetKey(Object query = null)
        {
            ObjectStore.EnsureActive();
            var range = ObjectStore.RequireRange(query);
            return Transaction.Request(this, () =>
            {
                _data.Records.GetBounds(range, out var start, out var end);
                return start < end ? _data.Records[start].PrimaryKey : IdbFactory.GetUndefined(Window);
            });
        }

        /// <summary>
        /// Requests the values of the records of a key or range.
        /// </summary>
        /// <param name="query">The key or range; by default all.</param>
        /// <param name="count">The most records to get.</param>
        /// <returns>The request, whose result is an array of the values.</returns>
        [DomName("getAll")]
        public IdbRequest GetAll(Object query = null, Double? count = null)
        {
            ObjectStore.EnsureActive();
            var range = IdbKeyRange.From(Window, query);
            return Transaction.Request(this, () => IdbObjectStore.Collect(_data.Records, range, count, GetValue));
        }

        /// <summary>
        /// Requests the primary keys of the records of a key or range.
        /// </summary>
        /// <param name="query">The key or range; by default all.</param>
        /// <param name="count">The most records to get.</param>
        /// <returns>The request, whose result is an array of the primary keys.</returns>
        [DomName("getAllKeys")]
        public IdbRequest GetAllKeys(Object query = null, Double? count = null)
        {
            ObjectStore.EnsureActive();
            var range = IdbKeyRange.From(Window, query);
            return Transaction.Request(this, () => IdbObjectStore.Collect(_data.Records, range, count, m => m.PrimaryKey));
        }

        /// <summary>
        /// Requests the number of the records of a key or range.
        /// </summary>
        /// <param name="query">The key or range; by default all.</param>
        /// <returns>The request, whose result is the number.</returns>
        [DomName("count")]
        public IdbRequest Count(Object query = null)
        {
            ObjectStore.EnsureActive();
            var range = IdbKeyRange.From(Window, query);
            return Transaction.Request(this, () =>
            {
                _data.Records.GetBounds(range, out var start, out var end);
                return (Double)Math.Max(end - start, 0);
            });
        }

        /// <summary>
        /// Requests a cursor over the records with their values.
        /// </summary>
        /// <param name="query">The key or range; by default all.</param>
        /// <param name="direction">The direction: next (the default), nextunique, prev or prevunique.</param>
        /// <returns>The request, whose result is the cursor, or null if there are no records.</returns>
        [DomName("openCursor")]
        public IdbRequest OpenCursor(Object query = null, String direction = null)
        {
            ObjectStore.EnsureActive();
            return IdbCursor.Open(ObjectStore, this, IdbKeyRange.From(Window, query), direction, true);
        }

        /// <summary>
        /// Requests a cursor over the keys of the records.
        /// </summary>
        /// <param name="query">The key or range; by default all.</param>
        /// <param name="direction">The direction: next (the default), nextunique, prev or prevunique.</param>
        /// <returns>The request, whose result is the cursor, or null if there are no records.</returns>
        [DomName("openKeyCursor")]
        public IdbRequest OpenKeyCursor(Object query = null, String direction = null)
        {
            ObjectStore.EnsureActive();
            return IdbCursor.Open(ObjectStore, this, IdbKeyRange.From(Window, query), direction, false);
        }

        private Object GetValue(IdbRecord record) => ObjectStore.Data.Records.Find(record.PrimaryKey, record.PrimaryKey)?.Value;
    }
}
