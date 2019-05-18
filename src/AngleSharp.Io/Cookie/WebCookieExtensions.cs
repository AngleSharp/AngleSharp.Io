namespace AngleSharp.Io.Cookie
{
    using AngleSharp.Text;
    using System;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A collection of extensions for the WebCookie class.
    /// </summary>
    public static class WebCookieExtensions
    {
        private static readonly Regex PathCharacters = new Regex("[\\x20-\\x3A\\x3C-\\x7E]+");
        private static readonly Regex ValueCharacters = new Regex("^[\\x21\\x23-\\x2B\\x2D-\\x3A\\x3C-\\x5B\\x5D-\\x7E]+$");

        /// <summary>
        /// Serializes the cookie to the set-cookie format.
        /// </summary>
        /// <param name="cookie">The cookie to serialize.</param>
        /// <returns>A string used in the HTTP set-cookie header.</returns>
        public static String ToSetCookie(this WebCookie cookie)
        {
            var str = StringBuilderPool.Obtain().Append(cookie.ToGetCookie());

            if (cookie.Expires.HasValue)
            {
                str.Append($"; Expires={Helpers.FormatDate(cookie.Expires.Value)}");
            }

            if (cookie.MaxAge.HasValue && cookie.MaxAge.Value != Int32.MaxValue)
            {
                str.Append($"; Max-Age={cookie.MaxAge}");
            }

            if (!String.IsNullOrEmpty(cookie.Domain) && !(cookie.IsHostOnly ?? false))
            {
                str.Append($"; Domain={cookie.Domain}");
            }

            if (!String.IsNullOrEmpty(cookie.Path))
            {
                str.Append($"; Path={cookie.Path}");
            }

            if (cookie.IsSecure)
            {
                str.Append("; Secure");
            }

            if (cookie.IsHttpOnly)
            {
                str.Append("; HttpOnly");
            }

            foreach (var extension in cookie.Extensions)
            {
                str.Append($"; {extension}");
            }

            return str.ToPool();
        }

        /// <summary>
        /// Serializes the cookie to the standard HTTP cookie format.
        /// </summary>
        /// <param name="cookie">The cookie to serialize.</param>
        /// <returns>A string used in the HTTP cookie header.</returns>
        public static String ToGetCookie(this WebCookie cookie)
        {
            var val = cookie.Value ?? String.Empty;

            if (String.IsNullOrEmpty(cookie.Key))
            {
                return val;
            }

            return $"{cookie.Key}={val}";
        }

        /// <summary>
        /// Validates the given cookie against invalid values.
        /// </summary>
        /// <param name="cookie">The cookie to validate.</param>
        /// <returns>True if the cookie is valid, false otherwise.</returns>
        public static Boolean Validate(this WebCookie cookie)
        {
            if (!ValueCharacters.IsMatch(cookie.Value))
            {
                return false;
            }

            if (cookie.MaxAge.HasValue && cookie.MaxAge <= 0)
            {
                return false;
            }

            if (cookie.Path != null && !PathCharacters.IsMatch(cookie.Path))
            {
                return false;
            }

            var cdomain = Helpers.CanonicalDomain(cookie.Domain);

            if (!String.IsNullOrEmpty(cdomain))
            {
                if (cdomain.EndsWith("."))
                {
                    // See section: 4.1.2.3.
                    // suggests that this is bad. domainMatch() tests confirm this
                    return false;
                }

                var suffix = Helpers.GetPublicSuffix(cdomain);

                if (suffix == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
