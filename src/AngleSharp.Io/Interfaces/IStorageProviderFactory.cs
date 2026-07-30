namespace AngleSharp.Io.Dom
{
    using AngleSharp.Dom;

    /// <summary>
    /// Represents a factory for creating storage views per window.
    /// </summary>
    public interface IStorageProviderFactory
    {
        /// <summary>
        /// Gets the storage views for the provided window.
        /// </summary>
        /// <param name="window">The window requesting storage.</param>
        /// <returns>The storage views or null.</returns>
        StorageViews GetStorages(IWindow window);
    }
}
