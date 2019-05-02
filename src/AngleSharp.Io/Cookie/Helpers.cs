namespace AngleSharp.Io.Cookie
{
    using AngleSharp.Text;
    using System;
    using System.Text.RegularExpressions;

    internal static class Helpers
    {
        private static readonly DateTime EpochDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly Regex NonAscii = new Regex("[^\\u0001-\\u007f]");
        private static readonly Regex IpAddress = new Regex("^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5]).){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$");
        private static readonly String[] MonthsAbbr = new[]
        {
            "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
        };
        private static readonly String[] DaysAbbr = new[]
        {
            "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"
        };

        public static Boolean CheckPaths(String requestPath, String cookiePath)
        {
            if (!cookiePath.Is(requestPath))
            {
                if (requestPath.StartsWith(cookiePath))
                {
                    if (cookiePath.EndsWith("/"))
                    {
                        return true;
                    }
                    else if (requestPath.Length > cookiePath.Length && requestPath[cookiePath.Length] == '/')
                    {
                        return true;
                    }
                }

                return false;
            }

            return true;
        }

        public static String GetPublicSuffix(String str)
        {
            //TODO
            return String.Empty;
        }

        public static String CanonicalDomain(String str)
        {
            if (str != null)
            {
                // See: S4.1.2.3 & S5.2.3: ignore leading .
                str = str.Trim().TrimStart('.');

                if (NonAscii.IsMatch(str))
                {
                    str = Punycode.Encode(str);
                }

                return str.ToLowerInvariant();
            }

            return str;
        }

        public static Boolean DomainMatch(String str, String domStr, Boolean canonicalize)
        {
            str = str ?? throw new ArgumentNullException(str);
            domStr = domStr ?? throw new ArgumentNullException(domStr);

            if (canonicalize)
            {
                str = CanonicalDomain(str);
                domStr = CanonicalDomain(domStr);
            }

            /*
             * "The domain string and the string are identical. (Note that both the
             * domain string and the string will have been canonicalized to lower case at
             * this point)"
             */
            if (str == domStr)
            {
                return true;
            }

            /* "All of the following [three] conditions hold:" (order adjusted from the RFC) */

            /* "* The string is a host name (i.e., not an IP address)." */
            if (IpAddress.IsMatch(str))
            {
                return false;
            }

            /* "* The domain string is a suffix of the string" */
            var idx = str.IndexOf(domStr);

            if (idx <= 0)
            {
                return false; // it's a non-match (-1) or prefix (0)
            }

            // e.g "a.b.c".indexOf("b.c") === 2
            // 5 === 3+2
            if (str.Length != domStr.Length + idx)
            {
                // it's not a suffix
                return false;
            }

            /* "* The last character of the string that is not included in the domain
            * string is a %x2E (".") character." */
            if (str[idx - 1] != '.')
            {
                return false;
            }

            return true;
        }

        public static String GetDefaultPath(String path)
        {
            // "2. If the uri-path is empty or if the first character of the uri-path is not
            // a %x2F ("/") character, output %x2F ("/") and skip the remaining steps.
            if (String.IsNullOrEmpty(path) || path[0] != '/')
            {
                return "/";
            }

            // "3. If the uri-path contains no more than one %x2F ("/") character, output
            // %x2F ("/") and skip the remaining step."
            if (path == "/")
            {
                return path;
            }

            var rightSlash = path.LastIndexOf('/');

            if (rightSlash == 0)
            {
                return "/";
            }

            // "4. Output the characters of the uri-path from the first character up to,
            // but not including, the right-most %x2F ("/")."
            return path.Substring(0, rightSlash);
        }

        public static String DecodeURIComponent(String component)
        {
            var content = component.UrlDecode();
            return TextEncoding.Utf8.GetString(content);
        }

        public static String EncodeURIComponent(String component)
        {
            var content = TextEncoding.Utf8.GetBytes(component);
            return content.UrlEncode();
        }

        public static Int32 ToEpoch(DateTime? current)
        {
            if (current.HasValue)
            {
                var time = current.Value.ToUniversalTime();
                return (Int32)Math.Round(time.Subtract(EpochDate).TotalSeconds);
            }

            return 0;
        }

        public static DateTime? FromEpoch(Int32 seconds) => EpochDate.AddSeconds(seconds);

        public static String FormatDate(DateTime date) => String.Concat(
            DaysAbbr[(Int32)date.DayOfWeek],
            ", ",
            date.Day.ToString().PadLeft(2, '0'),
            " ",
            MonthsAbbr[date.Month],
            " ",
            date.Year.ToString(),
            " ",
            date.Hour.ToString().PadLeft(2, '0'),
            ":",
            date.Minute.ToString().PadLeft(2, '0'),
            ":",
            date.Second.ToString().PadLeft(2, '0'),
            " GMT"
        );
    }
}
