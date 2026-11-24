namespace AngleSharp.Io.Dom
{
    using AngleSharp.Dom;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;

    internal sealed class UrlPatternMatcher
    {
        private static readonly String[] ComponentNames =
        {
            "protocol", "username", "password", "hostname", "port", "pathname", "search", "hash",
        };

        private readonly UrlPatternComponent[] _components;

        public UrlPatternMatcher(UrlPatternInit init, UrlPatternOptions options)
        {
            var values = Process(init, true);
            Patterns = new String[values.Length];
            _components = new UrlPatternComponent[values.Length];

            if (IsDefaultPort(values[0], values[4]))
            {
                values[4] = String.Empty;
            }

            for (var i = 0; i < values.Length; i++)
            {
                var opaquePath = i == 5 && !MatchesSpecialScheme(_components[0]);
                var component = new UrlPatternComponent(values[i] ?? "*", i, options?.IgnoreCase ?? false, opaquePath);
                _components[i] = component;
                Patterns[i] = component.Pattern;
                HasRegExpGroups |= component.HasRegExpGroups;
            }
        }

        public String[] Patterns { get; }

        public Boolean HasRegExpGroups { get; }

        public Dictionary<String, Object> Match(String input, String baseUrl, Object undefined)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            Url url;

            if (baseUrl != null)
            {
                var parsedBase = new Url(baseUrl);

                if (parsedBase.IsInvalid || parsedBase.IsRelative)
                {
                    return null;
                }

                if (HasOpaquePath(parsedBase) && !input.StartsWith("#", StringComparison.Ordinal))
                {
                    var absolute = new Url(input);

                    if (absolute.IsInvalid || absolute.IsRelative)
                    {
                        return null;
                    }
                }

                url = new Url(parsedBase, input);
            }
            else
            {
                url = new Url(input);
            }

            if (url.IsInvalid || url.IsRelative)
            {
                return null;
            }

            var inputs = baseUrl is null ? new Object[] { input } : new Object[] { input, baseUrl };
            return MatchComponents(FromUrl(url), inputs, undefined);
        }

        public Dictionary<String, Object> Match(UrlPatternInit input, Object undefined)
        {
            String[] values;

            try
            {
                values = Process(input, false);
            }
            catch (ArgumentException)
            {
                return null;
            }

            return MatchComponents(values, new Object[] { ToDictionary(input) }, undefined);
        }

        private Dictionary<String, Object> MatchComponents(String[] values, Object[] inputs, Object undefined)
        {
            var result = new Dictionary<String, Object> { ["inputs"] = inputs };

            for (var i = 0; i < _components.Length; i++)
            {
                var component = _components[i].Match(values[i] ?? String.Empty, undefined);

                if (component is null)
                {
                    return null;
                }

                result.Add(ComponentNames[i], component);
            }

            return result;
        }

        public static UrlPatternInit ParsePattern(String input, String baseUrl)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var init = new UrlPatternInit { BaseUrl = baseUrl };
            var schemeEnd = FindSeparator(input, 0, ":");
            var position = 0;
            var authority = false;

            if (schemeEnd.Index >= 0)
            {
                init.Protocol = input.Substring(0, schemeEnd.Index);
                position = schemeEnd.Index + schemeEnd.Length;

                if (input.Length - position >= 2 && input[position] == '/' && input[position + 1] == '/')
                {
                    position += 2;
                    authority = true;
                }
                else
                {
                    authority = MatchesSpecialScheme(new UrlPatternComponent(init.Protocol, 0, false));

                    if (!authority)
                    {
                        init.Hostname = String.Empty;
                        init.Port = String.Empty;
                    }
                }
            }
            else if (baseUrl is null)
            {
                throw new ArgumentException("A relative URL pattern requires a base URL.", nameof(input));
            }

            if (authority)
            {
                var end = FindSeparator(input, position, "/?#");
                var authorityEnd = end.Index < 0 ? input.Length : end.Index;
                var at = FindSeparator(input, position, "@", authorityEnd);
                var hostStart = position;

                if (at.Index >= 0)
                {
                    var colon = FindSeparator(input, position, ":", at.Index);
                    init.Username = input.Substring(position, (colon.Index < 0 ? at.Index : colon.Index) - position);

                    if (colon.Index >= 0)
                    {
                        init.Password = input.Substring(colon.Index + colon.Length, at.Index - colon.Index - colon.Length);
                    }

                    hostStart = at.Index + at.Length;
                }

                var port = FindSeparator(input, hostStart, ":", authorityEnd, true);
                init.Hostname = input.Substring(hostStart, (port.Index < 0 ? authorityEnd : port.Index) - hostStart);
                init.Port = port.Index < 0 ? String.Empty : input.Substring(port.Index + port.Length, authorityEnd - port.Index - port.Length);
                position = authorityEnd;
            }

            if (position < input.Length && input[position] != '?' && input[position] != '#')
            {
                var end = FindSeparator(input, position, "?#");
                var pathEnd = end.Index < 0 ? input.Length : end.Index;
                init.Pathname = input.Substring(position, pathEnd - position);
                position = pathEnd;
            }
            else if (authority && position < input.Length)
            {
                init.Pathname = "/";
            }
            else if (!authority && init.Protocol != null && position == input.Length)
            {
                init.Pathname = String.Empty;
            }

            var separator = FindSeparator(input, position, "?#");

            if (separator.Index == position && separator.Value == '?')
            {
                var start = position + separator.Length;
                var end = FindSeparator(input, start, "#");
                var searchEnd = end.Index < 0 ? input.Length : end.Index;
                init.Search = input.Substring(start, searchEnd - start);
                position = searchEnd;
            }

            separator = FindSeparator(input, position, "#");

            if (separator.Index == position)
            {
                if (init.Protocol != null || init.Hostname != null || init.Pathname != null)
                {
                    init.Search ??= String.Empty;
                }

                init.Hash = input.Substring(position + separator.Length);
            }

            return init;
        }

        private static String[] Process(UrlPatternInit init, Boolean pattern)
        {
            var specified = ToArray(init);
            var result = new String[8];
            Url baseUrl = null;

            if (init.BaseUrl != null)
            {
                baseUrl = new Url(init.BaseUrl);

                if (baseUrl.IsInvalid || baseUrl.IsRelative)
                {
                    throw new ArgumentException("Invalid base URL.", nameof(init));
                }

                var baseParts = FromUrl(baseUrl);
                var inherited = true;
                var order = new[] { 0, 3, 4, 5, 6, 7 };

                foreach (var index in order)
                {
                    inherited &= specified[index] is null;

                    if (inherited)
                    {
                        result[index] = pattern ? EscapePattern(baseParts[index]) : baseParts[index];
                    }
                }

                if (!pattern && specified[0] is null && specified[3] is null && specified[4] is null && specified[1] is null)
                {
                    result[1] = baseParts[1];

                    if (specified[2] is null)
                    {
                        result[2] = baseParts[2];
                    }
                }
            }

            for (var i = 0; i < specified.Length; i++)
            {
                var value = specified[i];

                if (value is null)
                {
                    continue;
                }

                if (i == 0 && value.EndsWith(":", StringComparison.Ordinal))
                {
                    value = value.Substring(0, value.Length - 1);
                }
                else if (i == 6 && value.StartsWith("?", StringComparison.Ordinal) ||
                         i == 7 && value.StartsWith("#", StringComparison.Ordinal))
                {
                    value = value.Substring(1);
                }
                else if (i == 5 && baseUrl != null && !HasOpaquePath(baseUrl) && !IsAbsolutePath(value, pattern))
                {
                    var path = GetPath(baseUrl);
                    var slash = path.LastIndexOf('/');
                    var prefix = slash < 0 ? String.Empty : path.Substring(0, slash + 1);
                    value = (pattern ? EscapePattern(prefix) : prefix) + value;
                }

                result[i] = pattern ? value : CanonicalizeInput(value, i, result[0]);
            }

            if (!pattern && IsDefaultPort(result[0], result[4]))
            {
                result[4] = String.Empty;
            }

            return result;
        }

        private static Boolean IsAbsolutePath(String value, Boolean pattern) =>
            value.StartsWith("/", StringComparison.Ordinal) ||
            (pattern && (value.StartsWith("\\/", StringComparison.Ordinal) || value.StartsWith("{/", StringComparison.Ordinal)));

        private static String[] ToArray(UrlPatternInit input) => new[]
        {
            input.Protocol, input.Username, input.Password, input.Hostname,
            input.Port, input.Pathname, input.Search, input.Hash,
        };

        private static String[] FromUrl(Url url) => new[]
        {
            url.Scheme ?? String.Empty, url.UserName ?? String.Empty, url.Password ?? String.Empty,
            CanonicalizeIpv6(url.HostName ?? String.Empty), url.Port ?? String.Empty, GetPath(url),
            url.Query ?? String.Empty, url.Fragment ?? String.Empty,
        };

        private static Boolean HasOpaquePath(Url url) =>
            !url.Href.StartsWith(url.Scheme + ":/", StringComparison.OrdinalIgnoreCase);

        private static String GetPath(Url url)
        {
            if (!HasOpaquePath(url))
            {
                return url.PathName ?? String.Empty;
            }

            var href = url.Href;
            var start = url.Scheme.Length + 1;
            var end = href.IndexOfAny(new[] { '?', '#' }, start);
            return end < 0 ? href.Substring(start) : href.Substring(start, end - start);
        }

        private static Dictionary<String, Object> ToDictionary(UrlPatternInit input)
        {
            var values = ToArray(input);
            var result = new Dictionary<String, Object>();

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                {
                    result.Add(ComponentNames[i], values[i]);
                }
            }

            if (input.BaseUrl != null)
            {
                result.Add("baseURL", input.BaseUrl);
            }

            return result;
        }

        public static String EscapePattern(String value)
        {
            var result = new StringBuilder();

            foreach (var c in value)
            {
                if (c == '+' || c == '*' || c == '?' || c == ':' || c == '{' || c == '}' || c == '(' || c == ')' || c == '\\')
                {
                    result.Append('\\');
                }

                result.Append(c);
            }

            return result.ToString();
        }

        public static String CanonicalizeLiteral(String value, Int32 component, Boolean opaquePath, Boolean ipv6Hostname)
        {
            if (value.Length == 0)
            {
                return value;
            }

            if (component == 0)
            {
                return value.ToLowerInvariant();
            }

            if (component == 3)
            {
                if (ipv6Hostname)
                {
                    foreach (var c in value)
                    {
                        if (!Uri.IsHexDigit(c) && c != '[' && c != ']' && c != ':' && c != '.')
                        {
                            throw new ArgumentException("Invalid IPv6 URL pattern hostname.");
                        }
                    }

                    return value.ToLowerInvariant();
                }

                value = value.Replace("\t", String.Empty).Replace("\r", String.Empty).Replace("\n", String.Empty);
                var end = value.IndexOfAny(new[] { '/', '?', '#', '\\' });

                if (end >= 0)
                {
                    value = value.Substring(0, end);
                }

                foreach (var c in value)
                {
                    if (c <= ' ' || "#%/:<>?@[\\]^|".IndexOf(c) >= 0)
                    {
                        throw new ArgumentException("Invalid URL pattern hostname.");
                    }
                }

                var labels = value.Split('.');
                var idn = new IdnMapping();

                for (var i = 0; i < labels.Length; i++)
                {
                    if (labels[i].Length > 0)
                    {
                        labels[i] = idn.GetAscii(labels[i]).ToLowerInvariant();
                    }
                }

                return String.Join(".", labels);
            }

            return CanonicalizeInput(value, component, opaquePath ? "data" : null);
        }

        private static String CanonicalizeInput(String value, Int32 component, String protocol)
        {
            if (value.Length == 0)
            {
                return value;
            }

            var url = new Url("https://pattern.invalid/");

            switch (component)
            {
                case 0:
                    value = value.Replace("\t", String.Empty).Replace("\r", String.Empty).Replace("\n", String.Empty);

                    if (value.Length == 0 || !IsAsciiLetter(value[0]))
                    {
                        throw new ArgumentException("Invalid URL pattern protocol.");
                    }

                    foreach (var character in value)
                    {
                        if (!IsAsciiLetter(character) && (character < '0' || character > '9') &&
                            character != '+' && character != '-' && character != '.')
                        {
                            throw new ArgumentException("Invalid URL pattern protocol.");
                        }
                    }

                    return value.ToLowerInvariant();
                case 1:
                    return PercentEncode(value, component, false);
                case 2:
                    return PercentEncode(value, component, false);
                case 3:
                    url = new Url("https://" + value + "/");

                    if (url.IsInvalid || url.IsRelative || String.IsNullOrEmpty(url.HostName))
                    {
                        throw new ArgumentException("Invalid URL pattern hostname.");
                    }

                    return CanonicalizeIpv6(url.HostName);
                case 4:
                    value = value.Replace("\t", String.Empty).Replace("\r", String.Empty).Replace("\n", String.Empty);
                    var digits = 0;

                    while (digits < value.Length && value[digits] >= '0' && value[digits] <= '9')
                    {
                        digits++;
                    }

                    if (digits == 0 || !UInt16.TryParse(value.Substring(0, digits), NumberStyles.None, CultureInfo.InvariantCulture, out var port))
                    {
                        throw new ArgumentException("Invalid URL pattern port.");
                    }

                    return IsDefaultPort(protocol, port.ToString(CultureInfo.InvariantCulture)) ? String.Empty : port.ToString(CultureInfo.InvariantCulture);
                case 5:
                    if (protocol != null && !IsSpecialScheme(protocol))
                    {
                        return PercentEncode(value, component, true);
                    }

                    url.PathName = "/-" + value;
                    var path = url.PathName.StartsWith("/-", StringComparison.Ordinal) ? url.PathName.Substring(2) : url.PathName;
                    return PercentEncode(path, component, false);
                case 6:
                    return PercentEncode(value, component, false);
                case 7:
                    return PercentEncode(value, component, false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(component));
            }
        }

        private static String PercentEncode(String value, Int32 component, Boolean opaquePath)
        {
            var result = new StringBuilder();

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var encode = c <= '\u001f' || c >= '\u007f' ||
                    (!opaquePath && (c == ' ' || c == '"' || c == '<' || c == '>')) ||
                    (component == 1 || component == 2) && "#?`{}/:;=@[\\]^|".IndexOf(c) >= 0 ||
                    component == 5 && !opaquePath && "#?`{}".IndexOf(c) >= 0 ||
                    component == 6 && (c == '#' || c == '\'') ||
                    component == 7 && c == '`';

                if (!encode)
                {
                    result.Append(c);
                    continue;
                }

                var length = Char.IsHighSurrogate(c) && i + 1 < value.Length && Char.IsLowSurrogate(value[i + 1]) ? 2 : 1;
                var bytes = Encoding.UTF8.GetBytes(value.Substring(i, length));

                foreach (var b in bytes)
                {
                    result.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
                }

                i += length - 1;
            }

            return result.ToString();
        }

        private static Boolean MatchesSpecialScheme(UrlPatternComponent protocol) =>
            protocol.Match("ftp") != null || protocol.Match("file") != null || protocol.Match("http") != null ||
            protocol.Match("https") != null || protocol.Match("ws") != null || protocol.Match("wss") != null;

        private static Boolean IsSpecialScheme(String protocol) =>
            protocol == "ftp" || protocol == "file" || protocol == "http" || protocol == "https" || protocol == "ws" || protocol == "wss";

        private static Boolean IsAsciiLetter(Char c) => c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z';

        private static String CanonicalizeIpv6(String hostname)
        {
            if (!hostname.StartsWith("[", StringComparison.Ordinal) || !hostname.EndsWith("]", StringComparison.Ordinal) ||
                !IPAddress.TryParse(hostname.Substring(1, hostname.Length - 2), out var address) ||
                address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                return hostname;
            }

            var bytes = address.GetAddressBytes();
            var words = new Int32[8];
            var longestStart = -1;
            var longestLength = 1;

            for (var i = 0; i < words.Length; i++)
            {
                words[i] = bytes[i * 2] * 256 + bytes[i * 2 + 1];
            }

            for (var i = 0; i < words.Length;)
            {
                if (words[i] != 0)
                {
                    i++;
                    continue;
                }

                var start = i;

                while (i < words.Length && words[i] == 0)
                {
                    i++;
                }

                if (i - start > longestLength)
                {
                    longestStart = start;
                    longestLength = i - start;
                }
            }

            var result = new StringBuilder("[");

            for (var i = 0; i < words.Length; i++)
            {
                if (i == longestStart)
                {
                    result.Append("::");
                    i += longestLength - 1;
                }
                else
                {
                    if (result[result.Length - 1] != '[' && result[result.Length - 1] != ':')
                    {
                        result.Append(':');
                    }

                    result.Append(words[i].ToString("x", CultureInfo.InvariantCulture));
                }
            }

            return result.Append(']').ToString();
        }

        private static Boolean IsDefaultPort(String protocol, String port) =>
            protocol != null && port != null &&
            (String.Equals(protocol, "http", StringComparison.OrdinalIgnoreCase) && port == "80" ||
             String.Equals(protocol, "https", StringComparison.OrdinalIgnoreCase) && port == "443" ||
             String.Equals(protocol, "ws", StringComparison.OrdinalIgnoreCase) && port == "80" ||
             String.Equals(protocol, "wss", StringComparison.OrdinalIgnoreCase) && port == "443" ||
             String.Equals(protocol, "ftp", StringComparison.OrdinalIgnoreCase) && port == "21");

        private static Separator FindSeparator(String input, Int32 start, String delimiters, Int32 end = -1, Boolean ipv6 = false)
        {
            if (end < 0)
            {
                end = input.Length;
            }

            var groupDepth = 0;
            var regexDepth = 0;
            var bracketDepth = 0;
            var modifier = false;

            for (var i = start; i < end; i++)
            {
                var c = input[i];
                var escaped = c == '\\' && i + 1 < end;

                if (escaped)
                {
                    c = input[i + 1];
                }
                else if (c == '(')
                {
                    regexDepth++;
                    modifier = true;
                    continue;
                }
                else if (c == ')' && regexDepth > 0)
                {
                    regexDepth--;
                    modifier = true;
                    continue;
                }

                if (regexDepth > 0)
                {
                    if (escaped)
                    {
                        i++;
                    }

                    continue;
                }

                if (!escaped && c == '{')
                {
                    groupDepth++;
                    continue;
                }

                if (!escaped && c == '}' && groupDepth > 0)
                {
                    groupDepth--;
                    modifier = true;
                    continue;
                }

                if (groupDepth > 0)
                {
                    if (escaped)
                    {
                        i++;
                    }

                    continue;
                }

                if (ipv6 && c == '[')
                {
                    bracketDepth++;
                }
                else if (ipv6 && c == ']')
                {
                    bracketDepth--;
                }

                if (!escaped && c == ':' && i + 1 < end && IsNameStart(input[i + 1]))
                {
                    i += 2;

                    while (i < end && IsNamePart(input[i]))
                    {
                        i++;
                    }

                    i--;
                    modifier = true;
                    continue;
                }

                if (bracketDepth == 0 && delimiters.IndexOf(c) >= 0 && (c != '?' || escaped || !modifier))
                {
                    return new Separator(i, escaped ? 2 : 1, c);
                }

                if (escaped)
                {
                    i++;
                    modifier = false;
                }
                else
                {
                    modifier = c == '*';
                }
            }

            return new Separator(-1, 0, '\0');
        }

        private static Boolean IsNameStart(Char c) => Char.IsLetter(c) || c == '_' || c == '$';

        private static Boolean IsNamePart(Char c) => IsNameStart(c) || Char.IsDigit(c);

        private readonly struct Separator
        {
            public Separator(Int32 index, Int32 length, Char value)
            {
                Index = index;
                Length = length;
                Value = value;
            }

            public Int32 Index { get; }
            public Int32 Length { get; }
            public Char Value { get; }
        }
    }
}
