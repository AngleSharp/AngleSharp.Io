namespace AngleSharp.Io.Tests.Dom
{
    using AngleSharp.Browser;
    using AngleSharp.Common;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using AngleSharp.Io;
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class ProgressEventTests
    {
        [Test]
        public void ConstructorUsesDefaultsWithoutInit()
        {
            var ev = new ProgressEvent("x");

            Assert.AreEqual("x", ev.Type);
            Assert.AreEqual(0, ev.Loaded);
            Assert.AreEqual(0, ev.Total);
            Assert.IsFalse(ev.IsLengthComputable);
            Assert.IsInstanceOf<Event>(ev);
        }

        [Test]
        public void ConstructorKeepsTheGivenValues()
        {
            var ev = new ProgressEvent("progress", bubbles: true, cancelable: true, lengthComputable: true, loaded: 10, total: 40);

            Assert.AreEqual("progress", ev.Type);
            Assert.IsTrue(ev.IsBubbling);
            Assert.IsTrue(ev.IsCancelable);
            Assert.IsTrue(ev.IsLengthComputable);
            Assert.AreEqual(10, ev.Loaded);
            Assert.AreEqual(40, ev.Total);
        }

        [Test]
        public async Task SuccessfulRequestFiresProgressEventsInSpecOrder()
        {
            var response = CreateResponse("https://api.example.test/data", "hello world", HttpStatusCode.OK);
            response.Headers["Content-Length"] = "11";

            var loader = new TestDocumentLoader(request => new TestDownload(request.Target, Task.FromResult<IResponse>(response)));
            var xhr = await CreateRequesterAsync(loader).ConfigureAwait(false);
            var recorder = new EventRecorder(xhr);

            xhr.Open("GET", "https://api.example.test/data");
            xhr.Send();

            await recorder.WaitForLoadEnd().ConfigureAwait(false);

            CollectionAssert.AreEqual(
                new[]
                {
                    "readystatechange:1",
                    "loadstart",
                    "readystatechange:2",
                    "readystatechange:3",
                    "progress",
                    "readystatechange:4",
                    "load",
                    "loadend",
                },
                recorder.Names);
        }

        [Test]
        public async Task LoadEventCarriesLoadedAndTotalFromContentLength()
        {
            var response = CreateResponse("https://api.example.test/data", "hello world", HttpStatusCode.OK);
            response.Headers["Content-Length"] = "11";

            var loader = new TestDocumentLoader(request => new TestDownload(request.Target, Task.FromResult<IResponse>(response)));
            var xhr = await CreateRequesterAsync(loader).ConfigureAwait(false);
            var recorder = new EventRecorder(xhr);

            xhr.Open("GET", "https://api.example.test/data");
            xhr.Send();

            await recorder.WaitForLoadEnd().ConfigureAwait(false);

            var load = recorder.Get("load");
            Assert.IsTrue(load.IsLengthComputable);
            Assert.AreEqual(11, load.Loaded);
            Assert.AreEqual(11, load.Total);
        }

        [Test]
        public async Task MissingContentLengthMakesTheLengthNotComputable()
        {
            var response = CreateResponse("https://api.example.test/data", "hello world", HttpStatusCode.OK);

            var loader = new TestDocumentLoader(request => new TestDownload(request.Target, Task.FromResult<IResponse>(response)));
            var xhr = await CreateRequesterAsync(loader).ConfigureAwait(false);
            var recorder = new EventRecorder(xhr);

            xhr.Open("GET", "https://api.example.test/data");
            xhr.Send();

            await recorder.WaitForLoadEnd().ConfigureAwait(false);

            var load = recorder.Get("load");
            Assert.IsFalse(load.IsLengthComputable);
            Assert.AreEqual(11, load.Loaded);
            Assert.AreEqual(0, load.Total);
        }

        [Test]
        public async Task ContentEncodingMakesTheLengthNotComputable()
        {
            var response = CreateResponse("https://api.example.test/data", "hello world", HttpStatusCode.OK);
            response.Headers["Content-Length"] = "5";
            response.Headers["Content-Encoding"] = "gzip";

            var loader = new TestDocumentLoader(request => new TestDownload(request.Target, Task.FromResult<IResponse>(response)));
            var xhr = await CreateRequesterAsync(loader).ConfigureAwait(false);
            var recorder = new EventRecorder(xhr);

            xhr.Open("GET", "https://api.example.test/data");
            xhr.Send();

            await recorder.WaitForLoadEnd().ConfigureAwait(false);

            var load = recorder.Get("load");
            Assert.IsFalse(load.IsLengthComputable);
            Assert.AreEqual(0, load.Total);
        }

        [Test]
        public async Task FailingRequestFiresErrorThenLoadEnd()
        {
            var failed = Task.FromException<IResponse>(new InvalidOperationException("boom"));
            var loader = new TestDocumentLoader(request => new TestDownload(request.Target, failed));
            var xhr = await CreateRequesterAsync(loader).ConfigureAwait(false);
            var recorder = new EventRecorder(xhr);

            xhr.Open("GET", "https://api.example.test/data");
            xhr.Send();

            await recorder.WaitForLoadEnd().ConfigureAwait(false);

            CollectionAssert.AreEqual(
                new[] { "readystatechange:1", "loadstart", "readystatechange:4", "error", "loadend" },
                recorder.Names);
        }

        [Test]
        public async Task TimedOutRequestFiresTimeoutThenLoadEnd()
        {
            var canceled = Task.FromCanceled<IResponse>(new CancellationToken(canceled: true));
            var loader = new TestDocumentLoader(request => new TestDownload(request.Target, canceled));
            var xhr = await CreateRequesterAsync(loader).ConfigureAwait(false);
            var recorder = new EventRecorder(xhr);

            xhr.Open("GET", "https://api.example.test/data");
            xhr.Send();

            await recorder.WaitForLoadEnd().ConfigureAwait(false);

            CollectionAssert.AreEqual(
                new[] { "readystatechange:1", "loadstart", "readystatechange:4", "timeout", "loadend" },
                recorder.Names);
        }

        [Test]
        public async Task AbortWhileLoadingFiresAbortThenLoadEndWithoutLoad()
        {
            var content = new BlockingStream(Encoding.UTF8.GetBytes("hello"));
            var response = CreateResponse("https://api.example.test/data", content);
            var loader = new TestDocumentLoader(request => new TestDownload(request.Target, Task.FromResult<IResponse>(response)));
            var xhr = await CreateRequesterAsync(loader).ConfigureAwait(false);
            var recorder = new EventRecorder(xhr);

            xhr.Open("GET", "https://api.example.test/data");
            xhr.Send();

            await content.FirstRead.Task.ConfigureAwait(false);
            xhr.Abort();

            await recorder.WaitForLoadEnd().ConfigureAwait(false);
            content.Release();

            Assert.IsNull(recorder.Find("load"));
            var abort = recorder.Get("abort");
            var index = recorder.Names.IndexOf("abort");
            Assert.AreEqual("loadend", recorder.Names[index + 1]);
            Assert.AreEqual(5, abort.Loaded);
            Assert.AreEqual(RequesterState.Done, xhr.ReadyState);
        }

        [Test]
        public async Task ChunkedResponseFiresProgressAndGrowsResponseText()
        {
            var chunks = new[] { "first ", "second ", "third" };
            var content = new ChunkedStream(chunks, TimeSpan.FromMilliseconds(70));
            var response = CreateResponse("https://api.example.test/data", content);
            response.Headers["Content-Length"] = "18";

            var loader = new TestDocumentLoader(request => new TestDownload(request.Target, Task.FromResult<IResponse>(response)));
            var xhr = await CreateRequesterAsync(loader).ConfigureAwait(false);
            var recorder = new EventRecorder(xhr);
            var textDuringProgress = new List<String>();
            xhr.Progress += (_, __) => textDuringProgress.Add(xhr.ResponseText);

            xhr.Open("GET", "https://api.example.test/data");
            xhr.Send();

            await recorder.WaitForLoadEnd().ConfigureAwait(false);

            Assert.AreEqual("first second third", xhr.ResponseText);
            Assert.GreaterOrEqual(textDuringProgress.Count, 2);
            Assert.IsTrue(textDuringProgress.Exists(text => text.Length > 0 && text.Length < 18), String.Join("|", textDuringProgress));
            var load = recorder.Get("load");
            Assert.AreEqual(18, load.Loaded);
        }

        [Test]
        public async Task UploadIsNotNullAndSettingHandlersDoesNotThrow()
        {
            var loader = new TestDocumentLoader(request => new TestDownload(request.Target, Task.FromResult<IResponse>(CreateResponse("https://api.example.test/data", "ok", HttpStatusCode.OK))));
            var xhr = await CreateRequesterAsync(loader).ConfigureAwait(false);

            Assert.IsNotNull(xhr.Upload);
            xhr.Upload.Progress += (_, __) => { };
        }

        [Test]
        public async Task RequestWithBodyFiresUploadEvents()
        {
            var loader = new TestDocumentLoader(request =>
            {
                //  the transmission is the requester reading the body
                using (var reader = new StreamReader(request.Body, Encoding.UTF8, false, 1024, true))
                {
                    reader.ReadToEnd();
                }

                return new TestDownload(request.Target, Task.FromResult<IResponse>(CreateResponse("https://api.example.test/data", "ok", HttpStatusCode.OK)));
            });

            var xhr = await CreateRequesterAsync(loader).ConfigureAwait(false);
            var recorder = new EventRecorder(xhr);
            var uploadEvents = new List<String>();
            var uploadLoaded = new List<Int64>();
            DomEventHandler record = (_, ev) =>
            {
                lock (uploadEvents)
                {
                    uploadEvents.Add(ev.Type);
                    uploadLoaded.Add(((ProgressEvent)ev).Loaded);
                }
            };
            xhr.Upload.Start += record;
            xhr.Upload.Loaded += record;
            xhr.Upload.End += record;

            xhr.Open("POST", "https://api.example.test/data");
            xhr.Send("payload");

            await recorder.WaitForLoadEnd().ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "loadstart", "load", "loadend" }, uploadEvents);
            Assert.AreEqual(0, uploadLoaded[0]);
            Assert.AreEqual(7, uploadLoaded[1]);
            Assert.AreEqual(7, uploadLoaded[2]);
        }

        [Test]
        public async Task RequestWithoutBodyFiresNoUploadEvents()
        {
            var loader = new TestDocumentLoader(request => new TestDownload(request.Target, Task.FromResult<IResponse>(CreateResponse("https://api.example.test/data", "ok", HttpStatusCode.OK))));
            var xhr = await CreateRequesterAsync(loader).ConfigureAwait(false);
            var recorder = new EventRecorder(xhr);
            var uploadEvents = new List<String>();
            xhr.Upload.Start += (_, ev) => uploadEvents.Add(ev.Type);
            xhr.Upload.End += (_, ev) => uploadEvents.Add(ev.Type);

            xhr.Open("GET", "https://api.example.test/data");
            xhr.Send();

            await recorder.WaitForLoadEnd().ConfigureAwait(false);

            Assert.IsEmpty(uploadEvents);
        }

        #region Helpers

        private static async Task<XmlHttpRequest> CreateRequesterAsync(IDocumentLoader loader)
        {
            var config = Configuration.Default
                .WithOnly<IDocumentLoader>(_ => loader)
                .WithOnly<IEventLoop>(_ => new ImmediateEventLoop());

            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Address("https://origin.example.test/").Content("<!doctype html>")).ConfigureAwait(false);
            return new XmlHttpRequest(document.DefaultView);
        }

        private static DefaultResponse CreateResponse(String address, String body, HttpStatusCode statusCode) =>
            CreateResponse(address, new MemoryStream(Encoding.UTF8.GetBytes(body ?? String.Empty)));

        private static DefaultResponse CreateResponse(String address, Stream content) => new DefaultResponse
        {
            Address = Url.Create(address),
            StatusCode = HttpStatusCode.OK,
            Headers = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase),
            Content = content,
        };

        private sealed class EventRecorder
        {
            private readonly List<String> _names = new List<String>();
            private readonly Dictionary<String, ProgressEvent> _events = new Dictionary<String, ProgressEvent>();
            private readonly TaskCompletionSource<Object> _loadEnd = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public EventRecorder(XmlHttpRequest xhr)
            {
                foreach (var name in new[] { "loadstart", "progress", "load", "error", "abort", "timeout", "loadend" })
                {
                    xhr.AddEventListener(name, (_, ev) =>
                    {
                        lock (_names)
                        {
                            //  the throttled progress events between the first and the last
                            //  are timing dependent, so only one of them is recorded
                            if (ev.Type != "progress" || !_events.ContainsKey("progress"))
                            {
                                _names.Add(ev.Type);
                            }

                            _events[ev.Type] = (ProgressEvent)ev;
                        }

                        if (ev.Type == "loadend")
                        {
                            _loadEnd.TrySetResult(null);
                        }
                    });
                }

                xhr.ReadyStateChanged += (_, __) =>
                {
                    lock (_names)
                    {
                        _names.Add("readystatechange:" + (Int32)xhr.ReadyState);
                    }
                };
            }

            public List<String> Names
            {
                get
                {
                    lock (_names)
                    {
                        return new List<String>(_names);
                    }
                }
            }

            public Task WaitForLoadEnd() => Task.WhenAny(_loadEnd.Task, Task.Delay(5000)).ContinueWith(t =>
            {
                if (!_loadEnd.Task.IsCompleted)
                {
                    Assert.Fail("The loadend event was not fired. Events: {0}", String.Join(", ", Names));
                }
            });

            public ProgressEvent Get(String name)
            {
                var found = Find(name);
                Assert.IsNotNull(found, "The {0} event was not fired. Events: {1}", name, String.Join(", ", Names));
                return found;
            }

            public ProgressEvent Find(String name)
            {
                lock (_names)
                {
                    return _events.TryGetValue(name, out var ev) ? ev : null;
                }
            }
        }

        private sealed class ChunkedStream : Stream
        {
            private readonly Queue<Byte[]> _chunks;
            private readonly TimeSpan _delay;

            public ChunkedStream(String[] chunks, TimeSpan delay)
            {
                _chunks = new Queue<Byte[]>();
                _delay = delay;

                foreach (var chunk in chunks)
                {
                    _chunks.Enqueue(Encoding.UTF8.GetBytes(chunk));
                }
            }

            public override Boolean CanRead => true;

            public override Boolean CanSeek => false;

            public override Boolean CanWrite => false;

            public override Int64 Length => throw new NotSupportedException();

            public override Int64 Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
            {
                if (_chunks.Count == 0)
                {
                    return 0;
                }

                Thread.Sleep(_delay);
                var chunk = _chunks.Dequeue();
                Array.Copy(chunk, 0, buffer, offset, chunk.Length);
                return chunk.Length;
            }

            public override Int64 Seek(Int64 offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(Int64 value) => throw new NotSupportedException();

            public override void Write(Byte[] buffer, Int32 offset, Int32 count) => throw new NotSupportedException();
        }

        private sealed class BlockingStream : Stream
        {
            private readonly SemaphoreSlim _release = new SemaphoreSlim(0);
            private readonly Byte[] _first;
            private Int32 _reads;

            public BlockingStream(Byte[] first)
            {
                _first = first;
            }

            public TaskCompletionSource<Object> FirstRead { get; } = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public void Release() => _release.Release();

            public override Boolean CanRead => true;

            public override Boolean CanSeek => false;

            public override Boolean CanWrite => false;

            public override Int64 Length => throw new NotSupportedException();

            public override Int64 Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
            {
                if (_reads++ == 0)
                {
                    Array.Copy(_first, 0, buffer, offset, _first.Length);
                    FirstRead.TrySetResult(null);
                    return _first.Length;
                }

                _release.Wait();
                return 0;
            }

            public override Int64 Seek(Int64 offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(Int64 value) => throw new NotSupportedException();

            public override void Write(Byte[] buffer, Int32 offset, Int32 count) => throw new NotSupportedException();
        }

        private sealed class TestDocumentLoader : IDocumentLoader
        {
            private readonly Func<DocumentRequest, IDownload> _createDownload;

            public TestDocumentLoader(Func<DocumentRequest, IDownload> createDownload)
            {
                _createDownload = createDownload;
            }

            public IDownload FetchAsync(DocumentRequest request) => _createDownload.Invoke(request);

            public IEnumerable<IDownload> GetDownloads()
            {
                yield break;
            }
        }

        private sealed class TestDownload : IDownload
        {
            public TestDownload(Url target, Task<IResponse> task)
            {
                Target = target;
                Task = task;
            }

            public Url Target { get; }

            public Object Source => null;

            public Task<IResponse> Task { get; }

            public Boolean IsCompleted => Task.IsCompleted;

            public Boolean IsRunning => !Task.IsCompleted;

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

        #endregion
    }
}
