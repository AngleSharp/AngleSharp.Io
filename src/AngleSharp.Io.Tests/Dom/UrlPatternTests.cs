namespace AngleSharp.Io.Tests.Dom
{
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;

    [TestFixture]
    public class UrlPatternTests
    {
        [Test]
        public void MatchesNamedAndWildcardGroups()
        {
            var pattern = new UrlPattern("https://*.example.com/books/:id");
            var match = pattern.Exec("https://store.example.com/books/123?source=home");

            Assert.IsNotNull(match);
            Assert.AreEqual("store", Groups(match, "hostname")["0"]);
            Assert.AreEqual("123", Groups(match, "pathname")["id"]);
            Assert.AreEqual("", Component(match, "port")["input"]);
            Assert.IsFalse(pattern.Test("https://store.example.com:8443/books/123"));
            Assert.IsFalse(pattern.Test("https://store.example.com/books/123/extra"));
        }

        [Test]
        public void ResolvesPatternAndInputAgainstBaseUrls()
        {
            var pattern = new UrlPattern("../books/:id", "https://example.com/a/page?old=1");
            var match = pattern.Exec("/books/42", "https://example.com/elsewhere");

            Assert.IsNotNull(match);
            Assert.AreEqual("/books/:id", pattern.Pathname);
            Assert.AreEqual("42", Groups(match, "pathname")["id"]);
            CollectionAssert.AreEqual(new[] { "/books/42", "https://example.com/elsewhere" }, (Object[])match["inputs"]);
            Assert.IsNull(pattern.Exec("/books/42"));
        }

        [Test]
        public void MatchesOptionalAndRepeatingSegments()
        {
            var optional = new UrlPattern(new UrlPatternInit { Pathname = "/books/:id?" });
            var repeating = new UrlPattern(new UrlPatternInit { Pathname = "/books/:id+" });

            Assert.IsTrue(optional.Test("https://example.com/books"));
            Assert.IsTrue(optional.Test("https://example.com/books/1"));
            Assert.IsFalse(optional.Test("https://example.com/books/"));
            Assert.AreEqual("1/2", Groups(repeating.Exec("https://example.com/books/1/2"), "pathname")["id"]);
            Assert.IsFalse(repeating.Test("https://example.com/books"));
        }

        [Test]
        public void MatchesRegexAndBracedGroupsIgnoringCase()
        {
            var pattern = new UrlPattern(
                new UrlPatternInit { Pathname = "/books/:id(\\d+){-:title}?" },
                new UrlPatternOptions { IgnoreCase = true });

            Assert.IsTrue(pattern.HasRegExpGroups);
            Assert.AreEqual("TITLE", Groups(pattern.Exec("https://example.com/BOOKS/123-TITLE"), "pathname")["title"]);
            Assert.IsTrue(pattern.Test("https://example.com/books/123"));
            Assert.IsFalse(pattern.Test("https://example.com/books/abc"));
        }

        [Test]
        public void AcceptsComponentInputsAndInheritsBase()
        {
            var pattern = new UrlPattern(new UrlPatternInit { Pathname = "/books/:id", BaseUrl = "https://example.com/old?q=1" });
            var match = pattern.Exec(new UrlPatternInit { Pathname = "/books/5", BaseUrl = "https://example.com/other" });

            Assert.IsNotNull(match);
            Assert.AreEqual("https", pattern.Protocol);
            Assert.AreEqual("example.com", pattern.Hostname);
            Assert.AreEqual("*", pattern.Search);
            Assert.AreEqual("5", Groups(match, "pathname")["id"]);
        }

        [Test]
        public void DefaultPatternMatchesEmptyInput()
        {
            var pattern = new UrlPattern();

            Assert.IsTrue(pattern.Test());
            Assert.AreEqual("*", pattern.Protocol);
            Assert.AreEqual("", Groups(pattern.Exec(), "protocol")["0"]);
        }

        [Test]
        public void InvalidPatternsThrowAndInvalidUrlsDoNotMatch()
        {
            Assert.Throws<ArgumentException>(() => new UrlPattern("/relative"));
            Assert.Throws<ArgumentException>(() => new UrlPattern(new UrlPatternInit { Pathname = "/:id/:id" }));
            Assert.Throws<ArgumentException>(() => new UrlPattern(new UrlPatternInit { Pathname = "/(abc" }));
            Assert.IsFalse(new UrlPattern().Test("/relative"));
            Assert.IsNull(new UrlPattern().Exec(new UrlPatternInit { BaseUrl = "not a URL" }));
        }

        [Test]
        public void NormalizesComponentsAndEscapesInheritedPatterns()
        {
            var pattern = new UrlPattern(new UrlPatternInit
            {
                Hostname = "BÜCHER.example",
                Pathname = "/café",
            });
            var inherited = new UrlPattern("#section", "https://example.com/?q=*");

            Assert.AreEqual("xn--bcher-kva.example", pattern.Hostname);
            Assert.AreEqual("/caf%C3%A9", pattern.Pathname);
            Assert.IsTrue(pattern.Test("https://bücher.example/café"));
            Assert.AreEqual("q=\\*", inherited.Search);
            Assert.IsTrue(inherited.Test("https://example.com/?q=*#section"));
        }

        [Test]
        public void TreatsUnpairedSurrogatesAsReplacementCharacters()
        {
            var pattern = new UrlPattern(new UrlPatternInit { Pathname = "\ud83d \udeb2" });

            Assert.AreEqual("%EF%BF%BD%20%EF%BF%BD", pattern.Pathname);
        }

        [Test]
        public void SerializesGroupsWithoutChangingTheirMeaning()
        {
            var original = new UrlPattern(new UrlPatternInit { Pathname = "{:foo}{bar(.*)}" });
            var reparsed = new UrlPattern(new UrlPatternInit { Pathname = original.Pathname });
            var repeated = new UrlPattern(new UrlPatternInit { Pathname = "/{:id-}+" });

            Assert.AreEqual(":foo{bar*}", original.Pathname);
            Assert.AreEqual("foo", Groups(reparsed.Exec(new UrlPatternInit { Pathname = "foobarbaz" }), "pathname")["foo"]);
            Assert.AreEqual("one-two", Groups(repeated.Exec(new UrlPatternInit { Pathname = "/one-two-" }), "pathname")["id"]);
        }

        [Test]
        public void MatchesOpaqueUrlsAndIpv6Addresses()
        {
            var opaque = new UrlPattern(new UrlPatternInit { Protocol = "data", Pathname = ":value" });
            var ipv6 = new UrlPattern(new UrlPatternInit { Hostname = "[\\:\\:ab\\::value]" });

            Assert.AreEqual("hello/world", Groups(opaque.Exec("data:hello/world"), "pathname")["value"]);
            Assert.AreEqual("1", Groups(ipv6.Exec("http://[::ab:1]/"), "hostname")["value"]);
            Assert.IsNull(opaque.Exec("foo", "data:base"));
        }

        [Test]
        public void MatchesUnicodeSetOperationsInRegexGroups()
        {
            var difference = new UrlPattern(new UrlPatternInit { Pathname = "/([[a-z]--a])" });
            var intersection = new UrlPattern(new UrlPatternInit { Pathname = "/([\\d&&[0-1]])" });

            Assert.IsFalse(difference.Test(new UrlPatternInit { Pathname = "/a" }));
            Assert.IsTrue(difference.Test(new UrlPatternInit { Pathname = "/z" }));
            Assert.IsTrue(intersection.Test(new UrlPatternInit { Pathname = "/0" }));
            Assert.IsFalse(intersection.Test(new UrlPatternInit { Pathname = "/3" }));
            Assert.Throws<NotSupportedException>(() => new UrlPattern(new UrlPatternInit { Pathname = "/([^a&&b])" }));
            Assert.Throws<ArgumentException>(() => new UrlPattern(new UrlPatternInit { Pathname = "/(\\m)" }));
        }

        [Test]
        public void UnmatchedOptionalGroupsRemainPresent()
        {
            var pattern = new UrlPattern(new UrlPatternInit { Pathname = "*{}**?" });
            var groups = Groups(pattern.Exec(new UrlPatternInit { Pathname = "foobar" }), "pathname");

            Assert.AreEqual("foobar", groups["0"]);
            Assert.IsTrue(groups.ContainsKey("1"));
            Assert.IsNull(groups["1"]);
        }

        private static Dictionary<String, Object> Component(Dictionary<String, Object> match, String component) =>
            (Dictionary<String, Object>)match[component];

        private static Dictionary<String, Object> Groups(Dictionary<String, Object> match, String component) =>
            (Dictionary<String, Object>)Component(match, component)["groups"];
    }
}
