namespace AngleSharp.Io.IndexedDb
{
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.Storage;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the default IndexedDB provider factory with origin-dependent views.
    /// </summary>
    public class IndexedDbProviderFactory : IIndexedDbProviderFactory
    {
        private readonly ConditionalWeakTable<IWindow, IndexedDbFactory> _factories;
        private readonly Dictionary<String, IndexedDbDatabaseBucket> _databasesByOriginAndName;

        /// <summary>
        /// Creates a new IndexedDB provider factory with temporary local storage and session storage enabled.
        /// </summary>
        /// <returns>The new IndexedDB provider factory.</returns>
        public static IIndexedDbProviderFactory CreateTemporary()
        {
            var factory = new IndexedDbProviderFactory();
            return factory;
        }

        /// <summary>
        /// Creates a new IndexedDB provider factory.
        /// </summary>
        public IndexedDbProviderFactory()
        {
            _factories = new ConditionalWeakTable<IWindow, IndexedDbFactory>();
            _databasesByOriginAndName = new Dictionary<String, IndexedDbDatabaseBucket>(StringComparer.Ordinal);
        }

        /// <inheritdoc />
        public IIndexedDbFactory GetIndexedDb(IWindow window)
        {
            if (window == null)
            {
                return null;
            }

            return _factories.GetValue(window, CreateFactory);
        }

        private IndexedDbFactory CreateFactory(IWindow window)
        {
            return new IndexedDbFactory(
                () => GetOrigin(window),
                (origin, name) => GetOrCreateDatabase(origin, name),
                (origin, name) => DeleteDatabase(origin, name));
        }

        private IndexedDbDatabaseBucket GetOrCreateDatabase(String origin, String name)
        {
            var key = GetDatabaseKey(origin, name);

            if (!_databasesByOriginAndName.TryGetValue(key, out var database))
            {
                database = new IndexedDbDatabaseBucket(name);
                _databasesByOriginAndName[key] = database;
            }

            return database;
        }

        private Boolean DeleteDatabase(String origin, String name)
        {
            return _databasesByOriginAndName.Remove(GetDatabaseKey(origin, name));
        }

        private static String GetDatabaseKey(String origin, String name)
        {
            return (origin ?? String.Empty) + "|" + (name ?? String.Empty);
        }

        private static String GetOrigin(IWindow window)
        {
            var href = window?.Location?.Href;

            if (String.IsNullOrEmpty(href))
            {
                return String.Empty;
            }

            var url = new Url(href);

            if (url.IsInvalid || url.IsRelative)
            {
                return String.Empty;
            }

            return url.Origin ?? String.Empty;
        }
    }

    internal sealed class IndexedDbFactory : IIndexedDbFactory
    {
        private readonly Func<String> _getOrigin;
        private readonly Func<String, String, IndexedDbDatabaseBucket> _getDatabase;
        private readonly Func<String, String, Boolean> _deleteDatabase;

        public IndexedDbFactory(Func<String> getOrigin, Func<String, String, IndexedDbDatabaseBucket> getDatabase, Func<String, String, Boolean> deleteDatabase)
        {
            _getOrigin = getOrigin ?? throw new ArgumentNullException(nameof(getOrigin));
            _getDatabase = getDatabase ?? throw new ArgumentNullException(nameof(getDatabase));
            _deleteDatabase = deleteDatabase ?? throw new ArgumentNullException(nameof(deleteDatabase));
        }

        public IIndexedDbDatabase Open(String name)
        {
            name = name ?? String.Empty;
            var origin = _getOrigin.Invoke();
            var database = _getDatabase.Invoke(origin, name);

            if (database.Version <= 0)
            {
                database.Version = 1;
            }

            var connection = new IndexedDbDatabase(name, database);
            database.OpenConnection(connection);
            return connection;
        }

        public IIndexedDbDatabase Open(String name, Int64 version)
        {
            return Open(name, version, null);
        }

