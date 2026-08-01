namespace AngleSharp.Io.Dom
{
    using AngleSharp.Dom;

    /// <summary>
    /// Represents a factory for creating IndexedDB views per window.
    /// </summary>
    public interface IIndexedDbProviderFactory
    {
        /// <summary>
        /// Gets the IndexedDB factory for the provided window.
        /// </summary>
        /// <param name="window">The window requesting IndexedDB access.</param>
        /// <returns>The IndexedDB factory or null.</returns>
        IIndexedDbFactory GetIndexedDb(IWindow window);
    }
}