namespace AngleSharp.Io.Tests.Network
{
    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Html;
    using AngleSharp.Io;
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    [TestFixture]
    public class FetchTests
    {
        [Test]
        public async Task FetchShouldUseGetByDefaultAndResolveRelativeUrl()
        {
            var loader = new TestDocumentLoader(request =>
                new TestDownload(Task.FromResult<IResponse>(CreateResponse("https://example.org/api/data", "ok"))));

            var window = await OpenWindowAsync(loader, "https://example.org/base/page").ConfigureAwait(false);
            var response = await window.Fetch("/api/data").ConfigureAwait(false);

            Assert.IsNotNull(response);
            Assert.IsNotNull(loader.LastRequest);
            Assert.AreEqual(HttpMethod.Get, loader.LastRequest.Method);
            Assert.AreEqual("https://example.org/api/data", loader.LastRequest.Target.Href);
        }

        [Test]
        public async Task FetchShouldApplyMethodHeadersAndBody()
        {
            var loader = new TestDocumentLoader(request =>
                new TestDownload(Task.FromResult<IResponse>(CreateResponse("https://example.org/api", "ok"))));

            var window = await OpenWindowAsync(loader, "https://example.org/base/page").ConfigureAwait(false);
            var options = new FetchOptions
            {
                Method = "POST",
                Headers = new Dictionary<String, String>
                {
                    ["X-Test"] = "abc"
                },
                Body = "payload",
            };

            await window.Fetch("/api", options).ConfigureAwait(false);

            Assert.AreEqual(HttpMethod.Post, loader.LastRequest.Method);
            Assert.AreEqual("abc", loader.LastRequest.Headers["X-Test"]);

            using (var reader = new StreamReader(loader.LastRequest.Body, Encoding.UTF8, true, 1024, true))
            {
                Assert.AreEqual("payload", reader.ReadToEnd());
            }
        }

        [Test]
        public async Task FetchShouldSetMultipartContentTypeForFormDataWhenMissing()
        {
            var loader = new TestDocumentLoader(request =>
                new TestDownload(Task.FromResult<IResponse>(CreateResponse("https://example.org/form", "ok"))));

            var window = await OpenWindowAsync(loader, "https://example.org/base/page").ConfigureAwait(false);
            var formData = new FormDataSet();
            formData.Append("name", "Ada", "text/plain");

            await window.Fetch("/form", new FetchOptions
            {
                Method = "POST",
                Body = formData,
            }).ConfigureAwait(false);

            Assert.IsTrue(loader.LastRequest.Headers.ContainsKey(HeaderNames.ContentType));
            Assert.IsTrue(loader.LastRequest.Headers[HeaderNames.ContentType].StartsWith("multipart/form-data; boundary="));
        }

        [Test]
        public async Task FetchShouldNotOverrideExplicitContentType()
        {
            var loader = new TestDocumentLoader(request =>
                new TestDownload(Task.FromResult<IResponse>(CreateResponse("https://example.org/form", "ok"))));

            var window = await OpenWindowAsync(loader, "https://example.org/base/page").ConfigureAwait(false);
            var formData = new FormDataSet();
            formData.Append("name", "Ada", "text/plain");

            await window.Fetch("/form", new FetchOptions
            {
                Method = "POST",
                Body = formData,
                Headers = new Dictionary<String, String>
                {
                    [HeaderNames.ContentType] = "application/custom"
                }
            }).ConfigureAwait(false);

            Assert.AreEqual("application/custom", loader.LastRequest.Headers[HeaderNames.ContentType]);
        }

        [Test]
        public void FetchShouldThrowWhenWindowIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () => await WindowExtensions.Fetch(null, "https://example.org"));
        }

        [Test]
        public async Task FetchShouldThrowWhenLoaderIsMissing()
        {
            var context = BrowsingContext.New();
            var document = await context.OpenAsync(req => req.Address("https://example.org/").Content("<!doctype html><title>fetch</title>")).ConfigureAwait(false);

            Assert.ThrowsAsync<InvalidOperationException>(async () => await document.DefaultView.Fetch("https://example.org/any").ConfigureAwait(false));
        }

        private static async Task<IWindow> OpenWindowAsync(IDocumentLoader loader, String address)
        {
            var config = Configuration.Default.WithOnly<IDocumentLoader>(_ => loader);
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Address(address).Content("<!doctype html><title>fetch</title>")).ConfigureAwait(false);
            return document.DefaultView;
        }

        private static DefaultResponse CreateResponse(String address, String body)
        {
            return new DefaultResponse
            {
                Address = Url.Create(address),
                StatusCode = System.Net.HttpStatusCode.OK,
                Headers = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase),
                Content = new MemoryStream(Encoding.UTF8.GetBytes(body ?? String.Empty)),
            };
        }

        private sealed class TestDocumentLoader : IDocumentLoader
        {
            private readonly Func<DocumentRequest, IDownload> _createDownload;

            public TestDocumentLoader(Func<DocumentRequest, IDownload> createDownload)
            {
                _createDownload = createDownload;
            }

            public DocumentRequest LastRequest { get; private set; }

            public IDownload FetchAsync(DocumentRequest request)
            {
                LastRequest = request;
                return _createDownload.Invoke(request);
            }

            public IEnumerable<IDownload> GetDownloads()
            {
                yield break;
            }
        }

        private sealed class TestDownload : IDownload
        {
            private readonly Task<IResponse> _task;

            public TestDownload(Task<IResponse> task)
            {
                _task = task;
                Target = Url.Create("https://example.org/");
            }

            public Url Target { get; }

            public Object Source => null;

            public Task<IResponse> Task => _task;

            public Boolean IsCompleted => _task.IsCompleted;

            public Boolean IsRunning => !_task.IsCompleted;

            public void Cancel()
            {
            }
        }
    }
}