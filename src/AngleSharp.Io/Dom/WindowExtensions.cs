namespace AngleSharp.Io.Dom
{
    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Attributes;

    /// <summary>
    /// Defines a set of extensions for the window object.
    /// </summary>
    [DomExposed("Window")]
    public static class WindowExtensions
    {
        /// <summary>
        /// Gets the localStorage object.
        /// </summary>
        [DomName("localStorage")]
        [DomAccessor(Accessors.Getter)]
        public static ILocalStorage LocalStorage(this IWindow window)
        {
            var factory = window?.Document?.Context?.GetService<IStorageProviderFactory>();
            return factory?.GetStorages(window)?.Local;
        }
        
        /// <summary>
        /// Gets the sessionStorage object.
        /// </summary>
        [DomName("sessionStorage")]
        [DomAccessor(Accessors.Getter)]
        public static ISessionStorage SessionStorage(this IWindow window)
        {
            var factory = window?.Document?.Context?.GetService<IStorageProviderFactory>();
            return factory?.GetStorages(window)?.Session;
        }

        /// <summary>
        /// Gets the indexedDB object.
        /// </summary>
        [DomName("indexedDB")]
        [DomAccessor(Accessors.Getter)]
        public static IIndexedDbFactory IndexedDb(this IWindow window)
        {
            var factory = window?.Document?.Context?.GetService<IIndexedDbProviderFactory>();
            return factory?.GetIndexedDb(window);
        }

        /// <summary>
        /// Gets the caches object.
        /// </summary>
        [DomName("caches")]
        [DomAccessor(Accessors.Getter)]
        public static ICacheStorage Caches(this IWindow window)
        {
            var factory = window?.Document?.Context?.GetService<ICacheProviderFactory>();
            return factory?.GetCaches(window);
        }
    }
}
