namespace AngleSharp.Io.Tests.Storage
{
    using AngleSharp;
    using AngleSharp.Browser;
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.Storage;
    using NUnit.Framework;
    using System;
    using System.Linq;
    using System.IO;
    using System.Threading.Tasks;

    [TestFixture]
    public class StorageTests
    {
        [Test]
        public void StorageUsesCurrentOriginBucket()
        {
            var origin = "https://a.example";
            var buckets = new System.Collections.Generic.Dictionary<String, StorageBucket>(StringComparer.Ordinal);
            var storage = new Storage(() => origin, o => GetOrCreateBucket(buckets, o), (o, b) => { });

            storage["a"] = "1";
            storage["b"] = "2";

            Assert.AreEqual(2, storage.Length);
            Assert.AreEqual("1", storage["a"]);
            Assert.AreEqual("a", storage.Key(0));
            Assert.AreEqual("b", storage.Key(1));

            origin = "https://b.example";
            Assert.AreEqual(0, storage.Length);
            Assert.AreEqual(null, storage["a"]);
        }

        [Test]
        public void StoragePersistsViaHandlerOnMutation()
        {
            var handler = new MemoryStorageHandler();
            var buckets = new System.Collections.Generic.Dictionary<String, StorageBucket>(StringComparer.Ordinal);
            var storage = new Storage(() => "https://a.example", o =>
            {
                if (!buckets.TryGetValue(o, out var bucket))
                {
                    bucket = new StorageBucket(handler.Read(o));
                    buckets[o] = bucket;
                }

                return bucket;
            }, (o, b) => handler.Write(o, b.ToEntries()));

            storage["a"] = "1";
            storage["b"] = "2";
            storage.Remove("a");
            storage.Clear();

            Assert.IsFalse(handler.Read("https://a.example").Any());
        }

        [Test]
        public void MemoryStorageHandlerReadsAndWritesPerOrigin()
        {
            var handler = new MemoryStorageHandler();
            handler.Write("https://a.example", new[]
            {
                new System.Collections.Generic.KeyValuePair<String, String>("foo", "bar"),
            });

            handler.Write("https://b.example", new[]
            {
                new System.Collections.Generic.KeyValuePair<String, String>("foo", "baz"),
            });

            var a = handler.Read("https://a.example").Single();
            var b = handler.Read("https://b.example").Single();

            Assert.AreEqual("bar", a.Value);
            Assert.AreEqual("baz", b.Value);
        }

        [Test]
        public void PersistenceStorageHandlerStoresFilesPerOrigin()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"anglesharp-io-storage-{Guid.NewGuid():N}");

            try
            {
                var handler = new PersistenceStorageHandler(directory);
                handler.Write("https://a.example", new[]
                {
                    new System.Collections.Generic.KeyValuePair<String, String>("foo", "bar"),
                });
                handler.Write("https://b.example", new[]
                {
                    new System.Collections.Generic.KeyValuePair<String, String>("foo", "baz"),
                });

                var files = Directory.GetFiles(directory, "*.storage");
                Assert.AreEqual(2, files.Length);
                Assert.AreEqual("bar", handler.Read("https://a.example").Single().Value);
                Assert.AreEqual("baz", handler.Read("https://b.example").Single().Value);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public async Task ConfigurationCanRegisterLocalAndSessionStorage()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"anglesharp-io-storage-{Guid.NewGuid():N}");

            try
            {
                var config = CreateConfigWithStorages(directory);
                var context = BrowsingContext.New(config);
                var window = await OpenWindowAsync(context, "https://storage.example/");

                Assert.IsNotNull(window.LocalStorage());
                Assert.IsNotNull(window.SessionStorage());
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public async Task LocalStorageIsOriginDependentAcrossWindows()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"anglesharp-io-storage-{Guid.NewGuid():N}");

            try
            {
                var config = CreateConfigWithStorages(directory);
                var contextA = BrowsingContext.New(config);
                var contextB = BrowsingContext.New(config);
                var contextC = BrowsingContext.New(config);
                var windowA = await OpenWindowAsync(contextA, "https://storage.example/");
                var windowB = await OpenWindowAsync(contextB, "https://other.example/");
                var windowC = await OpenWindowAsync(contextC, "https://storage.example/again");

                windowA.LocalStorage()["token"] = "x";
                Assert.AreEqual("x", windowA.LocalStorage()["token"]);
                Assert.AreEqual(null, windowB.LocalStorage()["token"]);
                Assert.AreEqual("x", windowC.LocalStorage()["token"]);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public async Task LocalStorageSharesStateWithinTheSameBrowsingContextTree()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"anglesharp-io-storage-{Guid.NewGuid():N}");

            try
            {
                var config = CreateConfigWithStorages(directory);
                var parentContext = BrowsingContext.New(config);
                var childContext = parentContext.CreateChild("child", Sandboxes.None);
                var parallelContext = BrowsingContext.New(config);

                var parentWindow = await OpenWindowAsync(parentContext, "https://alpha.example/");
                var childWindow = await OpenWindowAsync(childContext, "https://alpha.example/frame");
                var parallelWindow = await OpenWindowAsync(parallelContext, "https://alpha.example/");

                var localStorage = parentWindow.LocalStorage();
                localStorage["token"] = "x";

                Assert.AreEqual("x", childWindow.LocalStorage()["token"]);
                Assert.AreEqual("x", parallelWindow.LocalStorage()["token"]);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public async Task LocalStorageFiresStorageEventToOtherSameOriginWindows()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"anglesharp-io-storage-{Guid.NewGuid():N}");

