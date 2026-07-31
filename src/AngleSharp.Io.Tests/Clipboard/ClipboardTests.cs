namespace AngleSharp.Io.Tests.Clipboard
{
    using AngleSharp;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.Tests.Mocks;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    [TestFixture]
    public class ClipboardTests
    {
        [Test]
        public async Task ClipboardCanBeResolvedFromNavigatorWhenRegistered()
        {
            var platform = new FakeClipboardPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithClipboard(platform), "https://clipboard.example/").ConfigureAwait(false);
            var navigator = new FakeNavigator(context);

            Assert.IsNotNull(navigator.Clipboard());
        }

        [Test]
        public async Task ClipboardWriteTextUsesPlatformImplementation()
        {
            var platform = new FakeClipboardPlatform();
            var context = await OpenContextAsync(Configuration.Default.WithClipboard(platform), "https://clipboard.example/").ConfigureAwait(false);
            var navigator = new FakeNavigator(context);
            var clipboard = navigator.Clipboard();

            await clipboard.WriteText("copied").ConfigureAwait(false);

            Assert.AreEqual("copied", platform.LastWrittenText);
        }

        [Test]
        public async Task ClipboardReadTextUsesPlatformImplementation()
        {
            var platform = new FakeClipboardPlatform
            {
                CurrentText = "from-platform"
            };

            var context = await OpenContextAsync(Configuration.Default.WithClipboard(platform), "https://clipboard.example/").ConfigureAwait(false);
            var navigator = new FakeNavigator(context);
            var clipboard = navigator.Clipboard();

            var value = await clipboard.ReadText().ConfigureAwait(false);

            Assert.AreEqual("from-platform", value);
        }

        private static async Task<IBrowsingContext> OpenContextAsync(IConfiguration configuration, String address)
        {
            var context = BrowsingContext.New(configuration);
            await context.OpenAsync(res => res.Address(address).Content("<!doctype html><title>clipboard</title>")).ConfigureAwait(false);
            return context;
        }

        private sealed class FakeClipboardPlatform : IClipboardPlatform
        {
            public String CurrentText { get; set; } = String.Empty;

            public String LastWrittenText { get; private set; } = String.Empty;

            public Task<String> ReadTextAsync()
            {
                return Task.FromResult(CurrentText);
            }

            public Task WriteTextAsync(String text)
            {
                LastWrittenText = text;
                CurrentText = text;
                return Task.CompletedTask;
            }
        }
    }
}