namespace AngleSharp.Io.Tests.Integration
{
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using NUnit.Framework;
    using System.Threading.Tasks;

    [TestFixture]
    public class DownloadTests
    {
        [Test]
        public async Task DownloadPngIfWanted()
        {
            var downloadSeen = false;
            var config = Configuration.Default.WithDownload((type, response) =>
            {
                if (type == new MimeType(MimeTypeNames.Png))
                {
                    downloadSeen = true;
                    return true;
                }

                return false;
            });
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content("<a href=foo.png>Go to some image</a>"));
            var linkedDocument = await document.QuerySelector<IHtmlAnchorElement>("a").NavigateAsync();

            Assert.IsTrue(downloadSeen);
            Assert.IsNull(linkedDocument);
            Assert.AreEqual(document, context.Active);
        }

        [Test]
        public async Task DownloadBinaryNotReceived()
        {
            var downloadSeen = false;
            var config = Configuration.Default.WithDownload((type, response) =>
            {
                if (type == new MimeType(MimeTypeNames.Binary))
                {
                    downloadSeen = true;
                    return true;
                }

                return false;
            });
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content("<a href=foo.png>Go to some image</a>"));
            var linkedDocument = await document.QuerySelector<IHtmlAnchorElement>("a").NavigateAsync();

            Assert.IsFalse(downloadSeen);
            Assert.IsNotNull(linkedDocument);
            Assert.AreNotEqual(document, context.Active);
        }

        [Test]
        public async Task StandardDownloadBinary()
        {
            var downloadSeen = default(string);
            var config = Configuration.Default.WithStandardDownload((name, content) =>
            {
                downloadSeen = name;
                content.Dispose();
            });
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content("<a href=\"http://example.com/setup.exe\">Download setup</a>"));
            var linkedDownload = await document.QuerySelector<IHtmlAnchorElement>("a").NavigateAsync();

            Assert.IsNull(linkedDownload);
            Assert.AreEqual(document, context.Active);
            Assert.AreEqual("setup.exe", downloadSeen);
        }
    }
}
