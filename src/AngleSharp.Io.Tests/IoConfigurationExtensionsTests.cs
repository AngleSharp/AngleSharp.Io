namespace AngleSharp.Io.Tests
{
    using AngleSharp.Browser.Dom;
    using AngleSharp.Io.Cache;
    using AngleSharp.Io.Cookie;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.IndexedDb;
    using AngleSharp.Io.Storage;
    using NUnit.Framework;
    using System;
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
            };

            var configuration = Configuration.Default.WithIo(options);
            var requesters = configuration.Services.OfType<IRequester>().ToArray();

            Assert.IsNotNull(configuration.Services.OfType<ICookieProvider>().FirstOrDefault());
            Assert.AreSame(options.StorageFactory, configuration.Services.OfType<IStorageProviderFactory>().FirstOrDefault());
            Assert.AreSame(options.IndexedDbFactory, configuration.Services.OfType<IIndexedDbProviderFactory>().FirstOrDefault());
            Assert.AreSame(options.CacheFactory, configuration.Services.OfType<ICacheProviderFactory>().FirstOrDefault());
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
        }

        [Test]
        public async Task WithIoRegistersOptionalNavigatorApisWhenPlatformsAreProvided()
        {
            var options = new IoOptions
            {
                ClipboardPlatform = new FakeClipboardPlatform(),
                GeolocationPlatform = new FakeGeolocationPlatform(),
            };

            var context = await OpenContextAsync(Configuration.Default.WithIo(options), "https://io.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);

            Assert.IsNotNull(navigator.Clipboard());
            Assert.IsNotNull(navigator.Geolocation());
        }

        [Test]
        public async Task WithIoLeavesOptionalNavigatorApisUnavailableByDefault()
        {
            var context = await OpenContextAsync(Configuration.Default.WithIo(new IoOptions()), "https://io.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);

            Assert.IsNull(navigator.Clipboard());
            Assert.IsNull(navigator.Geolocation());
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
