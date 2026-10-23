namespace AngleSharp.Io.StorageManagerApi
{
    using AngleSharp.Browser.Dom;
    using AngleSharp.Io.Dom;
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents the default StorageManager provider factory.
    /// </summary>
    public sealed class StorageManagerProviderFactory : IStorageManagerProviderFactory
    {
        private readonly IStorageManagerPlatform _platform;
        private readonly ConditionalWeakTable<INavigator, StorageManager> _managers;

        /// <summary>
        /// Creates a new StorageManager provider factory.
        /// </summary>
        /// <param name="platform">The platform storage implementation.</param>
        public StorageManagerProviderFactory(IStorageManagerPlatform platform)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _managers = new ConditionalWeakTable<INavigator, StorageManager>();
        }

        /// <inheritdoc />
        public StorageManager GetStorageManager(INavigator navigator)
        {
            if (navigator == null)
            {
                return null;
            }

            // the origin is resolved per call as the active document changes
            // while the navigator stays the same
            return _managers.GetValue(navigator, _ => new StorageManager(_platform, () => GetOrigin(navigator), navigator.Permissions()));
        }

        private static String GetOrigin(INavigator navigator)
        {
            var window = NavigatorExtensions.GetContext(navigator)?.Active?.DefaultView;
            return LockManager.GetOrigin(window);
        }
    }
}
