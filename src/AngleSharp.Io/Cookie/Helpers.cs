namespace AngleSharp.Io.Cookie
{
    using AngleSharp.Text;
    using System;
    using System.Text.RegularExpressions;

    internal static class Helpers
    {
        private static readonly DateTime EpochDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly Regex NonAscii = new Regex("[^\\u0001-\\u007f]");
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
