namespace AngleSharp.Io.Tests.Network
{
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    [TestFixture]
    public class CookieHandlingTests
    {
        [Test]
        public async Task SettingOneCookiesInOneRequestAppearsInDocument()
        {
            if (Helper.IsNetworkAvailable())
            {
                var url = "https://httpbin.org/cookies/set?k1=v1";
                var config = Configuration.Default.WithCookies().WithRequesters().WithDefaultLoader();
                var context = BrowsingContext.New(config);
                var document = await context.OpenAsync(url);

                Assert.AreEqual("k1=v1", document.Cookie);
            }
        }

        [Test]
        public async Task SettingTwoCookiesInOneRequestAppearsInDocument()
        {
            if (Helper.IsNetworkAvailable())
            {
                var url = "https://httpbin.org/cookies/set?k2=v2&k1=v1";
                var config = Configuration.Default.WithCookies().WithRequesters().WithDefaultLoader();
                var context = BrowsingContext.New(config);
                var document = await context.OpenAsync(url);
                var cookies = document.Cookie.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries);
                CollectionAssert.AreEquivalent(new[] { "k2=v2", "k1=v1" }, cookies);
            }
        }

        [Test]
        public async Task SettingThreeCookiesInOneRequestAppearsInDocument()
        {
            if (Helper.IsNetworkAvailable())
            {
                var url = "https://httpbin.org/cookies/set?test=baz&k2=v2&k1=v1&foo=bar";
                var config = Configuration.Default.WithCookies().WithRequesters().WithDefaultLoader();
                var context = BrowsingContext.New(config);
                var document = await context.OpenAsync(url);
                var cookies = document.Cookie.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries);
                CollectionAssert.AreEquivalent(new[] { "test=baz", "k2=v2", "k1=v1", "foo=bar" }, cookies);
            }
        }

        [Test]
        public async Task SettingThreeCookiesInOneRequestAreTransportedToNextRequest()
        {
            if (Helper.IsNetworkAvailable())
            {
                var baseUrl = "https://httpbin.org/cookies";
                var url = baseUrl + "/set?test=baz&k2=v2&k1=v1&foo=bar";
                var config = Configuration.Default.WithCookies().WithRequesters().WithDefaultLoader();
                var context = BrowsingContext.New(config);
                await context.OpenAsync(url);
                var document = await context.OpenAsync(baseUrl);

                Assert.AreEqual(@"{
  ""cookies"": {
    ""foo"": ""bar"", 
    ""k1"": ""v1"", 
    ""k2"": ""v2"", 
    ""test"": ""baz""
  }
}
".Replace(Environment.NewLine, "\n"), document.Body.TextContent);
            }
        }

        [Test]
        public async Task SettingCookieIsPreservedViaRedirect()
        {
            if (Helper.IsNetworkAvailable())
            {
                var cookieUrl = "https://httpbin.org/cookies/set?test=baz";
                var redirectUrl = "https://httpbin.org/redirect-to?url=https%3A%2F%2Fhttpbin.org%2Fcookies";
                var config = Configuration.Default.WithCookies().WithRequesters().WithDefaultLoader();
                var context = BrowsingContext.New(config);
                await context.OpenAsync(cookieUrl);
                var document = await context.OpenAsync(redirectUrl);

                Assert.AreEqual(@"{
  ""cookies"": {
    ""test"": ""baz""
  }
}
".Replace(Environment.NewLine, "\n"), document.Body.TextContent);
            }
        }

        [Test]
        public async Task SettingCookieIsPreservedViaRedirectToDifferentProtocol()
        {
            if (Helper.IsNetworkAvailable())
            {
                var cookieUrl = "https://httpbin.org/cookies/set?test=baz";
                var redirectUrl = "http://httpbin.org/redirect-to?url=http%3A%2F%2Fhttpbin.org%2Fcookies";
                var config = Configuration.Default.WithCookies().WithRequesters().WithDefaultLoader();
                var context = BrowsingContext.New(config);
                await context.OpenAsync(cookieUrl);
                var document = await context.OpenAsync(redirectUrl);

                Assert.AreEqual(@"{
  ""cookies"": {
    ""test"": ""baz""
  }
}
".Replace(Environment.NewLine, "\n"), document.Body.TextContent);
            }
        }
    }
}