        public IIndexedDbDatabase Open(String name, Int64 version, Action<IIndexedDbUpgradeTransaction> upgrade)
        {
            if (version <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            name = name ?? String.Empty;
            var origin = _getOrigin.Invoke();
            var database = _getDatabase.Invoke(origin, name);
            var currentVersion = database.Version;

            if (currentVersion > version)
            {
                throw new InvalidOperationException("Cannot open IndexedDB with a lower version than the current one.");
            }

            if (currentVersion < version)
            {
                if (database.OpenConnections > 0)
                {
                    database.NotifyVersionChangeRequested(currentVersion, version);
                    throw new IndexedDbBlockedException("Cannot upgrade IndexedDB while open connections exist. Close existing database connections first.");
                }

                var tx = new IndexedDbUpgradeTransaction(database, currentVersion, version);
                upgrade?.Invoke(tx);
                tx.Commit();
            }
            else if (currentVersion == 0)
            {
                database.Version = version;
            }

            var connection = new IndexedDbDatabase(name, database);
            database.OpenConnection(connection);
            return connection;
        }

        public IIndexedDbRequest<IIndexedDbDatabase> OpenRequest(String name)
        {
            return IndexedDbRequest<IIndexedDbDatabase>.Run(() => Open(name));
        }

        public IIndexedDbRequest<IIndexedDbDatabase> OpenRequest(String name, Int64 version)
        {
            return IndexedDbRequest<IIndexedDbDatabase>.Run(() => Open(name, version));
        }

        public IIndexedDbRequest<IIndexedDbDatabase> OpenRequest(String name, Int64 version, Action<IIndexedDbUpgradeTransaction> upgrade)
        {
            return IndexedDbRequest<IIndexedDbDatabase>.Run(() => Open(name, version, upgrade));
        }

        public IIndexedDbRequest<IIndexedDbDatabase> OpenRequest(String name, Int64 version, Action<IIndexedDbUpgradeTransaction> upgrade, Boolean waitForUnblock)
        {
            if (!waitForUnblock)
            {
                return OpenRequest(name, version, upgrade);
            }

            name = name ?? String.Empty;
            var origin = _getOrigin.Invoke();
            var database = _getDatabase.Invoke(origin, name);
            var request = IndexedDbRequest<IIndexedDbDatabase>.CreatePending();

            void TryOpen()
            {
                try
                {
                    var opened = Open(name, version, upgrade);
                    request.SetSuccess(opened);
                }
                catch (IndexedDbBlockedException ex)
                {
                    request.SetBlocked(ex);
                }
                catch (Exception ex)
                {
                    request.SetError(ex);
                }
            }

            void HandleConnectionsChanged()
            {
                if (request.IsCompleted)
                {
                    database.ConnectionsChanged -= HandleConnectionsChanged;
                    return;
                }

                if (database.OpenConnections == 0)
                {
                    TryOpen();

                    if (request.IsCompleted)
                    {
                        database.ConnectionsChanged -= HandleConnectionsChanged;
                    }
                }
            }

            TryOpen();

            if (!request.IsCompleted)
            {
                database.ConnectionsChanged += HandleConnectionsChanged;
            }

            return request;
        }

        public Boolean DeleteDatabase(String name)
        {
            name = name ?? String.Empty;
            var origin = _getOrigin.Invoke();
            var database = _getDatabase.Invoke(origin, name);

            if (database.OpenConnections > 0)
            {
                database.NotifyVersionChangeRequested(database.Version, 0);
                throw new IndexedDbBlockedException("Cannot delete IndexedDB while open connections exist. Close existing database connections first.");
            }

            return _deleteDatabase.Invoke(origin, name);
        }

        public IIndexedDbRequest<Boolean> DeleteDatabaseRequest(String name)
        {
            return IndexedDbRequest<Boolean>.Run(() => DeleteDatabase(name));
        }

        public IIndexedDbRequest<Boolean> DeleteDatabaseRequest(String name, Boolean waitForUnblock)
        {
            if (!waitForUnblock)
            {
                return DeleteDatabaseRequest(name);
            }

            name = name ?? String.Empty;
            var origin = _getOrigin.Invoke();
            var database = _getDatabase.Invoke(origin, name);
            var request = IndexedDbRequest<Boolean>.CreatePending();

            void TryDelete()
            {
                try
                {
                    var deleted = DeleteDatabase(name);
                    request.SetSuccess(deleted);
                }
                catch (IndexedDbBlockedException ex)
                {
                    request.SetBlocked(ex);
                }
                catch (Exception ex)
                {
                    request.SetError(ex);
                }
            }

            void HandleConnectionsChanged()
            {
                if (request.IsCompleted)
                {
                    database.ConnectionsChanged -= HandleConnectionsChanged;
                    return;
                }

                if (database.OpenConnections == 0)
                {
                    TryDelete();

                    if (request.IsCompleted)
                    {
                        database.ConnectionsChanged -= HandleConnectionsChanged;
                    }
                }
            }

            TryDelete();

            if (!request.IsCompleted)
            {
                database.ConnectionsChanged += HandleConnectionsChanged;
            }

            return request;
        }
    }

