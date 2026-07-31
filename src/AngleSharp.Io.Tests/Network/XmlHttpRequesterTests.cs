namespace AngleSharp.Io.Tests.Network
{
    using AngleSharp.Browser;
    using AngleSharp.Common;
    using AngleSharp.Dom;
    using AngleSharp.Html;
    using AngleSharp.Io;
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class XmlHttpRequesterTests
    {
        [Test]
        public async Task OpenAndSendShouldCreateRequestWithHeadersAndBody()
        {
            var loader = new TestDocumentLoader(request =>
            {
                var response = CreateResponse("https://api.example.test/endpoint", "ok", HttpStatusCode.Accepted);
                return new TestDownload(request.Target, Task.FromResult<IResponse>(response));
            });

            var xhr = await CreateRequesterAsync(loader, "https://origin.example.test/").ConfigureAwait(false);
            xhr.Open("POST", "https://api.example.test/endpoint");
            xhr.SetRequestHeader("X-Test", "abc");
            xhr.Send("payload");

            await WaitForReadyStateAsync(xhr, RequesterState.Done).ConfigureAwait(false);

            Assert.IsNotNull(loader.LastRequest);
            Assert.AreEqual(HttpMethod.Post, loader.LastRequest.Method);
            Assert.AreEqual("https://api.example.test/endpoint", loader.LastRequest.Target.Href);
            Assert.AreEqual("abc", loader.LastRequest.Headers["X-Test"]);

            using (var reader = new StreamReader(loader.LastRequest.Body, Encoding.UTF8, true, 1024, true))
            {
                Assert.AreEqual("payload", reader.ReadToEnd());
            }
        }

        [Test]
        public async Task SendWithFormDataShouldSetMultipartContentTypeWhenMissing()
        {
            var loader = new TestDocumentLoader(request =>
            {
                var response = CreateResponse("https://api.example.test/endpoint", "ok", HttpStatusCode.OK);
                return new TestDownload(request.Target, Task.FromResult<IResponse>(response));
            });

            var xhr = await CreateRequesterAsync(loader, "https://origin.example.test/").ConfigureAwait(false);
            var formData = new FormDataSet();
            formData.Append("foo", "bar", "text/plain");

            xhr.Open("POST", "https://api.example.test/endpoint");
            xhr.Send(formData);

            await WaitForReadyStateAsync(xhr, RequesterState.Done).ConfigureAwait(false);

            Assert.IsTrue(loader.LastRequest.Headers.ContainsKey(HeaderNames.ContentType));
            Assert.IsTrue(loader.LastRequest.Headers[HeaderNames.ContentType].StartsWith("multipart/form-data; boundary="));
        }

        [Test]
        public async Task SendShouldNotOverrideExplicitContentType()
        {
            var loader = new TestDocumentLoader(request =>
            {
                var response = CreateResponse("https://api.example.test/endpoint", "ok", HttpStatusCode.OK);
                return new TestDownload(request.Target, Task.FromResult<IResponse>(response));
            });

            var xhr = await CreateRequesterAsync(loader, "https://origin.example.test/").ConfigureAwait(false);
            var formData = new FormDataSet();
            formData.Append("foo", "bar", "text/plain");

            xhr.Open("POST", "https://api.example.test/endpoint");
            xhr.SetRequestHeader(HeaderNames.ContentType, "application/custom");
            xhr.Send(formData);

            await WaitForReadyStateAsync(xhr, RequesterState.Done).ConfigureAwait(false);

            Assert.AreEqual("application/custom", loader.LastRequest.Headers[HeaderNames.ContentType]);
        }

        [Test]
        public async Task SendShouldPopulateResponseStatusTextHeadersAndBody()
        {
            var response = CreateResponse("https://api.example.test/endpoint", "{\"result\":true}", HttpStatusCode.Created);
            response.Headers["X-Result"] = "ok";

            var loader = new TestDocumentLoader(request =>
                new TestDownload(request.Target, Task.FromResult<IResponse>(response)));

            var xhr = await CreateRequesterAsync(loader, "https://origin.example.test/").ConfigureAwait(false);
            xhr.Open("GET", "https://api.example.test/endpoint");
            xhr.Send();

            await WaitForReadyStateAsync(xhr, RequesterState.Done).ConfigureAwait(false);

            Assert.AreEqual((Int32)HttpStatusCode.Created, xhr.StatusCode);
            Assert.AreEqual(HttpStatusCode.Created.ToString(), xhr.StatusText);
            Assert.AreEqual("https://api.example.test/endpoint", xhr.ResponseUrl);
            Assert.AreEqual("{\"result\":true}", xhr.ResponseText);
            Assert.AreEqual("ok", xhr.GetResponseHeader("X-Result"));
            Assert.IsTrue(xhr.GetAllResponseHeaders().Contains("X-Result: ok"));
        }

        [Test]
        public async Task ReadyStateShouldTransitionInExpectedOrderForSuccessfulRequest()
        {
            var loader = new TestDocumentLoader(request =>
            {
                var response = CreateResponse("https://api.example.test/endpoint", "ok", HttpStatusCode.OK);
                return new TestDownload(request.Target, Task.FromResult<IResponse>(response));
            });

            var xhr = await CreateRequesterAsync(loader, "https://origin.example.test/").ConfigureAwait(false);
            var states = new List<RequesterState>();

            xhr.ReadyStateChanged += (_, __) => states.Add(xhr.ReadyState);

            xhr.Open("GET", "https://api.example.test/endpoint");
            xhr.Send();

            await WaitForReadyStateAsync(xhr, RequesterState.Done).ConfigureAwait(false);

            CollectionAssert.AreEqual(
                new[] { RequesterState.Opened, RequesterState.HeadersReceived, RequesterState.Loading, RequesterState.Done },
                states);
        }

        [Test]
        public async Task SendShouldRaiseTimeoutEventWhenDownloadTaskIsCanceled()
        {
            var canceled = Task.FromCanceled<IResponse>(new CancellationToken(canceled: true));
            var loader = new TestDocumentLoader(request => new TestDownload(request.Target, canceled));
            var xhr = await CreateRequesterAsync(loader, "https://origin.example.test/").ConfigureAwait(false);

            var timedOut = false;
            xhr.Timedout += (_, __) => timedOut = true;

            xhr.Open("GET", "https://api.example.test/endpoint");
            xhr.Send();

            await WaitForReadyStateAsync(xhr, RequesterState.Done).ConfigureAwait(false);

            Assert.IsTrue(timedOut);
            Assert.AreEqual(0, xhr.StatusCode);
        }

        [Test]
        public async Task SendShouldRaiseErrorEventWhenDownloadTaskFails()
        {
            var failed = Task.FromException<IResponse>(new InvalidOperationException("boom"));
            var loader = new TestDocumentLoader(request => new TestDownload(request.Target, failed));
            var xhr = await CreateRequesterAsync(loader, "https://origin.example.test/").ConfigureAwait(false);

            var errored = false;
            xhr.Error += (_, __) => errored = true;

            xhr.Open("GET", "https://api.example.test/endpoint");
            xhr.Send();

            await WaitForReadyStateAsync(xhr, RequesterState.Done).ConfigureAwait(false);

            Assert.IsTrue(errored);
            Assert.AreEqual(0, xhr.StatusCode);
        }

        [Test]
        public async Task AbortShouldCancelDownloadAndRaiseAbortEventWhenLoading()
        {
            var loader = new TestDocumentLoader(request =>
                new TestDownload(request.Target, Task.FromResult<IResponse>(CreateResponse("https://api.example.test/endpoint", "ok", HttpStatusCode.OK))));

            var xhr = await CreateRequesterAsync(loader, "https://origin.example.test/").ConfigureAwait(false);
            var aborted = false;
            xhr.Aborted += (_, __) => aborted = true;

            xhr.Open("GET", "https://api.example.test/endpoint");

            var download = new TestDownload(Url.Create("https://api.example.test/endpoint"), Task.FromResult<IResponse>(CreateResponse("https://api.example.test/endpoint", "ok", HttpStatusCode.OK)));

            typeof(XmlHttpRequest).GetField("_download", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(xhr, download);

            typeof(XmlHttpRequest).GetProperty("ReadyState", BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(xhr, RequesterState.Loading);

            xhr.Abort();

            Assert.IsTrue(aborted);
            Assert.IsTrue(download.Canceled);
        }

        [Test]
        public async Task ResponseHeadersShouldBeUnavailableBeforeResponseIsReceived()
        {
            var pending = new TaskCompletionSource<IResponse>();
            var loader = new TestDocumentLoader(request => new TestDownload(request.Target, pending.Task));
            var xhr = await CreateRequesterAsync(loader, "https://origin.example.test/").ConfigureAwait(false);

            xhr.Open("GET", "https://api.example.test/endpoint");
            xhr.Send();

            Assert.AreEqual(String.Empty, xhr.GetResponseHeader("X-Anything"));
            Assert.AreEqual(String.Empty, xhr.GetAllResponseHeaders());

            var response = CreateResponse("https://api.example.test/endpoint", "ok", HttpStatusCode.OK);
            response.Headers["X-Anything"] = "value";
            pending.TrySetResult(response);

            await WaitForReadyStateAsync(xhr, RequesterState.Done).ConfigureAwait(false);

            Assert.AreEqual("value", xhr.GetResponseHeader("X-Anything"));
        }

        [Test]
        public void SerializeFormDataSetBodyShouldProduceMultipartContent()
        {
            var formData = new FormDataSet();
            formData.Append("name", "Ada Lovelace", "text/plain");
            formData.Append("role", "Mathematician", "text/plain");

            var method = typeof(XmlHttpRequest).GetMethod("Serialize", BindingFlags.Static | BindingFlags.NonPublic);
            var serialized = method.Invoke(null, new Object[] { formData });
            var serializedType = serialized.GetType();
            var content = (Stream)serializedType.GetProperty("Content").GetValue(serialized);
            var contentType = (String)serializedType.GetProperty("ContentType").GetValue(serialized);

            String body;

            using (var reader = new StreamReader(content, Encoding.UTF8, true, 1024, true))
            {
                body = reader.ReadToEnd();
            }

            Assert.IsTrue(body.Contains("name=\"name\""), body);
            Assert.IsTrue(body.Contains("Ada Lovelace"), body);
            Assert.IsTrue(body.Contains("name=\"role\""), body);
            Assert.IsTrue(body.Contains("Mathematician"), body);
            Assert.IsTrue(contentType.StartsWith("multipart/form-data; boundary="), contentType);
        }

        [Test]
        public void SerializeUrlSearchParamsShouldUseWwwFormUrlEncodedContentType()
        {
            var searchParams = new UrlSearchParams("a=1&b=2");
            var method = typeof(XmlHttpRequest).GetMethod("Serialize", BindingFlags.Static | BindingFlags.NonPublic);
            var serialized = method.Invoke(null, new Object[] { searchParams });
            var serializedType = serialized.GetType();
            var content = (Stream)serializedType.GetProperty("Content").GetValue(serialized);
            var contentType = (String)serializedType.GetProperty("ContentType").GetValue(serialized);

            String body;

            using (var reader = new StreamReader(content, Encoding.UTF8, true, 1024, true))
            {
                body = reader.ReadToEnd();
            }

            Assert.AreEqual("a=1&b=2", body);
            Assert.AreEqual("application/x-www-form-urlencoded; charset=UTF-8", contentType);
        }

        [Test]
        public void SerializeNullShouldProduceEmptyBodyWithoutContentType()
        {
            var method = typeof(XmlHttpRequest).GetMethod("Serialize", BindingFlags.Static | BindingFlags.NonPublic);
            var serialized = method.Invoke(null, new Object[] { null });
            var serializedType = serialized.GetType();
            var content = (Stream)serializedType.GetProperty("Content").GetValue(serialized);
            var contentType = (String)serializedType.GetProperty("ContentType").GetValue(serialized);

            Assert.AreEqual(String.Empty, new StreamReader(content, Encoding.UTF8, true, 1024, true).ReadToEnd());
            Assert.IsNull(contentType);
        }

        private static async Task<XmlHttpRequest> CreateRequesterAsync(IDocumentLoader loader, String address)
        {
            var config = Configuration.Default
                .WithOnly<IDocumentLoader>(_ => loader)
                .WithOnly<IEventLoop>(_ => new ImmediateEventLoop());

            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Address(address).Content("<!doctype html><title>xhr</title>")).ConfigureAwait(false);
            return new XmlHttpRequest(document.DefaultView);
        }

        private static async Task WaitForReadyStateAsync(XmlHttpRequest xhr, RequesterState state)
        {
            for (var i = 0; i < 200; i++)
            {
                if (xhr.ReadyState == state)
                {
                    return;
                }

                await Task.Delay(5).ConfigureAwait(false);
            }

            Assert.Fail("Expected ready state {0}, but was {1}.", state, xhr.ReadyState);
        }

        private static DefaultResponse CreateResponse(String address, String body, HttpStatusCode statusCode)
        {
            return new DefaultResponse
            {
                Address = Url.Create(address),
                StatusCode = statusCode,
                Headers = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase),
                Content = new MemoryStream(Encoding.UTF8.GetBytes(body ?? String.Empty)),
            };
        }

        private sealed class TestDocumentLoader : IDocumentLoader
        {
            private readonly Func<DocumentRequest, IDownload> _createDownload;

            public TestDocumentLoader(Func<DocumentRequest, IDownload> createDownload)
            {
                _createDownload = createDownload ?? throw new ArgumentNullException(nameof(createDownload));
            }

            public DocumentRequest LastRequest { get; private set; }

            public TestDownload LastDownload { get; private set; }

            public IDownload FetchAsync(DocumentRequest request)
            {
                LastRequest = request;
                var download = _createDownload.Invoke(request);

                if (download is TestDownload testDownload)
                {
                    LastDownload = testDownload;
                }

                return download;
            }

            public IEnumerable<IDownload> GetDownloads()
            {
                if (LastDownload != null)
                {
                    yield return LastDownload;
                }
            }
        }

        private sealed class TestDownload : IDownload
        {
            private readonly Task<IResponse> _task;

            public TestDownload(Url target, Task<IResponse> task, Object source = null)
            {
                Target = target;
                Source = source;
                _task = task ?? throw new ArgumentNullException(nameof(task));
            }

            public Url Target { get; }

            public Object Source { get; }

            public Task<IResponse> Task => _task;

            public Boolean IsCompleted => _task.IsCompleted;

            public Boolean IsRunning => !_task.IsCompleted;

            public Boolean Canceled { get; private set; }

            public void Cancel()
            {
                Canceled = true;
            }
        }

        private sealed class ImmediateEventLoop : IEventLoop
        {
            public ICancellable Enqueue(Action<CancellationToken> action, TaskPriority priority)
            {
                action(CancellationToken.None);
                return new CompletedCancellable();
            }

            public void CancelAll()
            {
            }

            public void Spin()
            {
            }
        }

        private sealed class CompletedCancellable : ICancellable
        {
            public Boolean IsCompleted => true;

            public Boolean IsRunning => false;

            public void Cancel()
            {
            }
        }
    }
}