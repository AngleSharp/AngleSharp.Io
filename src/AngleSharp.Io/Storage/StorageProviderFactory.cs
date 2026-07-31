namespace AngleSharp.Io.Storage
{
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using AngleSharp.Io.Dom;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents the default storage provider factory with origin- and browsing-context-dependent views.
    /// </summary>
    public class StorageProviderFactory : IStorageProviderFactory
    {
        private readonly ConditionalWeakTable<IWindow, StorageViews> _views;
        private readonly List<WeakReference<IWindow>> _windows;
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
            _windows = new List<WeakReference<IWindow>>();
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

            TrackWindow(window);
            return _views.GetValue(window, CreateStorageViews);
        }

        private StorageViews CreateStorageViews(IWindow window)
        {
            var getOrigin = new Func<String>(() => GetOrigin(window));

            var local = _hasLocalStorage
                ? new Storage(getOrigin, origin => GetOrCreateStorage(ScopeLocal, window, origin), (origin, storage, key, oldValue, newValue) =>
                {
                    PersistLocalStorage(origin, storage);
                    NotifyStorageEvent(window, ScopeLocal, origin, key, oldValue, newValue);
                })
                : null;
            var session = _hasSessionStorage
                ? new Storage(getOrigin, origin => GetOrCreateStorage(ScopeSession, window, origin), (origin, storage, key, oldValue, newValue) =>
                {
                    PersistSessionStorage(window, origin, storage);
                    NotifyStorageEvent(window, ScopeSession, origin, key, oldValue, newValue);
                })
                : null;

            return new StorageViews(local, session);
        }

        private StorageBucket GetOrCreateStorage(String scope, IWindow window, String origin)
        {
            origin = origin ?? String.Empty;
            var key = GetStorageKey(scope, window, origin);
            var handler = scope == ScopeLocal ? _localStorageHandler : _sessionStorageHandler;

            if (!_storageByScopeAndOrigin.TryGetValue(key, out var storage))
            {
                var entries = handler?.Read(GetStorageHandlerKey(scope, window, origin));
                storage = new StorageBucket(entries);
                _storageByScopeAndOrigin[key] = storage;
            }

            return storage;
        }

        private void PersistLocalStorage(String origin, StorageBucket storage)
        {
            _localStorageHandler?.Write(origin, storage.ToEntries());
        }

        private void PersistSessionStorage(IWindow window, String origin, StorageBucket storage)
        {
            _sessionStorageHandler?.Write(GetStorageHandlerKey(ScopeSession, window, origin), storage.ToEntries());
        }

        private void NotifyStorageEvent(IWindow sourceWindow, String scope, String origin, String key, String oldValue, String newValue)
        {
            var sourceKey = GetStorageKey(scope, sourceWindow, origin);

            CleanupWindows();

            for (var i = 0; i < _windows.Count; i++)
            {
                if (!_windows[i].TryGetTarget(out var target))
                {
                    continue;
                }

                if (ReferenceEquals(target, sourceWindow))
                {
                    continue;
                }

                var targetOrigin = GetOrigin(target);
                var targetKey = GetStorageKey(scope, target, targetOrigin);

                if (!String.Equals(sourceKey, targetKey, StringComparison.Ordinal))
                {
                    continue;
                }

                target.FireSimpleEvent(EventNames.Storage, false, false);
            }
        }

        private void TrackWindow(IWindow window)
        {
            CleanupWindows();

            for (var i = 0; i < _windows.Count; i++)
            {
                if (_windows[i].TryGetTarget(out var target) && ReferenceEquals(target, window))
                {
                    return;
                }
            }

            _windows.Add(new WeakReference<IWindow>(window));
        }

        private void CleanupWindows()
        {
            for (var i = _windows.Count - 1; i >= 0; i--)
            {
                if (!_windows[i].TryGetTarget(out var _))
                {
                    _windows.RemoveAt(i);
                }
            }
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

        private static String GetStorageKey(String scope, IWindow window, String origin)
        {
            if (scope == ScopeLocal)
            {
                return scope + origin;
            }

            var root = GetRootContext(window?.Document?.Context);
            var rootKey = root == null ? String.Empty : RuntimeHelpers.GetHashCode(root).ToString(System.Globalization.CultureInfo.InvariantCulture);

            return scope + rootKey + "|" + origin;
        }

        private static String GetStorageHandlerKey(String scope, IWindow window, String origin)
        {
            if (scope == ScopeLocal)
            {
                return origin;
            }

            var root = GetRootContext(window?.Document?.Context);
            var rootKey = root == null ? String.Empty : RuntimeHelpers.GetHashCode(root).ToString(System.Globalization.CultureInfo.InvariantCulture);

            return rootKey + "|" + origin;
        }

        private static AngleSharp.IBrowsingContext GetRootContext(AngleSharp.IBrowsingContext context)
        {
            while (context?.Parent != null)
            {
                context = context.Parent;
            }

            return context;
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
}