    internal sealed class IndexedDbDatabase : IIndexedDbDatabase
    {
        private readonly String _name;
        private readonly IndexedDbDatabaseBucket _database;

        private Boolean _isClosed;

        public event Action<Int64, Int64> VersionChangeRequested;

        public IndexedDbDatabase(String name, IndexedDbDatabaseBucket database)
        {
            _name = name ?? String.Empty;
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public String Name => _name;

        public Int64 Version => _database.Version;

        public Boolean IsClosed => _isClosed;

        public IIndexedDbObjectStore CreateObjectStore(String name)
        {
            EnsureOpen();
            return new IndexedDbObjectStore(() => _database.GetOrCreateStore(name), true);
        }

        public Boolean DeleteObjectStore(String name)
        {
            EnsureOpen();
            return _database.DeleteStore(name);
        }

        public IIndexedDbTransaction BeginTransaction(String storeName, IndexedDbTransactionMode mode)
        {
            EnsureOpen();

            if (String.IsNullOrEmpty(storeName))
            {
                throw new ArgumentNullException(nameof(storeName));
            }

            return BeginTransaction(new[] { storeName }, mode);
        }

        public IIndexedDbTransaction BeginTransaction(String[] storeNames, IndexedDbTransactionMode mode)
        {
            EnsureOpen();
            return new IndexedDbTransaction(_database, storeNames, mode);
        }

        public void Close()
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;
            _database.CloseConnection(this);
        }

        internal void NotifyVersionChangeRequested(Int64 oldVersion, Int64 newVersion)
        {
            if (!_isClosed)
            {
                VersionChangeRequested?.Invoke(oldVersion, newVersion);
            }
        }

        private void EnsureOpen()
        {
            if (_isClosed)
            {
                throw new InvalidOperationException("IndexedDB database connection is closed.");
            }
        }
    }

    internal sealed class IndexedDbObjectStore : IIndexedDbObjectStore
    {
        private readonly Func<IndexedDbStoreBucket> _getStore;
        private readonly Boolean _writable;

        public IndexedDbObjectStore(Func<IndexedDbStoreBucket> getStore, Boolean writable)
        {
            _getStore = getStore ?? throw new ArgumentNullException(nameof(getStore));
            _writable = writable;
        }

        public Int32 Length => CurrentBucket.Length;

        public Int32 Count() => CurrentBucket.Length;

        public Int32 Count(IndexedDbKeyRange range)
        {
            return GetAll(range).Count;
        }

        public String Key(Int32 index) => CurrentBucket.Key(index);

        public String Get(String key)
        {
            return CurrentBucket.Get(key);
        }

        public IReadOnlyList<String> GetAll(IndexedDbKeyRange range)
        {
            var store = CurrentStore;
            var bucket = store.Records;
            var matches = new List<String>();

            for (var i = 0; i < bucket.Length; i++)
            {
                var key = bucket.Key(i);

                if (range == null || range.Includes(key))
                {
                    matches.Add(bucket.Get(key));
                }
            }

            return matches;
        }

        public void Add(String key, String value)
        {
            EnsureWritable();

            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (CurrentBucket.Get(key) != null)
            {
                throw new InvalidOperationException(String.Concat("The key already exists: ", key));
            }

            EnsureIndexConstraints(key, value);

            CurrentBucket.Set(key, value);
        }

        public void Put(String key, String value)
        {
            EnsureWritable();

            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            EnsureIndexConstraints(key, value);
            CurrentBucket.Set(key, value);
        }

        public Boolean Delete(String key)
        {
            EnsureWritable();

            if (String.IsNullOrEmpty(key))
            {
                return false;
            }

            return CurrentBucket.Remove(key);
        }

        public String this[String key]
        {
            get => CurrentBucket.Get(key);
            set => Put(key, value);
        }

        public void Remove(String key)
        {
            EnsureWritable();

            if (String.IsNullOrEmpty(key))
            {
                return;
            }

            CurrentBucket.Remove(key);
        }

        public void Clear()
        {
            EnsureWritable();
            CurrentBucket.Clear();
        }

        public IIndexedDbIndex CreateIndex(String name, String keyPath, Boolean unique = false)
        {
            EnsureWritable();
            var store = CurrentStore;
            var index = store.CreateOrGetIndex(name, keyPath, unique);
            ValidateExistingRecordsAgainstIndex(index, store.Records);
            return new IndexedDbIndex(() => CurrentStore, name);
        }

