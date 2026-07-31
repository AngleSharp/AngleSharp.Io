namespace AngleSharp.Io.Tests.WebLocks
{
    using AngleSharp;
    using AngleSharp.Browser.Dom;
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    [TestFixture]
    public class WebLocksTests
    {
        [Test]
        public async Task NavigatorLocksCanBeCreatedFromADocumentWindow()
        {
            var context = await OpenContextAsync("https://locks.example/").ConfigureAwait(false);
            var navigator = new FakeNavigator(context);

            Assert.IsNotNull(navigator.Locks());
        }

        [Test]
        public async Task NavigatorLocksSerializeSameOriginRequests()
        {
            var contextA = await OpenContextAsync("https://locks.example/").ConfigureAwait(false);
            var contextB = await OpenContextAsync("https://locks.example/other").ConfigureAwait(false);
            var locksA = new FakeNavigator(contextA).Locks();
            var locksB = new FakeNavigator(contextB).Locks();

            var enteredA = new TaskCompletionSource<Boolean>();
            var enteredB = new TaskCompletionSource<Boolean>();
            var releaseA = new TaskCompletionSource<Boolean>();

            var requestA = locksA.Request("resource", async _ =>
            {
                enteredA.TrySetResult(true);
                await releaseA.Task.ConfigureAwait(false);
            });

            await enteredA.Task.ConfigureAwait(false);

            var requestB = locksB.Request("resource", async _ =>
            {
                enteredB.TrySetResult(true);
                await Task.CompletedTask;
            });

            var blocked = await Task.WhenAny(enteredB.Task, Task.Delay(200)).ConfigureAwait(false);
            Assert.AreNotEqual(enteredB.Task, blocked);

            releaseA.TrySetResult(true);

            var completed = await Task.WhenAny(enteredB.Task, Task.Delay(1000)).ConfigureAwait(false);
            Assert.AreEqual(enteredB.Task, completed);

            await Task.WhenAll(requestA, requestB).ConfigureAwait(false);
        }

        [Test]
        public async Task NavigatorLocksDoNotCrossOrigins()
        {
            var contextA = await OpenContextAsync("https://locks.example/").ConfigureAwait(false);
            var contextB = await OpenContextAsync("https://other.example/").ConfigureAwait(false);
            var locksA = new FakeNavigator(contextA).Locks();
            var locksB = new FakeNavigator(contextB).Locks();

            var enteredA = new TaskCompletionSource<Boolean>();
            var enteredB = new TaskCompletionSource<Boolean>();
            var releaseA = new TaskCompletionSource<Boolean>();

            var requestA = locksA.Request("resource", async _ =>
            {
                enteredA.TrySetResult(true);
                await releaseA.Task.ConfigureAwait(false);
            });

            await enteredA.Task.ConfigureAwait(false);

            var requestB = locksB.Request("resource", async _ =>
            {
                enteredB.TrySetResult(true);
                await Task.CompletedTask;
            });

            var completed = await Task.WhenAny(enteredB.Task, Task.Delay(500)).ConfigureAwait(false);
            Assert.AreEqual(enteredB.Task, completed);

            releaseA.TrySetResult(true);

            await Task.WhenAll(requestA, requestB).ConfigureAwait(false);
        }

        private static async Task<IBrowsingContext> OpenContextAsync(String address)
        {
            var context = BrowsingContext.New();

            var document = await context.OpenAsync(res =>
                res.Address(address).Content("<!doctype html><title>locks</title>")).ConfigureAwait(false);

            Assert.IsNotNull(document.DefaultView);
            return context;
        }

        private sealed class FakeNavigator : INavigator
        {
            private readonly HashSet<(String Scheme, String Url, String Title)> _protocolHandlers;
            private readonly HashSet<(String MimeType, String Url, String Title)> _contentHandlers;

            public FakeNavigator(IBrowsingContext context)
            {
                Context = context;
                _protocolHandlers = new HashSet<(String Scheme, String Url, String Title)>();
                _contentHandlers = new HashSet<(String MimeType, String Url, String Title)>();
            }

            public IBrowsingContext Context { get; }

            public String Name => "Fake Navigator";

            public String Version => "1.0";

            public String Platform => "test";

            public String UserAgent => "FakeNavigator/1.0";

            public Boolean IsOnline => true;

            public void WaitForStorageUpdates()
            {
            }

            public void RegisterProtocolHandler(String scheme, String url, String title)
            {
                _protocolHandlers.Add((scheme ?? String.Empty, url ?? String.Empty, title ?? String.Empty));
            }

            public void RegisterContentHandler(String mimeType, String url, String title)
            {
                _contentHandlers.Add((mimeType ?? String.Empty, url ?? String.Empty, title ?? String.Empty));
            }

            public Boolean IsProtocolHandlerRegistered(String scheme, String url)
            {
                var normalizedScheme = scheme ?? String.Empty;
                var normalizedUrl = url ?? String.Empty;

                return _protocolHandlers.Any(m =>
                    String.Equals(m.Scheme, normalizedScheme, StringComparison.Ordinal) &&
                    String.Equals(m.Url, normalizedUrl, StringComparison.Ordinal));
            }

            public Boolean IsContentHandlerRegistered(String mimeType, String url)
            {
                var normalizedMimeType = mimeType ?? String.Empty;
                var normalizedUrl = url ?? String.Empty;

                return _contentHandlers.Any(m =>
                    String.Equals(m.MimeType, normalizedMimeType, StringComparison.Ordinal) &&
                    String.Equals(m.Url, normalizedUrl, StringComparison.Ordinal));
            }

            public void UnregisterProtocolHandler(String scheme, String url)
            {
                var normalizedScheme = scheme ?? String.Empty;
                var normalizedUrl = url ?? String.Empty;
                _protocolHandlers.RemoveWhere(m =>
                    String.Equals(m.Scheme, normalizedScheme, StringComparison.Ordinal) &&
                    String.Equals(m.Url, normalizedUrl, StringComparison.Ordinal));
            }

            public void UnregisterContentHandler(String mimeType, String url)
            {
                var normalizedMimeType = mimeType ?? String.Empty;
                var normalizedUrl = url ?? String.Empty;
                _contentHandlers.RemoveWhere(m =>
                    String.Equals(m.MimeType, normalizedMimeType, StringComparison.Ordinal) &&
                    String.Equals(m.Url, normalizedUrl, StringComparison.Ordinal));
            }
        }
    }
}