            try
            {
                var config = CreateConfigWithStorages(directory);
                var parentContext = BrowsingContext.New(config);
                var childContext = parentContext.CreateChild("child", Sandboxes.None);
                var parallelContext = BrowsingContext.New(config);

                var parentWindow = await OpenWindowAsync(parentContext, "https://alpha.example/");
                var childWindow = await OpenWindowAsync(childContext, "https://alpha.example/frame");
                var parallelWindow = await OpenWindowAsync(parallelContext, "https://alpha.example/");

                var parentStorage = parentWindow.LocalStorage();
                childWindow.LocalStorage();
                parallelWindow.LocalStorage();

                var parentEvents = 0;
                var childEvents = 0;
                var parallelEvents = 0;

                parentWindow.AddEventListener(EventNames.Storage, (_, __) => parentEvents++, false);
                childWindow.AddEventListener(EventNames.Storage, (_, __) => childEvents++, false);
                parallelWindow.AddEventListener(EventNames.Storage, (_, __) => parallelEvents++, false);

                parentStorage["token"] = "x";

                Assert.AreEqual(0, parentEvents);
                Assert.AreEqual(1, childEvents);
                Assert.AreEqual(1, parallelEvents);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public async Task SessionStorageIsIsolatedAcrossParallelBrowsingContexts()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"anglesharp-io-storage-{Guid.NewGuid():N}");

            try
            {
                var config = CreateConfigWithStorages(directory);
                var contextA = BrowsingContext.New(config);
                var contextB = BrowsingContext.New(config);

                var windowA = await OpenWindowAsync(contextA, "https://alpha.example/");
                var windowB = await OpenWindowAsync(contextB, "https://alpha.example/");

                var sessionA = windowA.SessionStorage();
                var sessionB = windowB.SessionStorage();

                sessionA["sid"] = "1";

                Assert.AreEqual("1", sessionA["sid"]);
                Assert.AreEqual(null, sessionB["sid"]);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public async Task SessionStorageFiresStorageEventOnlyWithinTheSameBrowsingContextTree()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"anglesharp-io-storage-{Guid.NewGuid():N}");

            try
            {
                var config = CreateConfigWithStorages(directory);
                var parentContext = BrowsingContext.New(config);
                var childContext = parentContext.CreateChild("child", Sandboxes.None);
                var parallelContext = BrowsingContext.New(config);

                var parentWindow = await OpenWindowAsync(parentContext, "https://alpha.example/");
                var childWindow = await OpenWindowAsync(childContext, "https://alpha.example/frame");
                var parallelWindow = await OpenWindowAsync(parallelContext, "https://alpha.example/");

                var parentStorage = parentWindow.SessionStorage();
                childWindow.SessionStorage();
                parallelWindow.SessionStorage();

                var parentEvents = 0;
                var childEvents = 0;
                var parallelEvents = 0;

                parentWindow.AddEventListener(EventNames.Storage, (_, __) => parentEvents++, false);
                childWindow.AddEventListener(EventNames.Storage, (_, __) => childEvents++, false);
                parallelWindow.AddEventListener(EventNames.Storage, (_, __) => parallelEvents++, false);

                parentStorage["sid"] = "1";

                Assert.AreEqual(0, parentEvents);
                Assert.AreEqual(1, childEvents);
                Assert.AreEqual(0, parallelEvents);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public async Task SessionStorageSharesWithinTheSameBrowsingContextTree()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"anglesharp-io-storage-{Guid.NewGuid():N}");

            try
            {
                var config = CreateConfigWithStorages(directory);
                var parentContext = BrowsingContext.New(config);
                var childContext = parentContext.CreateChild("child", Sandboxes.None);
                var parallelContext = BrowsingContext.New(config);

                var parentWindow = await OpenWindowAsync(parentContext, "https://alpha.example/");
                var childWindow = await OpenWindowAsync(childContext, "https://alpha.example/frame");
                var parallelWindow = await OpenWindowAsync(parallelContext, "https://alpha.example/");

                var parentSession = parentWindow.SessionStorage();
                var childSession = childWindow.SessionStorage();
                var parallelSession = parallelWindow.SessionStorage();

                parentSession["sid"] = "1";

                Assert.AreEqual("1", parentSession["sid"]);
                Assert.AreEqual("1", childSession["sid"]);
                Assert.AreEqual(null, parallelSession["sid"]);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static StorageBucket GetOrCreateBucket(System.Collections.Generic.Dictionary<String, StorageBucket> buckets, String origin)
        {
            origin = origin ?? String.Empty;

            if (!buckets.TryGetValue(origin, out var bucket))
            {
                bucket = new StorageBucket();
                buckets[origin] = bucket;
            }

            return bucket;
        }

        private static IConfiguration CreateConfigWithStorages(String directory)
        {
            var factory = new StorageProviderFactory();
            factory.EnableLocalStorage(directory);
            factory.EnableSessionStorage();
            return Configuration.Default.WithStorageProviderFactory(factory);
        }

        private static async Task<IWindow> OpenWindowAsync(IBrowsingContext context, String address)
        {
            var document = await context.OpenAsync(res =>
                res.Address(address).Content("<!doctype html><title>storage</title>"));
            return document.DefaultView;
        }

        [TearDown]
        public void CleanupTempStorageFiles()
        {
            var tmp = Path.GetTempPath();

            foreach (var file in Directory.GetFiles(tmp, "anglesharp-io-storage-*.txt"))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Best effort cleanup for temporary test files.
                }
            }

            foreach (var directory in Directory.GetDirectories(tmp, "anglesharp-io-storage-*") )
            {
                try
                {
                    Directory.Delete(directory, true);
                }
                catch
                {
                    // Best effort cleanup for temporary test directories.
                }
            }
        }
    }
}