        public IIndexedDbIndex GetIndex(String name)
        {
            var store = CurrentStore;

            if (!store.TryGetIndex(name, out _))
            {
                return null;
            }

            return new IndexedDbIndex(() => CurrentStore, name);
        }

        public Boolean DeleteIndex(String name)
        {
            EnsureWritable();
            return CurrentStore.DeleteIndex(name);
        }

        public IIndexedDbCursor OpenCursor()
        {
            return OpenCursor(null, IndexedDbCursorDirection.Next);
        }

        public IIndexedDbCursor OpenCursor(IndexedDbKeyRange range, IndexedDbCursorDirection direction = IndexedDbCursorDirection.Next)
        {
            var items = new List<KeyValuePair<String, String>>();
            var bucket = CurrentBucket;

            for (var i = 0; i < bucket.Length; i++)
            {
                var key = bucket.Key(i);

                if (range == null || range.Includes(key))
                {
                    items.Add(new KeyValuePair<String, String>(key, bucket.Get(key)));
                }
            }

            if (direction == IndexedDbCursorDirection.Prev)
            {
                items.Reverse();
            }

            return IndexedDbCursor.From(items);
        }

        private void EnsureWritable()
        {
            if (!_writable)
            {
                throw new InvalidOperationException("Cannot modify data in a read-only IndexedDB transaction.");
            }
        }

