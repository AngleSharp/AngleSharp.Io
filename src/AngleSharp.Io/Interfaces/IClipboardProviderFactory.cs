namespace AngleSharp.Io.Dom
{
    using AngleSharp.Browser.Dom;

    /// <summary>
    /// Represents a factory for creating Clipboard views per navigator.
    /// </summary>
    public interface IClipboardProviderFactory
    {
        /// <summary>
        /// Gets the Clipboard object for the provided navigator.
        /// </summary>
        /// <param name="navigator">The navigator requesting clipboard access.</param>
        /// <returns>The Clipboard object or null.</returns>
        Clipboard GetClipboard(INavigator navigator);
    }
}