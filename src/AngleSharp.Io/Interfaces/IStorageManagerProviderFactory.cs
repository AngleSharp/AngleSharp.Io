namespace AngleSharp.Io.Dom
{
    using AngleSharp.Browser.Dom;

    /// <summary>
    /// Represents a factory for creating StorageManager views per navigator.
    /// </summary>
    public interface IStorageManagerProviderFactory
    {
        /// <summary>
        /// Gets the StorageManager object for the provided navigator.
        /// </summary>
        /// <param name="navigator">The navigator requesting storage access.</param>
        /// <returns>The StorageManager object or null.</returns>
        StorageManager GetStorageManager(INavigator navigator);
    }
}
