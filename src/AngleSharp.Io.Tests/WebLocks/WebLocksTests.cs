namespace AngleSharp.Io.Tests.WebLocks
{
    using AngleSharp;
    using AngleSharp.Browser.Dom;
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    [TestFixture]
    public class WebLocksTests
    {
        [Test]
        public async Task NavigatorLocksCanBeCreatedFromADocumentWindow()
        {
            var context = await OpenContextAsync("https://locks.example/").ConfigureAwait(false);
            var navigator = GetNavigator(context);

            Assert.IsNotNull(navigator.Locks());
        }

        [Test]
        public async Task NavigatorLocksSerializeSameOriginRequests()
        {
            var contextA = await OpenContextAsync("https://locks.example/").ConfigureAwait(false);
            var contextB = await OpenContextAsync("https://locks.example/other").ConfigureAwait(false);
            var locksA = GetNavigator(contextA).Locks();
            var locksB = GetNavigator(contextB).Locks();

            var enteredA = new TaskCompletionSource<Boolean>();
            var enteredB = new TaskCompletionSource<Boolean>();
            var releaseA = new TaskCompletionSource<Boolean>();

            var requestA = locksA.Request("resource", async _ =>
            {
                enteredA.TrySetResult(true);
                await releaseA.Task.ConfigureAwait(false);
            });

            await enteredA.Task.ConfigureAwait(false);

            var requestB = locksB.Request("resource", async _ =>
            {
                enteredB.TrySetResult(true);
                await Task.CompletedTask;
            });

            var blocked = await Task.WhenAny(enteredB.Task, Task.Delay(200)).ConfigureAwait(false);
            Assert.AreNotEqual(enteredB.Task, blocked);

            releaseA.TrySetResult(true);

            var completed = await Task.WhenAny(enteredB.Task, Task.Delay(1000)).ConfigureAwait(false);
            Assert.AreEqual(enteredB.Task, completed);

            await Task.WhenAll(requestA, requestB).ConfigureAwait(false);
        }

        private static async Task<LockManager> OpenLocksAsync([System.Runtime.CompilerServices.CallerMemberName] String name = null)
        {
            var context = await OpenContextAsync($"https://{name.ToLowerInvariant()}.example/").ConfigureAwait(false);
            return GetNavigator(context).Locks();
        }

        [Test]
        public async Task IfAvailableGetsNullWhileTheLockIsHeld()
        {
            var locks = await OpenLocksAsync();
            var release = new TaskCompletionSource<Boolean>();
            var entered = new TaskCompletionSource<Boolean>();
            var held = locks.Request("r", async _ =>
            {
                entered.TrySetResult(true);
                await release.Task;
            });
            await entered.Task;
            WebLock seen = null;
            var called = false;

            await locks.Request("r", new LockOptions { IfAvailable = true }, l =>
            {
                called = true;
                seen = l;
                return Task.CompletedTask;
            });

            Assert.IsTrue(called);
            Assert.IsNull(seen);
            release.SetResult(true);
            await held;
        }

        [Test]
        public async Task IfAvailableGetsTheLockWhenFree()
        {
            var locks = await OpenLocksAsync();
            WebLock seen = null;

            await locks.Request("r", new LockOptions { IfAvailable = true }, l =>
            {
                seen = l;
                return Task.CompletedTask;
            });

            Assert.AreEqual("r", seen.Name);
            Assert.AreEqual("exclusive", seen.Mode);
        }

        [Test]
        public async Task SharedLocksAreHeldTogetherAndExclusiveWaits()
        {
            var locks = await OpenLocksAsync();
            var bothIn = new TaskCompletionSource<Boolean>();
            var release = new TaskCompletionSource<Boolean>();
            var count = 0;
            var exclusiveRan = false;
            Func<WebLock, Task> shared = async l =>
            {
                Assert.AreEqual("shared", l.Mode);

                if (System.Threading.Interlocked.Increment(ref count) == 2)
                {
                    bothIn.TrySetResult(true);
                }

                await release.Task;
            };
            var a = locks.Request("r", new LockOptions { Mode = "shared" }, shared);
            var b = locks.Request("r", new LockOptions { Mode = "shared" }, shared);

            await bothIn.Task;
            var c = locks.Request("r", _ =>
            {
                exclusiveRan = true;
                return Task.CompletedTask;
            });
            await Task.Delay(30);
            Assert.IsFalse(exclusiveRan);

            release.SetResult(true);
            await Task.WhenAll(a, b, c);
            Assert.IsTrue(exclusiveRan);
        }

        [Test]
        public async Task SharedRequestAfterAWaitingExclusiveOneWaitsItsTurn()
        {
            var locks = await OpenLocksAsync();
            var release = new TaskCompletionSource<Boolean>();
            var entered = new TaskCompletionSource<Boolean>();
            var order = new System.Collections.Generic.List<String>();
            var a = locks.Request("r", new LockOptions { Mode = "shared" }, async _ =>
            {
                entered.TrySetResult(true);
                await release.Task;
            });
            await entered.Task;
            var b = locks.Request("r", _ =>
            {
                lock (order) { order.Add("exclusive"); }
                return Task.CompletedTask;
            });
            var c = locks.Request("r", new LockOptions { Mode = "shared" }, _ =>
            {
                lock (order) { order.Add("shared"); }
                return Task.CompletedTask;
            });
            await Task.Delay(30);
            Assert.IsEmpty(order);

            release.SetResult(true);
            await Task.WhenAll(a, b, c);
            Assert.AreEqual(new[] { "exclusive", "shared" }, order);
        }

        [Test]
        public async Task StealTakesTheLockAndTheOldHolderDoesNotReleaseIt()
        {
            var locks = await OpenLocksAsync();
            var release = new TaskCompletionSource<Boolean>();
            var entered = new TaskCompletionSource<Boolean>();
            var releaseThief = new TaskCompletionSource<Boolean>();
            var thiefIn = new TaskCompletionSource<Boolean>();
            var old = locks.Request("r", async _ =>
            {
                entered.TrySetResult(true);
                await release.Task;
            });
            await entered.Task;
            var thief = locks.Request("r", new LockOptions { Steal = true }, async _ =>
            {
                thiefIn.TrySetResult(true);
                await releaseThief.Task;
            });
            await thiefIn.Task;
            release.SetResult(true);
            await old;
            WebLock seen = new WebLock("", "", "");

            await locks.Request("r", new LockOptions { IfAvailable = true }, l =>
            {
                seen = l;
                return Task.CompletedTask;
            });

            Assert.IsNull(seen);
            releaseThief.SetResult(true);
            await thief;
        }

        [Test]
        public async Task AbortingStopsWaitingForTheLock()
        {
            var locks = await OpenLocksAsync();
            var release = new TaskCompletionSource<Boolean>();
            var entered = new TaskCompletionSource<Boolean>();
            var held = locks.Request("r", async _ =>
            {
                entered.TrySetResult(true);
                await release.Task;
            });
            await entered.Task;
            var controller = new AbortController();
            var called = false;
            var waiting = locks.Request("r", new LockOptions { Signal = controller.Signal }, _ =>
            {
                called = true;
                return Task.CompletedTask;
            });

            controller.Abort();

            try
            {
                await waiting;
                Assert.Fail("Expected the request to be aborted.");
            }
            catch (DomException ex)
            {
                Assert.AreEqual((Int32)DomError.Abort, ex.Code);
            }

            release.SetResult(true);
            await held;
            Assert.IsFalse(called);
        }

        [Test]
        public async Task AnAbortedSignalRejectsRightAway()
        {
            var locks = await OpenLocksAsync();
            var controller = new AbortController();
            controller.Abort();

            try
            {
                await locks.Request("r", new LockOptions { Signal = controller.Signal }, _ => Task.CompletedTask);
                Assert.Fail("Expected the request to be aborted.");
            }
            catch (DomException ex)
            {
                Assert.AreEqual((Int32)DomError.Abort, ex.Code);
            }
        }

        [Test]
        public async Task OptionsThatDoNotGoTogetherAreRejected()
        {
            var locks = await OpenLocksAsync();

            foreach (var options in new[]
            {
                new LockOptions { Steal = true, IfAvailable = true },
                new LockOptions { Steal = true, Mode = "shared" },
                new LockOptions { IfAvailable = true, Signal = new AbortController().Signal },
            })
            {
                try
                {
                    await locks.Request("r", options, _ => Task.CompletedTask);
                    Assert.Fail("Expected the request to be rejected.");
                }
                catch (DomException ex)
                {
                    Assert.AreEqual((Int32)DomError.NotSupported, ex.Code);
                }
            }
        }

        [Test]
        public async Task ThrowingCallbackReleasesTheLock()
        {
            var locks = await OpenLocksAsync();

            try
            {
                await locks.Request("r", new LockOptions(), _ => throw new InvalidOperationException());
            }
            catch (InvalidOperationException)
            {
            }

            WebLock seen = null;
            await locks.Request("r", new LockOptions { IfAvailable = true }, l =>
            {
                seen = l;
                return Task.CompletedTask;
            });
            Assert.IsNotNull(seen);
        }

        [Test]
        public async Task NavigatorLocksDoNotCrossOrigins()
        {
            var contextA = await OpenContextAsync("https://locks.example/").ConfigureAwait(false);
            var contextB = await OpenContextAsync("https://other.example/").ConfigureAwait(false);
            var locksA = GetNavigator(contextA).Locks();
            var locksB = GetNavigator(contextB).Locks();

            var enteredA = new TaskCompletionSource<Boolean>();
            var enteredB = new TaskCompletionSource<Boolean>();
            var releaseA = new TaskCompletionSource<Boolean>();

            var requestA = locksA.Request("resource", async _ =>
            {
                enteredA.TrySetResult(true);
                await releaseA.Task.ConfigureAwait(false);
            });

            await enteredA.Task.ConfigureAwait(false);

            var requestB = locksB.Request("resource", async _ =>
            {
                enteredB.TrySetResult(true);
                await Task.CompletedTask;
            });

            var completed = await Task.WhenAny(enteredB.Task, Task.Delay(500)).ConfigureAwait(false);
            Assert.AreEqual(enteredB.Task, completed);

            releaseA.TrySetResult(true);

            await Task.WhenAll(requestA, requestB).ConfigureAwait(false);
        }

        private static async Task<IBrowsingContext> OpenContextAsync(String address)
        {
            var context = BrowsingContext.New(Configuration.Default.WithNavigator());

            var document = await context.OpenAsync(res =>
                res.Address(address).Content("<!doctype html><title>locks</title>")).ConfigureAwait(false);

            Assert.IsNotNull(document.DefaultView);
            return context;
        }

        private static INavigator GetNavigator(IBrowsingContext context)
        {
            var navigator = context?.Active?.DefaultView?.Navigator;
            Assert.IsNotNull(navigator);
            return navigator;
        }
    }
}