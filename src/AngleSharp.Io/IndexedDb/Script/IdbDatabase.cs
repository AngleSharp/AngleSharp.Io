namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using System;
    using System.Linq;

    /// <summary>
    /// Represents the options of creating an object store.
    /// </summary>
    [DomName("IDBObjectStoreParameters")]
    public sealed class IdbObjectStoreParameters
    {
        /// <summary>
        /// Gets or sets where the value has its key, if it has it.
        /// </summary>
        [DomName("keyPath")]
        public Object KeyPath { get; set; }

        /// <summary>
        /// Gets or sets if keys are generated.
        /// </summary>
        [DomName("autoIncrement")]
        public Boolean? AutoIncrement { get; set; }
    }

    /// <summary>
    /// Represents a connection to a database.
    /// </summary>
    [DomName("IDBDatabase")]
    [DomExposed("Window")]
    public sealed class IdbDatabase : EventTarget
    {
        private readonly IWindow _window;
        private readonly IDocument _document;
        private readonly IdbScheduler _scheduler;
        private readonly IdbDatabaseData _data;
        private IdbTransaction _upgrade;
        private Boolean _isClosed;
        private Int32 _transactions;

        internal IdbDatabase(IWindow window, IdbScheduler scheduler, IdbDatabaseData data)
        {
            _window = window;
            _document = window.Document;
            _scheduler = scheduler;
            _data = data;
        }

        /// <summary>
        /// Gets the name of the database.
        /// </summary>
        [DomName("name")]
        public String Name => _data.Name;

        /// <summary>
        /// Gets the version of the database.
        /// </summary>
        [DomName("version")]
        public Int64 Version => _data.Version;

        /// <summary>
        /// Gets the names of the object stores, sorted.
        /// </summary>
        [DomName("objectStoreNames")]
        public IStringList ObjectStoreNames
        {
            get
            {
                lock (_data)
                {
                    return new IdbStringList(_data.Stores.Keys);
                }
            }
        }

        /// <summary>
        /// Raised when a transaction was aborted.
        /// </summary>
        [DomName("onabort")]
        public event DomEventHandler Aborted
        {
            add { AddEventListener("abort", value); }
            remove { RemoveEventListener("abort", value); }
        }

        /// <summary>
        /// Raised when the connection was closed for an unusual reason.
        /// </summary>
        [DomName("onclose")]
        public event DomEventHandler Closed
        {
            add { AddEventListener("close", value); }
            remove { RemoveEventListener("close", value); }
        }

        /// <summary>
        /// Raised when a request failed.
        /// </summary>
        [DomName("onerror")]
        public event DomEventHandler Failed
        {
            add { AddEventListener("error", value); }
            remove { RemoveEventListener("error", value); }
        }

        /// <summary>
        /// Raised when something wants to upgrade or delete the database.
        /// </summary>
        [DomName("onversionchange")]
        public event DomEventHandler VersionChanged
        {
            add { AddEventListener("versionchange", value); }
            remove { RemoveEventListener("versionchange", value); }
        }

        internal IWindow Window => _window;

        internal IdbScheduler Scheduler => _scheduler;

        internal IdbDatabaseData Data => _data;

        //  What a page left behind can't react anymore, and has to keep nobody waiting.
        internal Boolean IsStale => _document.Context == null || !ReferenceEquals(_document.Context.Active, _document);

        /// <summary>
        /// Creates an object store while the version of the database is changing.
        /// </summary>
        /// <param name="name">The name of the store.</param>
        /// <param name="options">The options of the store.</param>
        /// <returns>The store.</returns>
        [DomName("createObjectStore")]
        public IdbObjectStore CreateObjectStore(String name, IdbObjectStoreParameters options = null)
        {
            var transaction = GetUpgradeTransaction();
            var keyPath = IdbKeys.ToKeyPath(IdbFactory.Clone(_window, options?.KeyPath));
            var autoIncrement = options?.AutoIncrement == true;

            if (autoIncrement && (keyPath is String[] || keyPath is String path && path.Length == 0))
            {
                throw new DomException(DomError.InvalidAccess);
            }

            lock (_data)
            {
                if (_data.Stores.ContainsKey(name))
                {
                    throw new DomException("ConstraintError");
                }

                var store = new IdbStoreData(name, keyPath, autoIncrement);
                _data.Stores[name] = store;
                transaction.AddUndo(() => _data.Stores.Remove(name));
                transaction.AddToScope(name);
                return transaction.GetStore(name);
            }
        }

        /// <summary>
        /// Deletes an object store while the version of the database is changing.
        /// </summary>
        /// <param name="name">The name of the store.</param>
        [DomName("deleteObjectStore")]
        public void DeleteObjectStore(String name)
        {
            var transaction = GetUpgradeTransaction();

            lock (_data)
            {
                if (!_data.Stores.TryGetValue(name, out var store))
                {
                    throw new DomException(DomError.NotFound);
                }

                _data.Stores.Remove(name);
                transaction.AddUndo(() => _data.Stores[name] = store);
                transaction.RemoveFromScope(name);
            }
        }

        /// <summary>
        /// Starts a transaction.
        /// </summary>
        /// <param name="storeNames">The name of a store or the names of several.</param>
        /// <param name="mode">The mode: readonly (the default) or readwrite.</param>
        /// <param name="options">The options, which have no effect here.</param>
        /// <returns>The transaction.</returns>
        [DomName("transaction")]
        public IdbTransaction Transaction(Object storeNames, String mode = null, Object options = null)
        {
            if (_isClosed || _upgrade != null)
            {
                throw new DomException(DomError.InvalidState);
            }

            var names = ToNames(storeNames);
            var kind = mode ?? IdbTransaction.ReadOnly;

            if (kind != IdbTransaction.ReadOnly && kind != IdbTransaction.ReadWrite)
            {
                throw new ArgumentException($"The mode '{kind}' can't be used.", nameof(mode));
            }

            if (names.Length == 0)
            {
                throw new DomException(DomError.InvalidAccess);
            }

            lock (_data)
            {
                if (!names.All(_data.Stores.ContainsKey))
                {
                    throw new DomException(DomError.NotFound);
                }
            }

            return new IdbTransaction(this, names, kind, null);
        }

        /// <summary>
        /// Closes the connection, once its transactions are done.
        /// </summary>
        [DomName("close")]
        public void Close()
        {
            if (!_isClosed)
            {
                _isClosed = true;
                RemoveWhenDone();
            }
        }

        internal void Add(IdbTransaction transaction) => _transactions++;

        internal void Remove(IdbTransaction transaction)
        {
            _transactions--;
            RemoveWhenDone();
        }

        private void RemoveWhenDone()
        {
            if (_isClosed && _transactions == 0)
            {
                _data.Remove(this);
            }
        }

        internal IdbTransaction BeginUpgrade(IdbOpenRequest request, Action revert, Action<Boolean> finished)
        {
            String[] names;

            lock (_data)
            {
                names = _data.Stores.Keys.ToArray();
            }

            _upgrade = new IdbTransaction(this, names, IdbTransaction.VersionChange, finished);
            _upgrade.AddUndo(revert);
            return _upgrade;
        }

        internal void EndUpgrade() => _upgrade = null;

        internal void NotifyVersionChange(Int64 oldVersion, Int64? newVersion) => _scheduler.Post(() =>
        {
            if (!_isClosed)
            {
                this.Dispatch(new IdbVersionChangeEvent("versionchange", oldVersion, newVersion));
            }
        });

        private IdbTransaction GetUpgradeTransaction()
        {
            if (_upgrade == null)
            {
                throw new DomException(DomError.InvalidState);
            }

            if (!_upgrade.IsActive)
            {
                throw new DomException(IdbTransaction.InactiveError);
            }

            return _upgrade;
        }

        private String[] ToNames(Object storeNames)
        {
            switch (storeNames)
            {
                case String name:
                    return new[] { name };
                case String[] names:
                    return names;
                case IStringList list:
                    return list.ToArray();
                default:
                    if (IdbFactory.Clone(_window, storeNames) is Object[] items && items.All(m => m is String))
                    {
                        return items.Cast<String>().Distinct().ToArray();
                    }

                    throw new DomException(DomError.InvalidAccess);
            }
        }
    }

    /// <summary>
    /// A sorted list of names without duplicates.
    /// </summary>
    sealed class IdbStringList : IStringList
    {
        private readonly String[] _items;

        public IdbStringList(System.Collections.Generic.IEnumerable<String> items)
        {
            _items = items.Distinct().OrderBy(m => m, StringComparer.Ordinal).ToArray();
        }

        public String this[Int32 index] => index >= 0 && index < _items.Length ? _items[index] : null;

        public Int32 Length => _items.Length;

        public Boolean Contains(String entry) => Array.IndexOf(_items, entry) != -1;

        public System.Collections.Generic.IEnumerator<String> GetEnumerator() => ((System.Collections.Generic.IEnumerable<String>)_items).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();
    }
}
