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

        [Test]
        public async Task FetchAsyncShouldGiveTheStatusHeadersAndText()
        {
            var loader = new TestDocumentLoader(request =>
            {
                var response = CreateResponse("https://example.org/api/data", "h\u00e9llo");
                response.StatusCode = System.Net.HttpStatusCode.NotFound;
                response.Headers["Content-Type"] = "text/plain; charset=utf-8";
                return new TestDownload(Task.FromResult<IResponse>(response));
            });

            var window = await OpenWindowAsync(loader, "https://example.org/base/page").ConfigureAwait(false);
            var response = await window.FetchAsync("/api/data").ConfigureAwait(false);

            Assert.AreEqual(404, response.Status);
            Assert.AreEqual("Not Found", response.StatusText);
            Assert.IsFalse(response.IsOk);
            Assert.IsFalse(response.IsRedirected);
            Assert.AreEqual("https://example.org/api/data", response.Url);
            Assert.AreEqual("text/plain; charset=utf-8", response.Headers.Get("content-type"));
            Assert.IsFalse(response.Headers.Has("X-Missing"));
            Assert.AreEqual("h\u00e9llo", await response.Text().ConfigureAwait(false));
            Assert.IsTrue(response.IsBodyUsed);
            Assert.ThrowsAsync<InvalidOperationException>(() => response.Text());
        }

        [Test]
        public async Task FetchAsyncShouldReadJson()
        {
            var body = "{ \"token\": \"abc\", \"user\": null, \"count\": 2.5, \"tags\": [\"a\", true, {}], \"escaped\": \"\\u00e9\\n\" }";
            var loader = new TestDocumentLoader(request =>
                new TestDownload(Task.FromResult<IResponse>(CreateResponse("https://example.org/api/data", body))));

            var window = await OpenWindowAsync(loader, "https://example.org/base/page").ConfigureAwait(false);
            var response = await window.FetchAsync("/api/data").ConfigureAwait(false);
            var json = (IDictionary<String, Object>)await response.Json().ConfigureAwait(false);

            Assert.IsTrue(response.IsOk);
            Assert.AreEqual("abc", json["token"]);
            Assert.IsNull(json["user"]);
            Assert.AreEqual(2.5, json["count"]);
            Assert.AreEqual(new Object[] { "a", true, new Dictionary<String, Object>() }, json["tags"]);
            Assert.AreEqual("\u00e9\n", json["escaped"]);
        }

        [Test]
        public async Task FetchAsyncShouldRejectInvalidJson()
        {
            var loader = new TestDocumentLoader(request =>
                new TestDownload(Task.FromResult<IResponse>(CreateResponse("https://example.org/api/data", "{ \"a\": "))));

            var window = await OpenWindowAsync(loader, "https://example.org/base/page").ConfigureAwait(false);
            var response = await window.FetchAsync("/api/data").ConfigureAwait(false);

            Assert.ThrowsAsync<FormatException>(() => response.Json());
        }

        [Test]
        public async Task FormDataShouldTakeTheControlsOfTheForm()
        {
            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Content(@"<form id=f>
                <input name=a value=1><input name=b value=2 disabled><input type=checkbox name=c checked><input type=checkbox name=d>
                <input type=submit name=s value=go><select name=e multiple><option selected>x<option>y<option selected value=z>Z</select>
                <textarea name=f>text</textarea><input value=unnamed></form>")).ConfigureAwait(false);
            var form = (AngleSharp.Html.Dom.IHtmlFormElement)document.GetElementById("f");

            var data = new FormData(form);

            Assert.AreEqual("1", data.Get("a"));
            Assert.IsFalse(data.Has("b"));
            Assert.AreEqual("on", data.Get("c"));
            Assert.IsFalse(data.Has("d"));
            Assert.IsFalse(data.Has("s"));
            Assert.AreEqual(new[] { "x", "z" }, data.GetAll("e"));
            Assert.AreEqual("text", data.Get("f"));
        }

        [Test]
        public void FormDataShouldAppendSetAndDelete()
        {
            var data = new FormData();
            data.Append("a", "1");
            data.Append("b", "2");
            data.Append("a", "3");

            data.Set("a", "4");
            Assert.AreEqual(new[] { "4" }, data.GetAll("a"));

            data.Delete("b");
            Assert.IsFalse(data.Has("b"));
            Assert.IsNull(data.Get("b"));
        }

        [Test]
        public async Task FetchShouldSendFormDataAsMultipart()
        {
            var loader = new TestDocumentLoader(request =>
                new TestDownload(Task.FromResult<IResponse>(CreateResponse("https://example.org/form", "ok"))));

            var window = await OpenWindowAsync(loader, "https://example.org/base/page").ConfigureAwait(false);
            var data = new FormData();
            data.Append("name", "Ada");

            await window.FetchAsync("/form", new FetchOptions { Method = "POST", Body = data }).ConfigureAwait(false);

            Assert.IsTrue(loader.LastRequest.Headers[HeaderNames.ContentType].StartsWith("multipart/form-data; boundary="));

            using (var reader = new StreamReader(loader.LastRequest.Body, Encoding.UTF8, true, 1024, true))
            {
                StringAssert.Contains("name=\"name\"", reader.ReadToEnd());
            }
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