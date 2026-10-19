namespace AngleSharp.Io.Http
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// The parsed directives of a Cache-Control (and Pragma) header.
    /// </summary>
    internal sealed class CacheControl
    {
        public Boolean NoStore { get; private set; }

        public Boolean NoCache { get; private set; }

        public Boolean MustRevalidate { get; private set; }

        public Boolean Public { get; private set; }

        public Boolean OnlyIfCached { get; private set; }

        public Boolean MaxStaleAny { get; private set; }

        public TimeSpan? MaxAge { get; private set; }

        public TimeSpan? SMaxAge { get; private set; }

        public TimeSpan? MinFresh { get; private set; }

        public TimeSpan? MaxStale { get; private set; }

        public static CacheControl Parse(IDictionary<String, String> headers)
        {
            var result = new CacheControl();
            var value = HttpCachePolicy.GetHeader(headers, HeaderNames.CacheControl);

            if (!String.IsNullOrEmpty(value))
            {
                foreach (var directive in Split(value))
                {
                    result.Apply(directive.Key, directive.Value);
                }
            }
            else
            {
                // Pragma is only considered without Cache-Control (RFC 9111, 5.4)
                var pragma = HttpCachePolicy.GetHeader(headers, HeaderNames.Pragma);

                if (pragma != null && pragma.IndexOf("no-cache", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.NoCache = true;
                }
            }

            return result;
        }

        private void Apply(String name, String value)
        {
            switch (name.ToLowerInvariant())
            {
                case "no-store":
                    NoStore = true;
                    break;
                case "no-cache":
                    // the qualified form (no-cache="field") is treated like the
                    // unqualified one, which is the conservative choice
                    NoCache = true;
                    break;
                case "must-revalidate":
                case "proxy-revalidate":
                    MustRevalidate = true;
                    break;
                case "public":
                    Public = true;
                    break;
                case "only-if-cached":
                    OnlyIfCached = true;
                    break;
                case "max-age":
                    MaxAge = ParseSeconds(value) ?? TimeSpan.Zero;
                    break;
                case "s-maxage":
                    SMaxAge = ParseSeconds(value) ?? TimeSpan.Zero;
                    break;
                case "min-fresh":
                    MinFresh = ParseSeconds(value);
                    break;
                case "max-stale":
                    MaxStale = ParseSeconds(value);
                    MaxStaleAny = value == null;
                    break;
            }
        }

        private static TimeSpan? ParseSeconds(String value)
        {
            if (value != null && Int64.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            {
                return TimeSpan.FromSeconds(Math.Min(seconds, Int32.MaxValue));
            }

            return null;
        }

        private static IEnumerable<KeyValuePair<String, String>> Split(String value)
        {
            var index = 0;

            while (index < value.Length)
            {
                var start = index;
                var quoted = false;

                // commas inside quoted values (no-cache="a, b") do not separate
                while (index < value.Length && (quoted || value[index] != ','))
                {
                    if (value[index] == '"')
                    {
                        quoted = !quoted;
                    }

                    index++;
                }

                var part = value.Substring(start, index - start).Trim();
                index++;

                if (part.Length > 0)
                {
                    var eq = part.IndexOf('=');

                    if (eq < 0)
                    {
                        yield return new KeyValuePair<String, String>(part, null);
                    }
                    else
                    {
                        var name = part.Substring(0, eq).Trim();
                        var argument = part.Substring(eq + 1).Trim().Trim('"');
                        yield return new KeyValuePair<String, String>(name, argument);
                    }
                }
            }
        }
    }
}
