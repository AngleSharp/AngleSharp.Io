namespace AngleSharp.Io.Cookie
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    internal static class CookieParser
    {
        private static readonly Regex InvalidChars = new Regex("[\\x00-\\x1F]");
        private static readonly Char[] Terminators = new[] { '\n', '\r', '\0' };

        public static WebCookie Parse(String str, Boolean loose = false)
        {
            str = str.Trim();

            // See section 5.2
            var firstSemi = str.IndexOf(';');
            var cookiePair = (firstSemi == -1) ? str : str.Substring(0, firstSemi);
            var c = ParseCookiePair(cookiePair, loose);

            if (c == null)
            {
                return null;
            }

            if (firstSemi == -1)
            {
                return c;
            }

            // Section 5.2.3
            var unparsed = str.Substring(firstSemi + 1).Trim();

            if (unparsed.Length > 0)
            {
                /*
                 * 5.2 says that when looping over the items: "[p]rocess the attribute-name
                 * and attribute-value according to the requirements in the following
                 * subsections" for every item.  Plus, for many of the individual attributes
                 * in S5.3 it says to use the "attribute-value of the last attribute in the
                 * cookie-attribute-list".
                 * Therefore, in this implementation, we overwrite the previous value.
                 */
                var cookie_avs = new Queue<String>(unparsed.Split(';'));

                while (cookie_avs.Count > 0)
                {
                    var av = cookie_avs.Dequeue().Trim();

                    // happens if ";;" appears
                    if (av.Length == 0)
                    {
                        continue;
                    }

                    var av_sep = av.IndexOf('=');
                    var av_key = String.Empty;
                    var av_value = String.Empty;

                    if (av_sep == -1)
                    {
                        av_key = av;
                        av_value = null;
                    }
                    else
                    {
                        av_key = av.Substring(0, av_sep);
                        av_value = av.Substring(av_sep + 1);
                    }

                    av_key = av_key.Trim().ToLowerInvariant();

                    if (!String.IsNullOrEmpty(av_value))
                    {
                        av_value = av_value.Trim();
                    }

                    switch (av_key)
                    {
                        // Section 5.2.1
                        case "expires":
                            if (!String.IsNullOrEmpty(av_value))
                            {
                                var exp = CookieDateParser.Parse(av_value);

                                if (exp.HasValue)
                                {
                                    c.Expires = exp;
                                }
                            }
                            break;
                        // Section 5.2.2
                        case "max-age":
                            if (!String.IsNullOrEmpty(av_value))
                            {
                                if (Int32.TryParse(av_value, out var delta))
                                {
                                    c.MaxAge = delta;
                                }
                            }
                            break;
                        // Section 5.2.3
                        case "domain":
                            if (!String.IsNullOrEmpty(av_value))
                            {
                                var domain = av_value.Trim().TrimStart('.');

                                if (!String.IsNullOrEmpty(domain))
                                {
                                    c.Domain = domain.ToLowerInvariant();
                                }
                            }
                            break;
                        // Section 5.2.4
                        case "path":
                            c.Path = !String.IsNullOrEmpty(av_value) && av_value[0] == '/' ? av_value : null;
                            break;
                        // Section 5.2.5
                        case "secure":
                            c.IsSecure = true;
                            break;
                        // Section 5.2.6
                        case "httponly":
                            c.IsHttpOnly = true;
                            break;
                        default:
                            c.WithExtension(av);
                            break;
                    }
                }
            }

            return c;
        }

        private static WebCookie ParseCookiePair(String cookiePair, Boolean loose)
        {
            cookiePair = TrimTerminator(cookiePair);

            var firstEq = cookiePair.IndexOf('=');

            if (loose)
            {
                if (firstEq == 0)
                {
                    // '=' is immediately at start
                    cookiePair = cookiePair.Substring(1);
                    // might still need to split on '='
                    firstEq = cookiePair.IndexOf('=');
                }
            }
            else if (firstEq <= 0)
            {
                // no '=' or is at start
                // needs to have non-empty "cookie-name"
                return null;
            }

            var cookieName = String.Empty;
            var cookieValue = String.Empty;

            if (firstEq <= 0)
            {
                cookieValue = cookiePair.Trim();
            }
            else
            {
                cookieName = cookiePair.Substring(0, firstEq).Trim();
                cookieValue = cookiePair.Substring(firstEq + 1).Trim();
            }

            if (InvalidChars.IsMatch(cookieName) || InvalidChars.IsMatch(cookieValue))
            {
                return null;
            }

            return new WebCookie
            {
                Key = cookieName,
                Value = cookieValue,
            };
        }

        private static String TrimTerminator(String str)
        {
            foreach (var terminator in Terminators)
            {
                var terminatorIdx = str.IndexOf(terminator);

                if (terminatorIdx != -1)
                {
                    str = str.Substring(0, terminatorIdx);
                }
            }

            return str;
        }
    }
}
