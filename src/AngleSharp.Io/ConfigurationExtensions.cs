namespace AngleSharp
{
    using AngleSharp.Io.Network;
    using AngleSharp.Network;
    using AngleSharp.Network.Default;
    using AngleSharp.Services.Default;
    using System;
    using System.Net.Http;

    /// <summary>
    /// Additional extensions for improved requesters.
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Adds a loader service that comes with all (improved) requesters.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <param name="setup">Optional setup for the loader service.</param>
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithRequesters(this IConfiguration configuration, Action<LoaderService> setup = null)
        {
            var requesters = new IRequester[] { new HttpClientRequester(), new DataRequester(), new FtpRequester() };
            return configuration.WithDefaultLoader(setup, requesters);
        }

        /// <summary>
        /// Adds a loader service that comes with all (improved) requesters.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <param name="httpMessageHandler">
        /// The HTTP handler stack to use for sending requests.
        /// </param>
        /// <param name="setup">Optional setup for the loader service.</param>
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithRequesters(this IConfiguration configuration, HttpMessageHandler httpMessageHandler, Action<LoaderService> setup = null)
        {
            var httpClient = new HttpClient(httpMessageHandler);
            var requesters = new IRequester[] { new HttpClientRequester(httpClient), new DataRequester(), new FtpRequester() };
            return configuration.WithDefaultLoader(setup, requesters);
        }
    }
}