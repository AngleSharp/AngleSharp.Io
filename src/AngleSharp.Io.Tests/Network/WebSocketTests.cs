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
        public async Task ConnectToWebSocketEcho()
        {
            if (Helper.IsNetworkAvailable())
            {
                // ARRANGE
                var message = "foo";
                var haserror = false;
                var messages = new List<String>();
                var closed = new TaskCompletionSource<Boolean>();
                var document = await BrowsingContext.New().OpenNewAsync("https://echo.websocket.events/.ws");
                var ws = new WebSocket(document.DefaultView, "wss://echo.websocket.events/");

                // ACT
                ws.Opened += (s, ev) => ws.Send(message);
                ws.Message += (s, ev) =>
                {
                    var msg = ev as MessageEvent;
                    messages.Add(msg.Data.ToString());

                    if (messages.Count == 2)
                    {
                        ws.Close();
                    }
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
                messages.Count.Should().Be(2);
                messages[0].Should().Be("echo.websocket.events sponsored by Lob.com");
                messages[1].Should().Be(message);
            }
        }
    }
}
