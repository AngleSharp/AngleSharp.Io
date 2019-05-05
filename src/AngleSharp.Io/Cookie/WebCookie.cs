namespace AngleSharp.Io.Cookie
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents a parsed web cookie.
    /// </summary>
    public sealed class WebCookie : IComparable<WebCookie>, IEquatable<WebCookie>
    {
        private List<String> _extensions;

        internal WebCookie() { }

        /// <summary>
        /// Parses the provided string to produce a new web cookie.
        /// </summary>
        /// <param name="value">The value to parse.</param>
        /// <param name="loose">Optionally makes parsing less strict.</param>
        /// <returns>The created web cookie.</returns>
        public static WebCookie FromString(String value, Boolean loose = false) => CookieParser.ParseSingle(value, loose);

        /// <summary>
        /// Gets the associated domain of the cookie.
        /// </summary>
        public String Domain { get; internal set; }

        /// <summary>
        /// Gets the computed canonical domain of the cookie.
        /// </summary>
        public String CanonicalDomain => Helpers.CanonicalDomain(Domain);

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
        /// <param name="now">The reference time.</param>
        /// <returns>The time to live if any.</returns>
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

        /// <summary>
        /// Computes the expiration time relative to the provided now.
        /// </summary>
        /// <param name="now">The reference time.</param>
        /// <returns>The expiration time.</returns>
        public DateTime ComputeExpiration(DateTime now)
        {
            if (MaxAge.HasValue)
            {
                var relativeTo = now;
                var age = (MaxAge.Value <= 0) ? -Int32.MaxValue : MaxAge.Value;
                return relativeTo.AddSeconds(age);
            }

            if (!Expires.HasValue)
            {
                return DateTime.MaxValue;
            }

            return Expires.Value;
        }

        internal void WithExtension(String extension)
        {
            if (_extensions == null)
            {
                _extensions = new List<String>();
            }

            _extensions.Add(extension);
        }

        /// <inheritdoc />
        public Int32 CompareTo(WebCookie other)
        {
            var cmp = Domain?.CompareTo(other.Domain ?? String.Empty) ?? 0;

            if (cmp != 0)
            {
                return cmp;
            }

            cmp = Path?.CompareTo(other.Path ?? String.Empty) ?? 0;

            if (cmp != 0)
            {
                return cmp;
            }

            cmp = Key?.CompareTo(other.Key ?? String.Empty) ?? 0;

            if (cmp != 0)
            {
                return cmp;
            }

            cmp = Value?.CompareTo(other.Value ?? String.Empty) ?? 0;

            if (cmp != 0)
            {
                return cmp;
            }

            cmp = TimeToLive?.CompareTo(other.TimeToLive ?? TimeSpan.Zero) ?? 0;

            if (cmp != 0)
            {
                return cmp;
            }

            return GetHashCode().CompareTo(other.GetHashCode());
        }

        /// <summary>
        /// Checks if the current WebCookie is equal to the provided one.
        /// </summary>
        /// <param name="other">The instance to compare to.</param>
        /// <returns>True if both are value-wise equal, otherwise false.</returns>
        public Boolean Equals(WebCookie other) =>
            Domain.Equals(other.Domain) &&
            Expires.Equals(other.Expires) &&
            IsHostOnly.Equals(other.IsHostOnly) &&
            IsHttpOnly.Equals(other.IsHttpOnly) &&
            IsSecure.Equals(other.IsSecure) &&
            Key.Equals(other.Key) &&
            Value.Equals(other.Value) &&
            Path.Equals(other.Path) &&
            MaxAge.Equals(other.MaxAge);
    }
}
