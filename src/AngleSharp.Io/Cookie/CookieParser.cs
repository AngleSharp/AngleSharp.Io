namespace AngleSharp.Io.Cookie
{
    using AngleSharp.Text;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    internal class CookieParser
    {
        private static readonly Regex InvalidChars = new Regex("[\\x00-\\x1F]");
        private static readonly Char[] Terminators = new[] { '\n', '\r', '\0' };

        private readonly String[] _content;
        private readonly Boolean _loose;
        private Int32 _current;
        private Int32 _index;

        public CookieParser(String content, Boolean loose = false)
        {
            _content = (content ?? String.Empty).Split(',').Select(m => m.Trim()).ToArray();
            _loose = loose;
        }

        private String Content => _current < _content.Length ? _content[_current] : String.Empty;

        public List<WebCookie> Parse()
        {
            var cookies = new List<WebCookie>();
            _current = 0;
            _index = 0;

            while (_current < _content.Length)
            {
                var cookie = ParseNext();

                if (cookie != null)
                {
                    cookies.Add(cookie);
                }

                _current++;
                _index = 0;
            }

            return cookies;
        }

        public static WebCookie ParseSingle(String str, Boolean loose = false)
        {
            var parser = new CookieParser(str, loose);

            if (!String.IsNullOrEmpty(str))
            {
                return parser.ParseNext();
            }

            return null;
        }

        private WebCookie ParseNext()
        {
            var cookie = ParseCookiePair();

            if (cookie != null && _index < Content.Length)
            {
                /*
                 * 5.2 says that when looping over the items: "[p]rocess the attribute-name
                 * and attribute-value according to the requirements in the following
                 * subsections" for every item.  Plus, for many of the individual attributes
                 * in S5.3 it says to use the "attribute-value of the last attribute in the
                 * cookie-attribute-list".
                 * Therefore, in this implementation, we overwrite the previous value.
                 */
                while (_index < Content.Length)
                {
                    var content = Content;
                    var start = _index;
                    var end = NormalizeEnd(content.IndexOf(';', start), content.Length);
                    var contentEnd = Rewind(end);

                    // happens NOT if ";;" appears
                    if (contentEnd > start)
                    {
                        var av_sep = NormalizeEnd(content.IndexOf('=', start, contentEnd - start), contentEnd);
                        var av_key = content.Substring(start, Rewind(av_sep) - start).ToLowerInvariant();
                        var hasValue = false;

                        if (av_sep != contentEnd)
                        {
                            SkipWhitespace(av_sep + 1);
                            hasValue = _index < contentEnd;
                        }
                        else
                        {
                            _index = av_sep;
                        }

                        switch (av_key)
                        {
                            // Section 5.2.1
                            case "expires":
                                if (hasValue && TryParseDateTime(contentEnd, out var expires))
                                {
                                    cookie.Expires = expires;
                                }
                                break;
                            // Section 5.2.2
                            case "max-age":
                                if (hasValue && TryParseInteger(contentEnd, out var delta))
                                {
                                    cookie.MaxAge = delta;
                                }
                                break;
                            // Section 5.2.3
                            case "domain":
                                if (hasValue && TryParseDomain(contentEnd, out var domain))
                                {
                                    cookie.Domain = domain;
                                }
                                break;
                            // Section 5.2.4
                            case "path":
                                if (hasValue && TryParsePath(contentEnd, out var path))
                                {
                                    cookie.Path = path;
                                }
                                break;
                            // Section 5.2.5
                            case "secure":
                                cookie.IsSecure = true;
                                break;
                            // Section 5.2.6
                            case "httponly":
                                cookie.IsHttpOnly = true;
                                break;
                            default:
                                cookie.WithExtension(Cut(start, contentEnd));
                                break;
                        }
                    }

                    if (_index < Content.Length)
                    {
                        SkipWhitespace(NormalizeEnd(Content.IndexOf(';', _index), Content.Length) + 1);
                    }
                }
            }

            return cookie;
        }

        private WebCookie ParseCookiePair()
        {
            // See section 5.2
            var content = Content;
            var firstSemi = content.IndexOf(';', _index);
            var end = content.IndexOfAny(Terminators, _index);

            if (end == -1 && firstSemi == -1)
            {
                end = content.Length;
            }
            else if (end == -1)
            {
                end = firstSemi;
            }
            else if (firstSemi != -1)
            {
                end = Math.Min(firstSemi, end);
            }

            var firstEq = content.IndexOf('=', _index, end - _index);

            if (_loose)
            {
                if (firstEq == _index)
                {
                    // '=' is immediately at start
                    _index++;
                    // might still need to split on '='
                    firstEq = content.IndexOf('=', _index, end - _index);
                }
            }
            else if (firstEq <= _index)
            {
                // no '=' or is at start
                // needs to have non-empty "cookie-name"
                return null;
            }

            var cookieName = String.Empty;
            var cookieValue = String.Empty;

            if (firstEq <= _index)
            {
                cookieValue = content.Substring(_index, end - _index).Trim();
            }
            else
            {
                cookieName = content.Substring(_index, firstEq - _index).Trim();
                cookieValue = content.Substring(firstEq + 1, end - firstEq - 1).Trim();
            }

            if (!InvalidChars.IsMatch(cookieName) && !InvalidChars.IsMatch(cookieValue))
            {
                // Section 5.2.3
                SkipWhitespace(Math.Min(content.Length, end + 1));

                return new WebCookie
                {
                    Key = cookieName,
                    Value = cookieValue,
                };
            }

            return null;
        }

        public Boolean TryParseDomain(Int32 end, out String domain)
        {
            var offset = Content[_index] == '.' ? 1 : 0;
            domain = Cut(_index + offset, end).ToLowerInvariant();
            return domain.Length > 0;
        }

        private Boolean TryParsePath(Int32 end, out String path)
        {
            path = Cut(_index, end);
            return path.Length > 0 && path[0] == '/';
        }

        public Boolean TryParseInteger(Int32 end, out Int32 value)
        {
            var sign = 1;
            var content = Content;
            value = 0;

            if (content[_index] == '-')
            {
                sign = -1;
                _index++;
            }
            else if (content[_index] == '+')
            {
                _index++;
            }

            while (_index < end && content[_index].IsDigit())
            {
                value = value * 10 + content[_index++] - '0';
            }

            value = value * sign;
            return _index == end;
        }

        public Boolean TryParseDateTime(Int32 end, out DateTime dateTime)
        {
            // RFC6265 Section 5.1.1:
            var time = default(Int32[]);
            var dayOfMonth = default(Int32?);
            var month = default(Int32?);
            var year = default(Int32?);
            var start = _index;

            while (_index < end && (!dayOfMonth.HasValue || !month.HasValue || !year.HasValue || time == null))
            {
                var content = Content;

                if (IsDateDeliminator(content[_index]))
                {
                    _index++;
                    continue;
                }

                // See section 2.1
                if (time == null && TryParseTime(end, out var timeParts))
                {
                    time = timeParts;
                    continue;
                }

                // See section 2.2
                if (dayOfMonth == null && TryParseDigits(end, 1, 2, true, out var monthDayIndex))
                {
                    dayOfMonth = monthDayIndex;
                    continue;
                }

                // See section 2.3
                if (month == null && TryParseMonth(end, out var monthIndex))
                {
                    month = monthIndex;
                    continue;
                }

                // See section 2.4
                if (year == null && TryParseDigits(end, 2, 4, true, out var yearValue))
                {
                    year = yearValue;

                    // See Section 5.1.1
                    if (year >= 70 && year <= 99)
                    {
                        year += 1900;
                    }
                    else if (year >= 0 && year <= 69)
                    {
                        year += 2000;
                    }

                    continue;
                }

                while (_index < end && !IsDateDeliminator(content[_index]))
                {
                    _index++;
                }

                if (_index == end && end == content.Length && _current < _content.Length)
                {
                    _current++;
                    _index = 0;
                    end = NormalizeEnd(Content.IndexOf(';'), Content.Length);
                }
            }

            // See RFC 6265 Section 5.1.1
            if (!dayOfMonth.HasValue || !month.HasValue || !year.HasValue || time == null)
            {
                _index = start;
                dateTime = DateTime.UtcNow;
                return false;
            }

            dateTime = new DateTime(year.Value, month.Value, dayOfMonth.Value, time[0], time[1], time[2], DateTimeKind.Utc);
            return dayOfMonth > 0 && dayOfMonth < 32 && year > 1600 && time[0] < 24 && time[1] < 60 && time[2] < 60;
        }

        private Boolean TryParseMonth(Int32 end, out Int32 month)
        {
            month = 0;

            if (end - _index > 2)
            {
                var token = Cut(_index + 3).ToLowerInvariant();

                switch (token)
                {
                    case "jan":
                        month = 1;
                        break;
                    case "feb":
                        month = 2;
                        break;
                    case "mar":
                        month = 3;
                        break;
                    case "apr":
                        month = 4;
                        break;
                    case "may":
                        month = 5;
                        break;
                    case "jun":
                        month = 6;
                        break;
                    case "jul":
                        month = 7;
                        break;
                    case "aug":
                        month = 8;
                        break;
                    case "sep":
                        month = 9;
                        break;
                    case "oct":
                        month = 10;
                        break;
                    case "nov":
                        month = 11;
                        break;
                    case "dec":
                        month = 12;
                        break;
                }
            }

            if (month > 0)
            {
                _index += 3;
                return true;
            }

            return false;
        }

        private Boolean TryParseDigits(Int32 end, Int32 minDigits, Int32 maxDigits, Boolean trailing, out Int32 value)
        {
            var start = _index;
            var content = Content;
            value = 0;

            while (_index < end)
            {
                var c = content[_index];

                // "non-digit = %x00-2F / %x3A-FF"
                if (c <= 0x2F || c >= 0x3A)
                {
                    break;
                }

                value = value * 10 + c - '0';
                _index++;
            }

            var count = _index - start;

            // constrain to a minimum and maximum number of digits.
            if (count < minDigits || count > maxDigits)
            {
                _index = start;
                return false;
            }
            else if (!trailing && _index != end && !IsDateDeliminator(content[_index]))
            {
                _index = start;
                return false;
            }
            
            return true;
        }

        private Boolean TryParseTime(Int32 end, out Int32[] result)
        {
            var i = 0;
            var start = _index;
            var content = Content;

            result = new[] { 0, 0, 0 };

            // See RF6256 Section 5.1.1
            while (i < 3 && _index < end)
            {
                var next = i == 2 ? end : FindNext(':', end);

                if (!TryParseDigits(next, 1, 2, i == 2, out var value))
                {
                    break;
                }

                result[i++] = value;

                if (_index < end && content[_index] == ':')
                {
                    _index++;
                }
            }

            if (i < 3 || (_index <end && !IsDateDeliminator(content[_index])))
            {
                _index = start;
                return false;
            }

            return true;
        }

        private Int32 FindNext(Char target, Int32 end)
        {
            var idx = _index;
            var content = Content;

            while (idx < end && content[idx] != target)
            {
                idx++;
            }

            return idx;
        }

        private String Cut(Int32 end) => Content.Substring(_index, end - _index);

        private String Cut(Int32 start, Int32 end) => Content.Substring(start, end - start);

        private Int32 NormalizeEnd(Int32 end, Int32 length)
        {
            if (end < 0 || end >= length)
            {
                return length;
            }

            return end;
        }

        private Int32 Rewind(Int32 end)
        {
            var content = Content;

            while (end > _index && Char.IsWhiteSpace(content[end - 1]))
            {
                end--;
            }

            return end;
        }

        private void SkipWhitespace(Int32 position)
        {
            var content = Content;

            while (position < content.Length && Char.IsWhiteSpace(content[position]))
            {
                position++;
            }

            _index = position;
        }

        private static Boolean IsDateDeliminator(Char c) =>
            c == 0x09 || c.IsInRange(0x20, 0x2f) || c.IsInRange(0x3b, 0x40) || c.IsInRange(0x5b, 0x60) || c.IsInRange(0x7b, 0x7e);
    }
}
