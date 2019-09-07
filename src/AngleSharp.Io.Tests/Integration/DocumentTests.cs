namespace AngleSharp.Io.Tests.Integration
{
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    [TestFixture]
    public class DocumentTests
    {
        [Test]
        public async Task IframeWithDocumentViaDataSrc()
        {
            var cfg = Configuration.Default.WithRequesters().WithDefaultLoader(new LoaderOptions
            {
                IsResourceLoadingEnabled = true
            });
            var html = @"<!doctype html><iframe id=myframe src='data:text/html,<span>Hello World!</span>'></iframe></script>";
            var document = await BrowsingContext.New(cfg).OpenAsync(m => m.Content(html));
            var iframe = document.QuerySelector<IHtmlInlineFrameElement>("#myframe");
            Assert.IsNotNull(iframe);
            Assert.IsNotNull(iframe.ContentDocument);
            Assert.AreEqual("Hello World!", iframe.ContentDocument.Body.TextContent);
            Assert.AreEqual(iframe.ContentDocument, iframe.ContentWindow.Document);
        }

        [Test]
        public async Task ImportPageFromDataRequest()
        {
            var receivedRequest = new TaskCompletionSource<Boolean>();
            var config = Configuration.Default.WithRequesters().WithDefaultLoader(new LoaderOptions
            {
                IsResourceLoadingEnabled = true,
                Filter = request =>
                {
                    receivedRequest.SetResult(true);
                    return true;
                },
            });

            var document = await BrowsingContext.New(config).OpenAsync(m => m.Content("<!doctype html><link rel=import href='data:text/html,<div>foo</div>'>"));
            var link = document.QuerySelector<IHtmlLinkElement>("link");
            await receivedRequest.Task;

            Assert.AreEqual("import", link.Relation);
            Assert.IsNotNull(link.Import);
            Assert.AreEqual("foo", link.Import.QuerySelector("div").TextContent);
        }
    }
}
