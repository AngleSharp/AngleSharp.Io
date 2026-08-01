namespace AngleSharp.Io.Tests.Geolocation
{
    using AngleSharp;
    using AngleSharp.Browser.Dom;
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    [TestFixture]
    public class GeolocationTests
    {
        [Test]
        public async Task GeolocationCanBeResolvedFromNavigatorWhenRegistered()
        {
            var platform = new FakeGeolocationPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithGeolocation(platform), "https://geo.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);

            Assert.IsNotNull(navigator.Geolocation());
        }

        [Test]
        public async Task GeolocationMapsPlatformReadingToDomTypes()
        {
            var platform = new FakeGeolocationPlatform
            {
                NextReading = new GeolocationReading
                {
                    Latitude = 52.52,
                    Longitude = 13.405,
                    Accuracy = 8.0,
                    Altitude = 34.5,
                    AltitudeAccuracy = 1.2,
                    Heading = 90.0,
                    Speed = 2.5,
                    Timestamp = 1234567890,
                }
            };

            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithGeolocation(platform), "https://geo.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);
            var geolocation = navigator.Geolocation();

            var position = await geolocation.GetCurrentPosition().ConfigureAwait(false);

            Assert.AreEqual(52.52, position.Coordinates.Latitude);
            Assert.AreEqual(13.405, position.Coordinates.Longitude);
            Assert.AreEqual(8.0, position.Coordinates.Accuracy);
            Assert.AreEqual(34.5, position.Coordinates.Altitude);
            Assert.AreEqual(1.2, position.Coordinates.AltitudeAccuracy);
            Assert.AreEqual(90.0, position.Coordinates.Heading);
            Assert.AreEqual(2.5, position.Coordinates.Speed);
            Assert.AreEqual(1234567890, position.Timestamp);
        }

        [Test]
        public async Task GeolocationForwardsRequestOptionsToPlatform()
        {
            var platform = new FakeGeolocationPlatform
            {
                NextReading = new GeolocationReading
                {
                    Latitude = 1.0,
                    Longitude = 2.0,
                    Accuracy = 3.0,
                }
            };

            var options = new GeolocationOptions
            {
                EnableHighAccuracy = true,
                Timeout = 1500,
                MaximumAge = 200,
            };

            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithGeolocation(platform), "https://geo.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);
            var geolocation = navigator.Geolocation();

            await geolocation.GetCurrentPosition(options).ConfigureAwait(false);

            Assert.IsNotNull(platform.LastOptions);
            Assert.AreEqual(true, platform.LastOptions.EnableHighAccuracy);
            Assert.AreEqual(1500, platform.LastOptions.Timeout);
            Assert.AreEqual(200, platform.LastOptions.MaximumAge);
        }

        private static async Task<IBrowsingContext> OpenContextAsync(IConfiguration configuration, String address)
        {
            var context = BrowsingContext.New(configuration);
            await context.OpenAsync(res => res.Address(address).Content("<!doctype html><title>geolocation</title>")).ConfigureAwait(false);
            return context;
        }

        private static INavigator GetNavigator(IBrowsingContext context)
        {
            var navigator = context?.Active?.DefaultView?.Navigator;
            Assert.IsNotNull(navigator);
            return navigator;
        }

        private sealed class FakeGeolocationPlatform : IGeolocationPlatform
        {
            public GeolocationReading NextReading { get; set; } = GeolocationReading.Empty;

            public GeolocationOptions LastOptions { get; private set; }

            public Task<GeolocationReading> GetCurrentPositionAsync(GeolocationOptions options)
            {
                LastOptions = options;
                return Task.FromResult(NextReading);
            }
        }
    }
}