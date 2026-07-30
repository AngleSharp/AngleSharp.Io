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
        public static ILocalStorage LocalStorage(this IWindow window) =>
            window.Document.Context?.GetService<ILocalStorage>();
        
        /// <summary>
        /// Gets the sessionStorage object.
        /// </summary>
        [DomName("sessionStorage")]
        [DomAccessor(Accessors.Getter)]
        public static ISessionStorage SessionStorage(this IWindow window) =>
            window.Document.Context?.GetService<ISessionStorage>();
    }
}
