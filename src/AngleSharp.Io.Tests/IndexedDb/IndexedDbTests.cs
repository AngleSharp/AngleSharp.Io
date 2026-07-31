namespace AngleSharp.Io.Tests.IndexedDb
{
    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.IndexedDb;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    [TestFixture]
    public class IndexedDbTests
    {
        [Test]
        public async Task ConfigurationCanRegisterIndexedDb()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");

            Assert.IsNotNull(window.IndexedDb());
        }

        [Test]
        public async Task IndexedDbSharesStateAcrossSameOriginWindows()
        {
            var config = CreateConfigWithIndexedDb();
            var contextA = BrowsingContext.New(config);
            var contextB = BrowsingContext.New(config);

            var windowA = await OpenWindowAsync(contextA, "https://indexeddb.example/");
            var windowB = await OpenWindowAsync(contextB, "https://indexeddb.example/other");

            var storeA = windowA.IndexedDb().Open("app").CreateObjectStore("records");
            var storeB = windowB.IndexedDb().Open("app").CreateObjectStore("records");

            storeA["token"] = "abc";

            Assert.AreEqual("abc", storeB["token"]);
        }

        [Test]
        public async Task IndexedDbIsOriginBound()
        {
            var config = CreateConfigWithIndexedDb();
            var contextA = BrowsingContext.New(config);
            var contextB = BrowsingContext.New(config);

            var windowA = await OpenWindowAsync(contextA, "https://indexeddb.example/");
            var windowB = await OpenWindowAsync(contextB, "https://other.example/");

            var storeA = windowA.IndexedDb().Open("app").CreateObjectStore("records");
            var storeB = windowB.IndexedDb().Open("app").CreateObjectStore("records");

            storeA["token"] = "abc";

            Assert.AreEqual("abc", storeA["token"]);
            Assert.AreEqual(null, storeB["token"]);
        }

        private static IConfiguration CreateConfigWithIndexedDb()
        {
            var factory = new IndexedDbProviderFactory();
            return Configuration.Default.WithIndexedDbProviderFactory(factory);
        }

        private static async Task<IWindow> OpenWindowAsync(IBrowsingContext context, String address)
        {
            var document = await context.OpenAsync(res =>
                res.Address(address).Content("<!doctype html><title>indexeddb</title>"));
            return document.DefaultView;
        }
    }
}