        private void EnsureIndexConstraints(String key, String value)
        {
            var store = CurrentStore;
            var indexes = store.GetIndexes();

            for (var i = 0; i < indexes.Count; i++)
            {
                var index = indexes[i];

                if (!index.Unique)
                {
                    continue;
                }

                var indexValue = ExtractIndexValue(value, index.KeyPath);

                if (indexValue == null)
                {
                    continue;
                }

                for (var j = 0; j < CurrentBucket.Length; j++)
                {
                    var existingKey = CurrentBucket.Key(j);

                    if (String.Equals(existingKey, key, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var existingValue = CurrentBucket.Get(existingKey);
                    var existingIndexValue = ExtractIndexValue(existingValue, index.KeyPath);

                    if (String.Equals(existingIndexValue, indexValue, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(String.Concat("Unique index violation for index: ", index.Name));
                    }
                }
            }
        }

        private static void ValidateExistingRecordsAgainstIndex(IndexedDbIndexDefinition index, StorageBucket bucket)
        {
            if (index == null || !index.Unique)
            {
                return;
            }

            var seen = new HashSet<String>(StringComparer.Ordinal);

            for (var i = 0; i < bucket.Length; i++)
            {
                var key = bucket.Key(i);
                var value = bucket.Get(key);
                var indexValue = ExtractIndexValue(value, index.KeyPath);

                if (indexValue == null)
                {
                    continue;
                }

                if (!seen.Add(indexValue))
                {
                    throw new InvalidOperationException(String.Concat("Unique index violation for index: ", index.Name));
                }
            }
        }

        internal static String ExtractIndexValue(String value, String keyPath)
        {
            if (String.IsNullOrEmpty(value) || String.IsNullOrEmpty(keyPath))
            {
                return null;
            }

            var marker = String.Concat(keyPath, "=");
            var start = value.IndexOf(marker, StringComparison.Ordinal);

            if (start < 0)
            {
                return null;
            }

            start += marker.Length;
            var end = value.IndexOf(';', start);

            if (end < 0)
            {
                end = value.Length;
            }

            var extracted = value.Substring(start, end - start).Trim();
            return extracted.Length == 0 ? null : extracted;
        }

        private IndexedDbStoreBucket CurrentStore => _getStore.Invoke();

        private StorageBucket CurrentBucket => CurrentStore.Records;
    }

    internal sealed class IndexedDbIndex : IIndexedDbIndex
    {
        private readonly Func<IndexedDbStoreBucket> _getStore;
        private readonly String _name;

        public IndexedDbIndex(Func<IndexedDbStoreBucket> getStore, String name)
        {
            _getStore = getStore ?? throw new ArgumentNullException(nameof(getStore));
            _name = name ?? String.Empty;
        }

        public String Name => Definition.Name;

        public String KeyPath => Definition.KeyPath;

        public Boolean IsUnique => Definition.Unique;

        public String Get(String indexKey)
        {
            return GetAll(indexKey).FirstOrDefault();
        }

        public IReadOnlyList<String> GetAll(String indexKey)
        {
            var values = new List<String>();

            if (String.IsNullOrEmpty(indexKey))
            {
                return values;
            }

            var store = _getStore.Invoke();
            var bucket = store.Records;

            for (var i = 0; i < bucket.Length; i++)
            {
                var key = bucket.Key(i);
                var value = bucket.Get(key);
                var projected = IndexedDbObjectStore.ExtractIndexValue(value, Definition.KeyPath);

                if (String.Equals(projected, indexKey, StringComparison.Ordinal))
                {
                    values.Add(value);
                }
            }

            return values;
        }

        public IReadOnlyList<String> GetAll(IndexedDbKeyRange range)
        {
            var values = new List<String>();
            var store = _getStore.Invoke();
            var bucket = store.Records;

            for (var i = 0; i < bucket.Length; i++)
            {
                var key = bucket.Key(i);
                var value = bucket.Get(key);
                var projected = IndexedDbObjectStore.ExtractIndexValue(value, Definition.KeyPath);

                if (projected != null && (range == null || range.Includes(projected)))
                {
                    values.Add(value);
                }
            }

            return values;
        }

        public Int32 Count(String indexKey)
        {
            return GetAll(indexKey).Count;
        }

        public IIndexedDbCursor OpenCursor()
        {
            return OpenCursor(null, IndexedDbCursorDirection.Next);
        }

        public IIndexedDbCursor OpenCursor(IndexedDbKeyRange range, IndexedDbCursorDirection direction = IndexedDbCursorDirection.Next)
        {
            var rows = new List<IndexedDbIndexCursorRow>();
            var store = _getStore.Invoke();
            var bucket = store.Records;
            var definition = Definition;

            for (var i = 0; i < bucket.Length; i++)
            {
                var key = bucket.Key(i);
                var value = bucket.Get(key);
                var projected = IndexedDbObjectStore.ExtractIndexValue(value, definition.KeyPath);

                if (projected == null)
                {
                    continue;
                }

                if (range != null && !range.Includes(projected))
                {
                    continue;
                }

                rows.Add(new IndexedDbIndexCursorRow(projected, key, value));
            }

            rows.Sort((a, b) =>
            {
                var compare = StringComparer.Ordinal.Compare(a.IndexKey, b.IndexKey);

                if (compare != 0)
                {
                    return compare;
                }

                return StringComparer.Ordinal.Compare(a.PrimaryKey, b.PrimaryKey);
            });

            if (direction == IndexedDbCursorDirection.Prev)
            {
                rows.Reverse();
            }

            var items = rows.Select(m => new KeyValuePair<String, String>(m.PrimaryKey, m.Value)).ToList();
            return IndexedDbCursor.From(items);
        }

        private IndexedDbIndexDefinition Definition
        {
            get
            {
                var store = _getStore.Invoke();

                if (!store.TryGetIndex(_name, out var definition))
                {
                    throw new InvalidOperationException(String.Concat("Index does not exist: ", _name));
                }

                return definition;
            }
        }
    }

    internal sealed class IndexedDbRequest<T> : IIndexedDbRequest<T>
    {
        private readonly TaskCompletionSource<T> _source;

        private T _result;
        private Exception _exception;
        private IndexedDbRequestState _state;

        private event Action<T> SucceededInternal;
        private event Action<Exception> FailedInternal;
        private event Action BlockedInternal;

        private IndexedDbRequest(TaskCompletionSource<T> source, IndexedDbRequestState state)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _state = state;
        }

        public IndexedDbRequestState State => _state;

        public Boolean IsCompleted => _source.Task.IsCompleted;

        public Boolean HasError => _state == IndexedDbRequestState.Error || _state == IndexedDbRequestState.Blocked;

        public T Result
        {
            get
            {
                return _state == IndexedDbRequestState.Success ? _result : default;
            }
        }

        public Exception Exception => _exception;

        public event Action<T> Succeeded
        {
            add
            {
                SucceededInternal += value;

                if (_state == IndexedDbRequestState.Success)
                {
                    value?.Invoke(_result);
                }
            }
            remove
            {
                SucceededInternal -= value;
            }
        }

        public event Action<Exception> Failed
        {
            add
            {
                FailedInternal += value;

                if (_state == IndexedDbRequestState.Error || _state == IndexedDbRequestState.Blocked)
                {
                    value?.Invoke(_exception);
                }
            }
            remove
            {
                FailedInternal -= value;
            }
        }

        public event Action Blocked
        {
            add
            {
                BlockedInternal += value;

                if (_state == IndexedDbRequestState.Blocked)
                {
                    value?.Invoke();
                }
            }
            remove
            {
                BlockedInternal -= value;
            }
        }

        public Task<T> WaitAsync()
        {
            return _source.Task;
        }

        public void SetSuccess(T value)
        {
            if (IsCompleted)
            {
                return;
            }

            _state = IndexedDbRequestState.Success;
            _result = value;
            _exception = null;
            _source.TrySetResult(value);
            SucceededInternal?.Invoke(value);
        }

        public void SetBlocked(Exception exception)
        {
            if (IsCompleted)
            {
                return;
            }

            _state = IndexedDbRequestState.Blocked;
            _exception = exception;
            BlockedInternal?.Invoke();
            FailedInternal?.Invoke(exception);
        }

        public void SetError(Exception exception)
        {
            if (IsCompleted)
            {
                return;
            }

            _state = IndexedDbRequestState.Error;
            _exception = exception;
            _source.TrySetException(exception);
            FailedInternal?.Invoke(exception);
        }

        public static IIndexedDbRequest<T> Run(Func<T> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            try
            {
                var result = action.Invoke();
                var request = new IndexedDbRequest<T>(new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously), IndexedDbRequestState.Pending);
                request.SetSuccess(result);
                return request;
            }
            catch (IndexedDbBlockedException ex)
            {
                var request = new IndexedDbRequest<T>(new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously), IndexedDbRequestState.Pending);
                request.SetBlocked(ex);
                request._source.TrySetException(ex);
                return request;
            }
            catch (Exception ex)
            {
                var request = new IndexedDbRequest<T>(new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously), IndexedDbRequestState.Pending);
                request.SetError(ex);
                return request;
            }
        }

