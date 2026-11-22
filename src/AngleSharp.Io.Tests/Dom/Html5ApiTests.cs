namespace AngleSharp.Io.Tests.Dom
{
    using AngleSharp.Browser.Dom;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.Tests.Mocks;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// The APIs of HTML5 that pages test for (e.g. html5test.com): server-sent events, the
    /// application cache, the device events and the callbacks of the geolocation.
    /// </summary>
    [TestFixture]
    public class Html5ApiTests
    {
        [Test]
        public async Task EventSourceDispatchesTheEventsOfTheStream()
        {
            var stream = "retry: 60000\n: a comment\ndata: first\n\nevent: custom\nid: 7\ndata: a\ndata: b\n\ndata: unfinished";
            var requests = new List<Request>();
            var window = await OpenWindowAsync(request =>
            {
                requests.Add(request);
                return CreateResponse(request, stream, "text/event-stream");
            }).ConfigureAwait(false);
            var source = new EventSource(window, "/events");
            var messages = new List<String>();
            var opened = false;
            var done = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.AddEventListener("open", (s, e) => opened = true);
            source.AddEventListener("message", (s, e) => messages.Add("message:" + ((MessageEvent)e).Data));
            source.AddEventListener("custom", (s, e) =>
            {
                var ev = (MessageEvent)e;
                messages.Add("custom:" + ev.Data + "#" + ev.LastEventId);
                done.TrySetResult(true);
            });

            await Task.WhenAny(done.Task, Task.Delay(5000)).ConfigureAwait(false);
            source.Close();

            Assert.IsTrue(opened);
            CollectionAssert.AreEqual(new[] { "message:first", "custom:a\nb#7" }, messages);
            Assert.AreEqual("https://example.org/events", source.Url);
            Assert.AreEqual(EventSourceReadyState.Closed, source.ReadyState);
            Assert.AreEqual("text/event-stream", requests[0].Headers[HeaderNames.Accept]);
        }

        [Test]
        public async Task EventSourceFailsForAnotherType()
        {
            var window = await OpenWindowAsync(request => CreateResponse(request, "data: x\n\n", "text/plain")).ConfigureAwait(false);
            var source = new EventSource(window, "/events");
            var failed = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.AddEventListener("error", (s, e) => failed.TrySetResult(true));

            await Task.WhenAny(failed.Task, Task.Delay(5000)).ConfigureAwait(false);

            Assert.IsTrue(failed.Task.IsCompleted);
            Assert.AreEqual(EventSourceReadyState.Closed, source.ReadyState);
        }

        [Test]
        public async Task ApplicationCacheDownloadsTheEntriesOfTheManifest()
        {
            var requested = new List<String>();
            var window = await OpenWindowAsync(request =>
            {
                requested.Add(request.Address.Path);
                return request.Address.Path == "app.appcache"
                    ? CreateResponse(request, "CACHE MANIFEST\n# v1\nstyle.css\nNETWORK:\n*\nFALLBACK:\n/ offline.html\n", "text/cache-manifest")
                    : CreateResponse(request, "x", "text/plain");
            }, "<!doctype html><html manifest=app.appcache><title>app</title>").ConfigureAwait(false);
            var cache = window.ApplicationCache();
            var events = new List<String>();
            var done = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

            foreach (var type in new[] { "checking", "downloading", "progress", "cached", "error" })
            {
                cache.AddEventListener(type, (s, e) =>
                {
                    events.Add(type);

                    if (type is "cached" or "error")
                    {
                        done.TrySetResult(true);
                    }
                });
            }

            await Task.WhenAny(done.Task, Task.Delay(5000)).ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "checking", "downloading", "progress", "cached" }, events);
            Assert.AreEqual(CacheStatus.Idle, cache.Status);
            CollectionAssert.Contains(requested, "style.css");
            CollectionAssert.DoesNotContain(requested, "offline.html");
        }

        [Test]
        public async Task ApplicationCacheWithoutManifestIsUncached()
        {
            var window = await OpenWindowAsync(request => CreateResponse(request, "x", "text/plain")).ConfigureAwait(false);
            var cache = window.ApplicationCache();

            Assert.AreEqual(CacheStatus.Uncached, cache.Status);
            Assert.Throws<DomException>(() => cache.Update());
        }

        [Test]
        public async Task GeolocationWithoutPlatformIsThereButUnavailable()
        {
            var window = await OpenWindowAsync(request => CreateResponse(request, "x", "text/plain"), withNavigator: true).ConfigureAwait(false);
            var geolocation = window.Navigator.GeolocationOrUnavailable();
            var failed = new TaskCompletionSource<GeolocationPositionError>(TaskCreationOptions.RunContinuationsAsynchronously);

            geolocation.GetCurrentPosition(position => Assert.Fail("No position expected."), error => failed.TrySetResult(error));
            var result = await Task.WhenAny(failed.Task, Task.Delay(5000)).ConfigureAwait(false);

            Assert.IsNull(window.Navigator.Geolocation());
            Assert.AreSame(failed.Task, result);
            Assert.AreEqual(GeolocationPositionErrorCode.PositionUnavailable, failed.Task.Result.Code);
        }

        [Test]
        public void DeviceEventsTakeTheirReadings()
        {
            var orientation = new DeviceOrientationEvent("deviceorientation", alpha: 10, beta: 20, gamma: 30, absolute: true);
            var motion = new DeviceMotionEvent("devicemotion", acceleration: new DeviceMotionEventAcceleration { X = 1 }, interval: 16);

            Assert.AreEqual(10, orientation.Alpha);
            Assert.AreEqual(30, orientation.Gamma);
            Assert.IsTrue(orientation.IsAbsolute);
            Assert.AreEqual(1, motion.Acceleration.X);
            Assert.IsNull(motion.RotationRate);
            Assert.AreEqual(16, motion.Interval);
        }

        [Test]
        public async Task FetchOfAnEmptyAddressFetchesTheDocument()
        {
            var requested = new List<String>();
            var window = await OpenWindowAsync(request =>
            {
                requested.Add(request.Address.Href);
                return CreateResponse(request, "x", "text/plain");
            }).ConfigureAwait(false);

            var response = await window.Fetch(String.Empty, null).ConfigureAwait(false);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.Contains(requested, "https://example.org/page");
        }

        [Test]
        public void ManifestEntriesAreTheCacheSectionOfTheSameOrigin()
        {
            var manifest = "CACHE MANIFEST\na.css\nCACHE:\nb.js#x\nhttps://other.org/c.js\nNETWORK:\nd.php\nFALLBACK:\n/ e.html\nSETTINGS:\nprefer-online\n";
            var entries = ApplicationCache.ParseExplicitEntries(manifest, new Url("https://example.org/app/m.appcache"));

            CollectionAssert.AreEqual(new[] { "https://example.org/app/a.css", "https://example.org/app/b.js" }, entries.ConvertAll(m => m.Href));
        }

        private static async Task<IWindow> OpenWindowAsync(Func<Request, IResponse> respond, String content = "<!doctype html><title>page</title>", Boolean withNavigator = false)
        {
            var config = Configuration.Default.WithVirtualRequester(respond);

            if (withNavigator)
            {
                config = config.WithNavigator();
            }

            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(res => res.Address("https://example.org/page").Content(content)).ConfigureAwait(false);
            return document.DefaultView;
        }

        private static IResponse CreateResponse(Request request, String body, String type) => new DefaultResponse
        {
            Address = request.Address,
            StatusCode = HttpStatusCode.OK,
            Headers = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                { HeaderNames.ContentType, type },
            },
            Content = new MemoryStream(Encoding.UTF8.GetBytes(body)),
        };
    }
}
