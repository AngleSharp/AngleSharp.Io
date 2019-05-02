namespace AngleSharp.Io.Cookie
{
    using System;
    using System.Text.RegularExpressions;

    internal static class CookieDateParser
    {
        private static readonly Regex DateDeliminator = new Regex("[\\x09\\x20-\\x2F\\x3B-\\x40\\x5B-\\x60\\x7B-\\x7E]");

        public static DateTime? Parse(String str)
        {
            if (String.IsNullOrEmpty(str))
            {
                return null;
            }

            // RFC6265 Section 5.1.1:
            var tokens = DateDeliminator.Split(str);
            var hour = default(Int32?);
            var minute = default(Int32?);
            var second = default(Int32?);
            var dayOfMonth = default(Int32?);
            var month = default(Int32?);
            var year = default(Int32?);

            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i].Trim();

                if (token.Length == 0)
                {
                    continue;
                }
                
                // See section 2.1
                if (second == null)
                {
                    var result = ParseTime(token);

                    if (result != null)
                    {
                        hour = result[0];
                        minute = result[1];
                        second = result[2];
                        continue;
                    }
                }

                // See section 2.2
                if (dayOfMonth == null)
                {
                    var result = ParseDigits(token, 1, 2, true);

                    if (result.HasValue)
                    {
                        dayOfMonth = result;
                        continue;
                    }
                }

                // See section 2.3
                if (month == null)
                {
                    var result = ParseMonth(token);

                    if (result != null)
                    {
                        month = result;
                        continue;
                    }
                }

                // See section 2.4
                if (year == null)
                {
                    var result = ParseDigits(token, 2, 4, true);

                    if (result.HasValue)
                    {
                        year = result;

                        // See Section 5.1.1
                        if (year >= 70 && year <= 99)
                        {
                            year += 1900;
                        }
                        else if (year >= 0 && year <= 69)
                        {
                            year += 2000;
                        }
                    }
                }
            }

            // See RFC 6265 Section 5.1.1
            if (!dayOfMonth.HasValue || !month.HasValue || !year.HasValue || !second.HasValue ||
                dayOfMonth < 1 || dayOfMonth > 31 || year < 1601 || hour > 23 || minute > 59 || second > 59)
            {
                return null;
            }

            return new DateTime(year.Value, month.Value, dayOfMonth.Value, hour.Value, minute.Value, second.Value, DateTimeKind.Utc);
        }

        private static Int32? ParseMonth(string token)
        {
            token = token.Substring(0, 3).ToLowerInvariant();

            switch (token)
            {
                case "jan":
                    return 1;
                case "feb":
                    return 2;
                case "mar":
                    return 3;
                case "apr":
                    return 4;
                case "may":
                    return 5;
                case "jun":
                    return 6;
                case "jul":
                    return 7;
                case "aug":
                    return 8;
                case "sep":
                    return 9;
                case "oct":
                    return 10;
                case "nov":
                    return 11;
                case "dec":
                    return 12;
                default:
                    return null;
            }
        }

        private static Int32? ParseDigits(string token, int minDigits, int maxDigits, bool trailing)
        {
            var count = 0;

            while (count < token.Length)
            {
                var c = token[count];

                // "non-digit = %x00-2F / %x3A-FF"
                if (c <= 0x2F || c >= 0x3A)
                {
                    break;
                }

                count++;
            }

            // constrain to a minimum and maximum number of digits.
            if (count < minDigits || count > maxDigits)
            {
                return null;
            }
            else if (!trailing && count != token.Length)
            {
                return null;
            }

            return Int32.Parse(token.Substring(0, count));
        }

        private static Int32[] ParseTime(string token)
        {
            var parts = token.Split(':');
            var result = new[] { 0, 0, 0 };

            // See RF6256 Section 5.1.1
            if (parts.Length != 3)
            {
                return null;
            }

            for (var i = 0; i < 3; i++)
            {
                var num = ParseDigits(parts[i], 1, 2, i == 2);

                if (!num.HasValue)
                {
                    return null;
                }

                result[i] = num.Value;
            }

            return result;
        }
    }
}
