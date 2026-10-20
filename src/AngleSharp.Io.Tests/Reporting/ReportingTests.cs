namespace AngleSharp.Io.Tests.Reporting
{
    using AngleSharp;
    using AngleSharp.Browser;
    using AngleSharp.Common;
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.Http;
    using AngleSharp.Io.Network;
    using AngleSharp.Io.Reporting;
    using AngleSharp.Io.ServiceWorkerApi;
    using AngleSharp.Io.Tests.Mocks;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;
    using HttpMethod = AngleSharp.Io.HttpMethod;

    [TestFixture]
    public class ReportingTests
    {
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

        [Test]
        public void ReportSerializesTypeUrlAndBody()
        {
            var report = new Report(ReportTypes.NetworkError, "https://report.example/page", new NetworkErrorReportBody
            {
                ElapsedTime = 42,
                Phase = "connection",
                Type = "tcp.refused",
                Method = "GET",
                Referrer = "https://report.example/",
            });

            Assert.AreEqual(
                "{\"body\":{\"elapsed_time\":42,\"method\":\"GET\",\"phase\":\"connection\",\"protocol\":\"\",\"referrer\":\"https://report.example/\",\"sampling_fraction\":1,\"server_ip\":\"\",\"status_code\":0,\"type\":\"tcp.refused\"},\"type\":\"network-error\",\"url\":\"https://report.example/page\"}",
                report.ToJson());
        }

        [Test]
        public void ReportSerializesGenericBodies()
        {
            var body = new GenericReportBody(new Dictionary<String, Object>
            {
                ["message"] = "say \"hi\"\n\\",
                ["line"] = 3,
                ["ratio"] = 0.5,
                ["flags"] = new Object[] { true, false, null },
                ["nested"] = new Dictionary<String, Object> { ["b"] = 2L, ["a"] = "x" },
                ["control"] = "\u0001",
            });

            var report = new Report("custom", "https://report.example/", body);

            Assert.AreEqual(
                "{\"control\":\"\\u0001\",\"flags\":[true,false,null],\"line\":3,\"message\":\"say \\\"hi\\\"\\n\\\\\",\"nested\":{\"a\":\"x\",\"b\":2},\"ratio\":0.5}",
                body.ToJson());
            StringAssert.StartsWith("{\"body\":{\"control\"", report.ToJson());
        }

        [Test]
        public void ReportWithoutBodySerializesNull()
        {
            var report = new Report("custom", null, null);

            Assert.AreEqual("{\"body\":null,\"type\":\"custom\",\"url\":\"\"}", report.ToJson());
            Assert.AreEqual(Report.DefaultDestination, report.Destination);
            Assert.Throws<ArgumentException>(() => new Report(null, "https://report.example/", null));
            Assert.Throws<ArgumentException>(() => new Report(String.Empty, "https://report.example/", null));
        }

        [Test]
        public void DeliveryFormatContainsAgeAndUserAgent()
        {
            var service = CreateService(out var clock);
            var report = new Report("custom", "https://report.example/", null, "main");
            service.QueueReport(report);
            clock.Advance(TimeSpan.FromSeconds(1.5));

            Assert.AreEqual(Start, report.Timestamp);
            Assert.AreEqual(
                "{\"age\":1500,\"body\":null,\"type\":\"custom\",\"url\":\"https://report.example/\",\"user_agent\":\"\"}",
                report.ToDeliveryJson(clock.Now));
        }

        [Test]
        public void QueuedReportsCanBeEnumeratedAndFiltered()
        {
            var service = CreateService(out _);
            var a = Queue(service, "deprecation", "main");
            var b = Queue(service, ReportTypes.NetworkError, "default");
            var c = Queue(service, ReportTypes.NetworkError, "nel");

            CollectionAssert.AreEqual(new[] { a, b, c }, service.GetReports());
            CollectionAssert.AreEqual(new[] { b, c }, service.GetReports(ReportFilter.ForTypes(ReportTypes.NetworkError)));
            CollectionAssert.AreEqual(new[] { a, c }, service.GetReports(ReportFilter.ForDestinations("main", "nel")));

            var both = ReportFilter.ForTypes(ReportTypes.NetworkError);
            both.Destinations.Add("nel");
            CollectionAssert.AreEqual(new[] { c }, service.GetReports(both));
            Assert.IsFalse(both.Matches(null));
        }

        [Test]
        public void TheOptionsFilterDecidesWhichReportsAreQueued()
        {
            var options = new ReportingOptions { Filter = ReportFilter.ForTypes("deprecation") };
            var service = new ReportingService(options);
            var queued = new List<Report>();
            service.ReportQueued += (s, e) => queued.Add(e.Report);

            Assert.IsTrue(service.QueueReport(new Report("deprecation", "https://report.example/", null)));
            Assert.IsFalse(service.QueueReport(new Report("intervention", "https://report.example/", null)));

            Assert.AreEqual(1, service.GetReports().Count);
            CollectionAssert.AreEqual(service.GetReports(), queued);
            Assert.Throws<ArgumentNullException>(() => service.QueueReport(null));
        }

        [Test]
        public void TheQueueDropsTheOldestReports()
        {
            var service = new ReportingService(new ReportingOptions { MaxQueueSize = 2 });
            Queue(service, "first", null);
            var second = Queue(service, "second", null);
            var third = Queue(service, "third", null);

            CollectionAssert.AreEqual(new[] { second, third }, service.GetReports());
        }

        [Test]
        public async Task FlushHandsTheReportsToTheMatchingSinks()
        {
            var all = new MemoryReportSink();
            var errors = new MemoryReportSink();
            var main = new MemoryReportSink();
            var options = new ReportingOptions()
                .AddSink(all)
                .AddSink(errors, ReportFilter.ForTypes(ReportTypes.NetworkError))
                .AddSink(main, ReportFilter.ForDestinations("main"));
            var service = new ReportingService(options);
            var a = Queue(service, "deprecation", "main");
            var b = Queue(service, ReportTypes.NetworkError, "default");

            var flushed = await service.FlushAsync().ConfigureAwait(false);

            Assert.AreEqual(2, flushed);
            CollectionAssert.AreEqual(new[] { a, b }, all.Reports);
            CollectionAssert.AreEqual(new[] { b }, errors.Reports);
            CollectionAssert.AreEqual(new[] { a }, main.Reports);
            Assert.IsEmpty(service.GetReports());
            Assert.AreEqual(0, await service.FlushAsync().ConfigureAwait(false));
            Assert.AreEqual(2, all.Reports.Count);

            all.Clear();
            Assert.IsEmpty(all.Reports);
        }

        [Test]
        public async Task FlushAcceptsCallbacksAsSinks()
        {
            var received = new List<Report>();
            var options = new ReportingOptions().AddSink((reports, cancel) =>
            {
                received.AddRange(reports);
                return Task.CompletedTask;
            });
            var service = new ReportingService(options);
            var report = Queue(service, "custom", null);

            await service.FlushAsync().ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { report }, received);
            Assert.Throws<ArgumentNullException>(() => options.AddSink((Func<IReadOnlyList<Report>, CancellationToken, Task>)null));
            Assert.Throws<ArgumentNullException>(() => options.AddSink((IReportSink)null));
        }

        [Test]
        public void FailingSinksAreRethrownAfterAllSinksWereCalled()
        {
            var memory = new MemoryReportSink();
            var options = new ReportingOptions()
                .AddSink((reports, cancel) => Task.FromException(new InvalidOperationException("first")))
                .AddSink(memory);
            var service = new ReportingService(options);
            Queue(service, "custom", null);

            Assert.ThrowsAsync<InvalidOperationException>(() => service.FlushAsync());
            Assert.AreEqual(1, memory.Reports.Count);
            Assert.IsEmpty(service.GetReports());

            options.AddSink((reports, cancel) => throw new NotSupportedException("second"));
            Queue(service, "custom", null);

            var ex = Assert.ThrowsAsync<AggregateException>(() => service.FlushAsync());
            Assert.AreEqual(2, ex.InnerExceptions.Count);
        }

        [Test]
        public async Task FlushIntervalFlushesAutomatically()
        {
            var sink = new MemoryReportSink();
            var options = new ReportingOptions { FlushInterval = TimeSpan.FromMilliseconds(20) }.AddSink(sink);

            using (var service = new ReportingService(options))
            {
                Queue(service, "custom", null);
                await WaitForAsync(() => sink.Reports.Count == 1).ConfigureAwait(false);
                Assert.IsEmpty(service.GetReports());
            }
        }

        [Test]
        public void ConfiguredEndpointsResolveTheDestination()
        {
            var options = new ReportingOptions();
            options.Endpoints["default"] = "https://collector.example/reports";
            var service = new ReportingService(options);

            var known = Queue(service, "custom", "default");
            var unknown = Queue(service, "custom", "other");

            Assert.AreEqual("https://collector.example/reports", known.EndpointUrl);
            Assert.IsNull(unknown.EndpointUrl);
        }

        [Test]
        public async Task ReportingEndpointsHeadersAnnounceEndpointsPerOrigin()
        {
            var options = new ReportingOptions();
            options.Endpoints["main"] = "https://fallback.example/";
            var service = new ReportingService(options);
            var requester = new ReportingRequester(new VirtualRequester(request => Respond(request, 200, ("Reporting-Endpoints", "main=\"/reports\", csp=\"https://csp.example/a,b\", bad=\"ftp://x/\""))), service);

            await requester.RequestAsync(Get("https://report.example/page"), CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("https://report.example/reports", Queue(service, "custom", "main", "https://report.example/other").EndpointUrl);
            Assert.AreEqual("https://csp.example/a,b", Queue(service, "custom", "csp", "https://report.example/").EndpointUrl);
            Assert.IsNull(Queue(service, "custom", "bad", "https://report.example/").EndpointUrl);
            Assert.AreEqual("https://fallback.example/", Queue(service, "custom", "main", "https://elsewhere.example/").EndpointUrl);
        }

        [Test]
        public async Task EndpointSinkPostsReportsGroupedByEndpoint()
        {
            var posted = new List<(String Url, String ContentType, String Body)>();
            var transport = new VirtualRequester(request =>
            {
                using (var reader = new StreamReader(request.Content))
                {
                    posted.Add((request.Address.Href, request.Headers[HeaderNames.ContentType], reader.ReadToEnd()));
                }

                return Respond(request, 204);
            });
            var sink = new EndpointReportSink(transport) { Clock = () => Start.AddSeconds(2) };
            var options = new ReportingOptions { Clock = () => Start }.AddSink(sink);
            options.Endpoints["a"] = "https://a.example/reports";
            options.Endpoints["b"] = "https://b.example/reports";
            var service = new ReportingService(options);
            Queue(service, "one", "a");
            Queue(service, "two", "b");
            Queue(service, "three", "a");
            Queue(service, "four", "unknown");

            await service.FlushAsync().ConfigureAwait(false);

            Assert.AreEqual(2, posted.Count);
            Assert.AreEqual("https://a.example/reports", posted[0].Url);
            Assert.AreEqual("application/reports+json", posted[0].ContentType);
            Assert.AreEqual(
                "[{\"age\":2000,\"body\":null,\"type\":\"one\",\"url\":\"https://report.example/\",\"user_agent\":\"\"},{\"age\":2000,\"body\":null,\"type\":\"three\",\"url\":\"https://report.example/\",\"user_agent\":\"\"}]",
                posted[0].Body);
            Assert.AreEqual("https://b.example/reports", posted[1].Url);
            Assert.Throws<ArgumentNullException>(() => new EndpointReportSink(null));
        }

        [Test]
        public void EndpointSinkFailsForUnsuccessfulDeliveries()
        {
            var transport = new VirtualRequester(request => Respond(request, request.Address.Host == "a.example" ? 500 : 204));
            var options = new ReportingOptions().AddSink(new EndpointReportSink(transport));
            options.Endpoints["a"] = "https://a.example/reports";
            options.Endpoints["b"] = "https://b.example/reports";
            var service = new ReportingService(options);
            Queue(service, "one", "a");
            Queue(service, "two", "b");

            var ex = Assert.ThrowsAsync<InvalidOperationException>(() => service.FlushAsync());

            StringAssert.Contains("https://a.example/reports", ex.Message);
            StringAssert.DoesNotContain("https://b.example/reports", ex.Message);
        }

        [Test]
        public async Task HttpErrorsAreReportedWithTheRequestDetails()
        {
            var service = CreateService(out _);
            var requester = new ReportingRequester(new VirtualRequester(request => Respond(request, 404)), service);
            var request = Get("https://user:pass@report.example/missing?q=1#top");
            request.Headers[HeaderNames.Referer] = "https://report.example/";
            request.Headers[HeaderNames.UserAgent] = "TestAgent/1.0";

            var response = await requester.RequestAsync(request, CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            var report = service.GetReports().Single();
            var body = (NetworkErrorReportBody)report.Body;
            Assert.AreEqual(ReportTypes.NetworkError, report.Type);
            Assert.AreEqual("https://report.example/missing?q=1", report.Url);
            Assert.AreEqual("default", report.Destination);
            Assert.AreEqual("TestAgent/1.0", report.UserAgent);
            Assert.AreEqual("application", body.Phase);
            Assert.AreEqual("http.error", body.Type);
            Assert.AreEqual(404, body.StatusCode);
            Assert.AreEqual("GET", body.Method);
            Assert.AreEqual("https://report.example/", body.Referrer);
            Assert.GreaterOrEqual(body.ElapsedTime, 0);
        }

        [Test]
        public async Task SuccessfulResponsesAreNotReported()
        {
            var service = CreateService(out _);
            var requester = new ReportingRequester(new VirtualRequester(request => Respond(request, 302)), service);

            await requester.RequestAsync(Get("https://report.example/"), CancellationToken.None).ConfigureAwait(false);

            Assert.IsEmpty(service.GetReports());
        }

        [Test]
        public async Task CapturingCanBeTurnedOff()
        {
            var service = new ReportingService(new ReportingOptions { CaptureHttpErrors = false, CaptureNetworkErrors = false });
            var requester = new ReportingRequester(new VirtualRequester(request => Respond(request, request.Address.PathName == "/missing" ? 404 : 0)), service);

            await requester.RequestAsync(Get("https://report.example/missing"), CancellationToken.None).ConfigureAwait(false);
            await requester.RequestAsync(Get("https://report.example/failed"), CancellationToken.None).ConfigureAwait(false);

            Assert.IsEmpty(service.GetReports());
        }

        [Test]
        public async Task NetworkErrorsUseTheConfiguredDestination()
        {
            var service = new ReportingService(new ReportingOptions { NetworkErrorDestination = "nel" });
            var requester = new ReportingRequester(new VirtualRequester(request => Respond(request, 0)), service);

            await requester.RequestAsync(Get("https://report.example/"), CancellationToken.None).ConfigureAwait(false);

            var report = service.GetReports().Single();
            Assert.AreEqual("nel", report.Destination);
            Assert.AreEqual("unknown", ((NetworkErrorReportBody)report.Body).Type);
        }

        [Test]
        public async Task FailedResponsesAreClassifiedByTheirCause()
        {
            var service = CreateService(out _);
            var requester = new ReportingRequester(new VirtualRequester(request =>
            {
                var response = Respond(request, 0);
                NetworkFailures.Attach(response, new HttpRequestException("failed", new SocketException((Int32)SocketError.ConnectionRefused)));
                return response;
            }), service);

            await requester.RequestAsync(Get("https://report.example/"), CancellationToken.None).ConfigureAwait(false);

            var body = (NetworkErrorReportBody)service.GetReports().Single().Body;
            Assert.AreEqual("connection", body.Phase);
            Assert.AreEqual("tcp.refused", body.Type);
            Assert.AreEqual(0, body.StatusCode);
        }

        [Test]
        public void ThrowingRequestersAreReportedAndRethrown()
        {
            var service = CreateService(out _);
            var requester = new ReportingRequester(new ThrowingRequester(new SocketException((Int32)SocketError.HostNotFound)), service);

            Assert.ThrowsAsync<SocketException>(() => requester.RequestAsync(Get("https://missing.example/"), CancellationToken.None));

            var body = (NetworkErrorReportBody)service.GetReports().Single().Body;
            Assert.AreEqual("dns", body.Phase);
            Assert.AreEqual("dns.name_not_resolved", body.Type);
        }

        [Test]
        public void CancelledRequestsAreReportedAsAbandoned()
        {
            var service = CreateService(out _);
            var requester = new ReportingRequester(new ThrowingRequester(new OperationCanceledException()), service);

            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                Assert.CatchAsync<OperationCanceledException>(() => requester.RequestAsync(Get("https://report.example/"), cancel.Token));
            }

            Assert.AreEqual("abandoned", ((NetworkErrorReportBody)service.GetReports().Single().Body).Type);
        }

        [Test]
        public void ErrorsAreClassifiedLikeNetworkErrorLogging()
        {
            Assert.AreEqual(("dns", "dns.name_not_resolved"), Classify(new SocketException((Int32)SocketError.HostNotFound)));
            Assert.AreEqual(("dns", "dns.failed"), Classify(new SocketException((Int32)SocketError.TryAgain)));
            Assert.AreEqual(("connection", "tcp.refused"), Classify(new HttpRequestException("x", new SocketException((Int32)SocketError.ConnectionRefused))));
            Assert.AreEqual(("connection", "tcp.reset"), Classify(new SocketException((Int32)SocketError.ConnectionReset)));
            Assert.AreEqual(("connection", "tcp.aborted"), Classify(new SocketException((Int32)SocketError.ConnectionAborted)));
            Assert.AreEqual(("connection", "tcp.timed_out"), Classify(new SocketException((Int32)SocketError.TimedOut)));
            Assert.AreEqual(("connection", "tcp.address_unreachable"), Classify(new SocketException((Int32)SocketError.HostUnreachable)));
            Assert.AreEqual(("connection", "tcp.address_invalid"), Classify(new SocketException((Int32)SocketError.AddressNotAvailable)));
            Assert.AreEqual(("connection", "tcp.failed"), Classify(new SocketException((Int32)SocketError.Shutdown)));
            Assert.AreEqual(("connection", "tls.failed"), Classify(new HttpRequestException("x", new AuthenticationException())));
            Assert.AreEqual(("connection", "tcp.timed_out"), Classify(new TimeoutException()));
            Assert.AreEqual(("connection", "tcp.timed_out"), Classify(new TaskCanceledException()));
            Assert.AreEqual(("application", "unknown"), Classify(new InvalidOperationException()));
            Assert.AreEqual(("application", "unknown"), Classify(null));
        }

        [Test]
        public async Task RefusedConnectionsOfTheHttpClientRequesterAreReported()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            var service = CreateService(out _);
            var configuration = Configuration.Default.WithRequesters().WithReporting(service);
            var requester = configuration.Services.OfType<ReportingRequester>().First(m => m.SupportsProtocol("http"));

            var response = await requester.RequestAsync(Get($"http://127.0.0.1:{port}/"), CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(0, (Int32)response.StatusCode);
            var body = (NetworkErrorReportBody)service.GetReports().Single().Body;
            Assert.AreEqual("connection", body.Phase);
            Assert.AreEqual("tcp.refused", body.Type);
        }

        [Test]
        public async Task NavigationsGoThroughTheReportingPipeline()
        {
            var service = CreateService(out _);
            var configuration = Configuration.Default
                .WithVirtualRequester(request => Respond(request, request.Address.PathName == "/missing" ? 404 : 200))
                .WithReporting(service);
            var context = BrowsingContext.New(configuration);

            await context.OpenAsync("https://report.example/").ConfigureAwait(false);
            await context.OpenAsync("https://report.example/missing").ConfigureAwait(false);

            var report = service.GetReports().Single();
            Assert.AreEqual("https://report.example/missing", report.Url);
            Assert.AreEqual("http.error", ((NetworkErrorReportBody)report.Body).Type);
        }

        [Test]
        public void ReportingSitsClosestToTheNetwork()
        {
            var network = new VirtualRequester(_ => null);

            foreach (var configuration in new[]
            {
                Configuration.Default.With(network).WithReporting().WithHttpCache().WithServiceWorkers(),
                Configuration.Default.With(network).WithServiceWorkers().WithHttpCache().WithReporting(),
                Configuration.Default.With(network).WithHttpCache().WithReporting().WithServiceWorkers(),
            })
            {
                var serviceWorker = (ServiceWorkerRequester)configuration.Services.OfType<IRequester>().Single();
                var caching = (CachingRequester)serviceWorker.Inner;
                var reporting = (ReportingRequester)caching.Inner;
                Assert.AreSame(network, reporting.Inner);
            }
        }

        [Test]
        public void RegisteringReportingTwiceDoesNotNestTheRequesters()
        {
            var network = new VirtualRequester(_ => null);
            var service = CreateService(out _);
            var configuration = Configuration.Default.With(network).WithReporting().WithReporting(service);

            var requester = (ReportingRequester)configuration.Services.OfType<IRequester>().Single();

            Assert.AreSame(network, requester.Inner);
            Assert.AreSame(service, requester.Service);
            Assert.AreSame(service, configuration.Services.OfType<ReportingService>().Single());
            Assert.AreSame(configuration, configuration.WithReporting((ReportingOptions)null));
            Assert.Throws<ArgumentNullException>(() => new ReportingRequester(null, service));
            Assert.Throws<ArgumentNullException>(() => new ReportingRequester(network, null));
            Assert.Throws<ArgumentNullException>(() => new ReportingService(null));
        }

        [Test]
        public async Task WindowReportsReachObserversAndTheService()
        {
            var service = CreateService(out _);
            var window = await OpenWindowAsync(Configuration.Default.WithNavigator().WithReporting(service), "https://user:pw@report.example/page#top").ConfigureAwait(false);
            var observed = new List<Report>();
            var observer = new ReportingObserver(window, (reports, o) => observed.AddRange(reports));
            observer.Observe();

            var report = window.QueueReport(ReportTypes.Deprecation, new GenericReportBody(new Dictionary<String, Object> { ["id"] = "old-api" }), "main");

            CollectionAssert.AreEqual(new[] { report }, observed);
            CollectionAssert.AreEqual(new[] { report }, service.GetReports());
            Assert.AreEqual("https://report.example/page", report.Url);
            Assert.AreEqual("main", report.Destination);
            Assert.AreEqual(window.Navigator.UserAgent, report.UserAgent);
        }

        [Test]
        public async Task ObserversFilterByType()
        {
            var window = await OpenWindowAsync(Configuration.Default, "https://report.example/").ConfigureAwait(false);
            var observed = new List<String>();
            var observer = new ReportingObserver(window, (reports, o) => observed.AddRange(reports.Select(m => m.Type)), new ReportingObserverOptions { Types = new[] { "intervention" } });
            observer.Observe();

            window.QueueReport("deprecation", null);
            window.QueueReport("intervention", null);

            CollectionAssert.AreEqual(new[] { "intervention" }, observed);
        }

        [Test]
        public async Task DisconnectedObserversReceiveNothing()
        {
            var window = await OpenWindowAsync(Configuration.Default, "https://report.example/").ConfigureAwait(false);
            var observed = 0;
            var observer = new ReportingObserver(window, (reports, o) => observed += reports.Length);
            observer.Observe();
            observer.Observe();

            window.QueueReport("deprecation", null);
            observer.Disconnect();
            window.QueueReport("deprecation", null);

            Assert.AreEqual(1, observed);
        }

        [Test]
        public async Task BufferedObserversReceiveEarlierReports()
        {
            var window = await OpenWindowAsync(Configuration.Default, "https://report.example/").ConfigureAwait(false);
            var earlier = window.QueueReport("deprecation", null);
            var buffered = new List<Report[]>();
            var unbuffered = new List<Report[]>();
            new ReportingObserver(window, (reports, o) => buffered.Add(reports), new ReportingObserverOptions { Buffered = true }).Observe();
            new ReportingObserver(window, (reports, o) => unbuffered.Add(reports)).Observe();

            Assert.AreEqual(1, buffered.Count);
            CollectionAssert.AreEqual(new[] { earlier }, buffered[0]);
            Assert.IsEmpty(unbuffered);
        }

        [Test]
        public async Task TheReportBufferKeepsTheLatestHundredReports()
        {
            var window = await OpenWindowAsync(Configuration.Default, "https://report.example/").ConfigureAwait(false);

            for (var i = 0; i < 105; i++)
            {
                window.QueueReport("r" + i, null);
            }

            var observed = new List<Report>();
            new ReportingObserver(window, (reports, o) => observed.AddRange(reports), new ReportingObserverOptions { Buffered = true }).Observe();

            Assert.AreEqual(100, observed.Count);
            Assert.AreEqual("r5", observed[0].Type);
        }

        [Test]
        public async Task TakeRecordsReturnsTheUndeliveredReports()
        {
            var window = await OpenWindowAsync(Configuration.Default, "https://report.example/").ConfigureAwait(false);
            var taken = default(Report[]);
            var observer = default(ReportingObserver);
            observer = new ReportingObserver(window, (reports, o) => taken = o.TakeRecords());
            observer.Observe();

            window.QueueReport("deprecation", null);

            Assert.IsEmpty(taken);
            Assert.IsEmpty(observer.TakeRecords());
            Assert.Throws<ArgumentNullException>(() => new ReportingObserver(null, (r, o) => { }));
            Assert.Throws<ArgumentNullException>(() => new ReportingObserver(window, null));
        }

        [Test]
        public async Task ObserverCallbacksAreQueuedAsTasksOnTheEventLoop()
        {
            var loop = new ManualEventLoop();
            var window = await OpenWindowAsync(Configuration.Default.WithOnly<IEventLoop>(_ => loop), "https://report.example/").ConfigureAwait(false);
            loop.Reset();
            var calls = new List<Report[]>();
            new ReportingObserver(window, (reports, o) => calls.Add(reports)).Observe();

            var first = window.QueueReport("deprecation", null);
            var second = window.QueueReport("intervention", null);

            Assert.IsEmpty(calls);
            CollectionAssert.AreEqual(new[] { TaskPriority.Normal }, loop.Priorities);

            loop.RunAll();

            Assert.AreEqual(1, calls.Count);
            CollectionAssert.AreEqual(new[] { first, second }, calls[0]);

            var third = window.QueueReport("deprecation", null);
            Assert.AreEqual(1, calls.Count);

            loop.RunAll();

            Assert.AreEqual(2, calls.Count);
            CollectionAssert.AreEqual(new[] { third }, calls[1]);
        }

        [Test]
        public async Task TakingTheRecordsBeforeTheTaskRunsSkipsTheCallback()
        {
            var loop = new ManualEventLoop();
            var window = await OpenWindowAsync(Configuration.Default.WithOnly<IEventLoop>(_ => loop), "https://report.example/").ConfigureAwait(false);
            loop.Reset();
            var calls = 0;
            var observer = new ReportingObserver(window, (reports, o) => calls++);
            observer.Observe();

            var report = window.QueueReport("deprecation", null);
            var taken = observer.TakeRecords();
            loop.RunAll();

            CollectionAssert.AreEqual(new[] { report }, taken);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public async Task BufferedReportsAreDeliveredInOneTask()
        {
            var loop = new ManualEventLoop();
            var window = await OpenWindowAsync(Configuration.Default.WithOnly<IEventLoop>(_ => loop), "https://report.example/").ConfigureAwait(false);
            loop.Reset();
            var first = window.QueueReport("deprecation", null);
            var second = window.QueueReport("deprecation", null);
            var calls = new List<Report[]>();

            new ReportingObserver(window, (reports, o) => calls.Add(reports), new ReportingObserverOptions { Buffered = true }).Observe();

            Assert.IsEmpty(calls);
            Assert.AreEqual(1, loop.Priorities.Count);

            loop.RunAll();

            Assert.AreEqual(1, calls.Count);
            CollectionAssert.AreEqual(new[] { first, second }, calls[0]);
        }

        [Test]
        public async Task NetworkErrorsAreNotVisibleToObservers()
        {
            var service = CreateService(out _);
            var configuration = Configuration.Default
                .WithVirtualRequester(request => Respond(request, 500))
                .WithReporting(service);
            var context = BrowsingContext.New(configuration);
            var document = await context.OpenAsync(res => res.Address("https://report.example/").Content("<!doctype html>")).ConfigureAwait(false);
            var observed = 0;
            new ReportingObserver(document.DefaultView, (reports, o) => observed += reports.Length, new ReportingObserverOptions { Buffered = true }).Observe();

            await document.DefaultView.Fetch("/api").ConfigureAwait(false);

            Assert.AreEqual(1, service.GetReports().Count);
            Assert.AreEqual(0, observed);
        }

        private static (String, String) Classify(Exception error) => ReportingRequester.Classify(error, CancellationToken.None);

        private static ReportingService CreateService(out TestClock clock)
        {
            var current = new TestClock();
            clock = current;
            return new ReportingService(new ReportingOptions { Clock = () => current.Now });
        }

        private static Report Queue(ReportingService service, String type, String destination, String url = "https://report.example/")
        {
            var report = new Report(type, url, null, destination);
            service.QueueReport(report);
            return report;
        }

        private static Request Get(String address) => new Request
        {
            Method = HttpMethod.Get,
            Address = new Url(address),
            Content = Stream.Null,
        };

        private static IResponse Respond(Request request, Int32 status, params (String Name, String Value)[] headers) =>
            VirtualResponse.Create(res =>
            {
                res.Address(request.Address).Status(status);

                foreach (var (name, value) in headers)
                {
                    res.Header(name, value);
                }
            });

        private static async Task<IWindow> OpenWindowAsync(IConfiguration configuration, String address)
        {
            var context = BrowsingContext.New(configuration);
            var document = await context.OpenAsync(res => res.Address(address).Content("<!doctype html>")).ConfigureAwait(false);
            return document.DefaultView;
        }

        private static async Task WaitForAsync(Func<Boolean> condition)
        {
            for (var i = 0; i < 200; i++)
            {
                if (condition.Invoke())
                {
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            Assert.Fail("The condition was not met in time.");
        }

        private sealed class ManualEventLoop : IEventLoop
        {
            private readonly Queue<Action<CancellationToken>> _tasks = new Queue<Action<CancellationToken>>();

            public List<TaskPriority> Priorities { get; } = new List<TaskPriority>();

            public ICancellable Enqueue(Action<CancellationToken> action, TaskPriority priority)
            {
                Priorities.Add(priority);
                _tasks.Enqueue(action);
                return null;
            }

            public void RunAll()
            {
                while (_tasks.Count > 0)
                {
                    _tasks.Dequeue().Invoke(CancellationToken.None);
                }
            }

            public void Reset()
            {
                // runs the tasks the document queued while loading
                RunAll();
                Priorities.Clear();
            }

            public void CancelAll() => _tasks.Clear();

            public void Spin() => RunAll();
        }

        private sealed class TestClock
        {
            public DateTimeOffset Now { get; private set; } = Start;

            public void Advance(TimeSpan time) => Now += time;
        }

        private sealed class ThrowingRequester : BaseRequester
        {
            private readonly Exception _error;

            public ThrowingRequester(Exception error)
            {
                _error = error;
            }

            public override Boolean SupportsProtocol(String protocol) => true;

            protected override Task<IResponse> PerformRequestAsync(Request request, CancellationToken cancel) =>
                Task.FromException<IResponse>(_error);
        }
    }
}
