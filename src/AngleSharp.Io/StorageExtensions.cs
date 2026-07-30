namespace AngleSharp
{
    using AngleSharp.Io.Dom;

    /// <summary>
    /// Extensions for resolving storage services.
    /// </summary>
    public static class StorageExtensions
    {
        /// <summary>
        /// Gets the configured local storage service, if any.
        /// </summary>
        /// <param name="context">The browsing context to inspect.</param>
        /// <returns>The local storage service or null.</returns>
        public static ILocalStorage GetLocalStorage(this IBrowsingContext context) =>
            context?.GetService<ILocalStorage>();

        /// <summary>
        /// Gets the configured session storage service, if any.
        /// </summary>
        /// <param name="context">The browsing context to inspect.</param>
        /// <returns>The session storage service or null.</returns>
        public static ISessionStorage GetSessionStorage(this IBrowsingContext context) =>
            context?.GetService<ISessionStorage>();

    }
}
