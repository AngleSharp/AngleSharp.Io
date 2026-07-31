namespace AngleSharp.Io.Dom
{
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the platform geolocation implementation used by the DOM geolocation wrapper.
    /// </summary>
    public interface IGeolocationPlatform
    {
        /// <summary>
        /// Gets the current geolocation reading.
        /// </summary>
        /// <param name="options">The request options.</param>
        /// <returns>The current geolocation reading.</returns>
        Task<GeolocationReading> GetCurrentPositionAsync(GeolocationOptions options);
    }
}