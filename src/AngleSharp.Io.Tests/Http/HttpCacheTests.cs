namespace AngleSharp.Io.Tests.Http
{
    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Io.Http;
    using AngleSharp.Io.Network;
    using AngleSharp.Io.Tests.Mocks;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using HttpMethod = AngleSharp.Io.HttpMethod;

    [TestFixture]
    public class HttpCacheTests
    {
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

        [Test]
        public async Task FreshResponseIsServedFromCache()
        {
            var server = new Server(_ => Ok("hello", ("Cache-Control", "max-age=60")));
            var cache = CreateCache(out var clock);

            var first = await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            clock.Advance(TimeSpan.FromSeconds(30));
            var second = await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(1, server.Requests.Count);
            Assert.AreEqual("hello", Read(first));
            Assert.AreEqual("hello", Read(second));
            Assert.AreEqual("30", second.Headers[HeaderNames.Age]);
        }

        [Test]
        public async Task FragmentIsIgnoredForTheKey()
        {
            var server = new Server(_ => Ok("hello", ("Cache-Control", "max-age=60")));
            var cache = CreateCache(out _);

            await cache.SendAsync(Get("https://a.example/page#one"), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://a.example/page#two"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(1, server.Requests.Count);
        }

        [Test]
        public async Task ExpiredResponseWithoutValidatorIsFetchedAgain()
        {
            var server = new Server(_ => Ok("hello", ("Cache-Control", "max-age=10")));
            var cache = CreateCache(out var clock);

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            clock.Advance(TimeSpan.FromSeconds(11));
            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(2, server.Requests.Count);
            Assert.IsFalse(server.Requests[1].Headers.ContainsKey(HeaderNames.IfNoneMatch));
        }

        [Test]
        public async Task PostIsNeitherCachedNorServedFromCache()
        {
            var server = new Server(_ => Ok("hello", ("Cache-Control", "max-age=60")));
            var cache = CreateCache(out _);

            await cache.SendAsync(Create(HttpMethod.Post, "https://a.example/form"), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Create(HttpMethod.Post, "https://a.example/form"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(2, server.Requests.Count);
            Assert.IsNull(await cache.MatchAsync(Get("https://a.example/form"), CancellationToken.None));
        }

        [Test]
        public async Task SuccessfulPostInvalidatesTargetAndLocation()
        {
            var server = new Server(request => request.Method == HttpMethod.Post ?
                Ok("created", ("Location", "/items/1")) :
                Ok("item", ("Cache-Control", "max-age=60")));
            var cache = CreateCache(out _);

            await cache.SendAsync(Get("https://a.example/items"), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://a.example/items/1"), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Create(HttpMethod.Post, "https://a.example/items"), server.SendAsync, CancellationToken.None);

            Assert.IsNull(await cache.MatchAsync(Get("https://a.example/items"), CancellationToken.None));
            Assert.IsNull(await cache.MatchAsync(Get("https://a.example/items/1"), CancellationToken.None));
        }

        [Test]
        public async Task NoStoreResponseIsNotStored()
        {
            var server = new Server(_ => Ok("secret", ("Cache-Control", "no-store, max-age=60")));
            var cache = CreateCache(out _);

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(2, server.Requests.Count);
        }

        [Test]
        public async Task PrivateResponseIsStoredByPrivateCache()
        {
            var server = new Server(_ => Ok("mine", ("Cache-Control", "private, max-age=60")));
            var cache = CreateCache(out _);

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(1, server.Requests.Count);
        }

        [Test]
        public async Task AuthorizedRequestIsOnlyStoredWithPublicResponse()
        {
            var server = new Server(_ => Ok("data", ("Cache-Control", "max-age=60")));
            var cache = CreateCache(out _);

            await cache.SendAsync(Get("https://a.example/a", ("Authorization", "Bearer x")), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://a.example/a", ("Authorization", "Bearer x")), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(2, server.Requests.Count);

            server.Respond = _ => Ok("data", ("Cache-Control", "public, max-age=60"));
            await cache.SendAsync(Get("https://a.example/b", ("Authorization", "Bearer x")), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://a.example/b", ("Authorization", "Bearer x")), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(3, server.Requests.Count);
        }

        [Test]
        public async Task StaleResponseIsRevalidatedWithValidators()
        {
            var server = new Server(_ => Ok("original", ("Cache-Control", "max-age=10"), ("ETag", "\"v1\""), ("Last-Modified", "Sat, 01 Aug 2026 10:00:00 GMT")));
            var cache = CreateCache(out var clock);

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            clock.Advance(TimeSpan.FromSeconds(20));
            server.Respond = _ => Response(HttpStatusCode.NotModified, String.Empty, ("Cache-Control", "max-age=100"), ("ETag", "\"v1\""));
            var revalidated = await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            clock.Advance(TimeSpan.FromSeconds(50));
            var fresh = await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(2, server.Requests.Count);
            Assert.AreEqual("\"v1\"", server.Requests[1].Headers[HeaderNames.IfNoneMatch]);
            Assert.AreEqual("Sat, 01 Aug 2026 10:00:00 GMT", server.Requests[1].Headers[HeaderNames.IfModifiedSince]);
            Assert.AreEqual(HttpStatusCode.OK, revalidated.StatusCode);
            Assert.AreEqual("original", Read(revalidated));
            Assert.AreEqual("max-age=100", revalidated.Headers[HeaderNames.CacheControl]);
            Assert.AreEqual("original", Read(fresh));
        }

        [Test]
        public async Task ChangedResponseReplacesStoredOneOnRevalidation()
        {
            var server = new Server(_ => Ok("v1", ("Cache-Control", "max-age=0"), ("ETag", "\"v1\"")));
            var cache = CreateCache(out _);

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            server.Respond = _ => Ok("v2", ("Cache-Control", "max-age=60"), ("ETag", "\"v2\""));
            var changed = await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            var stored = await cache.MatchAsync(Get("https://a.example/page"), CancellationToken.None);

            Assert.AreEqual("v2", Read(changed));
            Assert.AreEqual("v2", Read(stored));
        }

        [Test]
        public async Task ConditionalRequestOfCallerIsPassedThrough()
        {
            var server = new Server(_ => Ok("v1", ("Cache-Control", "max-age=0"), ("ETag", "\"v1\"")));
            var cache = CreateCache(out _);

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            server.Respond = _ => Response(HttpStatusCode.NotModified, String.Empty);
            var response = await cache.SendAsync(Get("https://a.example/page", ("If-None-Match", "\"other\"")), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.NotModified, response.StatusCode);
            Assert.AreEqual("\"other\"", server.Requests[1].Headers[HeaderNames.IfNoneMatch]);
        }

        [Test]
        public async Task HeuristicFreshnessUsesLastModified()
        {
            // the Date is 10 days after Last-Modified, i.e., fresh for 1 day
            var server = new Server(_ => Ok("page", ("Date", "Mon, 19 Oct 2026 12:00:00 GMT"), ("Last-Modified", "Fri, 09 Oct 2026 12:00:00 GMT")));
            var cache = CreateCache(out var clock);

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            clock.Advance(TimeSpan.FromHours(23));
            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(1, server.Requests.Count);

            clock.Advance(TimeSpan.FromHours(2));
            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(2, server.Requests.Count);
            Assert.AreEqual("Fri, 09 Oct 2026 12:00:00 GMT", server.Requests[1].Headers[HeaderNames.IfModifiedSince]);
        }

        [Test]
        public async Task HeuristicFreshnessCanBeDisabled()
        {
            var server = new Server(_ => Ok("page", ("Date", "Mon, 19 Oct 2026 12:00:00 GMT"), ("Last-Modified", "Fri, 09 Oct 2026 12:00:00 GMT")));
            var cache = CreateCache(out _, new HttpCacheOptions { UseHeuristicFreshness = false });

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(2, server.Requests.Count);
        }

        [Test]
        public async Task ExpiresHeaderIsRespected()
        {
            var server = new Server(_ => Ok("page", ("Date", "Mon, 19 Oct 2026 12:00:00 GMT"), ("Expires", "Mon, 19 Oct 2026 12:01:00 GMT")));
            var cache = CreateCache(out var clock);

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            clock.Advance(TimeSpan.FromSeconds(59));
            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            clock.Advance(TimeSpan.FromSeconds(2));
            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(2, server.Requests.Count);
        }

        [Test]
        public async Task VaryKeepsSeparateVariants()
        {
            var server = new Server(request => Ok(request.Headers["Accept-Language"], ("Cache-Control", "max-age=60"), ("Vary", "Accept-Language")));
            var cache = CreateCache(out _);

            await cache.SendAsync(Get("https://a.example/page", ("Accept-Language", "en")), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://a.example/page", ("Accept-Language", "de")), server.SendAsync, CancellationToken.None);
            var en = await cache.SendAsync(Get("https://a.example/page", ("Accept-Language", "en")), server.SendAsync, CancellationToken.None);
            var de = await cache.SendAsync(Get("https://a.example/page", ("Accept-Language", "de")), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(2, server.Requests.Count);
            Assert.AreEqual("en", Read(en));
            Assert.AreEqual("de", Read(de));
        }

        [Test]
        public async Task VaryStarIsNotStored()
        {
            var server = new Server(_ => Ok("page", ("Cache-Control", "max-age=60"), ("Vary", "*")));
            var cache = CreateCache(out _);

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(2, server.Requests.Count);
        }

        [Test]
        public async Task SetCookieIsNotReplayedFromCache()
        {
            var server = new Server(_ => Ok("page", ("Cache-Control", "max-age=60"), ("Set-Cookie", "a=b")));
            var cache = CreateCache(out _);

            var first = await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            var second = await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual("a=b", first.Headers[HeaderNames.SetCookie]);
            Assert.IsFalse(second.Headers.ContainsKey(HeaderNames.SetCookie));
        }

        [Test]
        public async Task RequestNoCacheForcesRevalidation()
        {
            var server = new Server(_ => Ok("page", ("Cache-Control", "max-age=60"), ("ETag", "\"v1\"")));
            var cache = CreateCache(out _);

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://a.example/page", ("Cache-Control", "no-cache")), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://a.example/page", ("Pragma", "no-cache")), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(3, server.Requests.Count);
            Assert.AreEqual("\"v1\"", server.Requests[1].Headers[HeaderNames.IfNoneMatch]);
        }

        [Test]
        public async Task RequestMaxStaleAcceptsStaleResponse()
        {
            var server = new Server(_ => Ok("page", ("Cache-Control", "max-age=10")));
            var cache = CreateCache(out var clock);

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            clock.Advance(TimeSpan.FromSeconds(30));
            await cache.SendAsync(Get("https://a.example/page", ("Cache-Control", "max-stale=60")), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(1, server.Requests.Count);
        }

        [Test]
        public async Task OnlyIfCachedAnswersWithGatewayTimeoutOnMiss()
        {
            var server = new Server(_ => Ok("page", ("Cache-Control", "max-age=60")));
            var cache = CreateCache(out _, new HttpCacheOptions { Mode = HttpCacheMode.OnlyIfCached });

            var response = await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.GatewayTimeout, response.StatusCode);
            Assert.AreEqual(0, server.Requests.Count);
        }

        [Test]
        public async Task ForceCacheServesStaleResponses()
        {
            var server = new Server(_ => Ok("page", ("Cache-Control", "max-age=1")));
            var cache = CreateCache(out var clock, new HttpCacheOptions { Mode = HttpCacheMode.ForceCache });

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            clock.Advance(TimeSpan.FromDays(30));
            var response = await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(1, server.Requests.Count);
            Assert.AreEqual("page", Read(response));
        }

        [Test]
        public async Task ReloadAlwaysFetchesButStores()
        {
            var server = new Server(_ => Ok("page", ("Cache-Control", "max-age=60")));
            var cache = CreateCache(out _, new HttpCacheOptions { Mode = HttpCacheMode.Reload });

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(2, server.Requests.Count);
            Assert.IsNotNull(await cache.MatchAsync(Get("https://a.example/page"), CancellationToken.None));
        }

        [Test]
        public async Task NetworkFailureServesStaleResponse()
        {
            var server = new Server(_ => Ok("page", ("Cache-Control", "max-age=10")));
            var cache = CreateCache(out var clock);

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            clock.Advance(TimeSpan.FromSeconds(20));
            server.Respond = request => new DefaultResponse { Address = request.Address, StatusCode = 0 };
            var response = await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("page", Read(response));
        }

        [Test]
        public async Task HeadIsAnsweredFromStoredGet()
        {
            var server = new Server(_ => Ok("page", ("Cache-Control", "max-age=60")));
            var cache = CreateCache(out _);

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            var response = await cache.SendAsync(Create(HttpMethod.Head, "https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(1, server.Requests.Count);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(String.Empty, Read(response));
        }

        [Test]
        public async Task LargeResponseIsPassedThroughCompletely()
        {
            var text = new String('x', 1000);
            var server = new Server(_ => Ok(text, ("Cache-Control", "max-age=60")));
            var cache = CreateCache(out _, new HttpCacheOptions { MaxEntrySize = 100 });

            var response = await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(text, Read(response));
            Assert.IsNull(await cache.MatchAsync(Get("https://a.example/page"), CancellationToken.None));
        }

        [Test]
        public async Task NonHttpRequestsBypassTheCache()
        {
            var server = new Server(_ => Ok("file", ("Cache-Control", "max-age=60")));
            var cache = CreateCache(out _);

            await cache.SendAsync(Get("file:///tmp/a.txt"), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("file:///tmp/a.txt"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(2, server.Requests.Count);
        }

        [Test]
        public async Task BypassPredicateExcludesRequests()
        {
            var server = new Server(_ => Ok("page", ("Cache-Control", "max-age=60")));
            var cache = CreateCache(out _, new HttpCacheOptions { Bypass = request => request.Address.Path.StartsWith("api") });

            await cache.SendAsync(Get("https://a.example/api/x"), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://a.example/api/x"), server.SendAsync, CancellationToken.None);

            Assert.AreEqual(2, server.Requests.Count);
        }

        [Test]
        public async Task EntriesAreKeptPerOrigin()
        {
            var handler = new MemoryHttpCacheHandler();
            var server = new Server(_ => Ok("page", ("Cache-Control", "max-age=60")));
            var cache = CreateCache(out _, new HttpCacheOptions { Handler = handler });

            await cache.SendAsync(Get("https://a.example/page"), server.SendAsync, CancellationToken.None);
            await cache.SendAsync(Get("https://b.example/page"), server.SendAsync, CancellationToken.None);

            CollectionAssert.AreEquivalent(new[] { "https://a.example", "https://b.example" }, handler.GetOrigins().ToArray());

            await cache.ClearAsync("https://a.example", CancellationToken.None);

            Assert.IsNull(await cache.MatchAsync(Get("https://a.example/page"), CancellationToken.None));
            Assert.IsNotNull(await cache.MatchAsync(Get("https://b.example/page"), CancellationToken.None));

            await cache.ClearAsync(CancellationToken.None);

            Assert.IsEmpty(handler.GetOrigins());
        }

        [Test]
        public async Task PersistedEntriesSurviveANewCache()
        {
            var directory = Path.Combine(Path.GetTempPath(), "anglesharp-http-cache-" + Guid.NewGuid().ToString("N"));

            try
            {
                var server = new Server(_ => Ok("persisted", ("Cache-Control", "max-age=60"), ("Vary", "Accept"), ("Set-Cookie", "a=b")));
                var first = CreateCache(out _, new HttpCacheOptions { Handler = new PersistenceHttpCacheHandler(directory) });
                await first.SendAsync(Get("https://a.example/page", ("Accept", "text/html")), server.SendAsync, CancellationToken.None);

                var handler = new PersistenceHttpCacheHandler(directory);
                var second = CreateCache(out _, new HttpCacheOptions { Handler = handler });
                var response = await second.SendAsync(Get("https://a.example/page", ("Accept", "text/html")), server.SendAsync, CancellationToken.None);

                Assert.AreEqual(1, server.Requests.Count);
                Assert.AreEqual("persisted", Read(response));
                Assert.IsFalse(response.Headers.ContainsKey(HeaderNames.SetCookie));
                CollectionAssert.AreEqual(new[] { "https://a.example" }, handler.GetOrigins().ToArray());
                CollectionAssert.AreEqual(new[] { "https://a.example/page" }, handler.GetKeys("https://a.example").ToArray());

                await second.ClearAsync(CancellationToken.None);

                Assert.IsEmpty(handler.GetOrigins());
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void WithHttpCacheWrapsHttpRequestersOnly()
        {
            var config = Configuration.Default.WithRequesters().WithHttpCache();
            var requesters = config.Services.OfType<IRequester>().ToArray();

            Assert.IsInstanceOf<CachingRequester>(requesters[0]);
            Assert.IsInstanceOf<HttpClientRequester>(((CachingRequester)requesters[0]).Inner);
            Assert.AreEqual(1, requesters.OfType<CachingRequester>().Count());
            Assert.AreEqual(1, config.Services.OfType<IHttpCache>().Count());
        }

        [Test]
        public void WithHttpCacheTwiceDoesNotWrapTwice()
        {
            var cache = new HttpCache();
            var config = Configuration.Default.WithRequesters().WithHttpCache().WithHttpCache(cache);
            var caching = config.Services.OfType<CachingRequester>().Single();

            Assert.AreSame(cache, caching.Cache);
            Assert.IsInstanceOf<HttpClientRequester>(caching.Inner);
            Assert.AreSame(cache, config.Services.OfType<IHttpCache>().Single());
        }

        [Test]
        public void WithIoWithoutHttpCacheOptionsDoesNotWrap()
        {
            var config = Configuration.Default.WithIo(new IoOptions());

            Assert.IsEmpty(config.Services.OfType<CachingRequester>());
        }

        [Test]
        public void TemporaryIoOptionsEnableTheHttpCache()
        {
            var config = Configuration.Default.WithIo(IoOptions.CreateTemporary());

            Assert.AreEqual(1, config.Services.OfType<CachingRequester>().Count());
        }

        [Test]
        public void PersistentIoOptionsShareOneRootDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "anglesharp-io-" + Guid.NewGuid().ToString("N"));

            try
            {
                var options = IoOptions.CreatePersistent(directory);

                Assert.IsInstanceOf<PersistenceHttpCacheHandler>(options.HttpCache.Handler);
                Assert.IsTrue(Directory.Exists(Path.Combine(directory, "http-cache")));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public async Task DocumentLoadsAreCachedThroughConfiguration()
        {
            var requester = new MockRequester();
            var count = 0;
            requester.OnRequest = _ => count++;
            requester.BuildResponse(request => new DefaultResponse
            {
                Address = request.Address,
                StatusCode = HttpStatusCode.OK,
                Headers = new Dictionary<String, String>
                {
                    { "Cache-Control", "max-age=60" },
                    { "Content-Type", "text/html" },
                },
                Content = new MemoryStream(Encoding.UTF8.GetBytes("<title>Cached</title>")),
            });
            var config = Configuration.Default.WithDefaultLoader().WithRequester(requester).WithHttpCache();
            var context = BrowsingContext.New(config);

            var first = await context.OpenAsync("https://a.example/");
            var second = await context.OpenAsync("https://a.example/");

            Assert.AreEqual(1, count);
            Assert.AreEqual("Cached", first.Title);
            Assert.AreEqual("Cached", second.Title);
        }

        private static HttpCache CreateCache(out TestClock clock, HttpCacheOptions options = null)
        {
            var current = new TestClock(Start);
            clock = current;
            return new HttpCache(options ?? new HttpCacheOptions())
            {
                Clock = () => current.Now,
            };
        }

        private static Request Get(String address, params (String, String)[] headers) =>
            Create(HttpMethod.Get, address, headers);

        private static Request Create(HttpMethod method, String address, params (String, String)[] headers)
        {
            var request = new Request
            {
                Method = method,
                Address = new Url(address),
            };

            foreach (var (name, value) in headers)
            {
                request.Headers[name] = value;
            }

            return request;
        }

        private static DefaultResponse Ok(String content, params (String, String)[] headers) =>
            Response(HttpStatusCode.OK, content, headers);

        private static DefaultResponse Response(HttpStatusCode status, String content, params (String, String)[] headers)
        {
            // HttpClientRequester creates case-sensitive dictionaries, too
            var response = new DefaultResponse
            {
                StatusCode = status,
                Headers = new Dictionary<String, String>(),
                Content = new MemoryStream(Encoding.UTF8.GetBytes(content)),
            };

            foreach (var (name, value) in headers)
            {
                response.Headers[name] = value;
            }

            return response;
        }

        private static String Read(IResponse response)
        {
            using (var reader = new StreamReader(response.Content))
            {
                return reader.ReadToEnd();
            }
        }

        private sealed class TestClock
        {
            public TestClock(DateTimeOffset now) => Now = now;

            public DateTimeOffset Now { get; private set; }

            public void Advance(TimeSpan time) => Now += time;
        }

        private sealed class Server
        {
            public Server(Func<Request, DefaultResponse> respond) => Respond = respond;

            public Func<Request, DefaultResponse> Respond { get; set; }

            public List<Request> Requests { get; } = new List<Request>();

            public Task<IResponse> SendAsync(Request request, CancellationToken cancel)
            {
                Requests.Add(request);
                var response = Respond.Invoke(request);
                response.Address = response.Address ?? request.Address;
                return Task.FromResult<IResponse>(response);
            }
        }
    }
}
