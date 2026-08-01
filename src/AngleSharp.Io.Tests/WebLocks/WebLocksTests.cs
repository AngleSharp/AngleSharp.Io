namespace AngleSharp.Io.Tests.WebLocks
{
    using AngleSharp;
    using AngleSharp.Browser.Dom;
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