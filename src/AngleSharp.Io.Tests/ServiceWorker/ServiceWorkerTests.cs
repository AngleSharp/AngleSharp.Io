namespace AngleSharp.Io.Tests.ServiceWorker
{
    using AngleSharp;
    using AngleSharp.Browser.Dom;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.Http;
    using AngleSharp.Io.ServiceWorkerApi;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    [TestFixture]
    public class ServiceWorkerTests
    {
        [Test]
        public async Task ServiceWorkerContainerCanBeResolvedFromNavigatorWhenRegistered()
        {
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithServiceWorkers(), "https://sw.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);

            Assert.IsNotNull(navigator.ServiceWorker());
            Assert.AreSame(navigator.ServiceWorker(), navigator.ServiceWorker());
        }

        [Test]
        public async Task ServiceWorkerContainerIsUnavailableWhenNotRegistered()
        {
            var context = await OpenContextAsync(Configuration.Default.WithNavigator(), "https://sw.example/").ConfigureAwait(false);

            Assert.IsNull(GetNavigator(context).ServiceWorker());
        }

        [Test]
        public async Task ServiceWorkerContainerIsUnavailableWhenRegisteredWithoutPlatform()
        {
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithServiceWorkers((IServiceWorkerPlatform)null), "https://sw.example/").ConfigureAwait(false);

            Assert.IsNull(GetNavigator(context).ServiceWorker());
        }

        [Test]
        public void ServiceWorkerTypesRequireTheirDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceWorkerRegistry(null));
            Assert.Throws<ArgumentNullException>(() => new ServiceWorkerProviderFactory(null));
            Assert.Throws<ArgumentNullException>(() => new ServiceWorkerContainer(null, () => null));
            Assert.Throws<ArgumentNullException>(() => new ServiceWorkerContainer(new ServiceWorkerRegistry(new ServiceWorkerPlatform()), null));
            Assert.Throws<ArgumentNullException>(() => new ServiceWorkerRequester(null, new ServiceWorkerRegistry(new ServiceWorkerPlatform())));
            Assert.Throws<ArgumentNullException>(() => new ServiceWorkerRequester(new Mocks.VirtualRequester(_ => null), null));
            Assert.IsNull(new ServiceWorkerProviderFactory(new ServiceWorkerRegistry(new ServiceWorkerPlatform())).GetServiceWorkerContainer(null));
        }

        [Test]
        public async Task RegisterRunsTheLifecycleUntilActivated()
        {
            var steps = new List<String>();
            var platform = new ServiceWorkerPlatform
            {
                Install = worker =>
                {
                    steps.Add("install:" + worker.State.ToString());
                    worker.StateChanged += (s, e) => steps.Add(((ServiceWorker)s).State.ToString());
                    return Task.CompletedTask;
                },
                Activate = worker =>
                {
                    steps.Add("activate:" + worker.State.ToString());
                    return Task.CompletedTask;
                },
            };

            var container = await OpenContainerAsync(platform, "https://sw.example/app/index.html").ConfigureAwait(false);
            var registration = await container.Register("/app/sw.js").ConfigureAwait(false);

            Assert.AreEqual("https://sw.example/app/", registration.Scope);
            Assert.IsNull(registration.Installing);
            Assert.IsNull(registration.Waiting);
            Assert.IsNotNull(registration.Active);
            Assert.AreEqual("https://sw.example/app/sw.js", registration.Active.ScriptUrl);
            Assert.AreEqual(ServiceWorkerState.Activated, registration.Active.State);
            Assert.AreSame(registration, registration.Active.Registration);
            CollectionAssert.AreEqual(new[] { "install:Installing", "Installed", "Activating", "activate:Activating", "Activated" }, steps);
        }

        [Test]
        public async Task RegisterRaisesUpdateFoundWithTheInstallingWorker()
        {
            var registry = new ServiceWorkerRegistry(new ServiceWorkerPlatform());
            var found = new List<ServiceWorker>();
            var registration = await registry.RegisterAsync(new Url("https://sw.example/sw.js"), new Url("https://sw.example/")).ConfigureAwait(false);
            registration.UpdateFound += (s, e) => found.Add(((ServiceWorkerRegistration)s).Installing);

            await registry.RegisterAsync(new Url("https://sw.example/sw2.js"), new Url("https://sw.example/")).ConfigureAwait(false);

            Assert.AreEqual(1, found.Count);
            Assert.AreEqual("https://sw.example/sw2.js", found[0].ScriptUrl);
        }

        [Test]
        public async Task RegisterUsesTheGivenScope()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);

            var registration = await container.Register("/sw.js", new ServiceWorkerRegistrationOptions { Scope = "/app/" }).ConfigureAwait(false);

            Assert.AreEqual("https://sw.example/app/", registration.Scope);
        }

        [Test]
        public async Task RegisterIgnoresFragments()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);

            var registration = await container.Register("/sw.js#first", new ServiceWorkerRegistrationOptions { Scope = "/app/#top" }).ConfigureAwait(false);

            Assert.AreEqual("https://sw.example/app/", registration.Scope);
            Assert.AreEqual("https://sw.example/sw.js", registration.Active.ScriptUrl);
        }

        [Test]
        public async Task RegisteringTheSameScriptAgainReturnsTheExistingRegistration()
        {
            var installs = 0;
            var platform = new ServiceWorkerPlatform
            {
                Install = _ =>
                {
                    installs++;
                    return Task.CompletedTask;
                },
            };

            var container = await OpenContainerAsync(platform, "https://sw.example/").ConfigureAwait(false);
            var first = await container.Register("/sw.js").ConfigureAwait(false);
            var worker = first.Active;
            var second = await container.Register("/sw.js").ConfigureAwait(false);

            Assert.AreSame(first, second);
            Assert.AreSame(worker, second.Active);
            Assert.AreEqual(1, installs);
        }

        [Test]
        public async Task RegisteringAnotherScriptReplacesTheActiveWorker()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);
            var first = await container.Register("/sw.js").ConfigureAwait(false);
            var previous = first.Active;
            var second = await container.Register("/sw-v2.js", new ServiceWorkerRegistrationOptions { Scope = "/" }).ConfigureAwait(false);

            Assert.AreSame(first, second);
            Assert.AreEqual(ServiceWorkerState.Redundant, previous.State);
            Assert.AreEqual("https://sw.example/sw-v2.js", second.Active.ScriptUrl);
            Assert.AreEqual(ServiceWorkerState.Activated, second.Active.State);
        }

        [Test]
        public async Task FailingInstallationMakesTheWorkerRedundantAndDropsTheRegistration()
        {
            var platform = new ServiceWorkerPlatform
            {
                Install = _ => Task.FromException(new InvalidOperationException("broken")),
            };

            var container = await OpenContainerAsync(platform, "https://sw.example/").ConfigureAwait(false);
            var registration = await container.Register("/sw.js").ConfigureAwait(false);

            Assert.IsNull(registration.Installing);
            Assert.IsNull(registration.Waiting);
            Assert.IsNull(registration.Active);
            Assert.IsNull(await container.GetRegistration().ConfigureAwait(false));
            Assert.IsEmpty(await container.GetRegistrations().ConfigureAwait(false));
        }

        [Test]
        public async Task FailingUpdateKeepsTheActiveWorker()
        {
            var platform = new ServiceWorkerPlatform();
            var container = await OpenContainerAsync(platform, "https://sw.example/").ConfigureAwait(false);
            var registration = await container.Register("/sw.js").ConfigureAwait(false);
            var active = registration.Active;
            var failed = default(ServiceWorker);
            platform.Install = worker =>
            {
                failed = worker;
                return Task.FromException(new InvalidOperationException("broken"));
            };

            await container.Register("/sw-v2.js", new ServiceWorkerRegistrationOptions { Scope = "/" }).ConfigureAwait(false);

            Assert.AreSame(active, registration.Active);
            Assert.AreEqual(ServiceWorkerState.Activated, active.State);
            Assert.AreEqual(ServiceWorkerState.Redundant, failed.State);
            Assert.AreSame(registration, await container.GetRegistration().ConfigureAwait(false));
        }

        [Test]
        public async Task FailingActivationStillActivatesTheWorker()
        {
            var platform = new ServiceWorkerPlatform
            {
                Activate = _ => Task.FromException(new InvalidOperationException("broken")),
            };

            var container = await OpenContainerAsync(platform, "https://sw.example/").ConfigureAwait(false);
            var registration = await container.Register("/sw.js").ConfigureAwait(false);

            Assert.AreEqual(ServiceWorkerState.Activated, registration.Active.State);
        }

        [Test]
        public async Task GetRegistrationMatchesTheLongestScope()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);
            var root = await container.Register("/sw.js").ConfigureAwait(false);
            var app = await container.Register("/app/sw.js").ConfigureAwait(false);

            Assert.AreSame(root, await container.GetRegistration().ConfigureAwait(false));
            Assert.AreSame(root, await container.GetRegistration("/other/page").ConfigureAwait(false));
            Assert.AreSame(app, await container.GetRegistration("/app/").ConfigureAwait(false));
            Assert.AreSame(app, await container.GetRegistration("https://sw.example/app/deep/page?x=1").ConfigureAwait(false));
            Assert.AreSame(root, await container.GetRegistration("/application").ConfigureAwait(false));
        }

        [Test]
        public async Task GetRegistrationMatchesScopesAsPrefix()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);
            var registration = await container.Register("/sw.js", new ServiceWorkerRegistrationOptions { Scope = "/app" }).ConfigureAwait(false);

            Assert.AreSame(registration, await container.GetRegistration("/app").ConfigureAwait(false));
            Assert.AreSame(registration, await container.GetRegistration("/application").ConfigureAwait(false));
            Assert.IsNull(await container.GetRegistration("/ap").ConfigureAwait(false));
        }

        [Test]
        public async Task GetRegistrationIsNullWithoutMatchingScope()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);
            await container.Register("/app/sw.js").ConfigureAwait(false);

            Assert.IsNull(await container.GetRegistration().ConfigureAwait(false));
            Assert.IsNull(await container.GetRegistration("/other/").ConfigureAwait(false));
        }

        [Test]
        public async Task GetRegistrationsReturnsTheRegistrationsOfTheOrigin()
        {
            var configuration = Configuration.Default.WithNavigator().WithServiceWorkers();
            var first = GetNavigator(await OpenContextAsync(configuration, "https://first.example/").ConfigureAwait(false)).ServiceWorker();
            var second = GetNavigator(await OpenContextAsync(configuration, "https://second.example/").ConfigureAwait(false)).ServiceWorker();
            var sameOrigin = GetNavigator(await OpenContextAsync(configuration, "https://first.example/other").ConfigureAwait(false)).ServiceWorker();

            var app = await first.Register("/app/sw.js").ConfigureAwait(false);
            var root = await first.Register("/sw.js").ConfigureAwait(false);
            var other = await second.Register("/sw.js").ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { root, app }, await first.GetRegistrations().ConfigureAwait(false));
            CollectionAssert.AreEqual(new[] { root, app }, await sameOrigin.GetRegistrations().ConfigureAwait(false));
            CollectionAssert.AreEqual(new[] { other }, await second.GetRegistrations().ConfigureAwait(false));
        }

        [Test]
        public async Task UnregisterRemovesTheRegistration()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);
            var registration = await container.Register("/sw.js").ConfigureAwait(false);
            var worker = registration.Active;

            Assert.IsTrue(await registration.Unregister().ConfigureAwait(false));
            Assert.IsFalse(await registration.Unregister().ConfigureAwait(false));
            Assert.AreEqual(ServiceWorkerState.Redundant, worker.State);
            Assert.IsNull(registration.Active);
            Assert.IsNull(await container.GetRegistration().ConfigureAwait(false));
            Assert.IsEmpty(await container.GetRegistrations().ConfigureAwait(false));
        }

        [Test]
        public async Task RegisterAfterUnregisterCreatesANewRegistration()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);
            var first = await container.Register("/sw.js").ConfigureAwait(false);
            await first.Unregister().ConfigureAwait(false);

            var second = await container.Register("/sw.js").ConfigureAwait(false);

            Assert.AreNotSame(first, second);
            Assert.AreEqual(ServiceWorkerState.Activated, second.Active.State);
            Assert.IsFalse(await first.Unregister().ConfigureAwait(false));
            Assert.AreSame(second, await container.GetRegistration().ConfigureAwait(false));
        }

        [Test]
        public async Task ControllerIsTheActiveWorkerOfTheMatchingScope()
        {
            var configuration = Configuration.Default.WithNavigator().WithServiceWorkers();
            var inside = GetNavigator(await OpenContextAsync(configuration, "https://sw.example/app/page").ConfigureAwait(false)).ServiceWorker();
            var outside = GetNavigator(await OpenContextAsync(configuration, "https://sw.example/other").ConfigureAwait(false)).ServiceWorker();

            Assert.IsNull(inside.Controller);

            var registration = await inside.Register("/app/sw.js").ConfigureAwait(false);

            Assert.AreSame(registration.Active, inside.Controller);
            Assert.IsNull(outside.Controller);
        }

        [Test]
        public async Task RegisterRejectsCrossOriginUrls()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);

            AssertSecurityError(() => container.Register("https://other.example/sw.js"));
            AssertSecurityError(() => container.Register("/sw.js", new ServiceWorkerRegistrationOptions { Scope = "https://other.example/" }));
            AssertSecurityError(() => container.GetRegistration("https://other.example/"));
        }

        [Test]
        public async Task RegisterRejectsScopesAboveTheScriptDirectory()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);

            AssertSecurityError(() => container.Register("/app/sw.js", new ServiceWorkerRegistrationOptions { Scope = "/" }));
            AssertSecurityError(() => container.Register("/app/sw.js", new ServiceWorkerRegistrationOptions { Scope = "/app" }));
            Assert.IsEmpty(await container.GetRegistrations().ConfigureAwait(false));
        }

        [Test]
        public async Task RegisterRejectsInvalidUrls()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);

            Assert.ThrowsAsync<InvalidOperationException>(() => container.Register("ftp://sw.example/sw.js"));
            Assert.ThrowsAsync<InvalidOperationException>(() => container.Register("/sw.js", new ServiceWorkerRegistrationOptions { Scope = "data:text/plain,x" }));
            Assert.ThrowsAsync<InvalidOperationException>(() => container.Register("/a%2fb/sw.js"));
            Assert.ThrowsAsync<InvalidOperationException>(() => container.Register("/sw.js", new ServiceWorkerRegistrationOptions { Scope = "/a%5Cb/" }));
        }

        [TestCase("https://sw.example/", true)]
        [TestCase("http://localhost:8080/", true)]
        [TestCase("http://app.localhost/", true)]
        [TestCase("http://127.0.0.1/", true)]
        [TestCase("http://[::1]/", true)]
        [TestCase("http://sw.example/", false)]
        [TestCase("about:blank", false)]
        public async Task ServiceWorkerContainerIsOnlyExposedInSecureContexts(String address, Boolean exposed)
        {
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithServiceWorkers(), address).ConfigureAwait(false);

            Assert.AreEqual(exposed, GetNavigator(context).ServiceWorker() is not null);
        }

        [Test]
        public async Task ContainerRejectsInsecureDocuments()
        {
            var registry = new ServiceWorkerRegistry(new ServiceWorkerPlatform());
            var container = new ServiceWorkerContainer(registry, () => new Url("http://sw.example/"));
            await registry.RegisterAsync(new Url("http://sw.example/sw.js"), new Url("http://sw.example/")).ConfigureAwait(false);

            AssertSecurityError(() => container.Register("http://sw.example/sw.js"));
            AssertSecurityError(() => container.GetRegistration());
            AssertSecurityError(() => container.GetRegistrations());
            Assert.IsNull(container.Controller);
            Assert.IsFalse(container.Ready.IsCompleted);
        }

        [Test]
        public async Task ReadyCompletesRightAwayWithAnActiveRegistration()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/app/").ConfigureAwait(false);
            var registration = await container.Register("/app/sw.js").ConfigureAwait(false);

            var ready = container.Ready;

            Assert.IsTrue(ready.IsCompleted);
            Assert.AreSame(registration, await ready.ConfigureAwait(false));
        }

        [Test]
        public async Task ReadyCompletesOnceAMatchingWorkerIsActivated()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/app/page").ConfigureAwait(false);
            var ready = container.Ready;

            await container.Register("/other/sw.js").ConfigureAwait(false);
            Assert.IsFalse(ready.IsCompleted);

            var registration = await container.Register("/app/sw.js").ConfigureAwait(false);
            var completed = await Task.WhenAny(ready, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
            Assert.AreSame(ready, completed);
            var result = await ready.ConfigureAwait(false);

            Assert.AreSame(registration, result);
            Assert.AreEqual(ServiceWorkerState.Activated, result.Active.State);
        }

        [Test]
        public async Task ReadyStaysPendingWhenTheInstallationFails()
        {
            var platform = new ServiceWorkerPlatform
            {
                Install = _ => Task.FromException(new InvalidOperationException("broken")),
            };

            var container = await OpenContainerAsync(platform, "https://sw.example/").ConfigureAwait(false);
            var ready = container.Ready;

            await container.Register("/sw.js").ConfigureAwait(false);

            Assert.IsFalse(ready.IsCompleted);
        }

        [Test]
        public async Task RegisterAppliesTheTypeAndUpdateViaCacheOptions()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);

            var classic = await container.Register("/sw.js").ConfigureAwait(false);
            Assert.AreEqual(WorkerType.Classic, classic.Active.Type);
            Assert.AreEqual(ServiceWorkerUpdateViaCache.Imports, classic.UpdateViaCache);

            var module = await container.Register("/app/sw.js", new ServiceWorkerRegistrationOptions
            {
                Type = WorkerType.Module,
                UpdateViaCache = ServiceWorkerUpdateViaCache.None,
            }).ConfigureAwait(false);
            Assert.AreEqual(WorkerType.Module, module.Active.Type);
            Assert.AreEqual(ServiceWorkerUpdateViaCache.None, module.UpdateViaCache);
        }

        [Test]
        public async Task RegisteringTheSameScriptWithAnotherTypeInstallsANewWorker()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);
            var registration = await container.Register("/sw.js").ConfigureAwait(false);
            var classic = registration.Active;

            await container.Register("/sw.js", new ServiceWorkerRegistrationOptions { Type = WorkerType.Module }).ConfigureAwait(false);

            Assert.AreEqual(ServiceWorkerState.Redundant, classic.State);
            Assert.AreEqual(WorkerType.Module, registration.Active.Type);
            Assert.AreEqual("https://sw.example/sw.js", registration.Active.ScriptUrl);
        }

        [Test]
        public async Task RegisteringTheSameScriptWithAnotherUpdateViaCacheChecksForUpdates()
        {
            var checks = new List<ServiceWorkerUpdateViaCache>();
            var platform = new ServiceWorkerPlatform
            {
                CheckForUpdate = worker =>
                {
                    checks.Add(worker.Registration.UpdateViaCache);
                    return Task.FromResult(false);
                },
            };

            var container = await OpenContainerAsync(platform, "https://sw.example/").ConfigureAwait(false);
            var registration = await container.Register("/sw.js").ConfigureAwait(false);
            var worker = registration.Active;

            await container.Register("/sw.js").ConfigureAwait(false);
            await container.Register("/sw.js", new ServiceWorkerRegistrationOptions { UpdateViaCache = ServiceWorkerUpdateViaCache.All }).ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { ServiceWorkerUpdateViaCache.All }, checks);
            Assert.AreEqual(ServiceWorkerUpdateViaCache.All, registration.UpdateViaCache);
            Assert.AreSame(worker, registration.Active);
        }

        [Test]
        public async Task UpdateKeepsTheWorkerWhenTheScriptIsUnchanged()
        {
            var installs = 0;
            var platform = new ServiceWorkerPlatform
            {
                Install = _ =>
                {
                    installs++;
                    return Task.CompletedTask;
                },
            };

            var container = await OpenContainerAsync(platform, "https://sw.example/").ConfigureAwait(false);
            var registration = await container.Register("/sw.js").ConfigureAwait(false);
            var worker = registration.Active;

            Assert.AreSame(registration, await registration.Update().ConfigureAwait(false));
            Assert.AreSame(worker, registration.Active);
            Assert.AreEqual(1, installs);
        }

        [Test]
        public async Task UpdateInstallsANewWorkerWhenTheScriptChanged()
        {
            var platform = new ServiceWorkerPlatform();
            var container = await OpenContainerAsync(platform, "https://sw.example/").ConfigureAwait(false);
            var registration = await container.Register("/sw.js", new ServiceWorkerRegistrationOptions { Type = WorkerType.Module }).ConfigureAwait(false);
            var previous = registration.Active;
            var found = 0;
            var checkedWorker = default(ServiceWorker);
            registration.UpdateFound += (s, e) => found++;
            platform.CheckForUpdate = worker =>
            {
                checkedWorker = worker;
                return Task.FromResult(true);
            };

            await registration.Update().ConfigureAwait(false);

            Assert.AreSame(previous, checkedWorker);
            Assert.AreEqual(1, found);
            Assert.AreNotSame(previous, registration.Active);
            Assert.AreEqual(ServiceWorkerState.Redundant, previous.State);
            Assert.AreEqual(ServiceWorkerState.Activated, registration.Active.State);
            Assert.AreEqual(previous.ScriptUrl, registration.Active.ScriptUrl);
            Assert.AreEqual(WorkerType.Module, registration.Active.Type);
        }

        [Test]
        public async Task UpdateWithFailingInstallationKeepsTheActiveWorker()
        {
            var platform = new ServiceWorkerPlatform();
            var container = await OpenContainerAsync(platform, "https://sw.example/").ConfigureAwait(false);
            var registration = await container.Register("/sw.js").ConfigureAwait(false);
            var active = registration.Active;
            platform.CheckForUpdate = _ => Task.FromResult(true);
            platform.Install = _ => Task.FromException(new InvalidOperationException("broken"));

            await registration.Update().ConfigureAwait(false);

            Assert.AreSame(active, registration.Active);
            Assert.AreEqual(ServiceWorkerState.Activated, active.State);
        }

        [Test]
        public async Task UpdatePropagatesFailingChecks()
        {
            var platform = new ServiceWorkerPlatform
            {
                CheckForUpdate = _ => Task.FromException<Boolean>(new InvalidOperationException("offline")),
            };

            var container = await OpenContainerAsync(platform, "https://sw.example/").ConfigureAwait(false);
            var registration = await container.Register("/sw.js").ConfigureAwait(false);

            Assert.ThrowsAsync<InvalidOperationException>(() => registration.Update());
            Assert.AreEqual(ServiceWorkerState.Activated, registration.Active.State);
        }

        [Test]
        public async Task UpdateOfAnUnregisteredRegistrationIsRejected()
        {
            var container = await OpenContainerAsync(new ServiceWorkerPlatform(), "https://sw.example/").ConfigureAwait(false);
            var registration = await container.Register("/sw.js").ConfigureAwait(false);
            await registration.Unregister().ConfigureAwait(false);

            var ex = Assert.ThrowsAsync<DomException>(() => registration.Update());

            Assert.AreEqual((Int32)DomError.InvalidState, ex.Code);
        }

        [Test]
        public async Task PostMessageIsDeliveredToThePlatform()
        {
            var received = new List<String>();
            var platform = new ServiceWorkerPlatform
            {
                Message = (worker, message) => received.Add(worker.ScriptUrl + " " + message),
            };

            var container = await OpenContainerAsync(platform, "https://sw.example/").ConfigureAwait(false);
            var registration = await container.Register("/sw.js").ConfigureAwait(false);
            var worker = registration.Active;

            container.Controller.PostMessage("hello");
            await registration.Unregister().ConfigureAwait(false);
            worker.PostMessage("dropped");

            CollectionAssert.AreEqual(new[] { "https://sw.example/sw.js hello" }, received);
        }

        [Test]
        public async Task PostMessageToClientsReachesTheControlledDocuments()
        {
            var registry = new ServiceWorkerRegistry(new ServiceWorkerPlatform());
            var configuration = Configuration.Default.WithNavigator().WithServiceWorkers(registry);
            var inside = GetNavigator(await OpenContextAsync(configuration, "https://sw.example/app/page").ConfigureAwait(false)).ServiceWorker();
            var outside = GetNavigator(await OpenContextAsync(configuration, "https://sw.example/other").ConfigureAwait(false)).ServiceWorker();
            var insideMessages = new List<MessageEvent>();
            var outsideMessages = new List<MessageEvent>();
            inside.Message += (s, e) => insideMessages.Add((MessageEvent)e);
            outside.Message += (s, e) => outsideMessages.Add((MessageEvent)e);
            var registration = await inside.Register("/app/sw.js").ConfigureAwait(false);

            var count = registry.PostMessageToClients(registration.Active, "update available");

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, insideMessages.Count);
            Assert.AreEqual("update available", insideMessages[0].Data);
            Assert.AreEqual("https://sw.example", insideMessages[0].Origin);
            Assert.IsEmpty(outsideMessages);
            Assert.Throws<ArgumentNullException>(() => registry.PostMessageToClients(null, "x"));
        }

        [Test]
        public async Task ControllerChangeIsRaisedForTheControlledDocuments()
        {
            var platform = new ServiceWorkerPlatform();
            var configuration = Configuration.Default.WithNavigator().WithServiceWorkers(platform);
            var inside = GetNavigator(await OpenContextAsync(configuration, "https://sw.example/app/page").ConfigureAwait(false)).ServiceWorker();
            var outside = GetNavigator(await OpenContextAsync(configuration, "https://sw.example/other").ConfigureAwait(false)).ServiceWorker();
            var controllers = new List<ServiceWorker>();
            var outsideChanges = 0;
            inside.ControllerChanged += (s, e) => controllers.Add(((ServiceWorkerContainer)s).Controller);
            outside.ControllerChanged += (s, e) => outsideChanges++;

            var registration = await inside.Register("/app/sw.js").ConfigureAwait(false);
            var first = registration.Active;
            platform.CheckForUpdate = _ => Task.FromResult(true);
            await registration.Update().ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { first, registration.Active }, controllers);
            Assert.AreEqual(0, outsideChanges);
        }

        [Test]
        public async Task FetchWithinScopeIsAnsweredByTheActiveWorker()
        {
            var network = new List<String>();
            var intercepted = new List<String>();
            var platform = new ServiceWorkerPlatform
            {
                Fetch = (worker, request, cancel) =>
                {
                    intercepted.Add(worker.ScriptUrl + " " + request.Address.Href);
                    return Task.FromResult(Respond(request.Address, "from worker"));
                },
            };

            var window = await OpenNetworkWindowAsync(platform, network, "https://sw.example/").ConfigureAwait(false);
            await GetNavigator(window).ServiceWorker().Register("/app/sw.js").ConfigureAwait(false);

            var inside = await window.Fetch("/app/data.json").ConfigureAwait(false);
            var outside = await window.Fetch("/data.json").ConfigureAwait(false);

            Assert.AreEqual("from worker", Read(inside));
            Assert.AreEqual("from network", Read(outside));
            CollectionAssert.AreEqual(new[] { "https://sw.example/app/sw.js https://sw.example/app/data.json" }, intercepted);
            CollectionAssert.AreEqual(new[] { "https://sw.example/data.json" }, network);
        }

        [Test]
        public async Task FetchFallsBackToTheNetworkWhenTheWorkerDoesNotRespond()
        {
            var network = new List<String>();
            var intercepted = 0;
            var platform = new ServiceWorkerPlatform
            {
                Fetch = (worker, request, cancel) =>
                {
                    intercepted++;
                    return Task.FromResult<IResponse>(null);
                },
            };

            var window = await OpenNetworkWindowAsync(platform, network, "https://sw.example/").ConfigureAwait(false);
            await GetNavigator(window).ServiceWorker().Register("/sw.js").ConfigureAwait(false);

            var response = await window.Fetch("/data.json").ConfigureAwait(false);

            Assert.AreEqual("from network", Read(response));
            Assert.AreEqual(1, intercepted);
            CollectionAssert.AreEqual(new[] { "https://sw.example/data.json" }, network);
        }

        [Test]
        public async Task FetchIsNotInterceptedWithoutActiveRegistration()
        {
            var network = new List<String>();
            var intercepted = 0;
            var platform = new ServiceWorkerPlatform
            {
                Fetch = (worker, request, cancel) =>
                {
                    intercepted++;
                    return Task.FromResult(Respond(request.Address, "from worker"));
                },
            };

            var window = await OpenNetworkWindowAsync(platform, network, "https://sw.example/").ConfigureAwait(false);

            Assert.AreEqual("from network", Read(await window.Fetch("/data.json").ConfigureAwait(false)));

            var registration = await GetNavigator(window).ServiceWorker().Register("/sw.js").ConfigureAwait(false);
            Assert.AreEqual("from worker", Read(await window.Fetch("/data.json").ConfigureAwait(false)));

            await registration.Unregister().ConfigureAwait(false);
            Assert.AreEqual("from network", Read(await window.Fetch("/data.json").ConfigureAwait(false)));
            Assert.AreEqual(1, intercepted);
            Assert.AreEqual(2, network.Count);
        }

        [Test]
        public async Task NavigationWithinScopeIsAnsweredByTheActiveWorker()
        {
            var platform = new ServiceWorkerPlatform
            {
                Fetch = (worker, request, cancel) => Task.FromResult(Respond(request.Address, "<!doctype html><title>offline</title>")),
            };

            var configuration = Configuration.Default.WithNavigator().WithVirtualRequester(req => Respond(req.Address, "<!doctype html><title>online</title>")).WithServiceWorkers(platform);
            var context = BrowsingContext.New(configuration);
            var start = await context.OpenAsync("https://sw.example/").ConfigureAwait(false);
            await GetNavigator(start.DefaultView).ServiceWorker().Register("/app/sw.js").ConfigureAwait(false);

            var inside = await context.OpenAsync("https://sw.example/app/page").ConfigureAwait(false);
            var outside = await context.OpenAsync("https://sw.example/page").ConfigureAwait(false);

            Assert.AreEqual("online", start.Title);
            Assert.AreEqual("offline", inside.Title);
            Assert.AreEqual("online", outside.Title);
        }

        [Test]
        public void ServiceWorkersSitInFrontOfTheHttpCache()
        {
            var network = new Mocks.VirtualRequester(_ => null);
            var cacheFirst = Configuration.Default.With(network).WithHttpCache().WithServiceWorkers();
            var workersFirst = Configuration.Default.With(network).WithServiceWorkers().WithHttpCache();

            foreach (var configuration in new[] { cacheFirst, workersFirst })
            {
                var requester = configuration.Services.OfType<IRequester>().Single();
                var serviceWorker = requester as ServiceWorkerRequester;
                Assert.IsNotNull(serviceWorker);
                Assert.IsInstanceOf<CachingRequester>(serviceWorker.Inner);
                Assert.AreSame(network, ((CachingRequester)serviceWorker.Inner).Inner);
            }
        }

        [Test]
        public void RegisteringServiceWorkersTwiceDoesNotNestTheRequesters()
        {
            var network = new Mocks.VirtualRequester(_ => null);
            var registry = new ServiceWorkerRegistry(new ServiceWorkerPlatform());
            var configuration = Configuration.Default.With(network).WithServiceWorkers().WithServiceWorkers(registry);

            var requester = (ServiceWorkerRequester)configuration.Services.OfType<IRequester>().Single();

            Assert.AreSame(network, requester.Inner);
            Assert.AreSame(registry, requester.Registry);
            Assert.AreSame(registry, configuration.Services.OfType<ServiceWorkerRegistry>().Single());
        }

        private static void AssertSecurityError(AsyncTestDelegate action)
        {
            var ex = Assert.ThrowsAsync<DomException>(action);
            Assert.AreEqual((Int32)DomError.Security, ex.Code);
        }

        private static async Task<ServiceWorkerContainer> OpenContainerAsync(IServiceWorkerPlatform platform, String address)
        {
            var context = await OpenContextAsync(Configuration.Default.WithNavigator().WithServiceWorkers(platform), address).ConfigureAwait(false);
            var container = GetNavigator(context).ServiceWorker();
            Assert.IsNotNull(container);
            return container;
        }

        private static async Task<IWindow> OpenNetworkWindowAsync(IServiceWorkerPlatform platform, List<String> network, String address)
        {
            var configuration = Configuration.Default
                .WithNavigator()
                .WithVirtualRequester(req =>
                {
                    network.Add(req.Address.Href);
                    return Respond(req.Address, "from network");
                })
                .WithServiceWorkers(platform);
            var context = await OpenContextAsync(configuration, address).ConfigureAwait(false);
            return context.Active.DefaultView;
        }

        private static async Task<IBrowsingContext> OpenContextAsync(IConfiguration configuration, String address)
        {
            var context = BrowsingContext.New(configuration);
            await context.OpenAsync(res => res.Address(address).Content("<!doctype html><title>service-worker</title>")).ConfigureAwait(false);
            return context;
        }

        private static INavigator GetNavigator(IBrowsingContext context) => GetNavigator(context?.Active?.DefaultView);

        private static INavigator GetNavigator(IWindow window)
        {
            var navigator = window?.Navigator;
            Assert.IsNotNull(navigator);
            return navigator;
        }

        private static IResponse Respond(Url address, String content) =>
            VirtualResponse.Create(res => res.Address(address).Content(content));

        private static String Read(IResponse response)
        {
            using (var reader = new StreamReader(response.Content))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
