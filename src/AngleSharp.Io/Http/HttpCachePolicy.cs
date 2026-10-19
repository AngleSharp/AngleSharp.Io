namespace AngleSharp.Io.Http
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;

    /// <summary>
    /// The storage and freshness rules of a private cache (RFC 9111).
    /// </summary>
    internal static class HttpCachePolicy
    {
        private static readonly TimeSpan MaxHeuristicLifetime = TimeSpan.FromDays(7);

        private static readonly String[] DateFormats = new[]
        {
            "r",
            "dddd, dd-MMM-yy HH:mm:ss 'GMT'",
            "ddd MMM d HH:mm:ss yyyy",
        };

        private static readonly HashSet<Int32> HeuristicallyCacheable = new HashSet<Int32>
        {
            200, 203, 204, 300, 301, 308, 404, 405, 410, 414, 501,
        };

        // never stored, since cookies are applied by the loader on every
        // response it sees, including those served from the cache
        private static readonly String[] UnstoredHeaders = new[]
        {
            HeaderNames.SetCookie,
            "Set-Cookie2",
        };

        // not taken from a 304 when updating a stored response (RFC 9111, 3.2)
        private static readonly String[] UnmergedHeaders = new[]
        {
            HeaderNames.ContentLength,
            HeaderNames.SetCookie,
            "Set-Cookie2",
            "Transfer-Encoding",
            HeaderNames.Connection,
            "Keep-Alive",
        };

        public static String GetHeader(IDictionary<String, String> headers, String name)
        {
            if (headers == null)
            {
                return null;
            }

            if (headers.TryGetValue(name, out var value))
            {
                return value;
            }

            // redirected requests and HttpClient responses may come with
            // case-sensitive dictionaries
            foreach (var header in headers)
            {
                if (header.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return header.Value;
                }
            }

            return null;
        }

        public static Boolean TryParseDate(String value, out DateTimeOffset result) =>
            DateTimeOffset.TryParseExact(value?.Trim(), DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);

        public static Boolean IsStorable(Request request, IDictionary<String, String> responseHeaders, HttpStatusCode status)
        {
            var code = (Int32)status;

            if (code < 200 || status == HttpStatusCode.NotModified || status == HttpStatusCode.PartialContent)
            {
                return false;
            }

            var control = CacheControl.Parse(responseHeaders);

            if (control.NoStore || GetVary(responseHeaders).Contains("*"))
            {
                return false;
            }

            if (GetHeader(request.Headers, HeaderNames.Authorization) != null &&
                !control.Public && !control.MustRevalidate && !control.SMaxAge.HasValue)
            {
                return false;
            }

            return HeuristicallyCacheable.Contains(code) || control.Public || control.MaxAge.HasValue ||
                GetHeader(responseHeaders, HeaderNames.Expires) != null;
        }

        public static Boolean HasValidator(HttpCacheEntry entry) =>
            GetHeader(entry.ResponseHeaders, HeaderNames.ETag) != null ||
            GetHeader(entry.ResponseHeaders, HeaderNames.LastModified) != null;

        public static Boolean IsConditional(Request request) =>
            GetHeader(request.Headers, HeaderNames.IfNoneMatch) != null ||
            GetHeader(request.Headers, HeaderNames.IfModifiedSince) != null ||
            GetHeader(request.Headers, HeaderNames.IfMatch) != null ||
            GetHeader(request.Headers, HeaderNames.IfUnmodifiedSince) != null ||
            GetHeader(request.Headers, "If-Range") != null;

        public static Boolean IsFreshEnough(HttpCacheEntry entry, CacheControl requestControl, DateTimeOffset now, Boolean heuristic)
        {
            var responseControl = CacheControl.Parse(entry.ResponseHeaders);

            if (responseControl.NoCache)
            {
                return false;
            }

            var lifetime = GetFreshnessLifetime(entry, responseControl, heuristic);
            var age = GetCurrentAge(entry, now);

            if (requestControl.MaxAge.HasValue && age > requestControl.MaxAge.Value)
            {
                return false;
            }

            if (requestControl.MinFresh.HasValue)
            {
                return lifetime - age >= requestControl.MinFresh.Value;
            }

            if (lifetime > age)
            {
                return true;
            }

            if (responseControl.MustRevalidate)
            {
                return false;
            }

            return requestControl.MaxStaleAny ||
                (requestControl.MaxStale.HasValue && age - lifetime <= requestControl.MaxStale.Value);
        }

        public static Boolean MayServeStale(HttpCacheEntry entry)
        {
            var control = CacheControl.Parse(entry.ResponseHeaders);
            return !control.MustRevalidate && !control.NoCache;
        }

        public static TimeSpan GetFreshnessLifetime(HttpCacheEntry entry, CacheControl control, Boolean heuristic)
        {
            // s-maxage only applies to shared caches
            if (control.MaxAge.HasValue)
            {
                return control.MaxAge.Value;
            }

            var date = GetDate(entry);
            var expires = GetHeader(entry.ResponseHeaders, HeaderNames.Expires);

            if (expires != null)
            {
                // invalid values (e.g., "0") represent a time in the past
                return TryParseDate(expires, out var expiresDate) && expiresDate > date ? expiresDate - date : TimeSpan.Zero;
            }

            var lastModified = GetHeader(entry.ResponseHeaders, HeaderNames.LastModified);

            if (heuristic && lastModified != null && TryParseDate(lastModified, out var modified) && modified < date &&
                (HeuristicallyCacheable.Contains((Int32)entry.StatusCode) || control.Public))
            {
                var lifetime = TimeSpan.FromTicks((date - modified).Ticks / 10);
                return lifetime < MaxHeuristicLifetime ? lifetime : MaxHeuristicLifetime;
            }

            return TimeSpan.Zero;
        }

        public static TimeSpan GetCurrentAge(HttpCacheEntry entry, DateTimeOffset now)
        {
            var date = GetDate(entry);
            var ageValue = TimeSpan.Zero;

            if (Int64.TryParse(GetHeader(entry.ResponseHeaders, HeaderNames.Age), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            {
                ageValue = TimeSpan.FromSeconds(Math.Min(seconds, Int32.MaxValue));
            }

            var apparentAge = Max(TimeSpan.Zero, entry.ResponseTime - date);
            var responseDelay = entry.ResponseTime - entry.RequestTime;
            var correctedAgeValue = ageValue + responseDelay;
            var correctedInitialAge = Max(apparentAge, correctedAgeValue);
            var residentTime = now - entry.ResponseTime;
            return correctedInitialAge + residentTime;
        }

        public static IEnumerable<String> GetVary(IDictionary<String, String> responseHeaders)
        {
            var vary = GetHeader(responseHeaders, HeaderNames.Vary);

            if (String.IsNullOrEmpty(vary))
            {
                return Enumerable.Empty<String>();
            }

            return vary.Split(',').Select(m => m.Trim()).Where(m => m.Length > 0);
        }

        public static Boolean MatchesVary(HttpCacheEntry entry, Request request)
        {
            foreach (var name in GetVary(entry.ResponseHeaders))
            {
                if (name == "*")
                {
                    return false;
                }

                var stored = Normalize(GetHeader(entry.RequestHeaders, name));
                var current = Normalize(GetHeader(request.Headers, name));

                if (!String.Equals(stored, current, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public static HttpCacheEntry CreateEntry(Request request, IResponse response, Byte[] body, DateTimeOffset requestTime, DateTimeOffset responseTime)
        {
            var entry = new HttpCacheEntry
            {
                Address = response.Address?.Href ?? request.Address.Href,
                StatusCode = response.StatusCode,
                RequestTime = requestTime,
                ResponseTime = responseTime,
                Body = body,
            };

            foreach (var header in response.Headers)
            {
                if (!UnstoredHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                {
                    entry.ResponseHeaders[header.Key] = header.Value;
                }
            }

            foreach (var name in GetVary(entry.ResponseHeaders))
            {
                var value = GetHeader(request.Headers, name);

                if (value != null)
                {
                    entry.RequestHeaders[name] = value;
                }
            }

            return entry;
        }

        public static HttpCacheEntry Update(HttpCacheEntry stored, IDictionary<String, String> notModifiedHeaders, DateTimeOffset requestTime, DateTimeOffset responseTime)
        {
            var entry = new HttpCacheEntry
            {
                Address = stored.Address,
                StatusCode = stored.StatusCode,
                RequestTime = requestTime,
                ResponseTime = responseTime,
                Body = stored.Body,
            };

            foreach (var header in stored.RequestHeaders)
            {
                entry.RequestHeaders[header.Key] = header.Value;
            }

            foreach (var header in stored.ResponseHeaders)
            {
                entry.ResponseHeaders[header.Key] = header.Value;
            }

            foreach (var header in notModifiedHeaders)
            {
                if (!UnmergedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                {
                    entry.ResponseHeaders[header.Key] = header.Value;
                }
            }

            return entry;
        }

        private static DateTimeOffset GetDate(HttpCacheEntry entry)
        {
            var value = GetHeader(entry.ResponseHeaders, HeaderNames.Date);
            return value != null && TryParseDate(value, out var date) ? date : entry.ResponseTime;
        }

        private static String Normalize(String value)
        {
            if (value == null)
            {
                return null;
            }

            return String.Join(" ", value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
    }
}
