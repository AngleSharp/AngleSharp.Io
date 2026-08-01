namespace AngleSharp.Io.IndexedDb
{
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.Storage;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

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
            return new IndexedDbDatabase(name, database);
        }

        public Boolean DeleteDatabase(String name)
        {
            name = name ?? String.Empty;
            return _deleteDatabase.Invoke(_getOrigin.Invoke(), name);
        }
    }

    internal sealed class IndexedDbDatabase : IIndexedDbDatabase
    {
        private readonly String _name;
        private readonly IndexedDbDatabaseBucket _database;

        public IndexedDbDatabase(String name, IndexedDbDatabaseBucket database)
        {
            _name = name ?? String.Empty;
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public String Name => _name;

        public IIndexedDbObjectStore CreateObjectStore(String name)
        {
            return new IndexedDbObjectStore(() => _database.GetOrCreateStore(name));
        }

        public Boolean DeleteObjectStore(String name)
        {
            return _database.DeleteStore(name);
        }
    }

    internal sealed class IndexedDbObjectStore : IIndexedDbObjectStore
    {
        private readonly Func<StorageBucket> _getBucket;

        public IndexedDbObjectStore(Func<StorageBucket> getBucket)
        {
            _getBucket = getBucket ?? throw new ArgumentNullException(nameof(getBucket));
        }

        public Int32 Length => CurrentBucket.Length;

        public String Key(Int32 index) => CurrentBucket.Key(index);

        public String this[String key]
        {
            get => CurrentBucket.Get(key);
            set
            {
                if (String.IsNullOrEmpty(key))
                {
                    throw new ArgumentNullException(nameof(key));
                }

                CurrentBucket.Set(key, value);
            }
        }

        public void Remove(String key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return;
            }

            CurrentBucket.Remove(key);
        }

        public void Clear()
        {
            CurrentBucket.Clear();
        }

        private StorageBucket CurrentBucket => _getBucket.Invoke();
    }

    internal sealed class IndexedDbDatabaseBucket
    {
        private readonly Dictionary<String, StorageBucket> _stores;

        public IndexedDbDatabaseBucket(String name)
        {
            Name = name ?? String.Empty;
            _stores = new Dictionary<String, StorageBucket>(StringComparer.Ordinal);
        }

        public String Name { get; }

        public StorageBucket GetOrCreateStore(String name)
        {
            name = name ?? String.Empty;

            if (!_stores.TryGetValue(name, out var store))
            {
                store = new StorageBucket();
                _stores[name] = store;
            }

            return store;
        }

        public Boolean DeleteStore(String name)
        {
            return _stores.Remove(name ?? String.Empty);
        }
    }
}