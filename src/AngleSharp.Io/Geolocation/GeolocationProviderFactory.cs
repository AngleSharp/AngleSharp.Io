namespace AngleSharp.Io.GeolocationApi
{
    using AngleSharp.Browser.Dom;
    using AngleSharp.Io.Dom;
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents the default Geolocation provider factory.
    /// </summary>
    public sealed class GeolocationProviderFactory : IGeolocationProviderFactory
    {
        private readonly IGeolocationPlatform _platform;
        private readonly ConditionalWeakTable<INavigator, Geolocation> _geolocations;

        /// <summary>
        /// Creates a new Geolocation provider factory.
        /// </summary>
        /// <param name="platform">The platform geolocation implementation.</param>
        public GeolocationProviderFactory(IGeolocationPlatform platform)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _geolocations = new ConditionalWeakTable<INavigator, Geolocation>();
        }

        /// <inheritdoc />
        public Geolocation GetGeolocation(INavigator navigator)
        {
            if (navigator == null)
            {
                return null;
            }

            return _geolocations.GetValue(navigator, _ => new Geolocation(_platform));
        }
    }
}