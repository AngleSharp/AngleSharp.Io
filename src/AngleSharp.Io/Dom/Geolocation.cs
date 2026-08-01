namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the Geolocation API.
    /// </summary>
    [DomName("Geolocation")]
    public sealed class Geolocation
    {
        private readonly IGeolocationPlatform _platform;

        /// <summary>
        /// Creates a new geolocation wrapper using the provided platform implementation.
        /// </summary>
        /// <param name="platform">The platform geolocation implementation.</param>
        public Geolocation(IGeolocationPlatform platform)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        }

        /// <summary>
        /// Gets the current position with default options.
        /// </summary>
        /// <returns>The current position.</returns>
        [DomName("getCurrentPosition")]
        public Task<GeolocationPosition> GetCurrentPosition()
        {
            return GetCurrentPosition(new GeolocationOptions());
        }

        /// <summary>
        /// Gets the current position with the provided options.
        /// </summary>
        /// <param name="options">The position request options.</param>
        /// <returns>The current position.</returns>
        [DomName("getCurrentPosition")]
        public async Task<GeolocationPosition> GetCurrentPosition(GeolocationOptions options)
        {
            var reading = await _platform.GetCurrentPositionAsync(options ?? new GeolocationOptions()).ConfigureAwait(false);
            return GeolocationPosition.FromReading(reading ?? GeolocationReading.Empty);
        }
    }

    /// <summary>
    /// Represents geolocation request options.
    /// </summary>
    [DomName("PositionOptions")]
    public sealed class GeolocationOptions
    {
        /// <summary>
        /// Gets or sets if high accuracy should be preferred.
        /// </summary>
        [DomName("enableHighAccuracy")]
        public Boolean EnableHighAccuracy { get; set; }

        /// <summary>
        /// Gets or sets the timeout in milliseconds.
        /// </summary>
        [DomName("timeout")]
        public Int64 Timeout { get; set; }

        /// <summary>
        /// Gets or sets the maximum age in milliseconds.
        /// </summary>
        [DomName("maximumAge")]
        public Int64 MaximumAge { get; set; }
    }

    /// <summary>
    /// Represents a geolocation reading returned by the platform implementation.
    /// </summary>
    public sealed class GeolocationReading
    {
        /// <summary>
        /// Gets an empty geolocation reading.
        /// </summary>
        public static GeolocationReading Empty { get; } = new GeolocationReading();

        /// <summary>
        /// Gets or sets the latitude.
        /// </summary>
        public Double Latitude { get; set; }

        /// <summary>
        /// Gets or sets the longitude.
        /// </summary>
        public Double Longitude { get; set; }

        /// <summary>
        /// Gets or sets the accuracy in meters.
        /// </summary>
        public Double Accuracy { get; set; }

        /// <summary>
        /// Gets or sets the altitude in meters.
        /// </summary>
        public Double? Altitude { get; set; }

        /// <summary>
        /// Gets or sets the altitude accuracy in meters.
        /// </summary>
        public Double? AltitudeAccuracy { get; set; }

        /// <summary>
        /// Gets or sets the heading in degrees.
        /// </summary>
        public Double? Heading { get; set; }

        /// <summary>
        /// Gets or sets the speed in meters per second.
        /// </summary>
        public Double? Speed { get; set; }

        /// <summary>
        /// Gets or sets the timestamp in milliseconds since Unix epoch.
        /// </summary>
        public Int64 Timestamp { get; set; }
    }

    /// <summary>
    /// Represents a geolocation position.
    /// </summary>
    [DomName("GeolocationPosition")]
    public sealed class GeolocationPosition
    {
        internal GeolocationPosition(GeolocationCoordinates coordinates, Int64 timestamp)
        {
            Coordinates = coordinates;
            Timestamp = timestamp;
        }

        /// <summary>
        /// Gets the coordinates.
        /// </summary>
        [DomName("coords")]
        public GeolocationCoordinates Coordinates { get; }

        /// <summary>
        /// Gets the timestamp in milliseconds since Unix epoch.
        /// </summary>
        [DomName("timestamp")]
        public Int64 Timestamp { get; }

        internal static GeolocationPosition FromReading(GeolocationReading reading)
        {
            var timestamp = reading.Timestamp;

            if (timestamp <= 0)
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            var coordinates = new GeolocationCoordinates(
                reading.Latitude,
                reading.Longitude,
                reading.Accuracy,
                reading.Altitude,
                reading.AltitudeAccuracy,
                reading.Heading,
                reading.Speed);

            return new GeolocationPosition(coordinates, timestamp);
        }
    }

    /// <summary>
    /// Represents geolocation coordinates.
    /// </summary>
    [DomName("GeolocationCoordinates")]
    public sealed class GeolocationCoordinates
    {
        internal GeolocationCoordinates(Double latitude, Double longitude, Double accuracy, Double? altitude, Double? altitudeAccuracy, Double? heading, Double? speed)
        {
            Latitude = latitude;
            Longitude = longitude;
            Accuracy = accuracy;
            Altitude = altitude;
            AltitudeAccuracy = altitudeAccuracy;
            Heading = heading;
            Speed = speed;
        }

        /// <summary>
        /// Gets the latitude.
        /// </summary>
        [DomName("latitude")]
        public Double Latitude { get; }

        /// <summary>
        /// Gets the longitude.
        /// </summary>
        [DomName("longitude")]
        public Double Longitude { get; }

        /// <summary>
        /// Gets the accuracy in meters.
        /// </summary>
        [DomName("accuracy")]
        public Double Accuracy { get; }

        /// <summary>
        /// Gets the altitude in meters.
        /// </summary>
        [DomName("altitude")]
        public Double? Altitude { get; }

        /// <summary>
        /// Gets the altitude accuracy in meters.
        /// </summary>
        [DomName("altitudeAccuracy")]
        public Double? AltitudeAccuracy { get; }

        /// <summary>
        /// Gets the heading in degrees.
        /// </summary>
        [DomName("heading")]
        public Double? Heading { get; }

        /// <summary>
        /// Gets the speed in meters per second.
        /// </summary>
        [DomName("speed")]
        public Double? Speed { get; }
    }
}