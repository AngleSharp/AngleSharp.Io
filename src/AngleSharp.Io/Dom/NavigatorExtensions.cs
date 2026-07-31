namespace AngleSharp.Io.Dom
{
    using AngleSharp.Browser.Dom;
    using AngleSharp.Attributes;
    using System;

    /// <summary>
    /// Defines a set of extensions for the navigator object.
    /// </summary>
    [DomExposed("Navigator")]
    public static class NavigatorExtensions
    {
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

        private static AngleSharp.IBrowsingContext GetContext(INavigator navigator)
        {
            return (navigator as Navigator)?.Context;
        }
    }
}