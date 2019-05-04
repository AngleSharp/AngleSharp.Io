namespace AngleSharp.Io.Tests.Cookie
{
    using AngleSharp.Io.Cookie;
    using NUnit.Framework;
    using System;
    using System.Linq;

    [TestFixture]
    public class ParsingTests
    {
        [Test]
        public void SettingBasicCookie()
        {
            var container = CreateContainerWithSetup(c =>
            {
                var url = new Url("http://example.com/index.html");
                c.SetCookie(url, "a=b; Domain=example.com; Path=/");
            });
            var cookie = container.Cookies.First();
            Assert.IsFalse(cookie.IsHostOnly);
            Assert.IsFalse(cookie.IsPersistent);
            Assert.AreEqual(false, cookie.TimeToLive.HasValue);
            Assert.AreEqual(cookie, container.FindCookies("example.com").First());
        }

        [Test]
        public void SettingNoPathCookie()
        {
            var container = CreateContainerWithSetup(c =>
            {
                var url = new Url("http://example.com/index.html");
                c.SetCookie(url, "a=b; Domain=example.com");
            });
            var cookie = container.Cookies.First();
            Assert.IsFalse(cookie.IsHostOnly);
            Assert.AreEqual("example.com", cookie.Domain);
            Assert.AreEqual("/", cookie.Path);
            Assert.IsFalse(cookie.IsPersistent);
            Assert.AreEqual(false, cookie.TimeToLive.HasValue);
            Assert.AreEqual(cookie, container.FindCookies("example.com").First());
        }

        [Test]
        public void SettingSessionCookie()
        {
            var container = CreateContainerWithSetup(c =>
            {
                var url = new Url("http://www.example.com/dir/index.html");
                c.SetCookie(url, "a=b");
            });
            var cookie = container.Cookies.First();
            Assert.IsTrue(cookie.IsHostOnly);
            Assert.AreEqual("www.example.com", cookie.Domain);
            Assert.AreEqual("/dir", cookie.Path);
            Assert.IsFalse(cookie.IsPersistent);
            Assert.AreEqual(false, cookie.TimeToLive.HasValue);
            Assert.AreEqual(cookie, container.FindCookies("www.example.com").First());
        }

        [Test]
        public void SettingWrongDomainCookie()
        {
            var container = CreateContainerWithSetup(c =>
            {
                var url = new Url("http://example.com/index.html");
                c.SetCookie(url, "a=b; Domain=www.example.com; Path=/");
            });
            var cookie = container.Cookies.FirstOrDefault();
            Assert.IsNull(cookie);
        }

        [Test]
        public void SettingSuperDomainCookie()
        {
            var container = CreateContainerWithSetup(c =>
            {
                var url = new Url("http://www.app.example.com/index.html");
                c.SetCookie(url, "a=b; Domain=example.com; Path=/");
            });
            var cookie = container.Cookies.FirstOrDefault();
            Assert.IsNotNull(cookie);
        }

        [Test]
        public void SettingSubPathCookieOnSuperDomain()
        {
            var container = CreateContainerWithSetup(c =>
            {
                var url = new Url("http://www.example.com/index.html");
                c.SetCookie(url, "a=b; Domain=example.com; Path=/subpath");
            });
            var cookie = container.Cookies.FirstOrDefault();
            Assert.IsNotNull(cookie);
            Assert.AreEqual("example.com", cookie.Domain);
            Assert.AreEqual("/subpath", cookie.Path);
        }

        [Test]
        public void SimpleCookie()
        {
            var cookie = WebCookie.FromString("a=bcd");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("bcd", cookie.Value);
            Assert.IsTrue(cookie.Validate());
            Assert.AreEqual(null, cookie.Path);
            Assert.AreEqual(null, cookie.Domain);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void ExpiringCookie()
        {
            var cookie = WebCookie.FromString("a=bcd; Expires=Tue, 18 Oct 2011 07:05:03 GMT");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("bcd", cookie.Value);
            Assert.IsTrue(cookie.Validate());
            Assert.AreEqual(null, cookie.Path);
            Assert.AreEqual(null, cookie.Domain);
            Assert.AreEqual(new DateTime(2011, 10, 18), cookie.Expires.Value.Date);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void ExpiringCookieWithPath()
        {
            var cookie = WebCookie.FromString("a=\"xyzzy!\"; Expires=Tue, 18 Oct 2011 07:05:03 GMT; Path=/aBc");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("\"xyzzy!\"", cookie.Value);
            Assert.AreEqual("/aBc", cookie.Path);
            Assert.AreEqual(null, cookie.Domain);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
            Assert.AreEqual(new DateTime(2011, 10, 18), cookie.Expires.Value.Date);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void CompleteCookie()
        {
            var cookie = WebCookie.FromString("abc=\"xyzzy!\"; Expires=Tue, 18 Oct 2011 07:05:03 GMT; Path=/aBc; Domain=example.com; Secure; HTTPOnly; Max-Age=1234; Foo=Bar; Baz");
            Assert.AreEqual("abc", cookie.Key);
            Assert.AreEqual("\"xyzzy!\"", cookie.Value);
            Assert.AreEqual("/aBc", cookie.Path);
            Assert.AreEqual("example.com", cookie.Domain);
            Assert.IsTrue(cookie.IsSecure);
            Assert.IsTrue(cookie.IsHttpOnly);
            Assert.AreEqual(1234, cookie.MaxAge.Value);
            Assert.AreEqual(new DateTime(2011, 10, 18), cookie.Expires.Value.Date);
            Assert.AreEqual("Foo=Bar", cookie.Extensions.Skip(0).First());
            Assert.AreEqual("Baz", cookie.Extensions.Skip(1).First());
        }

        [Test]
        public void CookieWithInvalidExpirationDate()
        {
            var cookie = WebCookie.FromString("a=b; Expires=xyzzy");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("b", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.AreEqual(null, cookie.Domain);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void CookieWithZeroMaxAge()
        {
            var cookie = WebCookie.FromString("a=b; Max-Age=0");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("b", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.AreEqual(null, cookie.Domain);
            Assert.AreEqual(0, cookie.MaxAge.Value);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void CookieWithNegativeMaxAge()
        {
            var cookie = WebCookie.FromString("a=b; Max-Age=-1");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("b", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.AreEqual(null, cookie.Domain);
            Assert.AreEqual(-1, cookie.MaxAge.Value);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void CookieWitDotDomain()
        {
            var cookie = WebCookie.FromString("a=b; domain=.");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("b", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.AreEqual(null, cookie.Domain);
            Assert.IsFalse(cookie.MaxAge.HasValue);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void CookieWitDottedDomain()
        {
            var cookie = WebCookie.FromString("a=b; domain=.example.com");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("b", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.AreEqual("example.com", cookie.Domain);
            Assert.IsFalse(cookie.MaxAge.HasValue);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void CookieWitUppercaseDomain()
        {
            var cookie = WebCookie.FromString("a=b; domain=EXAMPLE.COM");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("b", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.AreEqual("example.com", cookie.Domain);
            Assert.IsFalse(cookie.MaxAge.HasValue);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void CookieWithEmptyPath()
        {
            var cookie = WebCookie.FromString("a=b; path=");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("b", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.AreEqual(null, cookie.Domain);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void CookieWithTrailingSemicolonsAfterPath()
        {
            var cookie = WebCookie.FromString("a=b; path=/;;;");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("b", cookie.Value);
            Assert.AreEqual("/", cookie.Path);
            Assert.AreEqual(null, cookie.Domain);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void CookieWithTrailingSemicolons()
        {
            var cookie = WebCookie.FromString("a=b;;;;");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("b", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.AreEqual(null, cookie.Domain);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void SecureCookieWithValue()
        {
            var cookie = WebCookie.FromString("a=b; Secure=xyxz");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("b", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.AreEqual(null, cookie.Domain);
            Assert.IsTrue(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void HttpOnlyCookieWithValue()
        {
            var cookie = WebCookie.FromString("a=b; HttpOnly=xyxz");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("b", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.AreEqual(null, cookie.Domain);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsTrue(cookie.IsHttpOnly);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.AreEqual(0, cookie.Extensions.Count());
        }

        [Test]
        public void GoogleGaps()
        {
            var cookie = WebCookie.FromString("GAPS=1:A1aaaaAaAAa1aaAaAaaAAAaaa1a11a:aaaAaAaAa-aaaA1-;Path=/;Expires=Thu, 17-Apr-2014 02:12:29 GMT;Secure;HttpOnly");
            Assert.AreEqual("GAPS", cookie.Key);
            Assert.AreEqual("1:A1aaaaAaAAa1aaAaAaaAAAaaa1a11a:aaaAaAaAa-aaaA1-", cookie.Value);
            Assert.AreEqual("/", cookie.Path);
            Assert.AreEqual(new DateTime(2014, 4, 17), cookie.Expires.Value.Date);
            Assert.IsTrue(cookie.IsSecure);
            Assert.IsTrue(cookie.IsHttpOnly);
        }

        [Test]
        public void CookieWithManyEqualSigns()
        {
            var cookie = WebCookie.FromString("queryPref=b=c&d=e; Path=/f=g; Expires=Thu, 17 Apr 2014 02:12:29 GMT; HttpOnly");
            Assert.AreEqual("queryPref", cookie.Key);
            Assert.AreEqual("b=c&d=e", cookie.Value);
            Assert.AreEqual("/f=g", cookie.Path);
            Assert.AreEqual(new DateTime(2014, 4, 17), cookie.Expires.Value.Date);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsTrue(cookie.IsHttpOnly);
        }

        [Test]
        public void CookieWithSpacesInTheValue()
        {
            var cookie = WebCookie.FromString("a=one two three");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("one two three", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
        }

        [Test]
        public void CookieWithQuotesSpacesInTheValue()
        {
            var cookie = WebCookie.FromString("a=\"one two three\"");
            Assert.AreEqual("a", cookie.Key);
            Assert.AreEqual("\"one two three\"", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
        }

        [Test]
        public void CookieWithNonAsciiInValue()
        {
            var cookie = WebCookie.FromString("farbe=weiß");
            Assert.AreEqual("farbe", cookie.Key);
            Assert.AreEqual("weiß", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
        }

        [Test]
        public void CookieWithEmptyKey()
        {
            var cookie = WebCookie.FromString("=foo", loose: true);
            Assert.AreEqual("", cookie.Key);
            Assert.AreEqual("foo", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
        }

        [Test]
        public void CookieWithNonExistingKey()
        {
            var cookie = WebCookie.FromString("foo", loose: true);
            Assert.AreEqual("", cookie.Key);
            Assert.AreEqual("foo", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
        }

        [Test]
        public void CookieWithWeirdFormat()
        {
            var cookie = WebCookie.FromString("=foo=bar", loose: true);
            Assert.AreEqual("foo", cookie.Key);
            Assert.AreEqual("bar", cookie.Value);
            Assert.AreEqual(null, cookie.Path);
            Assert.IsFalse(cookie.Expires.HasValue);
            Assert.IsFalse(cookie.IsSecure);
            Assert.IsFalse(cookie.IsHttpOnly);
        }

        private static AdvancedCookieProvider CreateContainerWithSetup(Action<ICookieProvider> setup)
        {
            var container = new AdvancedCookieProvider(new MemoryFileHandler());
            setup(container);
            return container;
        }

        private static ICookieProvider CreateContainer() =>
            new AdvancedCookieProvider(new MemoryFileHandler());
    }
}
