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
        // cookies are read far more often than changed, and the resources of a page are loaded in
        // parallel: a change copies the list and swaps the copy in, so readers never see a half change
        private List<WebCookie> _cookies;
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
            _cookies = ReadCookies();
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
        public WebCookie FindCookie(String domain, String path, String key) =>
            Find(Volatile.Read(ref _cookies), domain, path, key);

        /// <summary>
        /// Finds all cookies that match the given domain and optional path.
        /// </summary>
        /// <param name="domain">The domain to look for.</param>
        /// <param name="path">The optional path of the cookie.</param>
        /// <returns>The matching cookies.</returns>
        public IEnumerable<WebCookie> FindCookies(String domain, String path = null)
        {
            domain = CanonicalDomain(domain);
            var enumerable = EnumerateCookies().Where(cookie => cookie.Domain.Is(domain));

            if (!String.IsNullOrEmpty(path))
            {
                enumerable = enumerable
                    .Where(cookie => CheckPaths(path, cookie.Path));
            }

            return enumerable;
        }

        /// <summary>
        /// Adds a new cookie to the collection.
        /// </summary>
        /// <param name="cookie">The cookie to add.</param>
        public void AddCookie(WebCookie cookie)
        {
            if (cookie != null)
            {
                Change(cookies =>
                {
                    cookies.Remove(Find(cookies, cookie.Domain, cookie.Path, cookie.Key));
                    InsertCookie(cookies, cookie);
                });
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
            if (oldCookie != null)
            {
                Change(cookies => cookies.Remove(Find(cookies, oldCookie.Domain, oldCookie.Path, oldCookie.Key)));
            }

            AddCookie(newCookie);
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
                cookie = Find(cookies, domain, path, key);
                cookies.Remove(cookie);
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
            // taken before the removal, as the lazy query would find nothing afterwards
            var cookies = FindCookies(domain, path).ToArray();

            if (cookies.Length > 0)
            {
                Change(list => list.RemoveAll(cookie => cookies.Contains(cookie)));
                WriteCookies();
            }

            return cookies;
        }

        /// <summary>
        /// Removes all currently available cookies.
        /// </summary>
        /// <returns>The removed cookies.</returns>
        public IEnumerable<WebCookie> RemoveAllCookies()
        {
            var cookies = Interlocked.Exchange(ref _cookies, new List<WebCookie>());
            WriteCookies();
            return cookies.ToArray();
        }

        private List<WebCookie> ReadCookies() => Deserialize(_handler.ReadFile(), _forceParse, _httpOnlyExtension);

        // reads the list when enumerated, not when called, such that Cookies stays a live view
        private IEnumerable<WebCookie> EnumerateCookies()
        {
            foreach (var cookie in Volatile.Read(ref _cookies))
            {
                yield return cookie;
            }
        }

        private void Change(Action<List<WebCookie>> change)
        {
            while (true)
            {
                var current = Volatile.Read(ref _cookies);
                var next = new List<WebCookie>(current);
                change(next);

                if (Interlocked.CompareExchange(ref _cookies, next, current) == current)
                {
                    return;
                }
            }
        }

        private void WriteCookies()
        {
            Interlocked.Increment(ref _pendingWrites);

            // one thread writes the file at a time; a change made meanwhile is written in its
            // next round, so no thread waits for the file
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

                // a change made after the last round but before the release must not be lost
                if (Volatile.Read(ref _pendingWrites) == 0)
                {
                    break;
                }
            }
        }

        private static WebCookie Find(List<WebCookie> cookies, String domain, String path, String key)
        {
            domain = CanonicalDomain(domain);
            return cookies.FirstOrDefault(cookie => cookie.Domain.Is(domain) && cookie.Path.Is(path) && cookie.Key.Is(key));
        }

        private static void InsertCookie(List<WebCookie> cookies, WebCookie cookie)
        {
            for (var i = 0; i < cookies.Count; i++)
            {
                if (cookie.CompareTo(cookies[i]) > 0)
                {
                    cookies.Insert(i, cookie);
                    return;
                }
            }

            cookies.Add(cookie);
        }
    }
}
