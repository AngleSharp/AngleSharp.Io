namespace AngleSharp.Io.Tests.Network
{
    using AngleSharp.Dom;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    [TestFixture]
    public class CookieHandlingTests
    {
        [Test]
        public async Task SettingOneCookiesInOneRequestAppearsInDocument()
        {
            if (Helper.IsNetworkAvailable())
            {
                var url = "https://httpbingo.org/cookies/set?k1=v1";
                var config = Configuration.Default.WithCookies().WithDefaultLoader();
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
                var url = "https://httpbingo.org/cookies/set?k2=v2&k1=v1";
                var config = Configuration.Default.WithCookies().WithDefaultLoader();
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
                var url = "https://httpbingo.org/cookies/set?test=baz&k2=v2&k1=v1&foo=bar";
                var config = Configuration.Default.WithCookies().WithDefaultLoader();
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
                var baseUrl = "https://httpbingo.org/cookies";
                var url = baseUrl + "/set?test=baz&k2=v2&k1=v1&foo=bar";
                                var config = Configuration.Default.WithCookies().WithDefaultLoader();
                var context = BrowsingContext.New(config);
                await context.OpenAsync(url);
                var document = await context.OpenAsync(baseUrl);

                AssertCookies(document.Body.TextContent,
                    new KeyValuePair<String, String>("foo", "bar"),
                    new KeyValuePair<String, String>("k1", "v1"),
                    new KeyValuePair<String, String>("k2", "v2"),
                    new KeyValuePair<String, String>("test", "baz"));
            }
        }

        [Test]
        public async Task SettingCookieIsPreservedViaRedirect()
        {
            if (Helper.IsNetworkAvailable())
            {
                var cookieUrl = "https://httpbingo.org/cookies/set?test=baz";
                var redirectUrl = "https://httpbingo.org/redirect-to?url=https%3A%2F%2Fhttpbingo.org%2Fcookies";
                                var config = Configuration.Default.WithCookies().WithDefaultLoader();
                var context = BrowsingContext.New(config);
                await context.OpenAsync(cookieUrl);
                var document = await context.OpenAsync(redirectUrl);

                AssertCookies(document.Body.TextContent,
                    new KeyValuePair<String, String>("test", "baz"));
            }
        }

        [Test]
        public async Task SettingCookieIsPreservedViaRedirectToDifferentProtocol()
        {
            if (Helper.IsNetworkAvailable())
            {
                var cookieUrl = "https://httpbingo.org/cookies/set?test=baz";
                var redirectUrl = "http://httpbingo.org/redirect-to?url=http%3A%2F%2Fhttpbingo.org%2Fcookies";
                var config = Configuration.Default.WithCookies().WithDefaultLoader();
                var context = BrowsingContext.New(config);
                await context.OpenAsync(cookieUrl);
                var document = await context.OpenAsync(redirectUrl);

                AssertCookies(document.Body.TextContent);
            }
        }

        private static void AssertCookies(String body, params KeyValuePair<String, String>[] expected)
        {
            var parsed = JObject.Parse(body);
            var source = parsed["cookies"] as JObject ?? parsed;
            var actual = source.Properties().ToDictionary(m => m.Name, m => m.Value.ToString());

            CollectionAssert.AreEquivalent(expected.Select(m => m.Key), actual.Keys);

            foreach (var pair in expected)
            {
                Assert.AreEqual(pair.Value, actual[pair.Key]);
            }
        }
    }
}
