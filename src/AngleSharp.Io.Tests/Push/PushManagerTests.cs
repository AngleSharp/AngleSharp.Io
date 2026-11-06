namespace AngleSharp.Io.Tests.Push
{
    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.PermissionsApi;
    using AngleSharp.Io.PushApi;
    using AngleSharp.Io.ServiceWorkerApi;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [TestFixture]
    public class PushManagerTests
    {
        private const String Address = "https://push.example/app/";
        private const String Origin = "https://push.example";

        private static readonly Byte[] ServerKey = CreateKey(1);

        [Test]
        public async Task ThePushManagerIsUnavailableWithoutPlatform()
        {
            var registration = await RegisterAsync(ConfigureWorkers()).ConfigureAwait(false);

            Assert.IsNull(registration.PushManager);
        }

        [Test]
        public async Task ThePushManagerIsTheSameObject()
        {
            var registration = await RegisterAsync(ConfigureWorkers().WithPush()).ConfigureAwait(false);

            Assert.IsNotNull(registration.PushManager);
            Assert.AreSame(registration.PushManager, registration.PushManager);
        }

        [Test]
        public async Task PlatformsCanBeRegisteredViaIoOptions()
        {
            var options = new IoOptions
            {
                ServiceWorkerPlatform = new ServiceWorkerPlatform(),
                PushPlatform = new PushPlatform(),
            };
            var registration = await RegisterAsync(Configuration.Default.WithIo(options)).ConfigureAwait(false);

            Assert.IsNotNull(registration.PushManager);
        }

        [Test]
        public void SupportedContentEncodingsContainTheStandardEncoding()
        {
            CollectionAssert.Contains(PushManager.SupportedContentEncodings, "aes128gcm");
        }

        [Test]
        public async Task SubscribeCreatesASubscriptionUsingThePlatform()
        {
            var calls = new List<String>();
            var data = new PushSubscriptionData
            {
                Endpoint = "https://service.example/1",
                ExpirationTime = 4102444800000,
                P256dh = CreateKey(2),
                Auth = new Byte[] { 1, 2, 3 },
            };
            var seen = default(PushSubscriptionOptions);
            var platform = new PushPlatform
            {
                Subscribe = (origin, scope, options) =>
                {
                    calls.Add(origin + "|" + scope);
                    seen = options;
                    return Task.FromResult(data);
                },
            };
            var registration = await RegisterAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);

            var subscription = await registration.PushManager.Subscribe(new PushSubscriptionOptionsInit
            {
                UserVisibleOnly = true,
                ApplicationServerKey = ServerKey,
            }).ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { Origin + "|" + Address }, calls);
            Assert.AreEqual("https://service.example/1", subscription.Endpoint);
            Assert.AreEqual(4102444800000.0, subscription.ExpirationTime);
            Assert.IsTrue(subscription.Options.UserVisibleOnly);
            CollectionAssert.AreEqual(ServerKey, subscription.Options.ApplicationServerKey);
            Assert.AreSame(subscription.Options, seen);
            CollectionAssert.AreEqual(data.P256dh, subscription.GetKey(PushEncryptionKeyName.P256dh));
            CollectionAssert.AreEqual(data.Auth, subscription.GetKey(PushEncryptionKeyName.Auth));
        }

        [Test]
        public async Task KeysAreCopies()
        {
            var registration = await RegisterAsync(ConfigureWorkers().WithPush()).ConfigureAwait(false);
            var subscription = await registration.PushManager.Subscribe().ConfigureAwait(false);

            subscription.GetKey(PushEncryptionKeyName.Auth)[0] ^= 0xFF;
            var first = subscription.GetKey(PushEncryptionKeyName.Auth);
            first[0] ^= 0xFF;

            CollectionAssert.AreNotEqual(first, subscription.GetKey(PushEncryptionKeyName.Auth));
        }

        [Test]
        public async Task TheDefaultPlatformCreatesUsableSubscriptions()
        {
            var registration = await RegisterAsync(ConfigureWorkers().WithPush()).ConfigureAwait(false);

            var subscription = await registration.PushManager.Subscribe().ConfigureAwait(false);

            StringAssert.StartsWith("https://push.invalid/", subscription.Endpoint);
            Assert.IsNull(subscription.ExpirationTime);
            Assert.AreEqual(65, subscription.GetKey(PushEncryptionKeyName.P256dh).Length);
            Assert.AreEqual(0x04, subscription.GetKey(PushEncryptionKeyName.P256dh)[0]);
            Assert.AreEqual(16, subscription.GetKey(PushEncryptionKeyName.Auth).Length);
        }

        [Test]
        public async Task ToJsonUsesBase64UrlForTheKeys()
        {
            var platform = new PushPlatform
            {
                Subscribe = (origin, scope, options) => Task.FromResult(new PushSubscriptionData
                {
                    Endpoint = "https://service.example/1",
                    P256dh = new Byte[] { 0xFB, 0xFF },
                    Auth = new Byte[] { 0xFF, 0xFE, 0xFD },
                }),
            };
            var registration = await RegisterAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);
            var subscription = await registration.PushManager.Subscribe().ConfigureAwait(false);

            var json = subscription.ToJson();
            var keys = (IDictionary<String, Object>)json["keys"];

            Assert.AreEqual("https://service.example/1", json["endpoint"]);
            Assert.IsNull(json["expirationTime"]);
            Assert.AreEqual("-_8", keys["p256dh"]);
            Assert.AreEqual("__79", keys["auth"]);
        }

        [Test]
        public async Task SubscriptionsAreRetrievable()
        {
            var registration = await RegisterAsync(ConfigureWorkers().WithPush()).ConfigureAwait(false);
            var manager = registration.PushManager;

            Assert.IsNull(await manager.GetSubscription().ConfigureAwait(false));

            var subscription = await manager.Subscribe().ConfigureAwait(false);

            Assert.AreSame(subscription, await manager.GetSubscription().ConfigureAwait(false));
        }

        [Test]
        public async Task SubscribingWithTheSameOptionsReturnsTheExistingSubscription()
        {
            var calls = 0;
            var platform = new PushPlatform
            {
                Subscribe = (origin, scope, options) =>
                {
                    calls++;
                    return Task.FromResult(new PushSubscriptionData { Endpoint = "https://service.example/" + calls });
                },
            };
            var registration = await RegisterAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);
            var options = new PushSubscriptionOptionsInit { ApplicationServerKey = ServerKey };

            var first = await registration.PushManager.Subscribe(options).ConfigureAwait(false);
            var second = await registration.PushManager.Subscribe(new PushSubscriptionOptionsInit { ApplicationServerKey = Base64UrlOf(ServerKey) }).ConfigureAwait(false);

            Assert.AreSame(first, second);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public async Task ConcurrentSubscribesCreateOneSubscription()
        {
            var calls = 0;
            var finish = new TaskCompletionSource<Boolean>();
            var platform = new PushPlatform
            {
                Subscribe = async (origin, scope, options) =>
                {
                    calls++;
                    await finish.Task.ConfigureAwait(false);
                    return new PushSubscriptionData { Endpoint = "https://service.example/1" };
                },
            };
            var registration = await RegisterAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);

            var first = registration.PushManager.Subscribe();
            var second = registration.PushManager.Subscribe();
            finish.SetResult(true);

            Assert.AreSame(await first.ConfigureAwait(false), await second.ConfigureAwait(false));
            Assert.AreEqual(1, calls);
        }

        [Test]
        public async Task SubscribingWithDifferentOptionsIsAnInvalidState()
        {
            var registration = await RegisterAsync(ConfigureWorkers().WithPush()).ConfigureAwait(false);
            var manager = registration.PushManager;
            await manager.Subscribe(new PushSubscriptionOptionsInit { ApplicationServerKey = ServerKey }).ConfigureAwait(false);

            AssertRejected(DomError.InvalidState, () => manager.Subscribe());
            AssertRejected(DomError.InvalidState, () => manager.Subscribe(new PushSubscriptionOptionsInit { ApplicationServerKey = CreateKey(9) }));
            AssertRejected(DomError.InvalidState, () => manager.Subscribe(new PushSubscriptionOptionsInit { ApplicationServerKey = ServerKey, UserVisibleOnly = true }));
        }

        [Test]
        public async Task InvalidServerKeysAreRejected()
        {
            var calls = 0;
            var platform = new PushPlatform
            {
                Subscribe = (origin, scope, options) =>
                {
                    calls++;
                    return Task.FromResult(new PushSubscriptionData { Endpoint = "https://service.example/1" });
                },
            };
            var registration = await RegisterAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);
            var manager = registration.PushManager;
            var compressed = CreateKey(1);
            compressed[0] = 0x03;

            AssertRejected(DomError.InvalidCharacter, () => manager.Subscribe(new PushSubscriptionOptionsInit { ApplicationServerKey = "not*base64" }));
            AssertRejected(DomError.InvalidCharacter, () => manager.Subscribe(new PushSubscriptionOptionsInit { ApplicationServerKey = "a+b/" }));
            AssertRejected(DomError.InvalidAccess, () => manager.Subscribe(new PushSubscriptionOptionsInit { ApplicationServerKey = new Byte[64] }));
            AssertRejected(DomError.InvalidAccess, () => manager.Subscribe(new PushSubscriptionOptionsInit { ApplicationServerKey = compressed }));
            AssertRejected(DomError.InvalidAccess, () => manager.Subscribe(new PushSubscriptionOptionsInit { ApplicationServerKey = 42 }));
            Assert.AreEqual(0, calls);
        }

        [Test]
        public async Task ServerKeysAreAcceptedAsSegments()
        {
            var registration = await RegisterAsync(ConfigureWorkers().WithPush()).ConfigureAwait(false);
            var buffer = new Byte[] { 0 }.Concat(ServerKey).ToArray();

            var subscription = await registration.PushManager.Subscribe(new PushSubscriptionOptionsInit
            {
                ApplicationServerKey = new ArraySegment<Byte>(buffer, 1, ServerKey.Length),
            }).ConfigureAwait(false);

            CollectionAssert.AreEqual(ServerKey, subscription.Options.ApplicationServerKey);
        }

        [Test]
        public async Task SubscriptionsAreOriginScoped()
        {
            var calls = new List<String>();
            var platform = new PushPlatform
            {
                Subscribe = (origin, scope, options) =>
                {
                    calls.Add(origin);
                    return Task.FromResult(new PushSubscriptionData { Endpoint = "https://service.example/" + origin });
                },
            };
            var registry = new ServiceWorkerRegistry(new ServiceWorkerPlatform()) { PushPlatform = platform };
            var first = await registry.RegisterAsync(new Url("https://a.example/sw.js"), new Url("https://a.example/")).ConfigureAwait(false);
            var second = await registry.RegisterAsync(new Url("https://b.example/sw.js"), new Url("https://b.example/")).ConfigureAwait(false);

            var subscription = await first.PushManager.Subscribe().ConfigureAwait(false);

            Assert.IsNull(await second.PushManager.GetSubscription().ConfigureAwait(false));

            var other = await second.PushManager.Subscribe().ConfigureAwait(false);

            Assert.AreNotSame(subscription, other);
            Assert.AreSame(subscription, await first.PushManager.GetSubscription().ConfigureAwait(false));
            CollectionAssert.AreEqual(new[] { "https://a.example", "https://b.example" }, calls);
        }

        [Test]
        public async Task SubscriptionsAreRetrievableAcrossContextsOfTheSameRegistry()
        {
            var config = ConfigureWorkers().WithPush();
            var context = await OpenContextAsync(config).ConfigureAwait(false);
            var registration = await context.Active.DefaultView.Navigator.ServiceWorker().Register("/app/sw.js").ConfigureAwait(false);
            var subscription = await registration.PushManager.Subscribe().ConfigureAwait(false);

            var other = await OpenContextAsync(config).ConfigureAwait(false);
            var found = await other.Active.DefaultView.Navigator.ServiceWorker().GetRegistration().ConfigureAwait(false);

            Assert.AreSame(subscription, await found.PushManager.GetSubscription().ConfigureAwait(false));
        }

        [Test]
        public async Task UnsubscribeRemovesTheActiveSubscription()
        {
            var removed = new List<PushSubscription>();
            var platform = new PushPlatform { Unsubscribe = s => { removed.Add(s); return Task.CompletedTask; } };
            var registration = await RegisterAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);
            var manager = registration.PushManager;
            var subscription = await manager.Subscribe().ConfigureAwait(false);

            Assert.IsTrue(await subscription.Unsubscribe().ConfigureAwait(false));
            Assert.IsFalse(await subscription.Unsubscribe().ConfigureAwait(false));

            Assert.IsNull(await manager.GetSubscription().ConfigureAwait(false));
            CollectionAssert.AreEqual(new[] { subscription }, removed);
        }

        [Test]
        public async Task AFailingBackendStillDeactivatesTheSubscription()
        {
            var platform = new PushPlatform { Unsubscribe = _ => Task.FromException(new IOException()) };
            var registration = await RegisterAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);
            var subscription = await registration.PushManager.Subscribe().ConfigureAwait(false);

            Assert.IsTrue(await subscription.Unsubscribe().ConfigureAwait(false));

            Assert.IsNull(await registration.PushManager.GetSubscription().ConfigureAwait(false));
        }

        [Test]
        public async Task SubscribingAfterUnsubscribingCreatesANewSubscription()
        {
            var registration = await RegisterAsync(ConfigureWorkers().WithPush()).ConfigureAwait(false);
            var manager = registration.PushManager;
            var first = await manager.Subscribe(new PushSubscriptionOptionsInit { UserVisibleOnly = true }).ConfigureAwait(false);
            await first.Unsubscribe().ConfigureAwait(false);

            var second = await manager.Subscribe().ConfigureAwait(false);

            Assert.AreNotSame(first, second);
            Assert.AreNotEqual(first.Endpoint, second.Endpoint);
            Assert.IsFalse(await first.Unsubscribe().ConfigureAwait(false));
            Assert.AreSame(second, await manager.GetSubscription().ConfigureAwait(false));
        }

        [Test]
        public async Task AnExpiredSubscriptionIsNotActiveAnymore()
        {
            var removed = 0;
            var calls = 0;
            var platform = new PushPlatform
            {
                Subscribe = (origin, scope, options) =>
                {
                    calls++;
                    return Task.FromResult(new PushSubscriptionData
                    {
                        Endpoint = "https://service.example/" + calls,
                        ExpirationTime = calls == 1 ? 1.0 : (Double?)null,
                    });
                },
                Unsubscribe = _ => { removed++; return Task.CompletedTask; },
            };
            var registration = await RegisterAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);
            var manager = registration.PushManager;
            var expired = await manager.Subscribe().ConfigureAwait(false);

            Assert.IsNull(await manager.GetSubscription().ConfigureAwait(false));
            Assert.IsFalse(await expired.Unsubscribe().ConfigureAwait(false));
            Assert.AreEqual(0, removed);

            var fresh = await manager.Subscribe().ConfigureAwait(false);

            Assert.AreNotSame(expired, fresh);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task AFailingBackendRejectsWithAnAbortErrorWithoutSubscribing(Boolean returnsNothing)
        {
            var platform = new PushPlatform
            {
                Subscribe = (origin, scope, options) => returnsNothing
                    ? Task.FromResult<PushSubscriptionData>(null)
                    : Task.FromException<PushSubscriptionData>(new IOException()),
            };
            var registration = await RegisterAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);

            AssertRejected(DomError.Abort, () => registration.PushManager.Subscribe());

            Assert.IsNull(await registration.PushManager.GetSubscription().ConfigureAwait(false));
        }

        [Test]
        public async Task ABackendCanRejectWithItsOwnDomException()
        {
            var platform = new PushPlatform
            {
                Subscribe = (origin, scope, options) => Task.FromException<PushSubscriptionData>(new DomException(DomError.NotSupported)),
            };
            var registration = await RegisterAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);

            AssertRejected(DomError.NotSupported, () => registration.PushManager.Subscribe());
        }

        [Test]
        public async Task SubscribingIsRetryableAfterAFailure()
        {
            var fail = true;
            var platform = new PushPlatform
            {
                Subscribe = (origin, scope, options) => fail
                    ? Task.FromException<PushSubscriptionData>(new IOException())
                    : Task.FromResult(new PushSubscriptionData { Endpoint = "https://service.example/1" }),
            };
            var registration = await RegisterAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);

            AssertRejected(DomError.Abort, () => registration.PushManager.Subscribe());
            fail = false;

            Assert.IsNotNull(await registration.PushManager.Subscribe().ConfigureAwait(false));
        }

        [Test]
        public async Task ADeniedPermissionRejectsWithoutCallingTheBackend()
        {
            var calls = 0;
            var platform = new PushPlatform
            {
                Subscribe = (origin, scope, options) =>
                {
                    calls++;
                    return Task.FromResult(new PushSubscriptionData { Endpoint = "https://service.example/1" });
                },
            };
            var policy = new PermissionPolicy(PermissionState.Denied);
            var registration = await RegisterAsync(ConfigureWorkers().WithPermissions(policy).WithPush(platform)).ConfigureAwait(false);

            AssertNotAllowed(() => registration.PushManager.Subscribe());

            Assert.AreEqual(0, calls);
            Assert.IsNull(await registration.PushManager.GetSubscription().ConfigureAwait(false));
        }

        [Test]
        public async Task UserVisibleSubscriptionsAlsoNeedTheNotificationPermission()
        {
            var policy = new PermissionPolicy(PermissionState.Denied);
            policy.Set(PermissionNames.Push, PermissionState.Granted);
            var registration = await RegisterAsync(ConfigureWorkers().WithPermissions(policy).WithPush()).ConfigureAwait(false);
            var manager = registration.PushManager;
            var visible = new PushSubscriptionOptionsInit { UserVisibleOnly = true };

            AssertNotAllowed(() => manager.Subscribe(visible));

            var subscription = await manager.Subscribe().ConfigureAwait(false);
            await subscription.Unsubscribe().ConfigureAwait(false);

            policy.Set(PermissionNames.Notifications, PermissionState.Granted);

            Assert.IsNotNull(await manager.Subscribe(visible).ConfigureAwait(false));
        }

        [Test]
        public async Task PromptsAreAnsweredForTheOriginOfTheRegistration()
        {
            var prompts = new List<String>();
            var policy = new PermissionPolicy
            {
                PromptHandler = (origin, name) =>
                {
                    prompts.Add(origin + "|" + name);
                    return Task.FromResult(PermissionState.Granted);
                },
            };
            var registration = await RegisterAsync(ConfigureWorkers().WithPermissions(policy).WithPush()).ConfigureAwait(false);

            await registration.PushManager.Subscribe(new PushSubscriptionOptionsInit { UserVisibleOnly = true }).ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { Origin + "|push", Origin + "|notifications" }, prompts);
        }

        [Test]
        public async Task PermissionStateCombinesThePermissions()
        {
            var policy = new PermissionPolicy();
            var registration = await RegisterAsync(ConfigureWorkers().WithPermissions(policy).WithPush()).ConfigureAwait(false);
            var manager = registration.PushManager;
            var visible = new PushSubscriptionOptionsInit { UserVisibleOnly = true };

            Assert.AreEqual(PermissionState.Prompt, await manager.PermissionState().ConfigureAwait(false));

            policy.Set(PermissionNames.Push, PermissionState.Granted);

            Assert.AreEqual(PermissionState.Granted, await manager.PermissionState().ConfigureAwait(false));
            Assert.AreEqual(PermissionState.Prompt, await manager.PermissionState(visible).ConfigureAwait(false));

            policy.Set(PermissionNames.Notifications, PermissionState.Denied);

            Assert.AreEqual(PermissionState.Granted, await manager.PermissionState().ConfigureAwait(false));
            Assert.AreEqual(PermissionState.Denied, await manager.PermissionState(visible).ConfigureAwait(false));

            policy.Set(PermissionNames.Notifications, PermissionState.Granted);

            Assert.AreEqual(PermissionState.Granted, await manager.PermissionState(visible).ConfigureAwait(false));
        }

        [Test]
        public async Task WithoutPolicyTheStateIsGranted()
        {
            var registration = await RegisterAsync(ConfigureWorkers().WithPush()).ConfigureAwait(false);

            Assert.AreEqual(PermissionState.Granted, await registration.PushManager.PermissionState().ConfigureAwait(false));
        }

        [Test]
        public async Task SubscribingWithoutActiveWorkerIsAnInvalidState()
        {
            var registration = await RegisterAsync(ConfigureWorkers().WithPush()).ConfigureAwait(false);
            var manager = registration.PushManager;
            await registration.Unregister().ConfigureAwait(false);

            AssertRejected(DomError.InvalidState, () => manager.Subscribe());
        }

        [Test]
        public async Task SubscribingFromAnInsecureOriginIsNotAllowed()
        {
            var registry = new ServiceWorkerRegistry(new ServiceWorkerPlatform()) { PushPlatform = new PushPlatform() };
            var registration = await registry.RegisterAsync(new Url("http://push.example/sw.js"), new Url("http://push.example/")).ConfigureAwait(false);

            AssertNotAllowed(() => registration.PushManager.Subscribe());
        }

        [Test]
        public async Task UnregisteringTheServiceWorkerRemovesTheSubscription()
        {
            var removed = 0;
            var platform = new PushPlatform { Unsubscribe = _ => { removed++; return Task.FromException(new IOException()); } };
            var registration = await RegisterAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);
            var manager = registration.PushManager;
            var subscription = await manager.Subscribe().ConfigureAwait(false);

            Assert.IsTrue(await registration.Unregister().ConfigureAwait(false));

            Assert.AreEqual(1, removed);
            Assert.IsNull(await manager.GetSubscription().ConfigureAwait(false));
            Assert.IsFalse(await subscription.Unsubscribe().ConfigureAwait(false));
        }

        [Test]
        public async Task MessagesAreDeliveredToTheActiveWorkerOfTheSubscription()
        {
            var delivered = new List<(ServiceWorker Worker, PushMessageData Data)>();
            var platform = new PushPlatform
            {
                Dispatch = (worker, data) =>
                {
                    delivered.Add((worker, data));
                    return Task.CompletedTask;
                },
            };
            var context = await OpenContextAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);
            var registration = await context.Active.DefaultView.Navigator.ServiceWorker().Register("/app/sw.js").ConfigureAwait(false);
            var registry = context.GetService<ServiceWorkerRegistry>();
            var subscription = await registration.PushManager.Subscribe().ConfigureAwait(false);

            Assert.IsTrue(await registry.DeliverPushAsync(subscription.Endpoint, Encoding.UTF8.GetBytes("{\"a\":1}")).ConfigureAwait(false));
            Assert.IsTrue(await registry.DeliverPushAsync(subscription.Endpoint, null).ConfigureAwait(false));

            Assert.AreEqual(2, delivered.Count);
            Assert.AreSame(registration.Active, delivered[0].Worker);
            Assert.AreEqual("{\"a\":1}", delivered[0].Data.Text());
            Assert.IsNull(delivered[1].Data);
        }

        [Test]
        public async Task MessagesForUnknownOrRemovedSubscriptionsAreNotDelivered()
        {
            var delivered = 0;
            var platform = new PushPlatform { Dispatch = (worker, data) => { delivered++; return Task.CompletedTask; } };
            var context = await OpenContextAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);
            var registration = await context.Active.DefaultView.Navigator.ServiceWorker().Register("/app/sw.js").ConfigureAwait(false);
            var registry = context.GetService<ServiceWorkerRegistry>();
            var subscription = await registration.PushManager.Subscribe().ConfigureAwait(false);

            Assert.IsFalse(await registry.DeliverPushAsync("https://service.example/unknown", null).ConfigureAwait(false));

            await subscription.Unsubscribe().ConfigureAwait(false);

            Assert.IsFalse(await registry.DeliverPushAsync(subscription.Endpoint, null).ConfigureAwait(false));
            Assert.AreEqual(0, delivered);
        }

        [Test]
        public async Task AFailingDeliveryFaultsTheReturnedTask()
        {
            var platform = new PushPlatform { Dispatch = (worker, data) => Task.FromException(new IOException()) };
            var context = await OpenContextAsync(ConfigureWorkers().WithPush(platform)).ConfigureAwait(false);
            var registration = await context.Active.DefaultView.Navigator.ServiceWorker().Register("/app/sw.js").ConfigureAwait(false);
            var registry = context.GetService<ServiceWorkerRegistry>();
            var subscription = await registration.PushManager.Subscribe().ConfigureAwait(false);

            Assert.ThrowsAsync<IOException>(() => registry.DeliverPushAsync(subscription.Endpoint, null));
        }

        [Test]
        public void MessageDataExposesThePayload()
        {
            var content = Encoding.UTF8.GetBytes("{\"answer\":42,\"text\":\"Grüße\"}");
            var data = new PushMessageData(content);
            var json = (IDictionary<String, Object>)data.Json();
            var blob = data.Blob();
            var bytes = data.Bytes();
            bytes.Array[bytes.Offset] = 0;

            Assert.AreEqual("{\"answer\":42,\"text\":\"Grüße\"}", data.Text());
            Assert.AreEqual(42.0, json["answer"]);
            Assert.AreEqual("Grüße", json["text"]);
            CollectionAssert.AreEqual(content, data.ArrayBuffer());
            Assert.AreEqual(content.Length, blob.Length);
            Assert.Throws<ArgumentNullException>(() => new PushMessageData(null));
        }

        private static IConfiguration ConfigureWorkers() => Configuration.Default.WithNavigator().WithServiceWorkers();

        private static async Task<ServiceWorkerRegistration> RegisterAsync(IConfiguration configuration)
        {
            var context = await OpenContextAsync(configuration).ConfigureAwait(false);
            return await context.Active.DefaultView.Navigator.ServiceWorker().Register("/app/sw.js").ConfigureAwait(false);
        }

        private static async Task<IBrowsingContext> OpenContextAsync(IConfiguration configuration)
        {
            var context = BrowsingContext.New(configuration);
            await context.OpenAsync(res => res.Address(Address).Content("<!doctype html>")).ConfigureAwait(false);
            return context;
        }

        private static Byte[] CreateKey(Byte seed)
        {
            var key = new Byte[65];
            key[0] = 0x04;
            key[1] = seed;
            return key;
        }

        private static String Base64UrlOf(Byte[] value) =>
            Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static void AssertRejected(DomError error, AsyncTestDelegate action)
        {
            var ex = Assert.ThrowsAsync<DomException>(action);
            Assert.AreEqual((Int32)error, ex.Code);
        }

        private static void AssertNotAllowed(AsyncTestDelegate action)
        {
            var ex = Assert.ThrowsAsync<DomException>(action);
            Assert.AreEqual("NotAllowedError", ex.Name);
        }
    }
}
