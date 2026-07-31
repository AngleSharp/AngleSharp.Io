namespace AngleSharp.Io.Dom
{
    using AngleSharp.Dom;

    /// <summary>
    /// Represents a factory for creating Cache Storage views per window.
    /// </summary>
    public interface ICacheProviderFactory
    {
        /// <summary>
        /// Gets the Cache Storage object for the provided window.
        /// </summary>
        /// <param name="window">The window requesting cache access.</param>
        /// <returns>The Cache Storage object or null.</returns>
        ICacheStorage GetCaches(IWindow window);
    }
}