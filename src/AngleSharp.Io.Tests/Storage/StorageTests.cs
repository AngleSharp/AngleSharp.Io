namespace AngleSharp.Io.Tests.Storage
{
    using AngleSharp.Io.Cookie;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.Storage;
    using NUnit.Framework;
    using System;
    using System.IO;

    [TestFixture]
    public class StorageTests
    {
        [Test]
        public void SessionStorageStoresValuesInMemory()
        {
            var storage = new SessionStorage();
            storage["a"] = "1";
            storage["b"] = "2";

            Assert.AreEqual(2, storage.Length);
            Assert.AreEqual("1", storage["a"]);
            Assert.AreEqual("a", storage.Key(0));
            Assert.AreEqual("b", storage.Key(1));
        }

        [Test]
        public void SessionStorageCanRemoveAndClear()
        {
            var storage = new SessionStorage();
            storage["a"] = "1";
            storage["b"] = "2";
            storage.Remove("a");

            Assert.AreEqual(1, storage.Length);
            Assert.AreEqual(null, storage["a"]);
            Assert.AreEqual("b", storage.Key(0));

            storage.Clear();

            Assert.AreEqual(0, storage.Length);
            Assert.AreEqual(null, storage.Key(0));
        }

        [Test]
        public void LocalStoragePersistsValuesViaFileHandler()
        {
            var handler = new MemoryFileHandler();
            var first = new LocalStorage(handler);
            first["name"] = "anglesharp";
            first["version"] = "1";

            var second = new LocalStorage(handler);

            Assert.AreEqual(2, second.Length);
            Assert.AreEqual("anglesharp", second["name"]);
            Assert.AreEqual("1", second["version"]);
        }

        [Test]
        public void LocalStorageCanSyncToDiskByPath()
        {
            var path = Path.Combine(Path.GetTempPath(), $"anglesharp-io-storage-{Guid.NewGuid():N}.txt");

            try
            {
                var first = new LocalStorage(path);
                first["user"] = "foo";

                var second = new LocalStorage(path);
                Assert.AreEqual("foo", second["user"]);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void ConfigurationCanRegisterLocalAndSessionStorage()
        {
            var path = Path.Combine(Path.GetTempPath(), $"anglesharp-io-storage-{Guid.NewGuid():N}.txt");

            try
            {
                var config = Configuration.Default.WithStorages(path);
                var context = BrowsingContext.New(config);

                Assert.IsNotNull(context.GetLocalStorage());
                Assert.IsNotNull(context.GetSessionStorage());
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
