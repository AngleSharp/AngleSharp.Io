namespace AngleSharp.Io.Tests.Cookie
{
    using AngleSharp.Dom;
    using AngleSharp.Io.Cookie;
    using NUnit.Framework;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class ConcurrencyTests
    {
        private const Int32 Threads = 8;
        private const Int32 CookiesPerThread = 200;

        [Test]
        public async Task ParallelSetAndGetKeepAllCookies()
        {
            var handler = new MemoryFileHandler();
            var provider = new AdvancedCookieProvider(handler);
            var url = new Url("https://example.com/");

            await RunInParallel(thread =>
            {
                for (var i = 0; i < CookiesPerThread; i++)
                {
                    ((ICookieProvider)provider).SetCookie(url, $"k{thread}_{i}=v; Max-Age=1000");
                    ((ICookieProvider)provider).GetCookie(url);
                }
            }).ConfigureAwait(false);

            Assert.AreEqual(Threads * CookiesPerThread, provider.Cookies.Count());
            Assert.AreEqual(Threads * CookiesPerThread, new AdvancedCookieProvider(handler).Cookies.Count());
        }

        [Test]
        public async Task ParallelRemovalsRemoveEachCookieOnce()
        {
            var handler = new MemoryFileHandler();
            var provider = new AdvancedCookieProvider(handler);
            var url = new Url("https://example.com/");

            for (var i = 0; i < Threads * CookiesPerThread; i++)
            {
                ((ICookieProvider)provider).SetCookie(url, $"k{i}=v; Max-Age=1000");
            }

            var removed = 0;

            await RunInParallel(thread =>
            {
                for (var i = 0; i < Threads * CookiesPerThread; i++)
                {
                    if (provider.RemoveCookie("example.com", "/", $"k{i}") != null)
                    {
                        Interlocked.Increment(ref removed);
                    }
                }
            }).ConfigureAwait(false);

            Assert.AreEqual(Threads * CookiesPerThread, removed);
            Assert.IsEmpty(provider.Cookies);
            Assert.IsEmpty(new AdvancedCookieProvider(handler).Cookies);
        }

        [Test]
        public void CookiesCanBeEnumeratedWhileTheyChange()
        {
            var provider = new AdvancedCookieProvider(new MemoryFileHandler());
            ((ICookieProvider)provider).SetCookie(new Url("https://example.com/"), "a=b");

            foreach (var cookie in provider.Cookies)
            {
                ((ICookieProvider)provider).SetCookie(new Url("https://example.com/"), "c=d");
            }

            Assert.AreEqual(2, provider.Cookies.Count());
        }

        private static async Task RunInParallel(Action<Int32> action)
        {
            // own threads released together by the barrier, such that the calls really overlap
            using (var barrier = new Barrier(Threads))
            {
                var tasks = Enumerable.Range(0, Threads)
                    .Select(thread => Task.Factory.StartNew(() =>
                    {
                        barrier.SignalAndWait();
                        action(thread);
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
                    .ToArray();
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }
    }
}
