namespace AngleSharp.Io.Tests.Dom
{
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Io.Dom;
    using FluentAssertions;
    using NUnit.Framework;
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Aborting a fetch against a real server, where a body takes its time.
    /// </summary>
    [TestFixture]
    public class AbortedFetchTests
    {
        private sealed class TrickleServer : IDisposable
        {
            private readonly HttpListener _listener = new HttpListener();
            private readonly TaskCompletionSource<Boolean> _sent = new TaskCompletionSource<Boolean>();
            private readonly TaskCompletionSource<Boolean> _broken = new TaskCompletionSource<Boolean>();
            private readonly CancellationTokenSource _stop = new CancellationTokenSource();

            public TrickleServer(Boolean finishes)
            {
                var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                var port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
                Address = $"http://localhost:{port}/";
                _listener.Prefixes.Add(Address);
                _listener.Start();
                _ = RunAsync(finishes);
            }

            public String Address { get; }

            /// <summary>The first part of the body has been sent.</summary>
            public Task Sent => _sent.Task;

            /// <summary>The client went away while the body was being sent.</summary>
            public Task Broken => _broken.Task;

            private async Task RunAsync(Boolean finishes)
            {
                var context = await _listener.GetContextAsync();
                var bytes = Encoding.UTF8.GetBytes("part");
                context.Response.SendChunked = true;

                try
                {
                    await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    await context.Response.OutputStream.FlushAsync();
                    _sent.TrySetResult(true);

                    if (finishes)
                    {
                        context.Response.Close();
                        return;
                    }

                    while (!_stop.IsCancellationRequested)
                    {
                        await Task.Delay(20);
                        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                        await context.Response.OutputStream.FlushAsync();
                    }
                }
                catch (Exception)
                {
                    _broken.TrySetResult(true);
                }
            }

            public void Dispose()
            {
                _stop.Cancel();
                _listener.Abort();
            }
        }

        private static Task<IDocument> OpenAsync() =>
            BrowsingContext.New(Configuration.Default.WithRequesters().WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true }))
                .OpenNewAsync("http://localhost/");

        private static async Task ShouldFinishSoon(Task task)
        {
            var first = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));
            first.Should().BeSameAs(task, "the task should not hang");
        }

        [Test]
        public async Task AbortingWhileTheBodyIsComingRejectsAndStopsTheDownload()
        {
            using var server = new TrickleServer(finishes: false);
            var document = await OpenAsync();
            var controller = new AbortController();
            var fetching = document.DefaultView.FetchAsync(server.Address, new FetchOptions { Signal = controller.Signal });
            await ShouldFinishSoon(server.Sent);
            fetching.IsCompleted.Should().BeFalse("the body is not complete");

            controller.Abort();

            await ShouldFinishSoon(fetching);
            fetching.IsFaulted.Should().BeTrue();
            fetching.Exception.GetBaseException().Should().BeOfType<DomException>().Which.Code.Should().Be((Int32)DomError.Abort);
            await ShouldFinishSoon(server.Broken);
        }

        [Test]
        public async Task AbortingWithAReasonRejectsWithIt()
        {
            using var server = new TrickleServer(finishes: false);
            var document = await OpenAsync();
            var controller = new AbortController();
            var reason = new TimeoutException("mine");
            var fetching = document.DefaultView.FetchAsync(server.Address, new FetchOptions { Signal = controller.Signal });
            await ShouldFinishSoon(server.Sent);

            controller.Abort(reason);

            await ShouldFinishSoon(fetching);
            fetching.Exception.GetBaseException().Should().BeSameAs(reason);
        }

        [Test]
        public async Task SignalAbortedBeforehandDoesNotEvenAsk()
        {
            using var server = new TrickleServer(finishes: true);
            var document = await OpenAsync();

            var fetching = document.DefaultView.FetchAsync(server.Address, new FetchOptions { Signal = AbortSignal.Abort() });

            await ShouldFinishSoon(fetching);
            fetching.IsFaulted.Should().BeTrue();
            server.Sent.IsCompleted.Should().BeFalse();
        }

        [Test]
        public async Task AbortingAfterTheResponseStopsReadingIt()
        {
            using var server = new TrickleServer(finishes: true);
            var document = await OpenAsync();
            var controller = new AbortController();
            var response = await document.DefaultView.FetchAsync(server.Address, new FetchOptions { Signal = controller.Signal });

            controller.Abort();

            var reading = response.Text();
            await ShouldFinishSoon(reading);
            reading.IsFaulted.Should().BeTrue();
            reading.Exception.GetBaseException().Should().BeOfType<DomException>();
        }

        [Test]
        public async Task AbortingAfterReadingChangesNothing()
        {
            using var server = new TrickleServer(finishes: true);
            var document = await OpenAsync();
            var controller = new AbortController();
            var response = await document.DefaultView.FetchAsync(server.Address, new FetchOptions { Signal = controller.Signal });

            var text = await response.Text();
            controller.Abort();

            text.Should().Be("part");
        }

        [Test]
        public async Task FetchWithASignalNotAbortedWorksAsUsual()
        {
            using var server = new TrickleServer(finishes: true);
            var document = await OpenAsync();

            var response = await document.DefaultView.FetchAsync(server.Address, new FetchOptions { Signal = new AbortController().Signal });

            (await response.Text()).Should().Be("part");
        }
    }
}
