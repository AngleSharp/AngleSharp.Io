namespace AngleSharp.Io.Dom
{
    using AngleSharp.Browser.Dom;
    using AngleSharp.Attributes;
    using System;
    using System.Reflection;

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
            if (navigator == null)
            {
                return null;
            }

            var type = navigator.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var property = type.GetProperty("Context", flags);

            if (property?.GetValue(navigator) is AngleSharp.IBrowsingContext context)
            {
                return context;
            }

            var field = type.GetField("_context", flags);

            if (field?.GetValue(navigator) is AngleSharp.IBrowsingContext fieldContext)
            {
                return fieldContext;
            }

            field = type.GetField("context", flags);

            if (field?.GetValue(navigator) is AngleSharp.IBrowsingContext lowerContext)
            {
                return lowerContext;
            }

            return null;
        }
    }
}