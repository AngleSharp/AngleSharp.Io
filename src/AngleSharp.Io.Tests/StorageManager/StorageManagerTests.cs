namespace AngleSharp.Io.Tests.StorageManager
{
    using AngleSharp;
    using AngleSharp.Browser.Dom;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.StorageManagerApi;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    [TestFixture]
    public class StorageManagerTests
    {
        [Test]
        public async Task StorageManagerCanBeResolvedFromNavigatorWhenRegistered()
        {
            var platform = new MemoryStorageManagerPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithStorageManager(platform), "https://storage.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);

            Assert.IsNotNull(navigator.Storage());
        }

        [Test]
        public async Task StorageManagerIsUnavailableWhenNotRegistered()
        {
            var context = await OpenContextAsync(Configuration.Default.WithNavigator(), "https://storage.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);

            Assert.IsNull(navigator.Storage());
        }

        [Test]
        public async Task StorageManagerIsUnavailableWhenRegisteredWithoutPlatform()
        {
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithStorageManager(null), "https://storage.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);

            Assert.IsNull(navigator.Storage());
        }

        [Test]
        public async Task StorageManagerIsTheSameInstanceForTheSameNavigator()
        {
            var platform = new MemoryStorageManagerPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithStorageManager(platform), "https://storage.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);

            Assert.AreSame(navigator.Storage(), navigator.Storage());
        }

        [Test]
        public void StorageManagerProviderFactoryReturnsNullWithoutNavigator()
        {
            var factory = new StorageManagerProviderFactory(new MemoryStorageManagerPlatform());

            Assert.IsNull(factory.GetStorageManager(null));
        }

        [Test]
        public void StorageManagerRequiresPlatform()
        {
            Assert.Throws<ArgumentNullException>(() => new StorageManagerProviderFactory(null));
            Assert.Throws<ArgumentNullException>(() => new StorageManager(null, () => "https://storage.example"));
            Assert.Throws<ArgumentNullException>(() => new StorageManager(new MemoryStorageManagerPlatform(), null));
        }

        [Test]
        public async Task EstimateReturnsTheValuesOfThePlatform()
        {
            var platform = new MemoryStorageManagerPlatform
            {
                Quota = 1024 * 1024,
                Usage = 4096,
            };

            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithStorageManager(platform), "https://storage.example/").ConfigureAwait(false);
            var storage = GetNavigator(context).Storage();

            var estimate = await storage.Estimate().ConfigureAwait(false);

            Assert.AreEqual(1024UL * 1024UL, estimate.Quota);
            Assert.AreEqual(4096UL, estimate.Usage);
        }

        [Test]
        public async Task EstimateFallsBackToZeroWhenPlatformReturnsNothing()
        {
            var platform = new FakeStorageManagerPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithStorageManager(platform), "https://storage.example/").ConfigureAwait(false);
            var storage = GetNavigator(context).Storage();

            var estimate = await storage.Estimate().ConfigureAwait(false);

            Assert.IsNotNull(estimate);
            Assert.AreEqual(0UL, estimate.Quota);
            Assert.AreEqual(0UL, estimate.Usage);
        }

        [Test]
        public async Task StorageManagerPassesTheOriginOfTheDocumentToThePlatform()
        {
            var platform = new FakeStorageManagerPlatform
            {
                PersistResult = true,
                PersistedResult = true,
            };

            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithStorageManager(platform), "https://storage.example:8443/some/page").ConfigureAwait(false);
            var storage = GetNavigator(context).Storage();

            await storage.Estimate().ConfigureAwait(false);
            Assert.AreEqual("https://storage.example:8443", platform.LastOrigin);

            platform.LastOrigin = null;
            Assert.IsTrue(await storage.Persist().ConfigureAwait(false));
            Assert.AreEqual("https://storage.example:8443", platform.LastOrigin);

            platform.LastOrigin = null;
            Assert.IsTrue(await storage.Persisted().ConfigureAwait(false));
            Assert.AreEqual("https://storage.example:8443", platform.LastOrigin);
        }

        [Test]
        public async Task StorageManagerFollowsTheOriginAfterNavigation()
        {
            var platform = new FakeStorageManagerPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithStorageManager(platform), "https://first.example/").ConfigureAwait(false);
            var storage = GetNavigator(context).Storage();

            await context.OpenAsync(res => res.Address("https://second.example/").Content("<!doctype html><title>second</title>")).ConfigureAwait(false);
            await storage.Estimate().ConfigureAwait(false);

            Assert.AreEqual("https://second.example", platform.LastOrigin);
        }

        [Test]
        public async Task PersistAndPersistedMapToThePlatformResults()
        {
            var platform = new FakeStorageManagerPlatform
            {
                PersistResult = false,
                PersistedResult = true,
            };

            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithStorageManager(platform), "https://storage.example/").ConfigureAwait(false);
            var storage = GetNavigator(context).Storage();

            Assert.IsFalse(await storage.Persist().ConfigureAwait(false));
            Assert.IsTrue(await storage.Persisted().ConfigureAwait(false));
            Assert.AreEqual(1, platform.PersistCalls);
            Assert.AreEqual(1, platform.PersistedCalls);
        }

        [Test]
        public async Task PersistedIsFalseBeforePersistIsRequested()
        {
            var platform = new MemoryStorageManagerPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithStorageManager(platform), "https://storage.example/").ConfigureAwait(false);
            var storage = GetNavigator(context).Storage();

            Assert.IsFalse(await storage.Persisted().ConfigureAwait(false));
        }

        [Test]
        public async Task PersistIsRememberedWhenGranted()
        {
            var platform = new MemoryStorageManagerPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithStorageManager(platform), "https://storage.example/").ConfigureAwait(false);
            var storage = GetNavigator(context).Storage();

            Assert.IsTrue(await storage.Persist().ConfigureAwait(false));
            Assert.IsTrue(await storage.Persisted().ConfigureAwait(false));
        }

        [Test]
        public async Task PersistIsNotRememberedWhenDenied()
        {
            var platform = new MemoryStorageManagerPlatform
            {
                AllowPersistence = false,
            };

            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithStorageManager(platform), "https://storage.example/").ConfigureAwait(false);
            var storage = GetNavigator(context).Storage();

            Assert.IsFalse(await storage.Persist().ConfigureAwait(false));
            Assert.IsFalse(await storage.Persisted().ConfigureAwait(false));
        }

        [Test]
        public async Task PersistIsKeptPerOrigin()
        {
            var platform = new MemoryStorageManagerPlatform();
            var configuration = Configuration.Default.WithNavigator().WithStorageManager(platform);
            var first = await OpenContextAsync(configuration, "https://first.example/").ConfigureAwait(false);
            var second = await OpenContextAsync(configuration, "https://second.example/").ConfigureAwait(false);
            var sameOrigin = await OpenContextAsync(configuration, "https://first.example/other").ConfigureAwait(false);

            await GetNavigator(first).Storage().Persist().ConfigureAwait(false);

            Assert.IsTrue(await GetNavigator(first).Storage().Persisted().ConfigureAwait(false));
            Assert.IsFalse(await GetNavigator(second).Storage().Persisted().ConfigureAwait(false));
            Assert.IsTrue(await GetNavigator(sameOrigin).Storage().Persisted().ConfigureAwait(false));
        }

        [Test]
        public void StorageManagerRejectsOpaqueOrigins()
        {
            var platform = new FakeStorageManagerPlatform();
            var storage = new StorageManager(platform, () => String.Empty);

            Assert.ThrowsAsync<InvalidOperationException>(() => storage.Estimate());
            Assert.ThrowsAsync<InvalidOperationException>(() => storage.Persist());
            Assert.ThrowsAsync<InvalidOperationException>(() => storage.Persisted());
            Assert.AreEqual(0, platform.PersistCalls);
            Assert.AreEqual(0, platform.PersistedCalls);
        }

        [Test]
        public void StorageManagerPropagatesPlatformFailures()
        {
            var platform = new FakeStorageManagerPlatform
            {
                Failure = new NotSupportedException(),
            };

            var storage = new StorageManager(platform, () => "https://storage.example");

            Assert.ThrowsAsync<NotSupportedException>(() => storage.Estimate());
            Assert.ThrowsAsync<NotSupportedException>(() => storage.Persist());
            Assert.ThrowsAsync<NotSupportedException>(() => storage.Persisted());
        }

        private static async Task<IBrowsingContext> OpenContextAsync(IConfiguration configuration, String address)
        {
            var context = BrowsingContext.New(configuration);
            await context.OpenAsync(res => res.Address(address).Content("<!doctype html><title>storage</title>")).ConfigureAwait(false);
            return context;
        }

        private static INavigator GetNavigator(IBrowsingContext context)
        {
            var navigator = context?.Active?.DefaultView?.Navigator;
            Assert.IsNotNull(navigator);
            return navigator;
        }

        private sealed class FakeStorageManagerPlatform : IStorageManagerPlatform
        {
            public StorageEstimate EstimateResult { get; set; }

            public Boolean PersistResult { get; set; }

            public Boolean PersistedResult { get; set; }

            public Exception Failure { get; set; }

            public String LastOrigin { get; set; }

            public Int32 PersistCalls { get; private set; }

            public Int32 PersistedCalls { get; private set; }

            public Task<StorageEstimate> EstimateAsync(String origin)
            {
                LastOrigin = origin;
                return Failure is null ? Task.FromResult(EstimateResult) : Task.FromException<StorageEstimate>(Failure);
            }

            public Task<Boolean> PersistAsync(String origin)
            {
                LastOrigin = origin;
                PersistCalls++;
                return Failure is null ? Task.FromResult(PersistResult) : Task.FromException<Boolean>(Failure);
            }

            public Task<Boolean> PersistedAsync(String origin)
            {
                LastOrigin = origin;
                PersistedCalls++;
                return Failure is null ? Task.FromResult(PersistedResult) : Task.FromException<Boolean>(Failure);
            }
        }
    }
}
