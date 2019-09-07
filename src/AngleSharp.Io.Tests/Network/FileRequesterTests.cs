namespace AngleSharp.Io.Tests.Network
{
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using AngleSharp.Io.Network;
    using NUnit.Framework;
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class FileRequesterTests
    {
        private static String GetLocalPath()
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var path = Path.Combine(directory, "TestContent.txt");
            var uri = new Uri(path);
            return uri.AbsoluteUri;
        }

        [Test]
        public async Task GetLocalFileViaFileRequester()
        {
            var url = GetLocalPath();
            var requester = new FileRequester();
            var request = new Request { Address = Url.Create(url) };

            var response = await requester.RequestAsync(request, CancellationToken.None);
            Assert.IsNotNull(response);

            var content = await new StreamReader(response.Content).ReadToEndAsync();
            Assert.AreEqual("Foo!", content);
        }

        [Test]
        public async Task FollowLinkToUseFileRequesterUsingAllRequesters()
        {
            var url = GetLocalPath();
            var config = Configuration.Default.WithRequesters().WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(res => res.Content("<a href='" + url + "'>Download</a>"));
            var result = await document.QuerySelector<IHtmlAnchorElement>("a").NavigateAsync();
            var content = result.Body.TextContent;
            Assert.AreEqual("Foo!", content);
        }

        [Test]
        public async Task FollowLinkToUseFileRequesterUsingStandardRequesters()
        {
            var url = GetLocalPath();
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(res => res.Content("<a href='" + url + "'>Download</a>"));
            var result = await document.QuerySelector<IHtmlAnchorElement>("a").NavigateAsync();
            Assert.IsNull(result);
        }
    }
}
