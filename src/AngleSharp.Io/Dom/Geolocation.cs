namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Browser;
    using AngleSharp.Dom;
    using AngleSharp.Io.PermissionsApi;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// The callback receiving a position.
    /// </summary>
    /// <param name="position">The position.</param>
    [DomName("PositionCallback")]
    public delegate void PositionCallback(GeolocationPosition position);

    /// <summary>
    /// The callback receiving the error of a position request.
    /// </summary>
    /// <param name="error">The error.</param>
    [DomName("PositionErrorCallback")]
    public delegate void PositionErrorCallback(GeolocationPositionError error);

    /// <summary>
    /// Represents the Geolocation API.
    /// </summary>
    [DomName("Geolocation")]
    public sealed class Geolocation
    {
        private readonly IGeolocationPlatform _platform;
        private readonly Permissions _permissions;
        private readonly HashSet<Int32> _watches = new HashSet<Int32>();
        private Int32 _nextWatchId;

        /// <summary>
        /// Creates a new geolocation wrapper using the provided platform implementation.
        /// </summary>
        /// <param name="platform">The platform geolocation implementation.</param>
        public Geolocation(IGeolocationPlatform platform)
            : this(platform, null)
        {
        }

        /// <summary>
        /// Creates a new geolocation wrapper using the provided platform
        /// implementation, which is only used when the permissions allow it.
        /// </summary>
        /// <param name="platform">The platform geolocation implementation.</param>
        /// <param name="permissions">The permissions to check, or null to allow every access.</param>
        public Geolocation(IGeolocationPlatform platform, Permissions permissions)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _permissions = permissions;
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
            if (_permissions != null)
            {
                await _permissions.EnsureGrantedAsync(PermissionNames.Geolocation).ConfigureAwait(false);
            }

            var reading = await _platform.GetCurrentPositionAsync(options ?? new GeolocationOptions()).ConfigureAwait(false);
            return GeolocationPosition.FromReading(reading ?? GeolocationReading.Empty);
        }

        /// <summary>
        /// Gets the current position, which is given to the success callback, or the
        /// error to the error callback.
        /// </summary>
        /// <param name="success">The callback receiving the position.</param>
        /// <param name="error">The callback receiving the error, if any.</param>
        /// <param name="options">The position request options.</param>
        [DomName("getCurrentPosition")]
        public void GetCurrentPosition(PositionCallback success, PositionErrorCallback error = null, GeolocationOptions options = null)
        {
            Report(success, error, options, () => true).Forget();
        }

        /// <summary>
        /// Watches the position, which is given to the success callback (or the error
        /// to the error callback) until the watch is cleared. The platform only gives the
        /// current position, so it is reported once.
        /// </summary>
        /// <param name="success">The callback receiving the position.</param>
        /// <param name="error">The callback receiving the error, if any.</param>
        /// <param name="options">The position request options.</param>
        /// <returns>The id of the watch.</returns>
        [DomName("watchPosition")]
        public Int32 WatchPosition(PositionCallback success, PositionErrorCallback error = null, GeolocationOptions options = null)
        {
            var id = ++_nextWatchId;
            _watches.Add(id);
            Report(success, error, options, () => _watches.Contains(id)).Forget();
            return id;
        }

        /// <summary>
        /// Clears the watch with the given id.
        /// </summary>
        /// <param name="watchId">The id of the watch.</param>
        [DomName("clearWatch")]
        public void ClearWatch(Int32 watchId)
        {
            _watches.Remove(watchId);
        }

        /// <summary>
        /// Gets or sets the event loop the callbacks run on.
        /// </summary>
        internal IEventLoop Loop { get; set; }

        private async Task Report(PositionCallback success, PositionErrorCallback error, GeolocationOptions options, Func<Boolean> isWanted)
        {
            Action report;

            try
            {
                var position = await GetCurrentPosition(options).ConfigureAwait(false);
                report = () => success?.Invoke(position);
            }
            catch (DomException ex) when (ex.Name == Permissions.NotAllowedError)
            {
                var positionError = new GeolocationPositionError(GeolocationPositionErrorCode.PermissionDenied, "User denied Geolocation");
                report = () => error?.Invoke(positionError);
            }
            catch (TimeoutException ex)
            {
                var positionError = new GeolocationPositionError(GeolocationPositionErrorCode.Timeout, ex.Message);
                report = () => error?.Invoke(positionError);
            }
            catch (Exception ex)
            {
                var positionError = new GeolocationPositionError(GeolocationPositionErrorCode.PositionUnavailable, ex.Message);
                report = () => error?.Invoke(positionError);
            }

            void Run()
            {
                if (isWanted.Invoke())
                {
                    report.Invoke();
                }
            }

            if (Loop != null)
            {
                Loop.Enqueue(Run);
            }
            else
            {
                Run();
            }
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
    /// The codes of a geolocation error.
    /// </summary>
    [DomName("GeolocationPositionError")]
    public enum GeolocationPositionErrorCode : ushort
    {
        /// <summary>
        /// The permission to get the position was not given.
        /// </summary>
        [DomName("PERMISSION_DENIED")]
        PermissionDenied = 1,
        /// <summary>
        /// The position could not be determined.
        /// </summary>
        [DomName("POSITION_UNAVAILABLE")]
        PositionUnavailable = 2,
        /// <summary>
        /// The position was not determined in time.
        /// </summary>
        [DomName("TIMEOUT")]
        Timeout = 3,
    }

    /// <summary>
    /// Represents the error of a position request.
    /// </summary>
    [DomName("GeolocationPositionError")]
    public sealed class GeolocationPositionError
    {
        internal GeolocationPositionError(GeolocationPositionErrorCode code, String message)
        {
            Code = code;
            Message = message ?? String.Empty;
        }

        /// <summary>
        /// Gets the code of the error.
        /// </summary>
        [DomName("code")]
        public GeolocationPositionErrorCode Code { get; }

        /// <summary>
        /// Gets the message of the error.
        /// </summary>
        [DomName("message")]
        public String Message { get; }
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