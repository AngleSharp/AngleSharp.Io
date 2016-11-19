namespace AngleSharp.Io
{
    using AngleSharp.Io.Network;
    using System;
    using System.Net.Http;
    using LoaderSetup = AngleSharp.ConfigurationExtensions.LoaderSetup;

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
        public static IConfiguration WithRequesters(this IConfiguration configuration, Action<LoaderSetup> setup = null)
        {
            return configuration.WithRequesters(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }, setup);
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
        public static IConfiguration WithRequesters(this IConfiguration configuration, HttpMessageHandler httpMessageHandler, Action<LoaderSetup> setup = null)
        {
            var httpClient = new HttpClient(httpMessageHandler);
            var requesters = new IRequester[] 
            {
                new HttpClientRequester(httpClient),
                new DataRequester(),
                new FtpRequester(),
                new FileRequester(),
                new AboutRequester()
            };
            return configuration.WithDefaultLoader(setup, requesters);
        }
    }
}