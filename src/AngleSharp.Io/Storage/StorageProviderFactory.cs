namespace AngleSharp.Io.Storage
{
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents the default storage provider factory with origin-dependent views.
    /// </summary>
    public class StorageProviderFactory : IStorageProviderFactory
    {
        private readonly ConditionalWeakTable<IWindow, StorageViews> _views;
        private readonly Dictionary<String, StorageBucket> _storageByScopeAndOrigin;

        private IStorageHandler _localStorageHandler;
        private IStorageHandler _sessionStorageHandler;

        private Boolean _hasLocalStorage;
        private Boolean _hasSessionStorage;

        /// <summary>
        /// Creates a new storage provider factory.
        /// </summary>
        public StorageProviderFactory()
        {
            _views = new ConditionalWeakTable<IWindow, StorageViews>();
            _storageByScopeAndOrigin = new Dictionary<String, StorageBucket>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Enables local storage synchronization with the given directory.
        /// </summary>
        /// <param name="syncDirectoryPath">The directory used for local storage synchronization.</param>
        public void EnableLocalStorage(String syncDirectoryPath)
        {
            EnableLocalStorage(new PersistenceStorageHandler(syncDirectoryPath));
        }

        /// <summary>
        /// Enables local storage synchronization with the given storage handler.
        /// </summary>
        /// <param name="handler">The handler used for local storage synchronization.</param>
        public void EnableLocalStorage(IStorageHandler handler)
        {
            _hasLocalStorage = true;
            _localStorageHandler = handler ?? throw new ArgumentNullException(nameof(handler));
            ClearStorage(ScopeLocal);
        }

        /// <summary>
        /// Enables in-memory local storage.
        /// </summary>
        public void EnableTemporaryLocalStorage()
        {
            _hasLocalStorage = true;
            _localStorageHandler = new MemoryStorageHandler();
            ClearStorage(ScopeLocal);
        }

        /// <summary>
        /// Enables session storage with the given storage handler.
        /// </summary>
        /// <param name="handler">The handler used for session storage.</param>
        public void EnableSessionStorage(IStorageHandler handler)
        {
            _hasSessionStorage = true;
            _sessionStorageHandler = handler ?? throw new ArgumentNullException(nameof(handler));
            ClearStorage(ScopeSession);
        }

        /// <summary>
        /// Enables in-memory session storage.
        /// </summary>
        public void EnableSessionStorage()
        {
            EnableSessionStorage(new MemoryStorageHandler());
        }

        /// <inheritdoc />
        public StorageViews GetStorages(IWindow window)
        {
            if (window == null)
            {
                return null;
            }

            return _views.GetValue(window, CreateStorageViews);
        }

        private StorageViews CreateStorageViews(IWindow window)
        {
            var getOrigin = new Func<String>(() => GetOrigin(window));

            var local = _hasLocalStorage
                ? new Storage(getOrigin, origin => GetOrCreateStorage(ScopeLocal, origin), PersistLocalStorage)
                : null;
            var session = _hasSessionStorage
                ? new Storage(getOrigin, origin => GetOrCreateStorage(ScopeSession, origin), PersistSessionStorage)
                : null;

            return new StorageViews(local, session);
        }

        private StorageBucket GetOrCreateStorage(String scope, String origin)
        {
            origin = origin ?? String.Empty;
            var key = scope + origin;
            var handler = scope == ScopeLocal ? _localStorageHandler : _sessionStorageHandler;

            if (!_storageByScopeAndOrigin.TryGetValue(key, out var storage))
            {
                var entries = handler?.Read(origin);
                storage = new StorageBucket(entries);
                _storageByScopeAndOrigin[key] = storage;
            }

            return storage;
        }

        private void PersistLocalStorage(String origin, StorageBucket storage)
        {
            _localStorageHandler?.Write(origin, storage.ToEntries());
        }

        private void PersistSessionStorage(String origin, StorageBucket storage)
        {
            _sessionStorageHandler?.Write(origin, storage.ToEntries());
        }

        private void ClearStorage(String scope)
        {
            var keys = new List<String>();

            foreach (var key in _storageByScopeAndOrigin.Keys)
            {
                if (key.StartsWith(scope, StringComparison.Ordinal))
                {
                    keys.Add(key);
                }
            }

            foreach (var key in keys)
            {
                _storageByScopeAndOrigin.Remove(key);
            }
        }

        private const String ScopeLocal = "l|";
        private const String ScopeSession = "s|";

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
}
