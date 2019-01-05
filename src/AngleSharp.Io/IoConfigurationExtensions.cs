namespace AngleSharp
{
    using AngleSharp.Io.Network;
    using System;
    using System.Net.Http;

    /// <summary>
    /// Additional extensions for improved requesters.
    /// </summary>
    public static class IoConfigurationExtensions
    {
        /// <summary>
        /// Adds the requesters from the AngleSharp.Io package.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithRequesters(this IConfiguration configuration)
        {
            return configuration.WithRequesters(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
        }

        /// <summary>
        /// Adds the requesters from the AngleSharp.Io package.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <param name="httpMessageHandler">
        /// The HTTP handler stack to use for sending requests.
        /// </param>
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithRequesters(this IConfiguration configuration, HttpMessageHandler httpMessageHandler)
        {
            var httpClient = new HttpClient(httpMessageHandler);
            return configuration.With(new Object[]
            {
                new HttpClientRequester(httpClient),
                new DataRequester(),
                new FtpRequester(),
                new FileRequester(),
                new AboutRequester()
            });
        }
    }
}