        public static IndexedDbRequest<T> CreatePending()
        {
            return new IndexedDbRequest<T>(new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously), IndexedDbRequestState.Pending);
        }
    }

    internal sealed class IndexedDbCursor : IIndexedDbCursor
    {
        private readonly IReadOnlyList<KeyValuePair<String, String>> _items;
        private readonly Int32 _index;

        private IndexedDbCursor(IReadOnlyList<KeyValuePair<String, String>> items, Int32 index)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _index = index;
        }

        public String Key => _items[_index].Key;

        public String Value => _items[_index].Value;

        public IIndexedDbCursor Continue()
        {
            var next = _index + 1;

            if (next >= _items.Count)
            {
                return null;
            }

            return new IndexedDbCursor(_items, next);
        }

        public static IIndexedDbCursor From(IReadOnlyList<KeyValuePair<String, String>> items)
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            return new IndexedDbCursor(items, 0);
        }
    }

    internal sealed class IndexedDbIndexCursorRow
    {
        public IndexedDbIndexCursorRow(String indexKey, String primaryKey, String value)
        {
            IndexKey = indexKey ?? String.Empty;
            PrimaryKey = primaryKey ?? String.Empty;
            Value = value;
        }

        public String IndexKey { get; }

        public String PrimaryKey { get; }

        public String Value { get; }
    }

    internal sealed class IndexedDbTransaction : IIndexedDbTransaction
    {
        private readonly IndexedDbDatabaseBucket _database;
        private readonly Dictionary<String, IndexedDbStoreBucket> _stores;

        private Boolean _isCompleted;
        private Boolean _isAborted;
        private readonly Boolean _holdsWriteLock;

        public IndexedDbTransaction(IndexedDbDatabaseBucket database, String[] storeNames, IndexedDbTransactionMode mode)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            Mode = mode;
            _database.BeginTransaction(mode);
            _holdsWriteLock = mode == IndexedDbTransactionMode.ReadWrite;

            if (storeNames == null || storeNames.Length == 0)
            {
                throw new ArgumentException("At least one object store name is required.", nameof(storeNames));
            }

            _stores = new Dictionary<String, IndexedDbStoreBucket>(StringComparer.Ordinal);

            for (var i = 0; i < storeNames.Length; i++)
            {
                var name = storeNames[i] ?? String.Empty;

                if (_stores.ContainsKey(name))
                {
                    continue;
                }

                var existing = _database.GetStore(name);

                if (existing == null)
                {
                    throw new InvalidOperationException(String.Concat("Object store does not exist: ", name));
                }

                _stores[name] = CloneStore(existing);
            }
        }

        public IndexedDbTransactionMode Mode { get; }

        public Boolean IsCompleted => _isCompleted;

        public Boolean IsAborted => _isAborted;

        public event Action Completed;

        public event Action Aborted;

        public IIndexedDbObjectStore GetObjectStore(String name)
        {
            EnsureActive();
            name = name ?? String.Empty;

            if (!_stores.TryGetValue(name, out var store))
            {
                throw new InvalidOperationException(String.Concat("Object store is not part of the transaction scope: ", name));
            }

            return new IndexedDbObjectStore(() => store, Mode != IndexedDbTransactionMode.ReadOnly);
        }

        public void Commit()
        {
            EnsureActive();

            if (Mode != IndexedDbTransactionMode.ReadOnly)
            {
                foreach (var store in _stores)
                {
                    _database.SetStore(store.Key, CloneStore(store.Value));
                }
            }

            _isCompleted = true;
            _isAborted = false;
            ReleaseLockIfNeeded();
            Completed?.Invoke();
        }

        public void Abort()
        {
            EnsureActive();
            _isCompleted = true;
            _isAborted = true;
            ReleaseLockIfNeeded();
            Aborted?.Invoke();
        }

        public void Dispose()
        {
            if (!_isCompleted)
            {
                Abort();
            }
        }

        private static IndexedDbStoreBucket CloneStore(IndexedDbStoreBucket store)
        {
            return store?.Clone() ?? new IndexedDbStoreBucket();
        }

        private void EnsureActive()
        {
            if (_isCompleted)
            {
                throw new InvalidOperationException("The IndexedDB transaction is already completed.");
            }
        }

        private void ReleaseLockIfNeeded()
        {
            if (_holdsWriteLock)
            {
                _database.EndTransaction(Mode);
            }
        }
    }

    internal sealed class IndexedDbUpgradeTransaction : IIndexedDbUpgradeTransaction
    {
        private readonly IndexedDbDatabaseBucket _database;
        private readonly Dictionary<String, IndexedDbStoreBucket> _stores;

        public IndexedDbUpgradeTransaction(IndexedDbDatabaseBucket database, Int64 oldVersion, Int64 newVersion)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            OldVersion = oldVersion;
            NewVersion = newVersion;
            _stores = _database.CloneStores();
        }

        public Int64 OldVersion { get; }

        public Int64 NewVersion { get; }

        public IIndexedDbObjectStore CreateObjectStore(String name)
        {
            name = name ?? String.Empty;

            if (!_stores.TryGetValue(name, out var store))
            {
                store = new IndexedDbStoreBucket();
                _stores[name] = store;
            }

            return new IndexedDbObjectStore(() => store, true);
        }

        public Boolean DeleteObjectStore(String name)
        {
            return _stores.Remove(name ?? String.Empty);
        }

        public IIndexedDbObjectStore GetObjectStore(String name)
        {
            name = name ?? String.Empty;

            if (_stores.TryGetValue(name, out var store))
            {
                return new IndexedDbObjectStore(() => store, true);
            }

            return null;
        }

        public void Commit()
        {
            _database.ReplaceStores(_stores);
            _database.Version = NewVersion;
        }
    }

    internal sealed class IndexedDbDatabaseBucket
    {
        private readonly Dictionary<String, IndexedDbStoreBucket> _stores;
        private readonly List<WeakReference<IndexedDbDatabase>> _connections;

        private Int32 _openConnections;
        private Boolean _hasActiveReadWriteTransaction;

        public IndexedDbDatabaseBucket(String name)
        {
            Name = name ?? String.Empty;
            _stores = new Dictionary<String, IndexedDbStoreBucket>(StringComparer.Ordinal);
            _connections = new List<WeakReference<IndexedDbDatabase>>();
        }

        public String Name { get; }

        public Int64 Version { get; set; }

        public Int32 OpenConnections => _openConnections;

        public event Action ConnectionsChanged;

        public void BeginTransaction(IndexedDbTransactionMode mode)
        {
            if (mode == IndexedDbTransactionMode.ReadWrite)
            {
                if (_hasActiveReadWriteTransaction)
                {
                    throw new InvalidOperationException("Another read-write transaction is already active for this database.");
                }

                _hasActiveReadWriteTransaction = true;
            }
        }

        public void EndTransaction(IndexedDbTransactionMode mode)
        {
            if (mode == IndexedDbTransactionMode.ReadWrite)
            {
                _hasActiveReadWriteTransaction = false;
            }
        }

        public void OpenConnection(IndexedDbDatabase connection)
        {
            if (connection != null)
            {
                _connections.Add(new WeakReference<IndexedDbDatabase>(connection));
            }

            _openConnections++;
            ConnectionsChanged?.Invoke();
        }

        public void CloseConnection(IndexedDbDatabase connection)
        {
            if (_openConnections > 0)
            {
                _openConnections--;
            }

            CleanupConnections();
            ConnectionsChanged?.Invoke();
        }

        public void NotifyVersionChangeRequested(Int64 oldVersion, Int64 newVersion)
        {
            CleanupConnections();

            for (var i = 0; i < _connections.Count; i++)
            {
                if (_connections[i].TryGetTarget(out var connection))
                {
                    connection.NotifyVersionChangeRequested(oldVersion, newVersion);
                }
            }
        }

        private void CleanupConnections()
        {
            for (var i = _connections.Count - 1; i >= 0; i--)
            {
                if (!_connections[i].TryGetTarget(out var connection) || connection.IsClosed)
                {
                    _connections.RemoveAt(i);
                }
            }
        }

        public IndexedDbStoreBucket GetOrCreateStore(String name)
        {
            name = name ?? String.Empty;

            if (!_stores.TryGetValue(name, out var store))
            {
                store = new IndexedDbStoreBucket();
                _stores[name] = store;
            }

            return store;
        }

        public IndexedDbStoreBucket GetStore(String name)
        {
            name = name ?? String.Empty;
            return _stores.TryGetValue(name, out var store) ? store : null;
        }

        public void SetStore(String name, IndexedDbStoreBucket store)
        {
            _stores[name ?? String.Empty] = store ?? new IndexedDbStoreBucket();
        }

        public Boolean DeleteStore(String name)
        {
            return _stores.Remove(name ?? String.Empty);
        }

        public Dictionary<String, IndexedDbStoreBucket> CloneStores()
        {
            return _stores.ToDictionary(m => m.Key, m => m.Value.Clone(), StringComparer.Ordinal);
        }

        public void ReplaceStores(Dictionary<String, IndexedDbStoreBucket> stores)
        {
            _stores.Clear();

            if (stores == null)
            {
                return;
            }

            foreach (var store in stores)
            {
                _stores[store.Key] = store.Value.Clone();
            }
        }
    }

    internal sealed class IndexedDbStoreBucket
    {
        private readonly Dictionary<String, IndexedDbIndexDefinition> _indexes;

        public IndexedDbStoreBucket()
        {
            Records = new StorageBucket();
            _indexes = new Dictionary<String, IndexedDbIndexDefinition>(StringComparer.Ordinal);
        }

        private IndexedDbStoreBucket(StorageBucket records, Dictionary<String, IndexedDbIndexDefinition> indexes)
        {
            Records = records ?? new StorageBucket();
            _indexes = indexes ?? new Dictionary<String, IndexedDbIndexDefinition>(StringComparer.Ordinal);
        }

        public StorageBucket Records { get; }

        public IndexedDbIndexDefinition CreateOrGetIndex(String name, String keyPath, Boolean unique)
        {
            name = name ?? String.Empty;
            keyPath = keyPath ?? String.Empty;

            if (_indexes.TryGetValue(name, out var existing))
            {
                if (!String.Equals(existing.KeyPath, keyPath, StringComparison.Ordinal) || existing.Unique != unique)
                {
                    throw new InvalidOperationException(String.Concat("Index already exists with different definition: ", name));
                }

                return existing;
            }

            var index = new IndexedDbIndexDefinition(name, keyPath, unique);
            _indexes[name] = index;
            return index;
        }

        public Boolean TryGetIndex(String name, out IndexedDbIndexDefinition index)
        {
            return _indexes.TryGetValue(name ?? String.Empty, out index);
        }

        public Boolean DeleteIndex(String name)
        {
            return _indexes.Remove(name ?? String.Empty);
        }

        public List<IndexedDbIndexDefinition> GetIndexes()
        {
            return _indexes.Values.ToList();
        }

        public IndexedDbStoreBucket Clone()
        {
            var records = new StorageBucket(Records.ToEntries());
            var indexes = _indexes.ToDictionary(m => m.Key, m => m.Value.Clone(), StringComparer.Ordinal);
            return new IndexedDbStoreBucket(records, indexes);
        }
    }

    internal sealed class IndexedDbIndexDefinition
    {
        public IndexedDbIndexDefinition(String name, String keyPath, Boolean unique)
        {
            Name = name ?? String.Empty;
            KeyPath = keyPath ?? String.Empty;
            Unique = unique;
        }

        public String Name { get; }

        public String KeyPath { get; }

        public Boolean Unique { get; }

        public IndexedDbIndexDefinition Clone()
        {
            return new IndexedDbIndexDefinition(Name, KeyPath, Unique);
        }
    }

    internal sealed class IndexedDbBlockedException : InvalidOperationException
    {
        public IndexedDbBlockedException(String message)
            : base(message)
        {
        }
    }
}