namespace AngleSharp.Io.Tests.Beacon
{
    using AngleSharp;
    using AngleSharp.Browser.Dom;
    using AngleSharp.Dom;
    using AngleSharp.Html;
    using AngleSharp.Io;
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class BeaconTests
    {
        [Test]
        public async Task SendBeaconPostsTextWithPlainTextContentType()
        {
            var loader = new TestDocumentLoader();
            var navigator = await OpenNavigatorAsync(loader, "https://beacon.example/app/page").ConfigureAwait(false);

            var result = navigator.SendBeacon("/collect?x=1", "hello");

            Assert.IsTrue(result);
            var request = loader.Requests.Single();
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual("https://beacon.example/collect?x=1", request.Target.Href);
            Assert.AreEqual("https://beacon.example/app/page", request.Referer);
            Assert.AreEqual("text/plain;charset=UTF-8", request.ContentType);
            Assert.AreEqual("https://beacon.example", request.Origin);
            Assert.AreEqual("hello", Encoding.UTF8.GetString(request.Body));
        }

        [Test]
        public async Task SendBeaconWithoutDataPostsAnEmptyBody()
        {
            var loader = new TestDocumentLoader();
            var navigator = await OpenNavigatorAsync(loader, "https://beacon.example/").ConfigureAwait(false);

            Assert.IsTrue(navigator.SendBeacon("collect"));
            Assert.IsTrue(navigator.SendBeacon("collect", null));

            Assert.AreEqual(2, loader.Requests.Count);
            Assert.IsTrue(loader.Requests.All(request => request.Method == HttpMethod.Post && request.Body.Length == 0 && request.ContentType is null));
        }

        [Test]
        public async Task SendBeaconPostsBytesWithoutContentType()
        {
            var loader = new TestDocumentLoader();
            var navigator = await OpenNavigatorAsync(loader, "https://beacon.example/").ConfigureAwait(false);

            Assert.IsTrue(navigator.SendBeacon("/collect", new Byte[] { 1, 2, 3 }));
            Assert.IsTrue(navigator.SendBeacon("/collect", new ArraySegment<Byte>(new Byte[] { 1, 2, 3, 4, 5 }, 1, 3)));

            CollectionAssert.AreEqual(new Byte[] { 1, 2, 3 }, loader.Requests[0].Body);
            CollectionAssert.AreEqual(new Byte[] { 2, 3, 4 }, loader.Requests[1].Body);
            Assert.IsNull(loader.Requests[0].ContentType);
            Assert.IsNull(loader.Requests[1].ContentType);
        }

        [Test]
        public async Task SendBeaconPostsBlobsWithTheirType()
        {
            var loader = new TestDocumentLoader();
            var navigator = await OpenNavigatorAsync(loader, "https://beacon.example/").ConfigureAwait(false);
            var json = new InputFile("data.json", "application/json", Encoding.UTF8.GetBytes("{\"a\":1}"));
            var untyped = new InputFile("data.bin", String.Empty, new Byte[] { 7 });

            Assert.IsTrue(navigator.SendBeacon("/collect", json));
            Assert.IsTrue(navigator.SendBeacon("/collect", untyped));

            Assert.AreEqual("application/json", loader.Requests[0].ContentType);
            Assert.AreEqual("{\"a\":1}", Encoding.UTF8.GetString(loader.Requests[0].Body));
            Assert.IsNull(loader.Requests[1].ContentType);
            CollectionAssert.AreEqual(new Byte[] { 7 }, loader.Requests[1].Body);
        }

        [Test]
        public async Task SendBeaconPostsFormDataAsMultipart()
        {
            var loader = new TestDocumentLoader();
            var navigator = await OpenNavigatorAsync(loader, "https://beacon.example/").ConfigureAwait(false);
            var formData = new FormDataSet();
            formData.Append("name", "Ada", "text/plain");

            Assert.IsTrue(navigator.SendBeacon("/collect", formData));

            var request = loader.Requests.Single();
            Assert.AreEqual("multipart/form-data; boundary=" + formData.Boundary, request.ContentType);
            StringAssert.Contains("Ada", Encoding.UTF8.GetString(request.Body));
        }

        [Test]
        public async Task SendBeaconPostsSearchParamsAsUrlEncoded()
        {
            var loader = new TestDocumentLoader();
            var navigator = await OpenNavigatorAsync(loader, "https://beacon.example/").ConfigureAwait(false);
            var searchParams = new UrlSearchParams("a=1&b=two");

            Assert.IsTrue(navigator.SendBeacon("/collect", searchParams));

            var request = loader.Requests.Single();
            StringAssert.StartsWith("application/x-www-form-urlencoded", request.ContentType);
            Assert.AreEqual("a=1&b=two", Encoding.UTF8.GetString(request.Body));
        }

        [Test]
        public async Task SendBeaconRejectsStreams()
        {
            var loader = new TestDocumentLoader();
            var navigator = await OpenNavigatorAsync(loader, "https://beacon.example/").ConfigureAwait(false);

            Assert.IsFalse(navigator.SendBeacon("/collect", new MemoryStream(new Byte[] { 1 })));
            Assert.IsEmpty(loader.Requests);
        }

        [Test]
        public async Task SendBeaconRejectsDataThatCannotBeSerialized()
        {
            var loader = new TestDocumentLoader();
            var navigator = await OpenNavigatorAsync(loader, "https://beacon.example/").ConfigureAwait(false);

            Assert.IsFalse(navigator.SendBeacon("/collect", new UnserializableData()));
            Assert.IsEmpty(loader.Requests);
        }

        [Test]
        public async Task SendBeaconRejectsInvalidUrls()
        {
            var loader = new TestDocumentLoader();
            var navigator = await OpenNavigatorAsync(loader, "https://beacon.example/").ConfigureAwait(false);

            Assert.Throws<InvalidOperationException>(() => navigator.SendBeacon("ftp://beacon.example/collect", "x"));
            Assert.Throws<InvalidOperationException>(() => navigator.SendBeacon("data:text/plain,x", "x"));
            Assert.Throws<InvalidOperationException>(() => navigator.SendBeacon("http://[invalid/", "x"));
            Assert.IsEmpty(loader.Requests);
        }

        [Test]
        public async Task SendBeaconRejectsPayloadsExceedingTheQuotaInFlight()
        {
            var loader = new TestDocumentLoader { Complete = false };
            var navigator = await OpenNavigatorAsync(loader, "https://beacon.example/").ConfigureAwait(false);

            Assert.IsTrue(navigator.SendBeacon("/collect", new Byte[40 * 1024]));
            Assert.IsFalse(navigator.SendBeacon("/collect", new Byte[30 * 1024]));
            Assert.IsTrue(navigator.SendBeacon("/collect", new Byte[24 * 1024]));
            Assert.IsTrue(navigator.SendBeacon("/collect"));
            Assert.IsFalse(navigator.SendBeacon("/collect", "x"));

            loader.Requests[0].Finish();
            await WaitForAsync(() => navigator.SendBeacon("/collect", new Byte[30 * 1024])).ConfigureAwait(false);

            Assert.AreEqual(4, loader.Requests.Count);
        }

        [Test]
        public async Task SendBeaconRejectsPayloadsLargerThanTheQuota()
        {
            var loader = new TestDocumentLoader();
            var navigator = await OpenNavigatorAsync(loader, "https://beacon.example/").ConfigureAwait(false);

            Assert.IsFalse(navigator.SendBeacon("/collect", new Byte[BeaconOptions.DefaultQuota + 1]));
            Assert.IsTrue(navigator.SendBeacon("/collect", new Byte[BeaconOptions.DefaultQuota]));
        }

        [Test]
        public async Task SendBeaconUsesTheConfiguredQuota()
        {
            var loader = new TestDocumentLoader();
            var configuration = CreateConfiguration(loader).WithBeacon(new BeaconOptions { Quota = 10 });
            var navigator = await OpenNavigatorAsync(configuration, "https://beacon.example/").ConfigureAwait(false);

            Assert.IsFalse(navigator.SendBeacon("/collect", "eleven char"));
            Assert.IsTrue(navigator.SendBeacon("/collect", "ten chars!"));
        }

        [Test]
        public async Task SendBeaconTracksTheQuotaPerBrowsingContext()
        {
            var loader = new TestDocumentLoader { Complete = false };
            var configuration = CreateConfiguration(loader);
            var first = await OpenNavigatorAsync(configuration, "https://beacon.example/").ConfigureAwait(false);
            var second = await OpenNavigatorAsync(configuration, "https://beacon.example/").ConfigureAwait(false);

            Assert.IsTrue(first.SendBeacon("/collect", new Byte[BeaconOptions.DefaultQuota]));
            Assert.IsFalse(first.SendBeacon("/collect", "x"));
            Assert.IsTrue(second.SendBeacon("/collect", "x"));
        }

        [Test]
        public async Task SendBeaconReleasesTheQuotaWhenTheRequestFails()
        {
            var loader = new TestDocumentLoader { Complete = false };
            var navigator = await OpenNavigatorAsync(loader, "https://beacon.example/").ConfigureAwait(false);

            Assert.IsTrue(navigator.SendBeacon("/collect", new Byte[BeaconOptions.DefaultQuota]));
            loader.Requests[0].Fail(new InvalidOperationException("offline"));

            await WaitForAsync(() => navigator.SendBeacon("/collect", "x")).ConfigureAwait(false);
        }

        [Test]
        public async Task SendBeaconTreatsAThrowingLoaderAsNetworkError()
        {
            var loader = new TestDocumentLoader { Throw = true };
            var navigator = await OpenNavigatorAsync(loader, "https://beacon.example/").ConfigureAwait(false);

            Assert.IsTrue(navigator.SendBeacon("/collect", new Byte[BeaconOptions.DefaultQuota]));

            loader.Throw = false;
            await WaitForAsync(() => navigator.SendBeacon("/collect", new Byte[BeaconOptions.DefaultQuota])).ConfigureAwait(false);
        }

        [Test]
        public async Task SendBeaconQueuesBeyondTheMaximumOfConcurrentRequests()
        {
            var loader = new TestDocumentLoader { Complete = false };
            var configuration = CreateConfiguration(loader).WithBeacon(new BeaconOptions { MaxConcurrentRequests = 2 });
            var navigator = await OpenNavigatorAsync(configuration, "https://beacon.example/").ConfigureAwait(false);

            Assert.IsTrue(navigator.SendBeacon("/one"));
            Assert.IsTrue(navigator.SendBeacon("/two"));
            Assert.IsTrue(navigator.SendBeacon("/three"));
            Assert.IsTrue(navigator.SendBeacon("/four"));
            CollectionAssert.AreEqual(new[] { "/one", "/two" }, loader.Requests.Select(m => m.Target.PathName));

            loader.Requests[1].Finish();
            await WaitForAsync(() => loader.Requests.Count == 3).ConfigureAwait(false);
            loader.Requests[0].Fail(new InvalidOperationException("offline"));
            await WaitForAsync(() => loader.Requests.Count == 4).ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "/one", "/two", "/three", "/four" }, loader.Requests.Select(m => m.Target.PathName));
        }

        [Test]
        public async Task QueuedBeaconsCountTowardsTheQuota()
        {
            var loader = new TestDocumentLoader { Complete = false };
            var configuration = CreateConfiguration(loader).WithBeacon(new BeaconOptions { MaxConcurrentRequests = 1, Quota = 10 });
            var navigator = await OpenNavigatorAsync(configuration, "https://beacon.example/").ConfigureAwait(false);

            Assert.IsTrue(navigator.SendBeacon("/collect", "12345"));
            Assert.IsTrue(navigator.SendBeacon("/collect", "12345"));
            Assert.IsFalse(navigator.SendBeacon("/collect", "1"));
            Assert.AreEqual(1, loader.Requests.Count);

            loader.Requests[0].Finish();
            await WaitForAsync(() => loader.Requests.Count == 2).ConfigureAwait(false);

            Assert.IsTrue(navigator.SendBeacon("/collect", "12345"));
            Assert.IsFalse(navigator.SendBeacon("/collect", "1"));
        }

        [TestCase("text/plain", false)]
        [TestCase("TEXT/PLAIN; charset=utf-8", false)]
        [TestCase("application/x-www-form-urlencoded", false)]
        [TestCase("multipart/form-data; boundary=x", false)]
        [TestCase("application/json", true)]
        [TestCase("text/csv", true)]
        public async Task CrossOriginBeaconsWithOtherContentTypesArePreflighted(String contentType, Boolean preflight)
        {
            var server = new CorsServer();
            var navigator = await server.OpenNavigatorAsync("https://beacon.example/").ConfigureAwait(false);
            var blob = new InputFile("data", contentType, Encoding.UTF8.GetBytes("{}"));

            Assert.IsTrue(navigator.SendBeacon("https://api.example/collect", blob));
            await WaitForAsync(() => server.Requests.Any(m => m.Method == HttpMethod.Post)).ConfigureAwait(false);

            Assert.AreEqual(preflight, server.Requests.Any(m => m.Method == HttpMethod.Options));
            Assert.AreEqual(contentType, server.Requests.Last().Headers[HeaderNames.ContentType]);
        }

        [Test]
        public async Task SameOriginBeaconsAreNotPreflighted()
        {
            var server = new CorsServer();
            var navigator = await server.OpenNavigatorAsync("https://beacon.example/").ConfigureAwait(false);
            var blob = new InputFile("data.json", "application/json", Encoding.UTF8.GetBytes("{}"));

            Assert.IsTrue(navigator.SendBeacon("/collect", blob));
            await WaitForAsync(() => server.Requests.Count == 1).ConfigureAwait(false);

            Assert.AreEqual(HttpMethod.Post, server.Requests[0].Method);
            Assert.AreEqual("https://beacon.example", server.Requests[0].Headers[HeaderNames.Origin]);
        }

        [Test]
        public async Task PreflightIsSentWithoutCredentialsAndTheBeaconWithThem()
        {
            var server = new CorsServer();
            var context = server.CreateContext();
            await context.OpenAsync(res => res.Address("https://api.example/").Content("<!doctype html>").Cookie("id=1; Path=/")).ConfigureAwait(false);
            var document = await context.OpenAsync(res => res.Address("https://beacon.example/").Content("<!doctype html>")).ConfigureAwait(false);
            var blob = new InputFile("data.json", "application/json", Encoding.UTF8.GetBytes("{\"a\":1}"));

            Assert.IsTrue(document.DefaultView.Navigator.SendBeacon("https://api.example/collect", blob));
            await WaitForAsync(() => server.Requests.Count == 2).ConfigureAwait(false);

            var preflight = server.Requests[0];
            var beacon = server.Requests[1];
            Assert.AreEqual(HttpMethod.Options, preflight.Method);
            Assert.AreEqual("https://api.example/collect", preflight.Address.Href);
            Assert.AreEqual("https://beacon.example", preflight.Headers[HeaderNames.Origin]);
            Assert.AreEqual("POST", preflight.Headers["Access-Control-Request-Method"]);
            Assert.AreEqual("content-type", preflight.Headers["Access-Control-Request-Headers"]);
            Assert.IsFalse(preflight.Headers.ContainsKey(HeaderNames.Cookie));
            Assert.AreEqual(HttpMethod.Post, beacon.Method);
            Assert.AreEqual("id=1", beacon.Headers[HeaderNames.Cookie]);
            Assert.AreEqual("{\"a\":1}", beacon.Body);
        }

        [TestCase(200, null, "true", "content-type")]
        [TestCase(200, "*", "true", "content-type")]
        [TestCase(200, "https://other.example", "true", "content-type")]
        [TestCase(200, "https://beacon.example", null, "content-type")]
        [TestCase(200, "https://beacon.example", "TRUE", "content-type")]
        [TestCase(200, "https://beacon.example", "true", null)]
        [TestCase(200, "https://beacon.example", "true", "*")]
        [TestCase(200, "https://beacon.example", "true", "x-custom")]
        [TestCase(403, "https://beacon.example", "true", "content-type")]
        public async Task FailingPreflightDropsTheBeacon(Int32 status, String allowOrigin, String allowCredentials, String allowHeaders)
        {
            var server = new CorsServer
            {
                PreflightStatus = status,
                AllowOrigin = allowOrigin,
                AllowCredentials = allowCredentials,
                AllowHeaders = allowHeaders,
            };
            var navigator = await server.OpenNavigatorAsync("https://beacon.example/", new BeaconOptions { Quota = 10 }).ConfigureAwait(false);
            var blob = new InputFile("data.json", "application/json", Encoding.UTF8.GetBytes("0123456789"));

            Assert.IsTrue(navigator.SendBeacon("https://api.example/collect", blob));
            await WaitForAsync(() => navigator.SendBeacon("https://api.example/collect", "0123456789")).ConfigureAwait(false);
            await WaitForAsync(() => server.Requests.Count == 2).ConfigureAwait(false);

            Assert.AreEqual(HttpMethod.Options, server.Requests[0].Method);
            Assert.AreEqual(HttpMethod.Post, server.Requests[1].Method);
            Assert.AreEqual("0123456789", server.Requests[1].Body);
            Assert.AreEqual("text/plain;charset=UTF-8", server.Requests[1].Headers[HeaderNames.ContentType]);
        }

        [Test]
        public async Task SuccessfulPreflightAcceptsAllowedHeadersInAList()
        {
            var server = new CorsServer { AllowHeaders = "X-Custom, Content-Type" };
            var navigator = await server.OpenNavigatorAsync("https://beacon.example/").ConfigureAwait(false);
            var blob = new InputFile("data.json", "application/json", Encoding.UTF8.GetBytes("{}"));

            Assert.IsTrue(navigator.SendBeacon("https://api.example/collect", blob));
            await WaitForAsync(() => server.Requests.Count == 2).ConfigureAwait(false);

            Assert.AreEqual(HttpMethod.Post, server.Requests[1].Method);
        }

        [TestCase("navigate")]
        [TestCase("close")]
        [TestCase("dispose")]
        public async Task BeaconsKeepGoingWhenTheirDocumentGoesAway(String action)
        {
            var requester = new BlockingRequester();
            var context = BrowsingContext.New(Configuration.Default.WithNavigator().With(requester).WithDefaultLoader());
            var document = await context.OpenAsync(res => res.Address("https://beacon.example/").Content("<!doctype html>")).ConfigureAwait(false);

            Assert.IsTrue(document.DefaultView.Navigator.SendBeacon("/collect", new Byte[BeaconOptions.DefaultQuota]));
            await requester.Started.Task.ConfigureAwait(false);

            if (action == "navigate")
            {
                await context.OpenAsync(res => res.Address("https://beacon.example/next").Content("<!doctype html>")).ConfigureAwait(false);
            }
            else if (action == "close")
            {
                document.Close();
                document.Dispose();
            }
            else
            {
                context.Dispose();
            }

            await Task.Delay(50).ConfigureAwait(false);
            Assert.IsFalse(requester.Token.IsCancellationRequested);

            requester.Release();

            if (action == "navigate")
            {
                var navigator = context.Active.DefaultView.Navigator;
                await WaitForAsync(() => navigator.SendBeacon("/collect", new Byte[BeaconOptions.DefaultQuota])).ConfigureAwait(false);
            }
        }

        [Test]
        public async Task SendBeaconReturnsFalseWithoutLoader()
        {
            var context = BrowsingContext.New(Configuration.Default.WithNavigator());
            await context.OpenAsync(res => res.Address("https://beacon.example/").Content("<!doctype html>")).ConfigureAwait(false);

            Assert.IsFalse(context.Active.DefaultView.Navigator.SendBeacon("/collect", "x"));
        }

        [Test]
        public async Task SendBeaconGoesThroughTheRequesterPipelineWithCookies()
        {
            var received = new TaskCompletionSource<Request>(TaskCreationOptions.RunContinuationsAsynchronously);
            var configuration = Configuration.Default
                .WithNavigator()
                .WithCookies()
                .WithVirtualRequester(request =>
                {
                    received.TrySetResult(request);
                    return VirtualResponse.Create(res => res.Address(request.Address).Status(204));
                });
            var context = BrowsingContext.New(configuration);
            var document = await context.OpenAsync(res => res.Address("https://beacon.example/").Content("<!doctype html>").Cookie("session=abc")).ConfigureAwait(false);

            Assert.IsTrue(document.DefaultView.Navigator.SendBeacon("/collect", "bye"));

            var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
            Assert.AreSame(received.Task, completed);
            var sent = await received.Task.ConfigureAwait(false);
            Assert.AreEqual(HttpMethod.Post, sent.Method);
            Assert.AreEqual("https://beacon.example/collect", sent.Address.Href);
            Assert.AreEqual("text/plain;charset=UTF-8", sent.Headers[HeaderNames.ContentType]);
            Assert.AreEqual("session=abc", sent.Headers[HeaderNames.Cookie]);

            using (var reader = new StreamReader(sent.Content))
            {
                Assert.AreEqual("bye", reader.ReadToEnd());
            }
        }

        private static async Task WaitForAsync(Func<Boolean> condition)
        {
            for (var i = 0; i < 100; i++)
            {
                if (condition.Invoke())
                {
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            Assert.Fail("The condition was not met in time.");
        }

        private static IConfiguration CreateConfiguration(TestDocumentLoader loader) =>
            Configuration.Default.WithNavigator().WithOnly<IDocumentLoader>(_ => loader);

        private static Task<INavigator> OpenNavigatorAsync(TestDocumentLoader loader, String address) =>
            OpenNavigatorAsync(CreateConfiguration(loader), address);

        private static async Task<INavigator> OpenNavigatorAsync(IConfiguration configuration, String address)
        {
            var context = BrowsingContext.New(configuration);
            var document = await context.OpenAsync(res => res.Address(address).Content("<!doctype html><title>beacon</title>")).ConfigureAwait(false);
            var navigator = document.DefaultView.Navigator;
            Assert.IsNotNull(navigator);
            return navigator;
        }

        private sealed class CorsServer
        {
            public Int32 PreflightStatus { get; set; } = 204;

            public String AllowOrigin { get; set; } = "https://beacon.example";

            public String AllowCredentials { get; set; } = "true";

            public String AllowHeaders { get; set; } = "content-type";

            public List<ReceivedRequest> Requests { get; } = new List<ReceivedRequest>();

            public IBrowsingContext CreateContext(BeaconOptions options = null) =>
                BrowsingContext.New(Configuration.Default
                    .WithNavigator()
                    .WithCookies()
                    .WithVirtualRequester(Handle)
                    .WithBeacon(options));

            public async Task<INavigator> OpenNavigatorAsync(String address, BeaconOptions options = null)
            {
                var context = CreateContext(options);
                var document = await context.OpenAsync(res => res.Address(address).Content("<!doctype html>")).ConfigureAwait(false);
                return document.DefaultView.Navigator;
            }

            private IResponse Handle(Request request)
            {
                lock (Requests)
                {
                    Requests.Add(new ReceivedRequest(request));
                }

                if (request.Method == HttpMethod.Options)
                {
                    return VirtualResponse.Create(res =>
                    {
                        res.Address(request.Address).Status(PreflightStatus);

                        if (AllowOrigin is not null)
                        {
                            res.Header(HeaderNames.AccessControlAllowOrigin, AllowOrigin);
                        }

                        if (AllowCredentials is not null)
                        {
                            res.Header("Access-Control-Allow-Credentials", AllowCredentials);
                        }

                        if (AllowHeaders is not null)
                        {
                            res.Header("Access-Control-Allow-Headers", AllowHeaders);
                        }
                    });
                }

                return VirtualResponse.Create(res => res.Address(request.Address).Status(204));
            }
        }

        private sealed class ReceivedRequest
        {
            public ReceivedRequest(Request request)
            {
                Method = request.Method;
                Address = request.Address;
                Headers = new Dictionary<String, String>(request.Headers, StringComparer.OrdinalIgnoreCase);

                using (var reader = new StreamReader(request.Content ?? Stream.Null))
                {
                    Body = reader.ReadToEnd();
                }
            }

            public HttpMethod Method { get; }

            public Url Address { get; }

            public Dictionary<String, String> Headers { get; }

            public String Body { get; }
        }

        private sealed class BlockingRequester : BaseRequester
        {
            private readonly TaskCompletionSource<Boolean> _release = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<Boolean> Started { get; } = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

            public CancellationToken Token { get; private set; }

            public void Release() => _release.TrySetResult(true);

            public override Boolean SupportsProtocol(String protocol) => true;

            protected override async Task<IResponse> PerformRequestAsync(Request request, CancellationToken cancel)
            {
                Token = cancel;
                Started.TrySetResult(true);
                await _release.Task.ConfigureAwait(false);
                return VirtualResponse.Create(res => res.Address(request.Address).Status(204));
            }
        }

        private sealed class UnserializableData
        {
            public override String ToString() => throw new NotSupportedException();
        }

        private sealed class TestDocumentLoader : IDocumentLoader
        {
            public Boolean Complete { get; set; } = true;

            public Boolean Throw { get; set; }

            public List<SentBeacon> Requests { get; } = new List<SentBeacon>();

            public IDownload FetchAsync(DocumentRequest request)
            {
                if (Throw)
                {
                    throw new InvalidOperationException("broken loader");
                }

                var beacon = new SentBeacon(request);
                Requests.Add(beacon);

                if (Complete)
                {
                    beacon.Finish();
                }

                return beacon;
            }

            public IEnumerable<IDownload> GetDownloads() => Requests;
        }

        private sealed class SentBeacon : IDownload
        {
            private readonly TaskCompletionSource<IResponse> _completion = new TaskCompletionSource<IResponse>();

            public SentBeacon(DocumentRequest request)
            {
                Target = request.Target;
                Method = request.Method;
                Referer = request.Referer;
                Origin = request.Headers.TryGetValue(HeaderNames.Origin, out var origin) ? origin : null;
                ContentType = request.Headers.TryGetValue(HeaderNames.ContentType, out var type) ? type : null;

                using (var buffer = new MemoryStream())
                {
                    request.Body.CopyTo(buffer);
                    Body = buffer.ToArray();
                }
            }

            public Url Target { get; }

            public HttpMethod Method { get; }

            public String Referer { get; }

            public String Origin { get; }

            public String ContentType { get; }

            public Byte[] Body { get; }

            public Object Source => null;

            public Task<IResponse> Task => _completion.Task;

            public Boolean IsCompleted => _completion.Task.IsCompleted;

            public Boolean IsRunning => !_completion.Task.IsCompleted;

            public void Finish() => _completion.TrySetResult(VirtualResponse.Create(res => res.Address(Target).Status(204)));

            public void Fail(Exception error) => _completion.TrySetException(error);

            public void Cancel() => _completion.TrySetCanceled();
        }
    }
}
