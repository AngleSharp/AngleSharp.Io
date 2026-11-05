namespace AngleSharp.Io.Dom
{
    using AngleSharp.Browser.Dom;
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a set of extensions for the navigator object.
    /// </summary>
    [DomExposed("Navigator")]
    public static class NavigatorExtensions
    {
        private static readonly ConditionalWeakTable<INavigator, Clipboard> DeniedClipboards = new ConditionalWeakTable<INavigator, Clipboard>();
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
        /// Gets the user's preferred language, i.e., the first entry of
        /// navigator.languages.
        /// </summary>
        [DomName("language")]
        [DomAccessor(Accessors.Getter)]
        public static String Language(this INavigator navigator) => navigator.Languages()[0];

        /// <summary>
        /// Gets the user's preferred languages, most preferred first.
        /// </summary>
        [DomName("languages")]
        [DomAccessor(Accessors.Getter)]
        public static String[] Languages(this INavigator navigator)
        {
            // navigators not registered by this library fall back to the UI culture
            var languages = (navigator as Navigator)?.Languages ?? Navigator.GetLanguages(null);
            return (String[])languages.Clone();
        }

        /// <summary>
        /// Gets if cookies can be used.
        /// </summary>
        [DomName("cookieEnabled")]
        [DomAccessor(Accessors.Getter)]
        public static Boolean CookieEnabled(this INavigator navigator) => true;

        /// <summary>
        /// Gets the number of logical processors.
        /// </summary>
        [DomName("hardwareConcurrency")]
        [DomAccessor(Accessors.Getter)]
        public static Int32 HardwareConcurrency(this INavigator navigator) => Environment.ProcessorCount;

        /// <summary>
        /// Gets the number of touch points the device has, which is none here.
        /// </summary>
        [DomName("maxTouchPoints")]
        [DomAccessor(Accessors.Getter)]
        public static Int32 MaxTouchPoints(this INavigator navigator) => 0;

        /// <summary>
        /// Gets if the browser is automated, which only a host can tell.
        /// </summary>
        [DomName("webdriver")]
        [DomAccessor(Accessors.Getter)]
        public static Boolean Webdriver(this INavigator navigator) => false;

        /// <summary>
        /// Gets the clipboard object.
        /// </summary>
        public static Clipboard Clipboard(this INavigator navigator)
        {
            var context = GetContext(navigator);
            var factory = context?.GetService<IClipboardProviderFactory>();
            return factory?.GetClipboard(navigator);
        }

        /// <summary>
        /// Gets the clipboard object as script sees it: pages test for its members without
        /// expecting it to be null, so without a platform it is one denying all access.
        /// </summary>
        [DomName("clipboard")]
        [DomAccessor(Accessors.Getter)]
        public static Clipboard ClipboardOrDenied(this INavigator navigator) =>
            navigator.Clipboard() ?? DeniedClipboards.GetValue(navigator, _ => new Clipboard(new DeniedClipboardPlatform()));

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

    sealed class DeniedClipboardPlatform : IClipboardPlatform
    {
        public Task<String> ReadTextAsync() => throw new DomException(Permissions.NotAllowedError);

        public Task WriteTextAsync(String text) => throw new DomException(Permissions.NotAllowedError);
    }
}
