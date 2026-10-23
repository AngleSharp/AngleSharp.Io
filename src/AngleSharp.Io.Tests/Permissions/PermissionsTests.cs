namespace AngleSharp.Io.Tests.Permissions
{
    using AngleSharp;
    using AngleSharp.Browser.Dom;
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.PermissionsApi;
    using AngleSharp.Io.StorageManagerApi;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    [TestFixture]
    public class PermissionsTests
    {
        private const String Address = "https://permissions.example/page";
        private const String Origin = "https://permissions.example";

        [Test]
        public async Task PermissionsAreUnavailableWithoutPolicy()
        {
            var context = await OpenContextAsync(Configuration.Default.WithNavigator()).ConfigureAwait(false);

            Assert.IsNull(GetNavigator(context).Permissions());
        }

        [Test]
        public async Task PermissionsAreUnavailableWhenRegisteredWithoutPolicy()
        {
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithPermissions(null)).ConfigureAwait(false);

            Assert.IsNull(GetNavigator(context).Permissions());
        }

        [Test]
        public async Task PermissionsAreTheSameInstanceForTheSameNavigator()
        {
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithPermissions()).ConfigureAwait(false);
            var navigator = GetNavigator(context);

            Assert.IsNotNull(navigator.Permissions());
            Assert.AreSame(navigator.Permissions(), navigator.Permissions());
        }

        [Test]
        public async Task PermissionsCanBeRegisteredViaIoOptions()
        {
            var policy = new PermissionPolicy(PermissionState.Granted);
            var context = await OpenContextAsync(Configuration.Default.WithIo(new IoOptions { Permissions = policy })).ConfigureAwait(false);

            Assert.AreSame(policy, GetNavigator(context).Permissions().Policy);
        }

        [Test]
        public async Task QueryReturnsTheDefaultState()
        {
            var permissions = await GetPermissionsAsync(new PermissionPolicy()).ConfigureAwait(false);

            var status = await permissions.Query(Describe(PermissionNames.Geolocation)).ConfigureAwait(false);

            Assert.AreEqual(PermissionNames.Geolocation, status.Name);
            Assert.AreEqual(PermissionState.Prompt, status.State);
        }

        [Test]
        public async Task QueryPrefersTheStateOfTheOriginOverTheStateOfAllOrigins()
        {
            var policy = new PermissionPolicy();
            policy.Set(PermissionNames.Notifications, PermissionState.Denied);
            policy.Set(Origin, PermissionNames.Notifications, PermissionState.Granted);
            policy.Set("https://other.example", PermissionNames.ClipboardRead, PermissionState.Granted);
            var permissions = await GetPermissionsAsync(policy).ConfigureAwait(false);

            var notifications = await permissions.Query(Describe(PermissionNames.Notifications)).ConfigureAwait(false);
            var clipboard = await permissions.Query(Describe(PermissionNames.ClipboardRead)).ConfigureAwait(false);

            Assert.AreEqual(PermissionState.Granted, notifications.State);
            Assert.AreEqual(PermissionState.Prompt, clipboard.State);
        }

        [Test]
        public void PolicyNormalizesTheOrigin()
        {
            var policy = new PermissionPolicy();

            policy.Set(Origin + "/some/path?query", PermissionNames.Geolocation, PermissionState.Granted);

            Assert.AreEqual(PermissionState.Granted, policy.Query(Origin, PermissionNames.Geolocation));
            Assert.AreEqual(PermissionState.Granted, policy.Query(Origin + "/", PermissionNames.Geolocation));
        }

        [Test]
        public async Task QueryRejectsUnknownNames()
        {
            var permissions = await GetPermissionsAsync(new PermissionPolicy()).ConfigureAwait(false);

            Assert.ThrowsAsync<ArgumentException>(() => permissions.Query(Describe("teleportation")));
            Assert.ThrowsAsync<ArgumentException>(() => permissions.Query(Describe(null)));
            Assert.ThrowsAsync<ArgumentException>(() => permissions.Query(null));
        }

        [Test]
        public async Task QueryAcceptsRegisteredNames()
        {
            var policy = new PermissionPolicy(PermissionState.Denied);
            policy.Register("teleportation");
            var permissions = await GetPermissionsAsync(policy).ConfigureAwait(false);

            var status = await permissions.Query(Describe("teleportation")).ConfigureAwait(false);

            Assert.AreEqual(PermissionState.Denied, status.State);
        }

        [Test]
        public void SettingAnUnknownNameThrows()
        {
            var policy = new PermissionPolicy();

            Assert.Throws<ArgumentException>(() => policy.Set("teleportation", PermissionState.Granted));
            Assert.Throws<ArgumentException>(() => policy.Set(Origin, "teleportation", PermissionState.Granted));
            Assert.Throws<ArgumentException>(() => policy.Set(String.Empty, PermissionNames.Geolocation, PermissionState.Granted));
        }

        [Test]
        public async Task StatusFollowsChangesOfThePolicy()
        {
            var policy = new PermissionPolicy();
            var permissions = await GetPermissionsAsync(policy).ConfigureAwait(false);
            var status = await permissions.Query(Describe(PermissionNames.Geolocation)).ConfigureAwait(false);
            var changes = 0;
            status.Changed += (s, e) => changes++;

            policy.Set(Origin, PermissionNames.Geolocation, PermissionState.Granted);

            Assert.AreEqual(PermissionState.Granted, status.State);
            Assert.AreEqual(1, changes);

            policy.Set(PermissionNames.Geolocation, PermissionState.Denied);

            Assert.AreEqual(PermissionState.Granted, status.State);
            Assert.AreEqual(1, changes);

            policy.Set(Origin, PermissionNames.Geolocation, PermissionState.Denied);

            Assert.AreEqual(PermissionState.Denied, status.State);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public async Task StatusIgnoresChangesOfOtherOriginsAndPermissions()
        {
            var policy = new PermissionPolicy();
            var permissions = await GetPermissionsAsync(policy).ConfigureAwait(false);
            var status = await permissions.Query(Describe(PermissionNames.Geolocation)).ConfigureAwait(false);
            var changes = 0;
            status.Changed += (s, e) => changes++;

            policy.Set("https://other.example", PermissionNames.Geolocation, PermissionState.Granted);
            policy.Set(Origin, PermissionNames.Notifications, PermissionState.Granted);
            policy.Set(Origin, PermissionNames.Geolocation, PermissionState.Prompt);

            Assert.AreEqual(PermissionState.Prompt, status.State);
            Assert.AreEqual(0, changes);
        }

        [Test]
        public async Task PromptWithoutHandlerIsDismissed()
        {
            var policy = new PermissionPolicy();
            var permissions = await GetPermissionsAsync(policy).ConfigureAwait(false);

            var state = await permissions.RequestAsync(PermissionNames.Geolocation).ConfigureAwait(false);

            Assert.AreEqual(PermissionState.Denied, state);
            Assert.AreEqual(PermissionState.Prompt, permissions.GetState(PermissionNames.Geolocation));
        }

        [Test]
        public async Task PromptHandlerAnswersAreRememberedForTheOrigin()
        {
            var prompts = 0;
            var policy = new PermissionPolicy
            {
                PromptHandler = (origin, name) =>
                {
                    prompts++;
                    Assert.AreEqual(Origin, origin);
                    return Task.FromResult(PermissionState.Granted);
                },
            };
            var permissions = await GetPermissionsAsync(policy).ConfigureAwait(false);
            var status = await permissions.Query(Describe(PermissionNames.Geolocation)).ConfigureAwait(false);
            var changes = 0;
            status.Changed += (s, e) => changes++;

            await permissions.RequestAsync(PermissionNames.Geolocation).ConfigureAwait(false);
            await permissions.RequestAsync(PermissionNames.Geolocation).ConfigureAwait(false);

            Assert.AreEqual(1, prompts);
            Assert.AreEqual(1, changes);
            Assert.AreEqual(PermissionState.Granted, status.State);
        }

        [Test]
        public async Task PromptHandlerLeavingThePromptDeniesWithoutRemembering()
        {
            var policy = new PermissionPolicy
            {
                PromptHandler = (origin, name) => Task.FromResult(PermissionState.Prompt),
            };
            var permissions = await GetPermissionsAsync(policy).ConfigureAwait(false);

            var state = await permissions.RequestAsync(PermissionNames.Geolocation).ConfigureAwait(false);

            Assert.AreEqual(PermissionState.Denied, state);
            Assert.AreEqual(PermissionState.Prompt, permissions.GetState(PermissionNames.Geolocation));
        }

        [Test]
        public async Task ClipboardIsDeniedWithoutPermission()
        {
            var platform = new FakeClipboardPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithPermissions().WithClipboard(platform)).ConfigureAwait(false);
            var clipboard = GetNavigator(context).Clipboard();

            var error = Assert.ThrowsAsync<DomException>(() => clipboard.WriteText("copied"));

            Assert.AreEqual("NotAllowedError", error.Name);
            Assert.IsNull(platform.LastWrittenText);
        }

        [Test]
        public async Task ClipboardChecksReadAndWriteSeparately()
        {
            var policy = new PermissionPolicy(PermissionState.Denied);
            policy.Set(PermissionNames.ClipboardWrite, PermissionState.Granted);
            var platform = new FakeClipboardPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithPermissions(policy).WithClipboard(platform)).ConfigureAwait(false);
            var clipboard = GetNavigator(context).Clipboard();

            await clipboard.WriteText("copied").ConfigureAwait(false);

            Assert.AreEqual("copied", platform.LastWrittenText);
            Assert.ThrowsAsync<DomException>(() => clipboard.ReadText());
        }

        [Test]
        public async Task ClipboardFollowsChangesOfThePolicy()
        {
            var policy = new PermissionPolicy(PermissionState.Denied);
            var platform = new FakeClipboardPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithPermissions(policy).WithClipboard(platform)).ConfigureAwait(false);
            var clipboard = GetNavigator(context).Clipboard();

            Assert.ThrowsAsync<DomException>(() => clipboard.WriteText("first"));

            policy.Set(Origin, PermissionNames.ClipboardWrite, PermissionState.Granted);
            await clipboard.WriteText("second").ConfigureAwait(false);

            Assert.AreEqual("second", platform.LastWrittenText);
        }

        [Test]
        public async Task GeolocationIsGatedByThePermission()
        {
            var policy = new PermissionPolicy(PermissionState.Denied);
            var platform = new FakeGeolocationPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithPermissions(policy).WithGeolocation(platform)).ConfigureAwait(false);
            var geolocation = GetNavigator(context).Geolocation();

            Assert.ThrowsAsync<DomException>(() => geolocation.GetCurrentPosition());
            Assert.AreEqual(0, platform.Calls);

            policy.Set(Origin, PermissionNames.Geolocation, PermissionState.Granted);
            var position = await geolocation.GetCurrentPosition().ConfigureAwait(false);

            Assert.AreEqual(1, platform.Calls);
            Assert.AreEqual(47.0, position.Coordinates.Latitude);
        }

        [Test]
        public async Task PersistResolvesWithFalseWithoutPermission()
        {
            var platform = new MemoryStorageManagerPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithPermissions().WithStorageManager(platform)).ConfigureAwait(false);
            var storage = GetNavigator(context).Storage();

            var persisted = await storage.Persist().ConfigureAwait(false);

            Assert.IsFalse(persisted);
            Assert.IsFalse(await storage.Persisted().ConfigureAwait(false));
        }

        [Test]
        public async Task PersistUsesThePlatformWithPermission()
        {
            var policy = new PermissionPolicy();
            policy.Set(PermissionNames.PersistentStorage, PermissionState.Granted);
            var platform = new MemoryStorageManagerPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithPermissions(policy).WithStorageManager(platform)).ConfigureAwait(false);
            var storage = GetNavigator(context).Storage();

            var persisted = await storage.Persist().ConfigureAwait(false);

            Assert.IsTrue(persisted);
        }

        private static PermissionDescriptor Describe(String name) => new PermissionDescriptor { Name = name };

        private static async Task<Permissions> GetPermissionsAsync(IPermissionPolicy policy)
        {
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithPermissions(policy)).ConfigureAwait(false);
            return GetNavigator(context).Permissions();
        }

        private static async Task<IBrowsingContext> OpenContextAsync(IConfiguration configuration)
        {
            var context = BrowsingContext.New(configuration);
            await context.OpenAsync(res => res.Address(Address).Content("<!doctype html><title>permissions</title>")).ConfigureAwait(false);
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
            public String LastWrittenText { get; private set; }

            public Task<String> ReadTextAsync() => Task.FromResult(LastWrittenText ?? String.Empty);

            public Task WriteTextAsync(String text)
            {
                LastWrittenText = text;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeGeolocationPlatform : IGeolocationPlatform
        {
            public Int32 Calls { get; private set; }

            public Task<GeolocationReading> GetCurrentPositionAsync(GeolocationOptions options)
            {
                Calls++;
                return Task.FromResult(new GeolocationReading { Latitude = 47.0, Longitude = 11.0 });
            }
        }
    }
}
