namespace AngleSharp.Io.Dom
{
    using AngleSharp.Browser.Dom;

    /// <summary>
    /// Represents a factory for creating Geolocation views per navigator.
    /// </summary>
    public interface IGeolocationProviderFactory
    {
        /// <summary>
        /// Gets the Geolocation object for the provided navigator.
        /// </summary>
        /// <param name="navigator">The navigator requesting geolocation access.</param>
        /// <returns>The Geolocation object or null.</returns>
        Geolocation GetGeolocation(INavigator navigator);
    }
}