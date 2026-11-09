namespace AngleSharp.Io.Cookie
{
    using AngleSharp.Dom;
    using AngleSharp.Text;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using static Helpers;
    using static NetscapeCookieSerializer;

    /// <summary>
    /// Represents an advanced cookie provider that allows
    /// persistence, snapshots, and more.
    /// Stores the cookies in the Netscape compatible file
    /// format.
    /// </summary>
    public class AdvancedCookieProvider : ICookieProvider
    {
        private readonly ICookieFileHandler _handler;
        private readonly Boolean _forceParse;
        private readonly Boolean _httpOnlyExtension;
        // the resources of a page are loaded in parallel, so the cookies are an array that is
        // never changed in place: a change swaps in a new array, and readers keep the one they got
        private WebCookie[] _cookies;
        private Int32 _pendingWrites;
        private Int32 _writing;

        /// <summary>
        /// Creates a new cookie provider with the given handler and options.
        /// </summary>
        /// <param name="handler">The handler responsible for file system interaction.</param>
        /// <param name="options">The options to use for the cookie provider.</param>
        public AdvancedCookieProvider(ICookieFileHandler handler, AdvancedCookieProviderOptions options = default)
        {
            _handler = handler;
            _forceParse = options.IsForceParse;
            _httpOnlyExtension = options.IsHttpOnlyExtension;
            _cookies = ReadCookies().ToArray();
        }

        String ICookieProvider.GetCookie(Url url)
        {
            var host = CanonicalDomain(url.HostName);
            var path = String.IsNullOrEmpty(url.Path) ? "/" : $"/{url.Path}";
            var secure = url.Scheme.IsOneOf(ProtocolNames.Https, ProtocolNames.Wss);
            var now = DateTime.UtcNow;
            var cookies = Volatile.Read(ref _cookies)
                .Where(c =>
                {
                    if (c.IsHostOnly ?? false)
                    {
                        if (!c.Domain.Is(host))
                        {
                            return false;
                        }
                    }
                    else if (!DomainMatch(host, c.Domain, false))
                    {
                        return false;
                    }

                    if (!CheckPaths(path, c.Path))
                    {
                        return false;
                    }

                    // "If the cookie's secure-only-flag is true, then the request-uri's
                    // scheme must denote a "secure" protocol"
                    if (c.IsSecure && !secure)
                    {
                        return false;
                    }

                    // deferred from Section 5.3
                    // non-RFC: allow retention of expired cookies by choice
                    if (c.ComputeExpiration(now) <= now)
                    {
                        RemoveCookie(c.Domain, c.Path, c.Key);
                        return false;
                    }

                    return true;
                })
                .Select(c => c.ToGetCookie())
                .ToArray();
            return String.Join("; ", cookies);
        }

        void ICookieProvider.SetCookie(Url url, String value)
        {
            var host = CanonicalDomain(url.HostName);
            var parser = new CookieParser(value);
            var cookies = parser.Parse();

            foreach (var cookie in cookies)
            {
                if (!String.IsNullOrEmpty(cookie.Domain))
                {
                    var cdom = cookie.CanonicalDomain;
                    var suffix = GetPublicSuffix(cdom);

                    if (suffix == null || !DomainMatch(host, cdom, false))
                    {
                        continue;
                    }

                    // don't reset if already set
                    if (!cookie.IsHostOnly.HasValue)
                    {
                        cookie.IsHostOnly = false;
                    }
                }
                else
                {
                    cookie.Domain = host;
                    cookie.IsHostOnly = true;
                }

                if (String.IsNullOrEmpty(cookie.Path) || cookie.Path[0] != '/')
                {
                    cookie.Path = GetDefaultPath($"/{url.Path}");
                }

                AddCookie(cookie);
            }
        }

        /// <summary>
        /// Gets the available cookies.
        /// </summary>
        public IEnumerable<WebCookie> Cookies => EnumerateCookies();

        /// <summary>
        /// Gets the cookie with the given key.
        /// </summary>
        /// <param name="domain">The domain of the cookie.</param>
        /// <param name="path">The path of the cookie.</param>
        /// <param name="key">The key of the cookie.</param>
        /// <returns>If matching cookie, if any.</returns>
        public WebCookie FindCookie(String domain, String path, String key)
        {
            var cookies = Volatile.Read(ref _cookies);
            var index = IndexOf(cookies, domain, path, key);
            return index >= 0 ? cookies[index] : null;
        }

        /// <summary>
        /// Finds all cookies that match the given domain and optional path.
        /// </summary>
        /// <param name="domain">The domain to look for.</param>
        /// <param name="path">The optional path of the cookie.</param>
        /// <returns>The matching cookies.</returns>
        public IEnumerable<WebCookie> FindCookies(String domain, String path = null)
        {
            domain = CanonicalDomain(domain);
            return EnumerateCookies().Where(cookie => Matches(cookie, domain, path));
        }

        /// <summary>
        /// Adds a new cookie to the collection.
        /// </summary>
        /// <param name="cookie">The cookie to add.</param>
        public void AddCookie(WebCookie cookie)
        {
            if (cookie != null)
            {
                Change(cookies => Insert(Without(cookies, cookie), cookie));
                WriteCookies();
            }
        }

        /// <summary>
        /// Updates an existing cookie with a new cookie.
        /// </summary>
        /// <param name="oldCookie">The cookie to update.</param>
        /// <param name="newCookie">The updated cookie content.</param>
        public void UpdateCookie(WebCookie oldCookie, WebCookie newCookie)
        {
            if (newCookie != null)
            {
                // one change, such that no reader sees the cookie missing in between
                Change(cookies => Insert(Without(Without(cookies, oldCookie), newCookie), newCookie));
                WriteCookies();
            }
            else if (oldCookie != null)
            {
                // as before, a removal without a replacement is not written to the file
                Change(cookies => Without(cookies, oldCookie));
            }
        }

        /// <summary>
        /// Removes a specific cookie matched by its domain, path, and key.
        /// </summary>
        /// <param name="domain">The domain to look for.</param>
        /// <param name="path">The path to look for.</param>
        /// <param name="key">The key of the cookie.</param>
        /// <returns>The removed cookie if any.</returns>
        public WebCookie RemoveCookie(String domain, String path, String key)
        {
            var cookie = default(WebCookie);

            Change(cookies =>
            {
                var index = IndexOf(cookies, domain, path, key);
                cookie = index >= 0 ? cookies[index] : null;
                return index >= 0 ? RemoveAt(cookies, index) : cookies;
            });

            if (cookie != null)
            {
                WriteCookies();
            }

            return cookie;
        }

        /// <summary>
        /// Removes the cookies found for the provided domain and path.
        /// </summary>
        /// <param name="domain">The domain to look for.</param>
        /// <param name="path">The optional path to match.</param>
        /// <returns>The removed cookies.</returns>
        public IEnumerable<WebCookie> RemoveCookies(String domain, String path = null)
        {
            domain = CanonicalDomain(domain);
            var removed = Array.Empty<WebCookie>();

            Change(cookies =>
            {
                removed = cookies.Where(cookie => Matches(cookie, domain, path)).ToArray();
                return removed.Length > 0 ? cookies.Where(cookie => !Matches(cookie, domain, path)).ToArray() : cookies;
            });

            if (removed.Length > 0)
            {
                WriteCookies();
            }

            return removed;
        }

        /// <summary>
        /// Removes all currently available cookies.
        /// </summary>
        /// <returns>The removed cookies.</returns>
        public IEnumerable<WebCookie> RemoveAllCookies()
        {
            var cookies = Interlocked.Exchange(ref _cookies, Array.Empty<WebCookie>());
            WriteCookies();
            return cookies;
        }

        private List<WebCookie> ReadCookies() => Deserialize(_handler.ReadFile(), _forceParse, _httpOnlyExtension);

        // reads the array when enumerated, not when called, such that Cookies stays a live view
        private IEnumerable<WebCookie> EnumerateCookies()
        {
            foreach (var cookie in Volatile.Read(ref _cookies))
            {
                yield return cookie;
            }
        }

        private void Change(Func<WebCookie[], WebCookie[]> change)
        {
            while (true)
            {
                var current = Volatile.Read(ref _cookies);
                var next = change(current);

                if (ReferenceEquals(next, current) || Interlocked.CompareExchange(ref _cookies, next, current) == current)
                {
                    return;
                }
            }
        }

        private void WriteCookies()
        {
            Interlocked.Increment(ref _pendingWrites);

            // a single thread writes the file; a change made meanwhile only raises the counter
            // and is written in that thread's next round, so nobody waits for the file
            while (Interlocked.CompareExchange(ref _writing, 1, 0) == 0)
            {
                try
                {
                    while (Interlocked.Exchange(ref _pendingWrites, 0) > 0)
                    {
                        var selection = Volatile.Read(ref _cookies).Where(m => m.IsPersistent);
                        var content = Serialize(selection, _httpOnlyExtension);
                        _handler.WriteFile(content);
                    }
                }
                finally
                {
                    Volatile.Write(ref _writing, 0);
                }

                // a change raised after the last round but before the release would be lost otherwise
                if (Volatile.Read(ref _pendingWrites) == 0)
                {
                    break;
                }
            }
        }

        private static Boolean Matches(WebCookie cookie, String domain, String path) =>
            cookie.Domain.Is(domain) && (String.IsNullOrEmpty(path) || CheckPaths(path, cookie.Path));

        private static Int32 IndexOf(WebCookie[] cookies, String domain, String path, String key)
        {
            domain = CanonicalDomain(domain);

            for (var i = 0; i < cookies.Length; i++)
            {
                var cookie = cookies[i];

                if (cookie.Domain.Is(domain) && cookie.Path.Is(path) && cookie.Key.Is(key))
                {
                    return i;
                }
            }

            return -1;
        }

        private static WebCookie[] Without(WebCookie[] cookies, WebCookie cookie)
        {
            var index = cookie != null ? IndexOf(cookies, cookie.Domain, cookie.Path, cookie.Key) : -1;
            return index >= 0 ? RemoveAt(cookies, index) : cookies;
        }

        private static WebCookie[] RemoveAt(WebCookie[] cookies, Int32 index)
        {
            var result = new WebCookie[cookies.Length - 1];
            Array.Copy(cookies, 0, result, 0, index);
            Array.Copy(cookies, index + 1, result, index, result.Length - index);
            return result;
        }

        // keeps the order of the former list: before the first cookie it compares greater than
        private static WebCookie[] Insert(WebCookie[] cookies, WebCookie cookie)
        {
            var index = 0;

            while (index < cookies.Length && cookie.CompareTo(cookies[index]) <= 0)
            {
                index++;
            }

            var result = new WebCookie[cookies.Length + 1];
            Array.Copy(cookies, 0, result, 0, index);
            result[index] = cookie;
            Array.Copy(cookies, index, result, index + 1, cookies.Length - index);
            return result;
        }
    }
}
