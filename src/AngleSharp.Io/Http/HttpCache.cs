namespace AngleSharp.Io.Http
{
    using AngleSharp.Dom;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using HttpMethod = AngleSharp.Io.HttpMethod;

    /// <summary>
    /// Represents a private HTTP cache following RFC 9111. Entries are kept
    /// per origin of the requested address.
    /// </summary>
    public class HttpCache : IHttpCache
    {
        #region Fields

        private readonly HttpCacheOptions _options;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new HTTP cache keeping its entries in memory.
        /// </summary>
        public HttpCache()
            : this(new HttpCacheOptions())
        {
        }

        /// <summary>
        /// Creates a new HTTP cache with the given options.
        /// </summary>
        /// <param name="options">The options to use.</param>
        public HttpCache(HttpCacheOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (_options.Handler == null)
            {
                throw new ArgumentException("A handler for storing the cache entries is required.", nameof(options));
            }
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        public HttpCacheOptions Options => _options;

        internal Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.UtcNow;

        private IHttpCacheHandler Handler => _options.Handler;

        #endregion

        #region Methods

        /// <inheritdoc />
        public async Task<IResponse> SendAsync(Request request, Func<Request, CancellationToken, Task<IResponse>> network, CancellationToken cancel)
        {
            var address = request.Address;

            if (address == null || !IsHttp(address.Scheme) || (_options.Bypass?.Invoke(request) ?? false))
            {
                return await network.Invoke(request, cancel).ConfigureAwait(false);
            }

            if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
            {
                var result = await network.Invoke(request, cancel).ConfigureAwait(false);

                if (IsUnsafe(request.Method) && result != null)
                {
                    await InvalidateAsync(address, result, cancel).ConfigureAwait(false);
                }

                return result;
            }

            var requestControl = CacheControl.Parse(request.Headers);
            var mode = GetMode(requestControl);

            if (mode == HttpCacheMode.NoStore)
            {
                return await network.Invoke(request, cancel).ConfigureAwait(false);
            }

            var origin = GetOrigin(address);
            var key = GetKey(address);
            var variants = await Handler.ReadAsync(origin, key, cancel).ConfigureAwait(false);
            var stored = mode != HttpCacheMode.Reload ? variants.FirstOrDefault(m => HttpCachePolicy.MatchesVary(m, request)) : null;

            if (stored != null)
            {
                var now = Clock.Invoke();

                if (mode == HttpCacheMode.ForceCache || mode == HttpCacheMode.OnlyIfCached ||
                    (mode == HttpCacheMode.Default && HttpCachePolicy.IsFreshEnough(stored, requestControl, now, _options.UseHeuristicFreshness)))
                {
                    return CreateResponse(stored, request.Method, now);
                }
            }
            else if (mode == HttpCacheMode.OnlyIfCached)
            {
                return new DefaultResponse
                {
                    Address = address,
                    StatusCode = HttpStatusCode.GatewayTimeout,
                    Content = Stream.Null,
                };
            }

            // a request carrying its own validators is answered by the server
            var validating = stored != null && HttpCachePolicy.HasValidator(stored) && !HttpCachePolicy.IsConditional(request);
            var networkRequest = validating ? CreateConditionalRequest(request, stored) : request;
            var requestTime = Clock.Invoke();
            var response = await network.Invoke(networkRequest, cancel).ConfigureAwait(false);
            var responseTime = Clock.Invoke();

            if (response == null)
            {
                return null;
            }

            if (validating && response.StatusCode == HttpStatusCode.NotModified)
            {
                var updated = HttpCachePolicy.Update(stored, response.Headers, requestTime, responseTime);
                response.Dispose();
                await StoreAsync(origin, key, variants, request, updated, cancel).ConfigureAwait(false);
                return CreateResponse(updated, request.Method, responseTime);
            }

            // the requesters report network failures with status 0 (#28), in
            // which case a stale response is better than none (RFC 9111, 4.2.4)
            if (stored != null && response.StatusCode == 0 && HttpCachePolicy.MayServeStale(stored))
            {
                response.Dispose();
                return CreateResponse(stored, request.Method, responseTime);
            }

            if (request.Method != HttpMethod.Get || requestControl.NoStore || !HttpCachePolicy.IsStorable(request, response.Headers, response.StatusCode))
            {
                return response;
            }

            return await BufferAndStoreAsync(request, response, origin, key, variants, requestTime, responseTime, cancel).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IResponse> MatchAsync(Request request, CancellationToken cancel)
        {
            var address = request.Address;

            if (address == null || !IsHttp(address.Scheme))
            {
                return null;
            }

            var variants = await Handler.ReadAsync(GetOrigin(address), GetKey(address), cancel).ConfigureAwait(false);
            var stored = variants.FirstOrDefault(m => HttpCachePolicy.MatchesVary(m, request));
            return stored != null ? CreateResponse(stored, request.Method, Clock.Invoke()) : null;
        }

        /// <inheritdoc />
        public async Task<Boolean> RemoveAsync(Url address, CancellationToken cancel)
        {
            var origin = GetOrigin(address);
            var key = GetKey(address);
            var variants = await Handler.ReadAsync(origin, key, cancel).ConfigureAwait(false);

            if (variants.Count > 0)
            {
                await Handler.WriteAsync(origin, key, null, cancel).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public async Task ClearAsync(String origin, CancellationToken cancel)
        {
            foreach (var key in Handler.GetKeys(origin).ToArray())
            {
                await Handler.WriteAsync(origin, key, null, cancel).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task ClearAsync(CancellationToken cancel)
        {
            foreach (var origin in Handler.GetOrigins().ToArray())
            {
                await ClearAsync(origin, cancel).ConfigureAwait(false);
            }
        }

        #endregion

        #region Helpers

        private HttpCacheMode GetMode(CacheControl requestControl)
        {
            var mode = _options.Mode;

            // the request directives only refine the default behavior, just
            // like the Fetch specification does for the default cache mode
            if (mode == HttpCacheMode.Default)
            {
                if (requestControl.NoStore)
                {
                    return HttpCacheMode.NoStore;
                }
                else if (requestControl.OnlyIfCached)
                {
                    return HttpCacheMode.OnlyIfCached;
                }
                else if (requestControl.NoCache)
                {
                    return HttpCacheMode.NoCache;
                }
            }

            return mode;
        }

        private async Task<IResponse> BufferAndStoreAsync(Request request, IResponse response, String origin, String key, IReadOnlyList<HttpCacheEntry> variants, DateTimeOffset requestTime, DateTimeOffset responseTime, CancellationToken cancel)
        {
            var content = response.Content;
            var limit = _options.MaxEntrySize;
            var body = Array.Empty<Byte>();

            if (content != null)
            {
                var length = HttpCachePolicy.GetHeader(response.Headers, HeaderNames.ContentLength);

                if (Int64.TryParse(length, out var size) && size > limit)
                {
                    return response;
                }

                var buffer = new MemoryStream();
                var chunk = new Byte[81920];

                while (true)
                {
                    var read = await content.ReadAsync(chunk, 0, chunk.Length, cancel).ConfigureAwait(false);

                    if (read == 0)
                    {
                        break;
                    }

                    buffer.Write(chunk, 0, read);

                    if (buffer.Length > limit)
                    {
                        return Copy(response, new PrefixedStream(buffer.ToArray(), content));
                    }
                }

                content.Dispose();
                body = buffer.ToArray();
            }

            var entry = HttpCachePolicy.CreateEntry(request, response, body, requestTime, responseTime);
            await StoreAsync(origin, key, variants, request, entry, cancel).ConfigureAwait(false);

            // the original headers are kept, such that the loader still sees
            // the Set-Cookie header of the network response
            return Copy(response, new MemoryStream(body, false));
        }

        private Task StoreAsync(String origin, String key, IReadOnlyList<HttpCacheEntry> variants, Request request, HttpCacheEntry entry, CancellationToken cancel)
        {
            // the new entry replaces all variants the request would have selected
            var remaining = variants.Where(m => !HttpCachePolicy.MatchesVary(m, request));
            return Handler.WriteAsync(origin, key, remaining.Concat(new[] { entry }).ToArray(), cancel);
        }

        private async Task InvalidateAsync(Url address, IResponse response, CancellationToken cancel)
        {
            var status = (Int32)response.StatusCode;

            if (status >= 200 && status < 400)
            {
                await RemoveAsync(address, cancel).ConfigureAwait(false);

                foreach (var name in new[] { HeaderNames.Location, HeaderNames.ContentLocation })
                {
                    var value = HttpCachePolicy.GetHeader(response.Headers, name);

                    if (!String.IsNullOrEmpty(value))
                    {
                        var target = new Url(address, value);

                        // only same-origin targets may be invalidated (RFC 9111, 4.4)
                        if (!target.IsInvalid && GetOrigin(target) == GetOrigin(address))
                        {
                            await RemoveAsync(target, cancel).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        private static Request CreateConditionalRequest(Request request, HttpCacheEntry stored)
        {
            var headers = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            foreach (var header in request.Headers)
            {
                headers[header.Key] = header.Value;
            }

            var etag = HttpCachePolicy.GetHeader(stored.ResponseHeaders, HeaderNames.ETag);
            var lastModified = HttpCachePolicy.GetHeader(stored.ResponseHeaders, HeaderNames.LastModified);

            if (etag != null)
            {
                headers[HeaderNames.IfNoneMatch] = etag;
            }

            if (lastModified != null)
            {
                headers[HeaderNames.IfModifiedSince] = lastModified;
            }

            return new Request
            {
                Method = request.Method,
                Address = request.Address,
                Content = request.Content,
                Headers = headers,
            };
        }

        private static IResponse CreateResponse(HttpCacheEntry entry, HttpMethod method, DateTimeOffset now)
        {
            var headers = new Dictionary<String, String>(entry.ResponseHeaders, StringComparer.OrdinalIgnoreCase);
            var age = HttpCachePolicy.GetCurrentAge(entry, now);
            headers[HeaderNames.Age] = ((Int64)Math.Max(0, age.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);

            return new DefaultResponse
            {
                Address = new Url(entry.Address),
                StatusCode = entry.StatusCode,
                Headers = headers,
                Content = method == HttpMethod.Head ? Stream.Null : new MemoryStream(entry.Body, false),
            };
        }

        private static IResponse Copy(IResponse response, Stream content) => new DefaultResponse
        {
            Address = response.Address,
            StatusCode = response.StatusCode,
            Headers = response.Headers,
            Content = content,
        };

        private static Boolean IsHttp(String scheme) =>
            scheme.Equals(ProtocolNames.Http, StringComparison.OrdinalIgnoreCase) ||
            scheme.Equals(ProtocolNames.Https, StringComparison.OrdinalIgnoreCase);

        private static Boolean IsUnsafe(HttpMethod method) =>
            method != HttpMethod.Get && method != HttpMethod.Head &&
            method != HttpMethod.Options && method != HttpMethod.Trace;

        private static String GetOrigin(Url address) => address.Origin ?? String.Empty;

        private static String GetKey(Url address)
        {
            var href = address.Href;
            var index = href.IndexOf('#');
            return index >= 0 ? href.Substring(0, index) : href;
        }

        #endregion
    }
}
