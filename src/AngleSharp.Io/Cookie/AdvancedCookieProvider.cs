namespace AngleSharp.Io.Cookie
{
    using AngleSharp.Text;
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        private readonly List<WebCookie> _cookies;

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
            var cookies = FindCookies(host, path)
                .ToArray()
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
        public IEnumerable<WebCookie> Cookies => _cookies.AsEnumerable();

        /// <summary>
        /// Gets the cookie with the given key.
        /// </summary>
        /// <param name="domain">The domain of the cookie.</param>
        /// <param name="path">The path of the cookie.</param>
        /// <param name="key">The key of the cookie.</param>
        /// <returns>If matching cookie, if any.</returns>
        public WebCookie FindCookie(String domain, String path, String key)
        {
            domain = CanonicalDomain(domain);
            return _cookies.FirstOrDefault(cookie => cookie.Domain.Is(domain) && cookie.Path.Is(path) && cookie.Key.Is(key));
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
            var enumerable = _cookies.Where(cookie => cookie.Domain.Is(domain));

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
                _cookies.Remove(FindCookie(cookie.Domain, cookie.Path, cookie.Key));
                InsertCookie(cookie);
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
                _cookies.Remove(FindCookie(oldCookie.Domain, oldCookie.Path, oldCookie.Key));
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
            var cookie = FindCookie(domain, path, key);

            if (cookie != null)
            {
                _cookies.Remove(cookie);
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
            var cookies = FindCookies(domain, path);

            if (cookies.Any())
            {
                _cookies.RemoveAll(cookie => cookies.Contains(cookie));
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
            var cookies = _cookies.ToArray();
            _cookies.RemoveAll(cookie => true);
            return cookies;
        }

        private List<WebCookie> ReadCookies() => Deserialize(_handler.ReadFile(), _forceParse, _httpOnlyExtension);

        private void InsertCookie(WebCookie cookie)
        {
            for (var i = 0; i < _cookies.Count; i++)
            {
                if (cookie.CompareTo(_cookies[i]) > 0)
                {
                    _cookies.Insert(i, cookie);
                    return;
                }
            }

            _cookies.Add(cookie);
        }

        private void WriteCookies()
        {
            var selection = _cookies.Where(m => m.IsPersistent);
            var content = Serialize(selection, _httpOnlyExtension);
            _handler.WriteFile(content);
        }
    }
}
