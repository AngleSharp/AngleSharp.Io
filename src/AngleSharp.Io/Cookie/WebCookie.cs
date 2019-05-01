namespace AngleSharp.Io.Cookie
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents a parsed web cookie.
    /// </summary>
    public sealed class WebCookie
    {
        private List<String> _extensions;

        internal WebCookie() { }

        /// <summary>
        /// Parses the provided string to produce a new web cookie.
        /// </summary>
        /// <param name="value">The value to parse.</param>
        /// <returns>The created web cookie.</returns>
        public static WebCookie FromString(String value)
        {
            var parser = new CookieParser();
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the associated domain of the cookie.
        /// </summary>
        public String Domain { get; internal set; }

        /// <summary>
        /// Gets the path of the cookie.
        /// </summary>
        public String Path { get; internal set; }

        /// <summary>
        /// Gets if the cookie is a secure cookie.
        /// </summary>
        public Boolean IsSecure { get; internal set; }

        /// <summary>
        /// Gets the expiration date of the cookie, if any.
        /// </summary>
        public DateTime? Expires { get; internal set; }

        /// <summary>
        /// Gets the maximum age, if any.
        /// </summary>
        public Int32? MaxAge { get; internal set; }

        /// <summary>
        /// Gets if the cookie should be stored on the file system.
        /// </summary>
        public Boolean IsPersistent => MaxAge.HasValue || Expires.HasValue;

        /// <summary>
        /// Gets the transport only property of the cookie.
        /// </summary>
        public Boolean IsHttpOnly { get; internal set; }

        /// <summary>
        /// Gets the host only property of the cookie, if set.
        /// According to S5.2.6 effectively the same as 'IsSecure'.
        /// </summary>
        public Boolean? IsHostOnly { get; internal set; }

        /// <summary>
        /// Gets the time to live for the current time.
        /// </summary>
        public TimeSpan? TimeToLive => ComputeTimeToLive(DateTime.UtcNow);

        /// <summary>
        /// Gets the key of the cookie.
        /// </summary>
        public String Key { get; internal set; }

        /// <summary>
        /// Gets the value of the cookie.
        /// </summary>
        public String Value { get; internal set; }

        /// <summary>
        /// Gets the available extensions for the cookie.
        /// </summary>
        public IEnumerable<String> Extensions => _extensions ?? Enumerable.Empty<String>();

        /// <summary>
        /// Computes the time to live.
        /// For reference, see RFC6265 S4.1.2.2.
        /// </summary>
        /// <param name="now"></param>
        /// <returns></returns>
        public TimeSpan? ComputeTimeToLive(DateTime now)
        {
            /*
             * If a cookie has both the Max-Age and the Expires
             * attribute, the Max-Age attribute has precedence and controls the
             * expiration date of the cookie. [Concurs with S5.3 step 3]
             */
            if (!MaxAge.HasValue)
            {
                var expires = Expires;

                if (expires.HasValue)
                {
                    return expires.Value - now;
                }

                return null;
            }

            return TimeSpan.FromSeconds(Math.Max(0, MaxAge.Value));
        }

        internal void WithExtension(String extension)
        {
            if (_extensions == null)
            {
                _extensions = new List<String>();
            }

            _extensions.Add(extension);
        }
    }
}
