namespace AngleSharp.Io.Dom
{
    using AngleSharp.Browser.Dom;
    using AngleSharp.Attributes;
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Defines a set of extensions for the navigator object.
    /// </summary>
    [DomExposed("Navigator")]
    public static class NavigatorExtensions
    {
        private static readonly ConditionalWeakTable<INavigator, Permissions> PermissionsByNavigator = new ConditionalWeakTable<INavigator, Permissions>();

        /// <summary>
        /// Gets the permissions object.
        /// </summary>
        [DomName("permissions")]
        [DomAccessor(Accessors.Getter)]
        public static Permissions Permissions(this INavigator navigator)
        {
            var context = GetContext(navigator);
            var policy = context?.GetService<IPermissionPolicy>();

            if (policy == null)
            {
                return null;
            }

            // the origin is resolved per call as the active document changes
            // while the navigator stays the same
            return PermissionsByNavigator.GetValue(navigator, _ => new Permissions(policy, () => LockManager.GetOrigin(context.Active?.DefaultView)));
        }

        /// <summary>
        /// Gets the clipboard object.
        /// </summary>
        [DomName("clipboard")]
        [DomAccessor(Accessors.Getter)]
        public static Clipboard Clipboard(this INavigator navigator)
        {
            var context = GetContext(navigator);
            var factory = context?.GetService<IClipboardProviderFactory>();
            return factory?.GetClipboard(navigator);
        }

        /// <summary>
        /// Gets the geolocation object.
        /// </summary>
        [DomName("geolocation")]
        [DomAccessor(Accessors.Getter)]
        public static Geolocation Geolocation(this INavigator navigator)
        {
            var context = GetContext(navigator);
            var factory = context?.GetService<IGeolocationProviderFactory>();
            return factory?.GetGeolocation(navigator);
        }

        /// <summary>
        /// Gets the storage manager object.
        /// </summary>
        [DomName("storage")]
        [DomAccessor(Accessors.Getter)]
        public static StorageManager Storage(this INavigator navigator)
        {
            var context = GetContext(navigator);
            var factory = context?.GetService<IStorageManagerProviderFactory>();
            return factory?.GetStorageManager(navigator);
        }

        /// <summary>
        /// Gets the service worker container object. Only available in secure
        /// contexts, i.e., for https or localhost documents.
        /// </summary>
        [DomName("serviceWorker")]
        [DomAccessor(Accessors.Getter)]
        public static ServiceWorkerContainer ServiceWorker(this INavigator navigator)
        {
            var context = GetContext(navigator);
            var address = context?.Active?.Url;

            if (String.IsNullOrEmpty(address) || !ServiceWorkerContainer.IsPotentiallyTrustworthy(new AngleSharp.Dom.Url(address)))
            {
                return null;
            }

            var factory = context.GetService<IServiceWorkerProviderFactory>();
            return factory?.GetServiceWorkerContainer(navigator);
        }

        /// <summary>
        /// Queues a POST request without body to the given URL.
        /// </summary>
        /// <param name="navigator">The navigator sending the beacon.</param>
        /// <param name="url">The target URL, relative to the current document.</param>
        /// <returns>True if the beacon was queued, otherwise false.</returns>
        [DomName("sendBeacon")]
        public static Boolean SendBeacon(this INavigator navigator, String url) => navigator.SendBeacon(url, null);

        /// <summary>
        /// Queues a POST request with the given data to the given URL. The
        /// request is sent in the background, i.e., its outcome is not
        /// observable.
        /// </summary>
        /// <param name="navigator">The navigator sending the beacon.</param>
        /// <param name="url">The target URL, relative to the current document.</param>
        /// <param name="data">
        /// The data to send, e.g., a string, a blob, a byte array, form data,
        /// or URL search parameters.
        /// </param>
        /// <returns>
        /// True if the beacon was queued, otherwise false, e.g., when the data
        /// exceeds the quota or is not supported.
        /// </returns>
        [DomName("sendBeacon")]
        public static Boolean SendBeacon(this INavigator navigator, String url, Object data) =>
            BeaconSender.Send(GetContext(navigator), url, data);

        /// <summary>
        /// Gets the locks object.
        /// </summary>
        [DomName("locks")]
        [DomAccessor(Accessors.Getter)]
        public static LockManager Locks(this INavigator navigator)
        {
            var context = GetContext(navigator);

            if (context == null)
            {
                return null;
            }

            var scope = LockManager.GetScopeKey(context);
            return String.IsNullOrEmpty(scope) ? null : new LockManager(scope);
        }

        internal static AngleSharp.IBrowsingContext GetContext(INavigator navigator)
        {
            return (navigator as Navigator)?.Context;
        }
    }
}