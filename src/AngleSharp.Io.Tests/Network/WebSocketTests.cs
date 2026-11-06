namespace AngleSharp.Io.Tests.Network
{
    using AngleSharp.Dom.Events;
    using AngleSharp.Io.Dom;
    using FluentAssertions;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    [TestFixture]
    public class WebSocketTests
    {
        [Test]
        public async Task FailingConnectionFiresAnErrorEventWithTheProblem()
        {
            // ARRANGE
            var document = await BrowsingContext.New().OpenNewAsync("https://example.com/");
            var error = new TaskCompletionSource<ErrorEvent>();

            // ACT: nothing listens on the port, as it was just free
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var ws = new WebSocket(document.DefaultView, $"ws://127.0.0.1:{port}/");
            ws.Error += (s, ev) => error.TrySetResult(ev as ErrorEvent);
            var finished = await Task.WhenAny(error.Task, Task.Delay(TimeSpan.FromSeconds(30)));

            // ASSERT
            finished.Should().BeSameAs(error.Task);
            var received = await error.Task;
            received.Should().NotBeNull();
            received.Type.Should().Be("error");
            received.Error.Should().NotBeNull();
            received.Message.Should().NotBeNullOrEmpty();
        }

        [Test]
        public async Task ConnectToWebSocketEcho()
        {
            if (Helper.IsNetworkAvailable())
            {
                // ARRANGE
                var message = "foo";
                var haserror = false;
                var messages = new List<String>();
                var closed = new TaskCompletionSource<Boolean>();
                var document = await BrowsingContext.New().OpenNewAsync("https://anglesharp-tests.anglevisions.com/");
                var ws = new WebSocket(document.DefaultView, "wss://anglesharp-tests.anglevisions.com/ws-echo");

                // ACT
                ws.Opened += (s, ev) => ws.Send(message);
                ws.Message += (s, ev) =>
                {
                    var msg = ev as MessageEvent;
                    messages.Add(msg.Data.ToString());
                    ws.Close();
                };
                ws.Closed += (s, ev) =>
                {
                    closed.SetResult(true);
                };
                ws.Error += (s, ev) =>
                {
                    haserror = true;
                    ws.Close();
                };
                await closed.Task;

                // ASSERT
                haserror.Should().BeFalse();
                messages.Count.Should().Be(1);
                messages[0].Should().Be(message);
            }
        }
    }
}
