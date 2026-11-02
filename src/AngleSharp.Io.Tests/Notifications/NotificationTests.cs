namespace AngleSharp.Io.Tests.Notifications
{
    using AngleSharp;
    using AngleSharp.Browser;
    using AngleSharp.Common;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.NotificationsApi;
    using AngleSharp.Io.PermissionsApi;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class NotificationTests
    {
        private const String Address = "https://notify.example/app/page";
        private const String Origin = "https://notify.example";

        [Test]
        public async Task ConstructionWithoutPlatformIsNotSupported()
        {
            var window = await OpenWindowAsync(Configuration.Default).ConfigureAwait(false);

            var ex = Assert.Throws<DomException>(() => new Notification(window, "Hi"));

            Assert.AreEqual((Int32)DomError.NotSupported, ex.Code);
        }

        [Test]
        public void ConstructionRequiresAWindow()
        {
            Assert.Throws<ArgumentNullException>(() => new Notification(null, "Hi"));
        }

        [Test]
        public async Task WithoutPolicyNotificationsAreNotGated()
        {
            var shown = new List<Notification>();
            var platform = new NotificationPlatform { Show = n => { shown.Add(n); return Task.CompletedTask; } };
            var window = await OpenWindowAsync(Configuration.Default.WithNotifications(platform)).ConfigureAwait(false);

            var permission = await Notification.RequestPermission(window).ConfigureAwait(false);
            var notification = new Notification(window, "Hi");

            Assert.AreEqual(NotificationPermission.Granted, permission);
            Assert.AreEqual(NotificationPermission.Granted, Notification.GetPermission(window));
            Assert.IsTrue(await notification.Delivery.ConfigureAwait(false));
            CollectionAssert.AreEqual(new[] { notification }, shown);
        }

        [Test]
        public async Task PermissionStartsAsDefaultAndFollowsThePolicy()
        {
            var policy = new PermissionPolicy();
            var window = await OpenWindowAsync(Configure(policy, new NotificationPlatform())).ConfigureAwait(false);

            Assert.AreEqual(NotificationPermission.Default, Notification.GetPermission(window));

            policy.Set(Origin, PermissionNames.Notifications, PermissionState.Granted);
            Assert.AreEqual(NotificationPermission.Granted, Notification.GetPermission(window));

            policy.Set(Origin, PermissionNames.Notifications, PermissionState.Denied);
            Assert.AreEqual(NotificationPermission.Denied, Notification.GetPermission(window));
        }

        [Test]
        public async Task RequestPermissionResolvesThroughThePolicyAndIsRemembered()
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
            var window = await OpenWindowAsync(Configure(policy, new NotificationPlatform())).ConfigureAwait(false);

            var first = await Notification.RequestPermission(window).ConfigureAwait(false);
            var second = await Notification.RequestPermission(window).ConfigureAwait(false);

            Assert.AreEqual(NotificationPermission.Granted, first);
            Assert.AreEqual(NotificationPermission.Granted, second);
            Assert.AreEqual(NotificationPermission.Granted, Notification.GetPermission(window));
            CollectionAssert.AreEqual(new[] { Origin + "|notifications" }, prompts);
        }

        [Test]
        public async Task RequestPermissionCanBeDenied()
        {
            var policy = new PermissionPolicy { PromptHandler = (origin, name) => Task.FromResult(PermissionState.Denied) };
            var window = await OpenWindowAsync(Configure(policy, new NotificationPlatform())).ConfigureAwait(false);

            var permission = await Notification.RequestPermission(window).ConfigureAwait(false);

            Assert.AreEqual(NotificationPermission.Denied, permission);
            Assert.AreEqual(NotificationPermission.Denied, Notification.GetPermission(window));
        }

        [Test]
        public async Task ADismissedPromptLeavesThePermissionAtDefault()
        {
            var window = await OpenWindowAsync(Configure(new PermissionPolicy(), new NotificationPlatform())).ConfigureAwait(false);

            var permission = await Notification.RequestPermission(window).ConfigureAwait(false);

            Assert.AreEqual(NotificationPermission.Default, permission);
            Assert.AreEqual(NotificationPermission.Default, Notification.GetPermission(window));
        }

        [Test]
        public async Task ThePermissionsApiSeesTheOutcomeOfTheRequest()
        {
            var policy = new PermissionPolicy { PromptHandler = (origin, name) => Task.FromResult(PermissionState.Granted) };
            var window = await OpenWindowAsync(Configure(policy, new NotificationPlatform())).ConfigureAwait(false);
            var status = await window.Navigator.Permissions().Query(new PermissionDescriptor { Name = PermissionNames.Notifications }).ConfigureAwait(false);

            Assert.AreEqual(PermissionState.Prompt, status.State);

            await Notification.RequestPermission(window).ConfigureAwait(false);

            Assert.AreEqual(PermissionState.Granted, status.State);
        }

        [Test]
        public async Task GatingDoesNotDependOnTheNavigatorBeingRegistered()
        {
            var shown = 0;
            var platform = new NotificationPlatform { Show = _ => { shown++; return Task.CompletedTask; } };
            var policy = new PermissionPolicy(PermissionState.Denied);
            var config = Configuration.Default.WithPermissions(policy).WithNotifications(platform);
            var window = await OpenWindowAsync(config).ConfigureAwait(false);

            Assert.AreEqual(NotificationPermission.Denied, Notification.GetPermission(window));
            Assert.IsFalse(await new Notification(window, "Hi").Delivery.ConfigureAwait(false));
            Assert.AreEqual(0, shown);
        }

        [Test]
        public async Task CreationIsBlockedWhenThePermissionIsDenied()
        {
            var loop = new ManualEventLoop();
            var shown = 0;
            var platform = new NotificationPlatform { Show = _ => { shown++; return Task.CompletedTask; } };
            var config = Configure(new PermissionPolicy(PermissionState.Denied), platform).WithOnly<IEventLoop>(_ => loop);
            var window = await OpenWindowAsync(config).ConfigureAwait(false);
            loop.RunAll();

            var notification = new Notification(window, "Hi");
            var events = Record(notification);
            loop.RunAll();

            Assert.AreEqual(0, shown);
            Assert.IsFalse(await notification.Delivery.ConfigureAwait(false));
            CollectionAssert.AreEqual(new[] { "error" }, events);
        }

        [Test]
        public async Task CreationDoesNotPromptAndIsBlockedForTheDefaultPermission()
        {
            var loop = new ManualEventLoop();
            var prompts = 0;
            var shown = 0;
            var platform = new NotificationPlatform { Show = _ => { shown++; return Task.CompletedTask; } };
            var policy = new PermissionPolicy
            {
                PromptHandler = (origin, name) =>
                {
                    prompts++;
                    return Task.FromResult(PermissionState.Granted);
                },
            };
            var window = await OpenWindowAsync(Configure(policy, platform).WithOnly<IEventLoop>(_ => loop)).ConfigureAwait(false);
            loop.RunAll();

            var notification = new Notification(window, "Hi");
            var events = Record(notification);
            loop.RunAll();

            Assert.AreEqual(0, prompts);
            Assert.AreEqual(0, shown);
            CollectionAssert.AreEqual(new[] { "error" }, events);
        }

        [Test]
        public async Task ListenersAttachedAfterConstructionSeeTheFirstEvents()
        {
            var loop = new ManualEventLoop();
            var config = Configure(new PermissionPolicy(PermissionState.Granted), new NotificationPlatform()).WithOnly<IEventLoop>(_ => loop);
            var window = await OpenWindowAsync(config).ConfigureAwait(false);
            loop.RunAll();

            var notification = new Notification(window, "Hi");

            Assert.IsFalse(notification.Delivery.IsCompleted);

            var events = Record(notification);
            loop.RunAll();

            CollectionAssert.AreEqual(new[] { "show" }, events);
        }

        [Test]
        public async Task TheDeliveryCallbackReceivesTheNotificationWithItsOptions()
        {
            var shown = default(Notification);
            var platform = new NotificationPlatform { Show = n => { shown = n; return Task.CompletedTask; } };
            var window = await OpenWindowAsync(Configure(new PermissionPolicy(PermissionState.Granted), platform)).ConfigureAwait(false);
            var data = new Object();

            var notification = new Notification(window, "Title", new NotificationOptions
            {
                Direction = NotificationDirection.Rtl,
                Language = "de",
                Body = "Body",
                Tag = "tag",
                Icon = "icon.png",
                Image = "/image.png",
                Badge = "https://cdn.example/badge.png",
                Timestamp = 1234,
                Renotify = true,
                Silent = false,
                RequireInteraction = true,
                Data = data,
            });

            Assert.IsTrue(await notification.Delivery.ConfigureAwait(false));
            Assert.AreSame(notification, shown);
            Assert.AreEqual("Title", shown.Title);
            Assert.AreEqual(NotificationDirection.Rtl, shown.Direction);
            Assert.AreEqual("de", shown.Language);
            Assert.AreEqual("Body", shown.Body);
            Assert.AreEqual("tag", shown.Tag);
            Assert.AreEqual("https://notify.example/app/icon.png", shown.Icon);
            Assert.AreEqual("https://notify.example/image.png", shown.Image);
            Assert.AreEqual("https://cdn.example/badge.png", shown.Badge);
            Assert.AreEqual(1234.0, shown.Timestamp);
            Assert.IsTrue(shown.Renotify);
            Assert.AreEqual(false, shown.Silent);
            Assert.IsTrue(shown.RequireInteraction);
            Assert.AreSame(data, shown.Data);
            Assert.AreEqual(Origin, shown.Origin);
        }

        [Test]
        public async Task OptionsHaveDefaults()
        {
            var window = await OpenWindowAsync(Configuration.Default.WithNotifications()).ConfigureAwait(false);
            var before = (Double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var notification = new Notification(window, null);

            Assert.AreEqual(String.Empty, notification.Title);
            Assert.AreEqual(NotificationDirection.Auto, notification.Direction);
            Assert.AreEqual(String.Empty, notification.Tag);
            Assert.AreEqual(String.Empty, notification.Icon);
            Assert.IsNull(notification.Silent);
            Assert.IsNull(notification.Data);
            Assert.GreaterOrEqual(notification.Timestamp, before);
        }

        [Test]
        public async Task RenotifyWithoutTagIsATypeError()
        {
            var window = await OpenWindowAsync(Configuration.Default.WithNotifications()).ConfigureAwait(false);

            Assert.Throws<ArgumentException>(() => new Notification(window, "Hi", new NotificationOptions { Renotify = true }));
        }

        [Test]
        public async Task AFailingPlatformDispatchesTheErrorEvent()
        {
            var loop = new ManualEventLoop();
            var platform = new NotificationPlatform { Show = _ => Task.FromException(new InvalidOperationException()) };
            var window = await OpenWindowAsync(Configure(new PermissionPolicy(PermissionState.Granted), platform).WithOnly<IEventLoop>(_ => loop)).ConfigureAwait(false);
            loop.RunAll();

            var notification = new Notification(window, "Hi");
            var events = Record(notification);
            loop.RunAll();

            Assert.IsFalse(await notification.Delivery.ConfigureAwait(false));
            CollectionAssert.AreEqual(new[] { "error" }, events);
        }

        [Test]
        public async Task CloseRemovesTheNotificationOnceAndDispatchesTheCloseEvent()
        {
            var loop = new ManualEventLoop();
            var closed = new List<Notification>();
            var platform = new NotificationPlatform { Close = n => { closed.Add(n); return Task.CompletedTask; } };
            var window = await OpenWindowAsync(Configure(new PermissionPolicy(PermissionState.Granted), platform).WithOnly<IEventLoop>(_ => loop)).ConfigureAwait(false);
            loop.RunAll();
            var notification = new Notification(window, "Hi");
            var events = Record(notification);
            loop.RunAll();

            notification.Close();
            notification.Close();
            loop.RunAll();

            CollectionAssert.AreEqual(new[] { notification }, closed);
            CollectionAssert.AreEqual(new[] { "show", "close" }, events);
        }

        [Test]
        public async Task ClosingBeforeTheNotificationIsPassedOnPreventsTheDelivery()
        {
            var loop = new ManualEventLoop();
            var calls = 0;
            var platform = new NotificationPlatform
            {
                Show = _ => { calls++; return Task.CompletedTask; },
                Close = _ => { calls++; return Task.CompletedTask; },
            };
            var window = await OpenWindowAsync(Configure(new PermissionPolicy(PermissionState.Granted), platform).WithOnly<IEventLoop>(_ => loop)).ConfigureAwait(false);
            loop.RunAll();
            var notification = new Notification(window, "Hi");
            var events = Record(notification);

            notification.Close();
            loop.RunAll();

            Assert.AreEqual(0, calls);
            Assert.IsFalse(await notification.Delivery.ConfigureAwait(false));
            CollectionAssert.IsEmpty(events);
        }

        [Test]
        public async Task ClosingWhileBeingShownRemovesItAfterwards()
        {
            var loop = new ManualEventLoop();
            var showing = new TaskCompletionSource<Boolean>();
            var closed = 0;
            var platform = new NotificationPlatform
            {
                Show = _ => showing.Task,
                Close = _ => { closed++; return Task.CompletedTask; },
            };
            var window = await OpenWindowAsync(Configure(new PermissionPolicy(PermissionState.Granted), platform).WithOnly<IEventLoop>(_ => loop)).ConfigureAwait(false);
            loop.RunAll();
            var notification = new Notification(window, "Hi");
            var events = Record(notification);
            loop.RunAll();

            notification.Close();
            Assert.AreEqual(0, closed);

            showing.SetResult(true);
            await notification.Delivery.ConfigureAwait(false);
            await WaitUntilAsync(() => closed == 1).ConfigureAwait(false);
            loop.RunAll();

            CollectionAssert.AreEqual(new[] { "show", "close" }, events);
        }

        [Test]
        public async Task AFailingRemovalStillClosesTheNotification()
        {
            var loop = new ManualEventLoop();
            var platform = new NotificationPlatform { Close = _ => Task.FromException(new InvalidOperationException()) };
            var window = await OpenWindowAsync(Configure(new PermissionPolicy(PermissionState.Granted), platform).WithOnly<IEventLoop>(_ => loop)).ConfigureAwait(false);
            loop.RunAll();
            var notification = new Notification(window, "Hi");
            var events = Record(notification);
            loop.RunAll();

            notification.Close();
            loop.RunAll();

            CollectionAssert.AreEqual(new[] { "show", "close" }, events);
        }

        [Test]
        public async Task ThePlatformReportsClicksAndDismissals()
        {
            var loop = new ManualEventLoop();
            var closed = 0;
            var platform = new NotificationPlatform { Close = _ => { closed++; return Task.CompletedTask; } };
            var window = await OpenWindowAsync(Configure(new PermissionPolicy(PermissionState.Granted), platform).WithOnly<IEventLoop>(_ => loop)).ConfigureAwait(false);
            loop.RunAll();
            var notification = new Notification(window, "Hi");
            var events = Record(notification);

            notification.NotifyClicked();
            loop.RunAll();
            notification.NotifyClicked();
            notification.NotifyClosed();
            notification.NotifyClicked();
            notification.NotifyClosed();
            notification.Close();
            loop.RunAll();

            Assert.AreEqual(0, closed);
            CollectionAssert.AreEqual(new[] { "show", "click", "close" }, events);
        }

        [Test]
        public async Task UnloadingTheWindowClosesTheNotification()
        {
            var closed = 0;
            var platform = new NotificationPlatform { Close = _ => { closed++; return Task.CompletedTask; } };
            var window = await OpenWindowAsync(Configuration.Default.WithNotifications(platform)).ConfigureAwait(false);
            var notification = new Notification(window, "Hi");
            await notification.Delivery.ConfigureAwait(false);

            window.Dispatch(new Event(EventNames.Unload));
            await WaitUntilAsync(() => closed == 1).ConfigureAwait(false);

            window.Dispatch(new Event(EventNames.Unload));
            Assert.AreEqual(1, closed);
        }

        [Test]
        public void NotificationsCanBeRegisteredViaIoOptions()
        {
            var platform = new NotificationPlatform();
            var config = Configuration.Default.WithIo(new IoOptions { NotificationPlatform = platform });

            CollectionAssert.Contains(config.Services, platform);
        }

        private static IConfiguration Configure(IPermissionPolicy policy, INotificationPlatform platform) =>
            Configuration.Default.WithNavigator().WithPermissions(policy).WithNotifications(platform);

        private static async Task<IWindow> OpenWindowAsync(IConfiguration configuration)
        {
            var context = BrowsingContext.New(configuration);
            var document = await context.OpenAsync(res => res.Address(Address).Content("<!doctype html>")).ConfigureAwait(false);
            return document.DefaultView;
        }

        private static List<String> Record(Notification notification)
        {
            var events = new List<String>();
            notification.Show += (s, e) => events.Add(e.Type);
            notification.Error += (s, e) => events.Add(e.Type);
            notification.Click += (s, e) => events.Add(e.Type);
            notification.Closed += (s, e) => events.Add(e.Type);
            return events;
        }

        private static async Task WaitUntilAsync(Func<Boolean> condition)
        {
            for (var i = 0; i < 200 && !condition.Invoke(); i++)
            {
                await Task.Delay(10).ConfigureAwait(false);
            }

            Assert.IsTrue(condition.Invoke());
        }

        private sealed class ManualEventLoop : IEventLoop
        {
            private readonly Queue<Action<CancellationToken>> _tasks = new Queue<Action<CancellationToken>>();

            public ICancellable Enqueue(Action<CancellationToken> action, TaskPriority priority)
            {
                lock (_tasks)
                {
                    _tasks.Enqueue(action);
                }

                return null;
            }

            public void RunAll()
            {
                while (true)
                {
                    Action<CancellationToken> task;

                    lock (_tasks)
                    {
                        if (_tasks.Count == 0)
                        {
                            return;
                        }

                        task = _tasks.Dequeue();
                    }

                    task.Invoke(CancellationToken.None);
                }
            }

            public void CancelAll()
            {
                lock (_tasks)
                {
                    _tasks.Clear();
                }
            }

            public void Spin() => RunAll();
        }
    }
}
