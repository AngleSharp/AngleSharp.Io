namespace AngleSharp.Io.Cookie
{
    using AngleSharp.Text;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using static Helpers;

    internal static class NetscapeCookieSerializer
    {
        private static readonly Regex NewLine = new Regex("\\r\\n|\\n");
        private static readonly Regex DetectHead = new Regex("^\\#(?: Netscape)? HTTP Cookie File");
        private static readonly Regex WhitespacesOnly = new Regex("^\\s*$");
        private static readonly Regex WhitespacesWithComment = new Regex("^\\s*\\#");
        private static readonly Regex HttpOnlyDeclaration = new Regex("^#HttpOnly_(.*)");
        private static readonly Regex DotCheck = new Regex("^\\.");

        public static String Serialize(IEnumerable<WebCookie> cookies, Boolean httpOnlyExtension)
        {
            var data = StringBuilderPool.Obtain();

            data.Append("# Netscape HTTP Cookie File\n");
            data.Append("# http://www.netscape.com/newsref/std/cookie_spec.html\n");
            data.Append("# This is a generated file!  Do not edit.\n\n");

            foreach (var cookie in cookies)
            {
                var cookie_domain = cookie.Domain;
                var head = httpOnlyExtension && cookie.IsHttpOnly ? "#HttpOnly_" : String.Empty;

                if (cookie.IsHostOnly ?? false == false)
                {
                    cookie_domain = $".{cookie_domain}";
                }

                var line = String.Join("\t",
                    $"{head}{cookie_domain}",
                    cookie_domain.StartsWith(".") ? "TRUE" : "FALSE",
                    cookie.Path,
                    cookie.IsSecure ? "TRUE" : "FALSE",
                    ToEpoch(cookie.Expires),
                    EncodeURIComponent(cookie.Key),
                    EncodeURIComponent(cookie.Value)
                );

                data.Append($"{line}\n");
            }

            return data.ToPool();
        }

        public static List<WebCookie> Deserialize(String content, Boolean forceParse, Boolean httpOnlyExtension)
        {
            var result = new List<WebCookie>();

            if (!String.IsNullOrEmpty(content))
            {
                var lines = NewLine.Split(content);

                InitialCheck(lines, forceParse);
                ParseAll(lines, forceParse, httpOnlyExtension, result);
            }

            return result;
        }

        private static void InitialCheck(IEnumerable<String> lines, Boolean forceParse)
        {
            var magic = lines.FirstOrDefault();

            if ((String.IsNullOrEmpty(magic) || !DetectHead.IsMatch(magic)) && !forceParse)
            {
                throw new InvalidOperationException("The given file does not look like a Netscape cookies file!");
            }
        }

        private static void ParseAll(IEnumerable<String> lines, Boolean forceParse, Boolean httpOnlyExtension, List<WebCookie> result)
        {
            var httpOnly = false;
            var lineCount = 0;

            foreach (var current in lines)
            {
                var line = current;
                ++lineCount;

                if (!WhitespacesOnly.IsMatch(line) && (!WhitespacesWithComment.IsMatch(line) || HttpOnlyDeclaration.IsMatch(line)))
                {
                    httpOnly = httpOnlyExtension && HttpOnlyDeclaration.IsMatch(line);

                    if (httpOnly)
                    {
                        line = HttpOnlyDeclaration.Replace(line, "$1");
                    }

                    var parsed = line.Split('\t');

                    if (parsed.Length != 7)
                    {
                        if (!forceParse)
                        {
                            throw new InvalidOperationException($"Line {lineCount} is not valid");
                        }

                        continue;
                    }

                    var domain = CanonicalDomain(parsed[0]);
                    var cookie = new WebCookie
                    {
                        Domain = domain,
                        Path = parsed[2],
                        IsSecure = parsed[3].Is("TRUE"),
                        Expires = Int32.TryParse(parsed[4], out var seconds) ? FromEpoch(seconds) : null,
                        Key = DecodeURIComponent(parsed[5]),
                        Value = DecodeURIComponent(parsed[6]),
                        IsHttpOnly = httpOnly,
                        IsHostOnly = !DotCheck.IsMatch(domain),
                    };

                    result.Add(cookie);
                }
            }
        }
    }
}
