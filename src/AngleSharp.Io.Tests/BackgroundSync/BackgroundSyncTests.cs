namespace AngleSharp.Io.Tests.BackgroundSync
{
    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Io.BackgroundSyncApi;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.PermissionsApi;
    using AngleSharp.Io.ServiceWorkerApi;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    [TestFixture]
    public class BackgroundSyncTests
    {
        private const String Address = "https://sync.example/app/";
        private const String Origin = "https://sync.example";

        [Test]
        public async Task ManagersAreUnavailableWithoutPlatform()
        {
            var registration = await RegisterAsync(Configuration.Default.WithNavigator().WithServiceWorkers()).ConfigureAwait(false);

            Assert.IsNull(registration.Sync);
            Assert.IsNull(registration.PeriodicSync);
        }

        [Test]
        public async Task ManagersAreTheSameObjects()
        {
            var registration = await RegisterAsync(new RecordingPlatform()).ConfigureAwait(false);

            Assert.IsNotNull(registration.Sync);
            Assert.IsNotNull(registration.PeriodicSync);
            Assert.AreSame(registration.Sync, registration.Sync);
            Assert.AreSame(registration.PeriodicSync, registration.PeriodicSync);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task TheOrderOfTheConfigurationDoesNotMatter(Boolean servicesFirst)
        {
            var platform = new RecordingPlatform();
            var policy = new PermissionPolicy(PermissionState.Denied);
            var config = servicesFirst
                ? Configuration.Default.WithPermissions(policy).WithBackgroundSync(platform).WithNavigator().WithServiceWorkers()
                : Configuration.Default.WithNavigator().WithServiceWorkers().WithBackgroundSync(platform).WithPermissions(policy);
            var registration = await RegisterAsync(config).ConfigureAwait(false);

            AssertNotAllowed(() => registration.Sync.Register("tag"));
        }

        [Test]
        public async Task PlatformsCanBeRegisteredViaIoOptions()
        {
            var options = new IoOptions
            {
                ServiceWorkerPlatform = new ServiceWorkerPlatform(),
                SyncPlatform = new RecordingPlatform(),
            };
            var registration = await RegisterAsync(Configuration.Default.WithIo(options)).ConfigureAwait(false);

            Assert.IsNotNull(registration.Sync);
        }

        [Test]
        public async Task RegisterSchedulesATaskVisibleInTheTags()
        {
            var platform = new RecordingPlatform();
            var registration = await RegisterAsync(platform).ConfigureAwait(false);

            await registration.Sync.Register("one").ConfigureAwait(false);
            await registration.Sync.Register("two").ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "one", "two" }, await registration.Sync.GetTags().ConfigureAwait(false));
            Assert.AreEqual(2, platform.Scheduled.Count);
            Assert.AreEqual("one", platform.Scheduled[0].Tag);
            Assert.AreEqual(SyncTaskKind.OneOff, platform.Scheduled[0].Kind);
            Assert.AreSame(registration, platform.Scheduled[0].Registration);
            Assert.AreEqual(0UL, platform.Scheduled[0].MinInterval);
        }

        [Test]
        public async Task RegisteringAPendingTagAgainDoesNothing()
        {
            var platform = new RecordingPlatform();
            var registration = await RegisterAsync(platform).ConfigureAwait(false);

            await registration.Sync.Register("tag").ConfigureAwait(false);
            await registration.Sync.Register("tag").ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "tag" }, await registration.Sync.GetTags().ConfigureAwait(false));
            Assert.AreEqual(1, platform.Scheduled.Count);
        }

        [Test]
        public async Task FiringDispatchesToTheActiveWorkerAndRemovesTheTag()
        {
            var platform = new RecordingPlatform();
            var registration = await RegisterAsync(platform).ConfigureAwait(false);
            await registration.Sync.Register("tag").ConfigureAwait(false);
            var worker = registration.Active;

            var result = await platform.Scheduled[0].FireAsync().ConfigureAwait(false);

            Assert.IsTrue(result);
            Assert.AreEqual(1, platform.Dispatched.Count);
            Assert.AreSame(worker, platform.Dispatched[0].Worker);
            Assert.AreEqual("tag", platform.Dispatched[0].Task.Tag);
            CollectionAssert.IsEmpty(await registration.Sync.GetTags().ConfigureAwait(false));
            Assert.AreEqual(1, platform.Scheduled.Count);
        }

        [Test]
        public async Task ADoneTaskCannotBeFiredAgainButItsTagCanBeRegisteredAgain()
        {
            var platform = new RecordingPlatform();
            var registration = await RegisterAsync(platform).ConfigureAwait(false);
            await registration.Sync.Register("tag").ConfigureAwait(false);
            var task = platform.Scheduled[0];
            await task.FireAsync().ConfigureAwait(false);

            Assert.IsFalse(await task.FireAsync().ConfigureAwait(false));
            Assert.AreEqual(1, platform.Dispatched.Count);

            await registration.Sync.Register("tag").ConfigureAwait(false);

            Assert.AreEqual(2, platform.Scheduled.Count);
            Assert.AreNotSame(task, platform.Scheduled[1]);
        }

        [Test]
        public async Task AFailedAttemptIsRetriedUntilTheLastChance()
        {
            var lastChances = new List<Boolean>();
            var platform = new RecordingPlatform
            {
                OnDispatch = task =>
                {
                    lastChances.Add(task.LastChance);
                    return Task.FromException(new InvalidOperationException());
                },
            };
            var registration = await RegisterAsync(platform).ConfigureAwait(false);
            await registration.Sync.Register("tag").ConfigureAwait(false);
            var task = platform.Scheduled[0];

            Assert.IsFalse(await task.FireAsync().ConfigureAwait(false));
            CollectionAssert.AreEqual(new[] { "tag" }, await registration.Sync.GetTags().ConfigureAwait(false));
            Assert.AreEqual(2, platform.Scheduled.Count);

            Assert.IsFalse(await task.FireAsync().ConfigureAwait(false));
            Assert.AreEqual(3, platform.Scheduled.Count);

            Assert.IsFalse(await task.FireAsync().ConfigureAwait(false));
            Assert.AreEqual(3, platform.Scheduled.Count);
            CollectionAssert.IsEmpty(await registration.Sync.GetTags().ConfigureAwait(false));
            CollectionAssert.AreEqual(new[] { false, false, true }, lastChances);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task RegisteringWhileFiringFiresOnceMore(Boolean failing)
        {
            var running = new TaskCompletionSource<Boolean>();
            var finish = new TaskCompletionSource<Boolean>();
            var platform = new RecordingPlatform
            {
                OnDispatch = async _ =>
                {
                    running.TrySetResult(true);

                    if (!await finish.Task.ConfigureAwait(false))
                    {
                        throw new InvalidOperationException();
                    }
                },
            };
            var registration = await RegisterAsync(platform).ConfigureAwait(false);
            await registration.Sync.Register("tag").ConfigureAwait(false);
            var task = platform.Scheduled[0];

            var firing = task.FireAsync();
            await running.Task.ConfigureAwait(false);
            await registration.Sync.Register("tag").ConfigureAwait(false);

            Assert.AreEqual(1, platform.Scheduled.Count);
            Assert.IsFalse(await task.FireAsync().ConfigureAwait(false));

            finish.SetResult(!failing);
            await firing.ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "tag" }, await registration.Sync.GetTags().ConfigureAwait(false));
            Assert.AreEqual(2, platform.Scheduled.Count);
            Assert.IsFalse(task.LastChance);
        }

        [Test]
        public async Task UnregisterRemovesAOneOffTaskAndCancelsIt()
        {
            var platform = new RecordingPlatform();
            var registration = await RegisterAsync(platform).ConfigureAwait(false);
            await registration.Sync.Register("tag").ConfigureAwait(false);
            var task = platform.Scheduled[0];

            await registration.Sync.Unregister("tag").ConfigureAwait(false);
            await registration.Sync.Unregister("unknown").ConfigureAwait(false);

            CollectionAssert.IsEmpty(await registration.Sync.GetTags().ConfigureAwait(false));
            CollectionAssert.AreEqual(new[] { task }, platform.Canceled);
            Assert.IsFalse(await task.FireAsync().ConfigureAwait(false));
            Assert.AreEqual(0, platform.Dispatched.Count);
        }

        [Test]
        public async Task PeriodicTasksStayRegisteredAndGetScheduledAgain()
        {
            var platform = new RecordingPlatform();
            var registration = await RegisterAsync(platform).ConfigureAwait(false);

            await registration.PeriodicSync.Register("tag", new BackgroundSyncOptions { MinInterval = 1000 }).ConfigureAwait(false);
            var task = platform.Scheduled[0];

            Assert.AreEqual(SyncTaskKind.Periodic, task.Kind);
            Assert.AreEqual(1000UL, task.MinInterval);
            Assert.IsTrue(await task.FireAsync().ConfigureAwait(false));
            CollectionAssert.AreEqual(new[] { "tag" }, await registration.PeriodicSync.GetTags().ConfigureAwait(false));
            Assert.AreEqual(2, platform.Scheduled.Count);
            Assert.AreEqual(1, platform.Dispatched.Count);
        }

        [TestCase(-1.0)]
        [TestCase(Double.NaN)]
        public async Task InvalidMinimumIntervalsAreTypeErrors(Double minInterval)
        {
            var platform = new RecordingPlatform();
            var registration = await RegisterAsync(platform).ConfigureAwait(false);

            Assert.ThrowsAsync<ArgumentException>(() => registration.PeriodicSync.Register("tag", new BackgroundSyncOptions { MinInterval = minInterval }));
            Assert.AreEqual(0, platform.Scheduled.Count);
        }

        [Test]
        public async Task AFailingPeriodicTaskStaysRegisteredWithoutEverBeingALastChance()
        {
            var lastChances = new List<Boolean>();
            var platform = new RecordingPlatform
            {
                OnDispatch = task =>
                {
                    lastChances.Add(task.LastChance);
                    return Task.FromException(new InvalidOperationException());
                },
            };
            var registration = await RegisterAsync(platform).ConfigureAwait(false);
            await registration.PeriodicSync.Register("tag").ConfigureAwait(false);
            var task = platform.Scheduled[0];

            for (var i = 0; i < 5; i++)
            {
                Assert.IsFalse(await task.FireAsync().ConfigureAwait(false));
            }

            CollectionAssert.AreEqual(new[] { "tag" }, await registration.PeriodicSync.GetTags().ConfigureAwait(false));
            CollectionAssert.DoesNotContain(lastChances, true);
        }

        [Test]
        public async Task RegisteringAPeriodicTagAgainOnlyReschedulesForADifferentInterval()
        {
            var platform = new RecordingPlatform();
            var registration = await RegisterAsync(platform).ConfigureAwait(false);
            var periodic = registration.PeriodicSync;

            await periodic.Register("tag", new BackgroundSyncOptions { MinInterval = 1000 }).ConfigureAwait(false);
            await periodic.Register("tag", new BackgroundSyncOptions { MinInterval = 1000 }).ConfigureAwait(false);

            Assert.AreEqual(1, platform.Scheduled.Count);
            Assert.AreEqual(0, platform.Canceled.Count);

            await periodic.Register("tag", new BackgroundSyncOptions { MinInterval = 2000 }).ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "tag" }, await periodic.GetTags().ConfigureAwait(false));
            Assert.AreEqual(2, platform.Scheduled.Count);
            Assert.AreEqual(1, platform.Canceled.Count);
            Assert.AreSame(platform.Scheduled[0], platform.Scheduled[1]);
            Assert.AreEqual(2000UL, platform.Scheduled[1].MinInterval);
        }

        [Test]
        public async Task ChangingAnIntervalWhileFiringAppliesToTheNextSchedule()
        {
            var finish = new TaskCompletionSource<Boolean>();
            var platform = new RecordingPlatform { OnDispatch = _ => finish.Task };
            var registration = await RegisterAsync(platform).ConfigureAwait(false);
            var periodic = registration.PeriodicSync;
            await periodic.Register("tag", new BackgroundSyncOptions { MinInterval = 1000 }).ConfigureAwait(false);
            var firing = platform.Scheduled[0].FireAsync();

            await periodic.Register("tag", new BackgroundSyncOptions { MinInterval = 2000 }).ConfigureAwait(false);

            Assert.AreEqual(0, platform.Canceled.Count);
            Assert.AreEqual(1, platform.Scheduled.Count);

            finish.SetResult(true);
            await firing.ConfigureAwait(false);

            Assert.AreEqual(2, platform.Scheduled.Count);
            Assert.AreEqual(2000UL, platform.Scheduled[1].MinInterval);
        }

        [Test]
        public async Task UnregisterRemovesAPeriodicTask()
        {
            var platform = new RecordingPlatform();
            var registration = await RegisterAsync(platform).ConfigureAwait(false);
            await registration.PeriodicSync.Register("tag").ConfigureAwait(false);
            var task = platform.Scheduled[0];

            await registration.PeriodicSync.Unregister("tag").ConfigureAwait(false);

            CollectionAssert.IsEmpty(await registration.PeriodicSync.GetTags().ConfigureAwait(false));
            CollectionAssert.AreEqual(new[] { task }, platform.Canceled);
            Assert.IsFalse(await task.FireAsync().ConfigureAwait(false));
        }

        [Test]
        public async Task UnregisteringWhileFiringDoesNotScheduleAgain()
        {
            var finish = new TaskCompletionSource<Boolean>();
            var platform = new RecordingPlatform { OnDispatch = _ => finish.Task };
            var registration = await RegisterAsync(platform).ConfigureAwait(false);
            await registration.PeriodicSync.Register("tag").ConfigureAwait(false);
            var firing = platform.Scheduled[0].FireAsync();

            await registration.PeriodicSync.Unregister("tag").ConfigureAwait(false);
            finish.SetResult(true);
            await firing.ConfigureAwait(false);

            Assert.AreEqual(1, platform.Scheduled.Count);
            CollectionAssert.IsEmpty(await registration.PeriodicSync.GetTags().ConfigureAwait(false));
        }

        [Test]
        public async Task OneOffAndPeriodicTagsAreSeparate()
        {
            var platform = new RecordingPlatform();
            var registration = await RegisterAsync(platform).ConfigureAwait(false);

            await registration.Sync.Register("tag").ConfigureAwait(false);
            await registration.PeriodicSync.Register("tag").ConfigureAwait(false);
            await registration.PeriodicSync.Unregister("tag").ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "tag" }, await registration.Sync.GetTags().ConfigureAwait(false));
            CollectionAssert.IsEmpty(await registration.PeriodicSync.GetTags().ConfigureAwait(false));
        }

        [Test]
        public async Task TagsBelongToTheirRegistration()
        {
            var platform = new RecordingPlatform();
            var context = await OpenContextAsync(ConfigureWorkers().WithBackgroundSync(platform)).ConfigureAwait(false);
            var container = context.Active.DefaultView.Navigator.ServiceWorker();
            var first = await container.Register("/app/sw.js").ConfigureAwait(false);
            var second = await container.Register("/app/sub/sw.js").ConfigureAwait(false);

            await first.Sync.Register("tag").ConfigureAwait(false);

            CollectionAssert.IsEmpty(await second.Sync.GetTags().ConfigureAwait(false));
        }

        [Test]
        public async Task ARejectedScheduleLeavesNoTaskBehind()
        {
            var platform = new RecordingPlatform { OnSchedule = _ => Task.FromException(new InvalidOperationException()) };
            var registration = await RegisterAsync(platform).ConfigureAwait(false);

            Assert.ThrowsAsync<InvalidOperationException>(() => registration.Sync.Register("tag"));

            CollectionAssert.IsEmpty(await registration.Sync.GetTags().ConfigureAwait(false));
        }

        [Test]
        public async Task PermissionsGateTheRegistrations()
        {
            var policy = new PermissionPolicy(PermissionState.Denied);
            var platform = new RecordingPlatform();
            var registration = await RegisterAsync(ConfigureWorkers().WithPermissions(policy).WithBackgroundSync(platform)).ConfigureAwait(false);

            AssertNotAllowed(() => registration.Sync.Register("tag"));
            AssertNotAllowed(() => registration.PeriodicSync.Register("tag"));
            Assert.AreEqual(0, platform.Scheduled.Count);
            CollectionAssert.IsEmpty(await registration.Sync.GetTags().ConfigureAwait(false));

            policy.Set(PermissionNames.BackgroundSync, PermissionState.Granted);

            await registration.Sync.Register("tag").ConfigureAwait(false);
            AssertNotAllowed(() => registration.PeriodicSync.Register("tag"));

            policy.Set(Origin, PermissionNames.PeriodicBackgroundSync, PermissionState.Granted);

            await registration.PeriodicSync.Register("tag").ConfigureAwait(false);
            Assert.AreEqual(2, platform.Scheduled.Count);
        }

        [Test]
        public async Task ThePermissionIsTheOneOfTheRegistrationsOrigin()
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
            var context = await OpenContextAsync(ConfigureWorkers().WithPermissions(policy).WithBackgroundSync(new RecordingPlatform())).ConfigureAwait(false);
            var registration = await context.Active.DefaultView.Navigator.ServiceWorker().Register("/app/sw.js").ConfigureAwait(false);
            await context.OpenAsync(res => res.Address("https://other.example/").Content("<!doctype html>")).ConfigureAwait(false);

            await registration.Sync.Register("tag").ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { Origin + "|background-sync" }, prompts);
        }

        [Test]
        public async Task ARegistrationWithoutActiveWorkerIsRejected()
        {
            var platform = new RecordingPlatform();
            var registration = await RegisterAsync(platform).ConfigureAwait(false);
            var sync = registration.Sync;
            var periodic = registration.PeriodicSync;

            await registration.Unregister().ConfigureAwait(false);

            AssertInvalidState(() => sync.Register("tag"));
            AssertInvalidState(() => periodic.Register("tag"));
            Assert.AreEqual(0, platform.Scheduled.Count);
        }

        [Test]
        public async Task AnInsecureOriginIsRejected()
        {
            // the container would never hand out such a registration, the registry does
            var platform = new RecordingPlatform();
            var registry = new ServiceWorkerRegistry(new ServiceWorkerPlatform()) { SyncPlatform = platform };
            var registration = await registry.RegisterAsync(new Url("http://sync.example/sw.js"), new Url("http://sync.example/")).ConfigureAwait(false);

            var ex = Assert.ThrowsAsync<DomException>(() => registration.Sync.Register("tag"));

            Assert.AreEqual((Int32)DomError.Security, ex.Code);
            Assert.AreEqual(0, platform.Scheduled.Count);
        }

        [Test]
        public async Task UnregisteringTheServiceWorkerCancelsItsTasks()
        {
            var platform = new RecordingPlatform { OnCancel = _ => Task.FromException(new InvalidOperationException()) };
            var registration = await RegisterAsync(platform).ConfigureAwait(false);
            await registration.Sync.Register("one").ConfigureAwait(false);
            await registration.PeriodicSync.Register("two").ConfigureAwait(false);
            var sync = registration.Sync;
            var periodic = registration.PeriodicSync;
            var one = platform.Scheduled[0];

            Assert.IsTrue(await registration.Unregister().ConfigureAwait(false));

            Assert.AreEqual(2, platform.Canceled.Count);
            CollectionAssert.IsEmpty(await sync.GetTags().ConfigureAwait(false));
            CollectionAssert.IsEmpty(await periodic.GetTags().ConfigureAwait(false));
            Assert.IsFalse(await one.FireAsync().ConfigureAwait(false));
        }

        [Test]
        public async Task TheCallbackPlatformWorksWithoutCallbacks()
        {
            var task = default(SyncTask);
            var platform = new SyncPlatform { Schedule = t => { task = t; return Task.CompletedTask; } };
            var registration = await RegisterAsync(platform).ConfigureAwait(false);
            await registration.Sync.Register("tag").ConfigureAwait(false);

            Assert.IsTrue(await task.FireAsync().ConfigureAwait(false));

            var defaults = await RegisterAsync(ConfigureWorkers().WithBackgroundSync()).ConfigureAwait(false);
            await defaults.Sync.Register("tag").ConfigureAwait(false);
            await defaults.Sync.Unregister("tag").ConfigureAwait(false);
        }

        private static IConfiguration ConfigureWorkers() => Configuration.Default.WithNavigator().WithServiceWorkers();

        private static Task<ServiceWorkerRegistration> RegisterAsync(ISyncPlatform platform) =>
            RegisterAsync(ConfigureWorkers().WithBackgroundSync(platform));

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

        private static void AssertNotAllowed(AsyncTestDelegate action)
        {
            var ex = Assert.ThrowsAsync<DomException>(action);
            Assert.AreEqual("NotAllowedError", ex.Name);
        }

        private static void AssertInvalidState(AsyncTestDelegate action)
        {
            var ex = Assert.ThrowsAsync<DomException>(action);
            Assert.AreEqual((Int32)DomError.InvalidState, ex.Code);
        }

        private sealed class RecordingPlatform : ISyncPlatform
        {
            public List<SyncTask> Scheduled { get; } = new List<SyncTask>();

            public List<SyncTask> Canceled { get; } = new List<SyncTask>();

            public List<(ServiceWorker Worker, SyncTask Task)> Dispatched { get; } = new List<(ServiceWorker, SyncTask)>();

            public Func<SyncTask, Task> OnSchedule { get; set; }

            public Func<SyncTask, Task> OnCancel { get; set; }

            public Func<SyncTask, Task> OnDispatch { get; set; }

            public Task ScheduleAsync(SyncTask task)
            {
                Scheduled.Add(task);
                return OnSchedule?.Invoke(task) ?? Task.CompletedTask;
            }

            public Task CancelAsync(SyncTask task)
            {
                Canceled.Add(task);
                return OnCancel?.Invoke(task) ?? Task.CompletedTask;
            }

            public Task DispatchAsync(ServiceWorker worker, SyncTask task)
            {
                Dispatched.Add((worker, task));
                return OnDispatch?.Invoke(task) ?? Task.CompletedTask;
            }
        }
    }
}
