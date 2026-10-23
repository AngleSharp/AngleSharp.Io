namespace AngleSharp.Io.Tests
{
    using AngleSharp.Browser.Dom;
    using AngleSharp.Io.Cache;
    using AngleSharp.Io.Cookie;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.IndexedDb;
    using AngleSharp.Io.Reporting;
    using AngleSharp.Io.ServiceWorkerApi;
    using AngleSharp.Io.Storage;
    using AngleSharp.Io.StorageManagerApi;
    using NUnit.Framework;
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    [TestFixture]
    public sealed class IoConfigurationExtensionsTests
    {
        [Test]
        public void WithIoRegistersCoreServices()
        {
            var options = new IoOptions
            {
                CookieHandler = new MemoryFileHandler(),
                HttpHandler = new HttpClientHandler(),
                StorageFactory = StorageProviderFactory.CreateTemporary(),
                IndexedDbFactory = IndexedDbProviderFactory.CreateTemporary(),
                CacheFactory = CacheProviderFactory.CreateTemporary(),
                Beacon = new BeaconOptions { Quota = 1024 },
                Reporting = new ReportingOptions(),
            };

            var configuration = Configuration.Default.WithIo(options);
            var requesters = configuration.Services.OfType<IRequester>().ToArray();

            Assert.IsNotNull(configuration.Services.OfType<ICookieProvider>().FirstOrDefault());
            Assert.AreSame(options.StorageFactory, configuration.Services.OfType<IStorageProviderFactory>().FirstOrDefault());
            Assert.AreSame(options.IndexedDbFactory, configuration.Services.OfType<IIndexedDbProviderFactory>().FirstOrDefault());
            Assert.AreSame(options.CacheFactory, configuration.Services.OfType<ICacheProviderFactory>().FirstOrDefault());
            Assert.AreSame(options.Beacon, configuration.Services.OfType<BeaconOptions>().FirstOrDefault());
            Assert.AreSame(options.Reporting, configuration.Services.OfType<ReportingService>().Single().Options);
            Assert.IsTrue(requesters.Any(m => m is ReportingRequester));
            Assert.IsTrue(requesters.Any(m => m.SupportsProtocol("http")));
            Assert.IsTrue(requesters.Any(m => m.SupportsProtocol("data")));
            Assert.IsTrue(requesters.Any(m => m.SupportsProtocol("ftp")));
            Assert.IsTrue(requesters.Any(m => m.SupportsProtocol("file")));
            Assert.IsTrue(requesters.Any(m => m.SupportsProtocol("about")));
            Assert.AreEqual(false, options.HttpHandler.UseCookies);
            Assert.AreEqual(false, options.HttpHandler.AllowAutoRedirect);
        }

        [Test]
        public void WithIoAcceptsNullOptions()
        {
            var configuration = Configuration.Default.WithIo(null);

            Assert.IsNotNull(configuration.Services.OfType<ICookieProvider>().FirstOrDefault());
            Assert.IsNotNull(configuration.Services.OfType<IStorageProviderFactory>().FirstOrDefault());
            Assert.IsNotNull(configuration.Services.OfType<IIndexedDbProviderFactory>().FirstOrDefault());
            Assert.IsNotNull(configuration.Services.OfType<ICacheProviderFactory>().FirstOrDefault());
            Assert.IsNull(configuration.Services.OfType<ReportingService>().FirstOrDefault());
        }

        [Test]
        public async Task WithIoRegistersOptionalNavigatorApisWhenPlatformsAreProvided()
        {
            var options = new IoOptions
            {
                ClipboardPlatform = new FakeClipboardPlatform(),
                GeolocationPlatform = new FakeGeolocationPlatform(),
                StorageManagerPlatform = new MemoryStorageManagerPlatform(),
                ServiceWorkerPlatform = new ServiceWorkerPlatform(),
            };

            var context = await OpenContextAsync(Configuration.Default.WithIo(options), "https://io.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);

            Assert.IsNotNull(navigator.Clipboard());
            Assert.IsNotNull(navigator.Geolocation());
            Assert.IsNotNull(navigator.Storage());
            Assert.IsNotNull(navigator.ServiceWorker());
        }

        [Test]
        public async Task WithIoLeavesOptionalNavigatorApisUnavailableByDefault()
        {
            var context = await OpenContextAsync(Configuration.Default.WithIo(new IoOptions()), "https://io.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);

            Assert.IsNull(navigator.Clipboard());
            Assert.IsNull(navigator.Geolocation());
            Assert.IsNull(navigator.Storage());
            Assert.IsNull(navigator.ServiceWorker());
        }

        [Test]
        public async Task WithIoExposesConfiguredNavigatorLanguages()
        {
            var options = new IoOptions
            {
                Navigator = new NavigatorOptions { Languages = new[] { "de-AT", "de", "", "DE-at" } },
            };

            var context = await OpenContextAsync(Configuration.Default.WithIo(options), "https://io.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);

            CollectionAssert.AreEqual(new[] { "de-AT", "de" }, navigator.Languages());
            Assert.AreEqual("de-AT", navigator.Language());
        }

        [Test]
        public async Task WithIoDerivesNavigatorLanguagesFromUiCultureByDefault()
        {
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo("fr-CA");

            try
            {
                var context = await OpenContextAsync(Configuration.Default.WithIo(new IoOptions()), "https://io.example/").ConfigureAwait(false);
                var navigator = GetNavigator(context);

                CollectionAssert.AreEqual(new[] { "fr-CA", "fr" }, navigator.Languages());
                Assert.AreEqual("fr-CA", navigator.Language());
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        }

        [Test]
        public async Task NavigatorLanguagesReturnsACopy()
        {
            var options = new IoOptions
            {
                Navigator = new NavigatorOptions { Languages = new[] { "en-US", "en" } },
            };

            var context = await OpenContextAsync(Configuration.Default.WithIo(options), "https://io.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);
            navigator.Languages()[0] = "xx";

            Assert.AreEqual("en-US", navigator.Language());
        }

        private static async Task<IBrowsingContext> OpenContextAsync(IConfiguration configuration, String address)
        {
            var context = BrowsingContext.New(configuration);
            await context.OpenAsync(res => res.Address(address).Content("<!doctype html><title>with-io</title>")).ConfigureAwait(false);
            return context;
        }

        private static INavigator GetNavigator(IBrowsingContext context)
        {
            var navigator = context?.Active?.DefaultView?.Navigator;
            Assert.IsNotNull(navigator);
            return navigator;
        }

        private sealed class FakeClipboardPlatform : IClipboardPlatform
        {
            public Task<String> ReadTextAsync()
            {
                return Task.FromResult(String.Empty);
            }

            public Task WriteTextAsync(String text)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeGeolocationPlatform : IGeolocationPlatform
        {
            public Task<GeolocationReading> GetCurrentPositionAsync(GeolocationOptions options)
            {
                return Task.FromResult(GeolocationReading.Empty);
            }
        }
    }
}
