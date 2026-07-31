namespace AngleSharp.Io.Tests.BroadcastChannel
{
    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    [TestFixture]
    public class BroadcastChannelTests
    {
        [Test]
        public async Task BroadcastChannelCanBeCreatedFromADocumentWindow()
        {
            var context = BrowsingContext.New();
            var window = await OpenWindowAsync(context, "https://broadcast.example/");

            Assert.IsNotNull(new BroadcastChannel(window, "app"));
        }

        [Test]
        public async Task BroadcastChannelDeliversMessagesToOtherSameOriginChannels()
        {
            var contextA = BrowsingContext.New();
            var contextB = BrowsingContext.New();

            var windowA = await OpenWindowAsync(contextA, "https://broadcast.example/");
            var windowB = await OpenWindowAsync(contextB, "https://broadcast.example/other");

            using var channelA = new BroadcastChannel(windowA, "app");
            using var channelB = new BroadcastChannel(windowB, "app");

            var received = new TaskCompletionSource<String>();
            var sourceEvents = 0;

            channelA.Message += (_, __) => sourceEvents++;
            channelB.Message += (_, ev) => received.TrySetResult(((MessageEvent)ev).Data?.ToString());

            channelA.PostMessage("hello");

            Assert.AreEqual("hello", await received.Task);
            Assert.AreEqual(0, sourceEvents);
        }

        [Test]
        public async Task BroadcastChannelDoesNotCrossOrigins()
        {
            var contextA = BrowsingContext.New();
            var contextB = BrowsingContext.New();

            var windowA = await OpenWindowAsync(contextA, "https://broadcast.example/");
            var windowB = await OpenWindowAsync(contextB, "https://other.example/");

            using var channelA = new BroadcastChannel(windowA, "app");
            using var channelB = new BroadcastChannel(windowB, "app");

            var received = new TaskCompletionSource<Boolean>();

            channelB.Message += (_, __) => received.TrySetResult(true);

            channelA.PostMessage("hello");

            var completed = await Task.WhenAny(received.Task, Task.Delay(200));
            Assert.AreNotEqual(received.Task, completed);
        }

        private static async Task<IWindow> OpenWindowAsync(IBrowsingContext context, String address)
        {
            var document = await context.OpenAsync(res =>
                res.Address(address).Content("<!doctype html><title>broadcast</title>"));
            return document.DefaultView;
        }
    }
}