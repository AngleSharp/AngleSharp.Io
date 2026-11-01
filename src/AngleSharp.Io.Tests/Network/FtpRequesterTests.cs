namespace AngleSharp.Io.Tests.Network
{
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using AngleSharp.Io.Network;
    using AngleSharp.Io.Tests.Network.Mocks;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class FtpRequesterTests
    {
        private const String FilePath = "/pub/standards/attributes.html";
        private const String BodyText = "Attributes served over FTP";
        private const String FileContent = "<!DOCTYPE html><html><head><title>Attributes</title></head><body><p>" + BodyText + "</p></body></html>";

        private FtpTestServer _server;

        [SetUp]
        public void StartServer()
        {
            _server = new FtpTestServer(new Dictionary<String, String>
            {
                { FilePath, FileContent },
            });
        }

        [TearDown]
        public void StopServer()
        {
            _server.Dispose();
        }

        [Test]
        public async Task DownloadFtpRfcViaFtpRequester()
        {
            var url = _server.GetUrl(FilePath);
            var requester = new FtpRequester();
            var request = new Request { Address = Url.Create(url) };

            var response = await requester.RequestAsync(request, CancellationToken.None);
            Assert.IsNotNull(response);

            var content = await new StreamReader(response.Content).ReadToEndAsync();
            Assert.AreEqual(FileContent, content);
        }

        [Test]
        public async Task FollowLinkToUseFtpRequesterUsingAllRequesters()
        {
            var config = Configuration.Default.WithRequesters().WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(res => res.Content($"<a href='{_server.GetUrl(FilePath)}'>Download</a>"));
            var result = await document.QuerySelector<IHtmlAnchorElement>("a").NavigateAsync();
            var content = result.Body.TextContent;
            Assert.AreEqual(BodyText, content);
        }

        [Test]
        public async Task FollowLinkToUseFtpRequesterUsingStandardRequesters()
        {
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(res => res.Content($"<a href='{_server.GetUrl(FilePath)}'>Download</a>"));
            var result = await document.QuerySelector<IHtmlAnchorElement>("a").NavigateAsync();
            Assert.IsNull(result);
        }
    }
}
