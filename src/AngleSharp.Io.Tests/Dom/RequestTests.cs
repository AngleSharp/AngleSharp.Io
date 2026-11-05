namespace AngleSharp.Io.Tests.Dom
{
    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    [TestFixture]
    public class RequestTests
    {
        private sealed class RecordingLoader : IDocumentLoader
        {
            public DocumentRequest Last { get; private set; }

            public IDownload FetchAsync(DocumentRequest request)
            {
                Last = request;
                return new CompletedDownload(request.Target);
            }

            public IEnumerable<IDownload> GetDownloads()
            {
                yield break;
            }
        }

        private sealed class CompletedDownload : IDownload
        {
            public CompletedDownload(Url target)
            {
                Target = target;
                Task = System.Threading.Tasks.Task.FromResult<IResponse>(new DefaultResponse
                {
                    Address = target,
                    StatusCode = HttpStatusCode.OK,
                    Content = new MemoryStream(Encoding.UTF8.GetBytes("done")),
                });
            }

            public Url Target { get; }

            public Object Source => null;

            public Task<IResponse> Task { get; }

            public Boolean IsCompleted => true;

            public Boolean IsRunning => false;

            public void Cancel()
            {
            }
        }

        private static async Task<(IWindow, RecordingLoader)> CreateAsync()
        {
            var loader = new RecordingLoader();
            var config = Configuration.Default.WithOnly<IDocumentLoader>(_ => loader);
            var document = await BrowsingContext.New(config).OpenAsync(m => m.Address("https://example.org/dir/page.html").Content("<!doctype html>"));
            return (document.DefaultView, loader);
        }

        private static async Task<IWindow> CreateWindowAsync() => (await CreateAsync()).Item1;

        private static String ReadBody(DocumentRequest request)
        {
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true))
            {
                return reader.ReadToEnd();
            }
        }

        [Test]
        public async Task AddressIsResolvedAgainstTheDocument()
        {
            var request = new FetchRequest(await CreateWindowAsync(), "other.json");

            Assert.AreEqual("https://example.org/dir/other.json", request.Url);
            Assert.AreEqual("GET", request.Method);
            Assert.AreEqual("cors", request.Mode);
            Assert.IsNotNull(request.Signal);
            Assert.IsFalse(request.Signal.IsAborted);
        }

        [Test]
        public async Task AddressCanBeAnUrl()
        {
            var request = new FetchRequest(await CreateWindowAsync(), new Url("https://example.com/x?y=1"));

            Assert.AreEqual("https://example.com/x?y=1", request.Url);
        }

        [TestCase("post", "POST")]
        [TestCase("Delete", "DELETE")]
        [TestCase("patch", "patch")]
        public async Task MethodIsNormalized(String given, String expected)
        {
            var request = new FetchRequest(await CreateWindowAsync(), "a", new FetchOptions { Method = given });

            Assert.AreEqual(expected, request.Method);
        }

        [TestCase("TRACE")]
        [TestCase("connect")]
        public async Task ForbiddenMethodIsRejected(String method)
        {
            var window = await CreateWindowAsync();

            Assert.Throws<InvalidOperationException>(() => new FetchRequest(window, "a", new FetchOptions { Method = method }));
        }

        [Test]
        public async Task GetWithABodyIsRejected()
        {
            var window = await CreateWindowAsync();

            Assert.Throws<InvalidOperationException>(() => new FetchRequest(window, "a", new FetchOptions { Body = "x" }));
        }

        [Test]
        public async Task TextBodyGetsItsTypeAndCanBeReadOnce()
        {
            var request = new FetchRequest(await CreateWindowAsync(), "a", new FetchOptions { Method = "POST", Body = "hällo" });

            Assert.AreEqual("text/plain;charset=UTF-8", request.Headers.Get("content-type"));
            Assert.IsFalse(request.IsBodyUsed);
            Assert.AreEqual("hällo", await request.Text());
            Assert.IsTrue(request.IsBodyUsed);
            Assert.Throws<InvalidOperationException>(() => request.Text());
        }

        [Test]
        public async Task GivenContentTypeIsKept()
        {
            var options = new FetchOptions { Method = "POST", Body = "{}", Headers = new Dictionary<String, String> { ["Content-Type"] = "application/json" } };
            var request = new FetchRequest(await CreateWindowAsync(), "a", options);

            Assert.AreEqual("application/json", request.Headers.Get("content-type"));
        }

        [Test]
        public async Task CopyTakesEverythingAndOptionsReplaceIt()
        {
            var window = await CreateWindowAsync();
            var controller = new AbortController();
            var source = new FetchRequest(window, "a", new FetchOptions { Method = "POST", Body = "x", Signal = controller.Signal, Mode = "no-cors" });
            var copy = new FetchRequest(window, source, new FetchOptions { Mode = "same-origin" });

            Assert.AreEqual(source.Url, copy.Url);
            Assert.AreEqual("POST", copy.Method);
            Assert.AreSame(controller.Signal, copy.Signal);
            Assert.AreEqual("same-origin", copy.Mode);
            Assert.AreEqual("x", await copy.Text());
            Assert.IsTrue(source.IsBodyUsed);
        }

        [Test]
        public async Task CloneHasABodyOfItsOwn()
        {
            var request = new FetchRequest(await CreateWindowAsync(), "a", new FetchOptions { Method = "POST", Body = "x" });
            var clone = request.Clone();
            clone.Headers.Set("x-a", "1");

            Assert.AreEqual("x", await clone.Text());
            Assert.AreEqual("x", await request.Text());
            Assert.IsFalse(request.Headers.Has("x-a"));
            Assert.Throws<InvalidOperationException>(() => request.Clone());
        }

        [Test]
        public async Task JsonBodyIsParsed()
        {
            var request = new FetchRequest(await CreateWindowAsync(), "a", new FetchOptions { Method = "POST", Body = "{\"a\":1}" });
            var value = (IDictionary<String, Object>)await request.Json();

            Assert.AreEqual(1.0, value["a"]);
        }

        [Test]
        public async Task FetchSendsTheRequest()
        {
            var (window, requester) = await CreateAsync();
            var request = new FetchRequest(window, "api", new FetchOptions
            {
                Method = "POST",
                Body = "payload",
                Headers = new Dictionary<String, String> { ["X-Custom"] = "1" },
            });

            var response = await window.FetchAsync((Object)request);

            Assert.AreEqual("done", await response.Text());
            Assert.AreEqual(HttpMethod.Post, requester.Last.Method);
            Assert.AreEqual("https://example.org/dir/api", requester.Last.Target.Href);
            Assert.AreEqual("payload", ReadBody(requester.Last));
            Assert.AreEqual("1", requester.Last.Headers["X-Custom"]);
            Assert.AreEqual("text/plain;charset=UTF-8", requester.Last.Headers["Content-Type"]);
            Assert.IsTrue(request.IsBodyUsed);
        }

        [Test]
        public async Task FetchHonorsTheSignalOfTheRequest()
        {
            var (window, _) = await CreateAsync();
            var controller = new AbortController();
            controller.Abort();
            var request = new FetchRequest(window, "api", new FetchOptions { Signal = controller.Signal });

            try
            {
                await window.FetchAsync((Object)request);
                Assert.Fail("Expected the fetch to be aborted.");
            }
            catch (DomException ex)
            {
                Assert.AreEqual((Int32)DomError.Abort, ex.Code);
            }
        }

        [Test]
        public async Task FetchTakesAnUrlObject()
        {
            var (window, requester) = await CreateAsync();

            await window.FetchAsync((Object)new Url("https://example.com/u"));

            Assert.AreEqual("https://example.com/u", requester.Last.Target.Href);
        }

        [Test]
        public void HeadersAreChangedRegardlessOfCase()
        {
            var headers = new Headers();
            headers.Set("X-A", "1");
            headers.Append("x-a", "2");
            headers.Append("B", "3");

            Assert.AreEqual("1, 2", headers.Get("x-A"));
            headers.Set("x-a", "4");
            Assert.AreEqual("4", headers.Get("X-A"));
            headers.Delete("X-a");
            Assert.IsFalse(headers.Has("x-a"));
            Assert.IsTrue(headers.Has("b"));
        }

        [Test]
        public void HeadersIterateSortedInLowerCase()
        {
            Headers headers = new[] { new[] { "Z", "1" }, new[] { "a", "2" } };
            var seen = new List<String>();
            headers.ForEach((value, name, _) => seen.Add(name + "=" + value));

            Assert.AreEqual(new[] { "a", "z" }, headers.Keys());
            Assert.AreEqual(new[] { "2", "1" }, headers.Values());
            Assert.AreEqual(new[] { "a", "2" }, headers.Entries()[0]);
            Assert.AreEqual(new[] { "a=2", "z=1" }, seen);
        }

        [Test]
        public void HeadersCopyDoesNotAffectTheOriginal()
        {
            Headers original = new Dictionary<String, String> { ["a"] = "1" };
            var copy = new Headers(original);
            copy.Set("a", "2");

            Assert.AreEqual("1", original.Get("a"));
            Assert.AreEqual("2", copy.Get("a"));
        }

        [Test]
        public async Task ResponseCanBeCreated()
        {
            var response = new Response("text", new ResponseInit
            {
                Status = 404,
                StatusText = "Gone",
                Headers = new Dictionary<String, String> { ["X-A"] = "1" },
            });

            Assert.AreEqual(404, response.Status);
            Assert.AreEqual("Gone", response.StatusText);
            Assert.AreEqual("default", response.Type);
            Assert.IsFalse(response.IsOk);
            Assert.AreEqual(String.Empty, response.Url);
            Assert.AreEqual("1", response.Headers.Get("x-a"));
            Assert.AreEqual("text/plain;charset=UTF-8", response.Headers.Get("content-type"));
            Assert.AreEqual("text", await response.Text());
        }

        [Test]
        public async Task ResponseCanHaveNoBody()
        {
            var response = new Response((Object)null, new ResponseInit { Status = 302, Headers = new Dictionary<String, String> { ["Location"] = "/x" } });

            Assert.AreEqual(302, response.Status);
            Assert.AreEqual("/x", response.Headers.Get("location"));
            Assert.AreEqual(String.Empty, await response.Text());
        }

        [Test]
        public async Task ClonedResponsesReadTheBodyIndependently()
        {
            var response = new Response("text", new ResponseInit { Headers = new Dictionary<String, String> { ["a"] = "1" } });
            var clone = response.Clone();
            var second = clone.Clone();
            clone.Headers.Set("a", "2");

            Assert.AreEqual("text", await clone.Text());
            Assert.AreEqual("text", await response.Text());
            Assert.AreEqual("text", await second.Text());
            Assert.AreEqual("1", response.Headers.Get("a"));
            Assert.IsTrue(response.IsBodyUsed);
            Assert.Throws<InvalidOperationException>(() => response.Clone());
        }

        [Test]
        public async Task ResponseWithoutABodyCanBeCloned()
        {
            var clone = new Response().Clone();

            Assert.AreEqual(String.Empty, await clone.Text());
        }

        [TestCase(199)]
        [TestCase(600)]
        public void ResponseWithAStatusOutOfRangeIsRejected(Int32 status)
        {
            Assert.Throws<InvalidOperationException>(() => new Response((Object)null, new ResponseInit { Status = status }));
        }

        [Test]
        public void FormDataIsIterated()
        {
            var data = new FormData();
            data.Append("a", "1");
            data.Append("b", "2");
            var seen = new List<String>();
            data.ForEach((value, name, _) => seen.Add(name + value));

            Assert.AreEqual(new[] { "a", "b" }, data.Keys());
            Assert.AreEqual(new[] { "1", "2" }, data.Values());
            Assert.AreEqual(new[] { "b", "2" }, data.Entries()[1]);
            Assert.AreEqual(new[] { "a1", "b2" }, seen);
        }
    }
}
