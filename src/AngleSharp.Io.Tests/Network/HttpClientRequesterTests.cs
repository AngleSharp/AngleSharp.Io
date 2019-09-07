namespace AngleSharp.Io.Tests.Network
{
    using AngleSharp.Io.Network;
    using AngleSharp.Io.Tests.Network.Mocks;
    using FluentAssertions;
    using NUnit.Framework;
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using AngleSharpHttpMethod = AngleSharp.Io.HttpMethod;
    using NetHttpMethod = System.Net.Http.HttpMethod;

    [TestFixture]
    public class HttpClientRequesterTests
    {
        [Test]
        public async Task RequestWithContent()
        {
            // ARRANGE
            var ts = new HttpMockState();

            // ACT
            await ts.Target.RequestAsync(ts.Request, CancellationToken.None);

            // ASSERT
            ts.HttpRequestMessage.Method.Should().Be(NetHttpMethod.Post);
            ts.HttpRequestMessage.RequestUri.Should().Be(new Uri("http://example/path?query=value"));
            Encoding.UTF8.GetString(ts.HttpRequestMessageContent).Should().Be("\"request\"");
            ts.HttpRequestMessage.Content.Headers.Select(p => p.Key).Should().BeEquivalentTo(new[] {"Content-Type", "Content-Length"});
            ts.HttpRequestMessage.Content.Headers.ContentType.ToString().Should().Be("application/json");
            ts.HttpRequestMessage.Content.Headers.ContentLength.Should().Be(9);
            ts.HttpRequestMessage.Properties.Should().BeEmpty();
            ts.HttpRequestMessage.Headers.Select(p => p.Key).Should().BeEquivalentTo(new[] {"User-Agent", "Cookie"});
            ts.HttpRequestMessage.Headers.UserAgent.ToString().Should().Be("Foo/2.0");
            ts.HttpRequestMessage.Headers.Single(p => p.Key == "Cookie").Value.Should().BeEquivalentTo(new[] {"foo=bar"});
        }

        [Test]
        public async Task RequestWithoutContent()
        {
            // ARRANGE
            var ts = new HttpMockState { Request = { Content = null, Method = AngleSharpHttpMethod.Get } };

            // ACT
            await ts.Target.RequestAsync(ts.Request, CancellationToken.None);

            // ASSERT
            ts.HttpRequestMessage.Method.Should().Be(NetHttpMethod.Get);
            ts.HttpRequestMessage.RequestUri.Should().Be(new Uri("http://example/path?query=value"));
            ts.HttpRequestMessage.Content.Should().BeNull();
            ts.HttpRequestMessage.Properties.Should().BeEmpty();
            ts.HttpRequestMessage.Headers.Select(p => p.Key).Should().BeEquivalentTo(new[] {"User-Agent", "Cookie"});
            ts.HttpRequestMessage.Headers.UserAgent.ToString().Should().Be("Foo/2.0");
            ts.HttpRequestMessage.Headers.Single(p => p.Key == "Cookie").Value.Should().BeEquivalentTo(new[] {"foo=bar"});
        }

        [Test]
        public async Task ResponseWithContent()
        {
            // ARRANGE
            var ts = new HttpMockState();

            // ACT
            var response = await ts.Target.RequestAsync(ts.Request, CancellationToken.None);

            // ASSERT
            response.Address.Should().BeEquivalentTo(ts.Request.Address);
            response.StatusCode.Should().Be(ts.HttpResponseMessage.StatusCode);
            response.Headers.Keys.Should().BeEquivalentTo(new[] {"Server", "X-Powered-By", "X-CSV", "Content-Type"});
            response.Headers["Server"].Should().Be("Fake");
            response.Headers["X-Powered-By"].Should().Be("Magic");
            response.Headers["X-CSV"].Should().Be("foo, bar");
            response.Headers["Content-Type"].Should().Be("application/json; charset=utf-8");
            new StreamReader(response.Content, Encoding.UTF8).ReadToEnd().Should().Be("\"response\"");
        }

        [Test]
        public async Task ResponseWithoutContent()
        {
            // ARRANGE
            var ts = new HttpMockState { HttpResponseMessage = { Content = null } };

            // ACT
            var response = await ts.Target.RequestAsync(ts.Request, CancellationToken.None);

            // ASSERT
            response.Address.Should().BeEquivalentTo(ts.Request.Address);
            response.StatusCode.Should().Be(ts.HttpResponseMessage.StatusCode);
            response.Headers.Keys.Should().BeEquivalentTo(new[] {"Server", "X-Powered-By", "X-CSV"});
            response.Headers["Server"].Should().Be("Fake");
            response.Headers["X-Powered-By"].Should().Be("Magic");
            response.Headers["X-CSV"].Should().Be("foo, bar");
            response.Content.Should().BeNull();
        }

        [Test]
        public void SupportsHttp()
        {
            // ARRANGE, ACT, ASSERT
            new HttpMockState().Target.SupportsProtocol("HTTP").Should().BeTrue();
        }

        [Test]
        public void SupportsHttps()
        {
            // ARRANGE, ACT, ASSERT
            new HttpMockState().Target.SupportsProtocol("HTTPS").Should().BeTrue();
        }

        [Test]
        public async Task EndToEnd()
        {
            if (Helper.IsNetworkAvailable())
            {
                // ARRANGE
                var httpClient = new HttpClient();
                var requester = new HttpClientRequester(httpClient);
                var configuration = Configuration.Default.With(requester).WithDefaultLoader();
                var context = BrowsingContext.New(configuration);
                var request = DocumentRequest.Get(Url.Create("http://httpbin.org/html"));

                // ACT
                var response = await context.GetService<IDocumentLoader>().FetchAsync(request).Task;
                var document = await context.OpenAsync(response, CancellationToken.None);

                // ASSERT
                document.QuerySelector("h1").ToHtml().Should().Be("<h1>Herman Melville - Moby-Dick</h1>");
            }
        }
    }
}