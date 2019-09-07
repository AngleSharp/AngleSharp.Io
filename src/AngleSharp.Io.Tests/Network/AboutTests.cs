namespace AngleSharp.Io.Tests.Network
{
    using AngleSharp.Io.Network;
    using NUnit.Framework;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class AboutTests
    {
        [Test]
        public async Task InvokeAboutSettingsLowercaseShouldWorkFine()
        {
            var about = new AboutRequester();
            var requests = new List<Request>();
            var req = new Request
            {
                Address = Url.Create("about://settings"),
                Method = HttpMethod.Get
            };

            about.SetRoute("settings", (request, cancel) =>
            {
                requests.Add(request);
                return Task.FromResult(default(IResponse));
            });

            var response = await about.RequestAsync(req, CancellationToken.None);
            Assert.IsNull(response);
            Assert.AreEqual(1, requests.Count);
        }

        [Test]
        public async Task InvokeAboutSettingsUppercaseShouldWorkFine()
        {
            var about = new AboutRequester();
            var requests = new List<Request>();
            var req = new Request
            {
                Address = Url.Create("about://Settings"),
                Method = HttpMethod.Get
            };

            about.SetRoute("settings", (request, cancel) =>
            {
                requests.Add(request);
                return Task.FromResult(default(IResponse));
            });

            var response = await about.RequestAsync(req, CancellationToken.None);
            Assert.IsNull(response);
            Assert.AreEqual(1, requests.Count);
        }

        [Test]
        public async Task InvokeAboutUnsetUrlShouldNotFire()
        {
            var about = new AboutRequester();
            var requests = new List<Request>();
            var req = new Request
            {
                Address = Url.Create("about://bookmarks"),
                Method = HttpMethod.Get
            };

            about.SetRoute("settings", (request, cancel) =>
            {
                requests.Add(request);
                return Task.FromResult(default(IResponse));
            });

            var response = await about.RequestAsync(req, CancellationToken.None);
            Assert.IsNull(response);
            Assert.AreEqual(0, requests.Count);
        }

        [Test]
        public async Task InvokeAboutUrlWithQueryShouldFire()
        {
            var about = new AboutRequester();
            var requests = new List<Request>();
            var req = new Request
            {
                Address = Url.Create("about://cache?device=memory"),
                Method = HttpMethod.Get
            };

            about.SetRoute("cache", (request, cancel) =>
            {
                requests.Add(request);
                return Task.FromResult(default(IResponse));
            });

            var response = await about.RequestAsync(req, CancellationToken.None);
            Assert.IsNull(response);
            Assert.AreEqual(1, requests.Count);
            Assert.AreEqual(req.Address.Data, requests[0].Address.Data);
        }
    }
}
