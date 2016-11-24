namespace AngleSharp.Io.Tests.Network
{
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using AngleSharp.Io.Network;
    using NUnit.Framework;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class FtpRequesterTests
    {
        [Test]
        public async Task DownloadFtpRfcViaFtpRequester()
        {
            var url = "ftp://ftp.funet.fi/pub/standards/RFC/rfc959.txt";
            var requester = new FtpRequester();
            var request = new Request { Address = Url.Create(url) };

            var response = await requester.RequestAsync(request, CancellationToken.None);
            Assert.IsNotNull(response);

            var content = await new StreamReader(response.Content).ReadToEndAsync();
            Assert.AreEqual(147316, content.Length);
        }

        [Test]
        public async Task FollowLinkToUseFtpRequesterUsingAllRequesters()
        {
            var config = Configuration.Default.WithRequesters().WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(res => res.Content("<a href='ftp://ftp.funet.fi/pub/standards/RFC/rfc959.txt'>Download</a>"));
            var result = await document.QuerySelector<IHtmlAnchorElement>("a").NavigateAsync();
            var content = result.Body.TextContent;
            Assert.AreEqual(147316, content.Length);
        }

        [Test]
        public async Task FollowLinkToUseFtpRequesterUsingStandardRequesters()
        {
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(res => res.Content("<a href='ftp://ftp.funet.fi/pub/standards/RFC/rfc959.txt'>Download</a>"));
            var result = await document.QuerySelector<IHtmlAnchorElement>("a").NavigateAsync();
            var content = result.Body.TextContent;
            Assert.AreEqual(0, content.Length);
        }
    }
}
