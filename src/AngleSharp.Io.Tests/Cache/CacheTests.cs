namespace AngleSharp.Io.Tests.Cache
{
    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Io.Cache;
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    [TestFixture]
    public class CacheTests
    {
        [Test]
        public async Task ConfigurationCanRegisterCacheStorage()
        {
            var config = CreateConfigWithCache();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://cache.example/");

            Assert.IsNotNull(window.Caches());
        }

        [Test]
        public async Task CacheSharesStateAcrossSameOriginWindows()
        {
            var config = CreateConfigWithCache();
            var contextA = BrowsingContext.New(config);
            var contextB = BrowsingContext.New(config);

            var windowA = await OpenWindowAsync(contextA, "https://cache.example/");
            var windowB = await OpenWindowAsync(contextB, "https://cache.example/other");

            var cacheA = windowA.Caches().Open("app");
            var cacheB = windowB.Caches().Open("app");

            cacheA.Put("https://cache.example/data", CreateResponse("https://cache.example/data", "hello"));

            Assert.AreEqual("hello", ReadContent(cacheB.Match("https://cache.example/data")));
        }

        [Test]
        public async Task CacheIsOriginBound()
        {
            var config = CreateConfigWithCache();
            var contextA = BrowsingContext.New(config);
            var contextB = BrowsingContext.New(config);

            var windowA = await OpenWindowAsync(contextA, "https://cache.example/");
            var windowB = await OpenWindowAsync(contextB, "https://other.example/");

            var cacheA = windowA.Caches().Open("app");
            var cacheB = windowB.Caches().Open("app");

            cacheA.Put("https://cache.example/data", CreateResponse("https://cache.example/data", "hello"));

            Assert.AreEqual("hello", ReadContent(cacheA.Match("https://cache.example/data")));
            Assert.AreEqual(null, cacheB.Match("https://cache.example/data"));
        }

        private static IConfiguration CreateConfigWithCache()
        {
            var factory = new CacheProviderFactory();
            return Configuration.Default.WithCacheProviderFactory(factory);
        }

        private static IResponse CreateResponse(String address, String content)
        {
            return new AngleSharp.Io.DefaultResponse
            {
                Address = new Url(address),
                StatusCode = HttpStatusCode.OK,
                Headers = new System.Collections.Generic.Dictionary<String, String>(),
                Content = new MemoryStream(Encoding.UTF8.GetBytes(content ?? String.Empty)),
            };
        }

        private static String ReadContent(IResponse response)
        {
            if (response?.Content == null)
            {
                return null;
            }

            using (response)
            using (var reader = new StreamReader(response.Content, Encoding.UTF8, true, 1024, true))
            {
                return reader.ReadToEnd();
            }
        }

        private static async Task<IWindow> OpenWindowAsync(IBrowsingContext context, String address)
        {
            var document = await context.OpenAsync(res =>
                res.Address(address).Content("<!doctype html><title>cache</title>"));
            return document.DefaultView;
        }
    }
}