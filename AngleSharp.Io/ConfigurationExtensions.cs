namespace AngleSharp
{
    using AngleSharp.Io.Network;
    using AngleSharp.Io.Services;
    using AngleSharp.Network;
    using AngleSharp.Network.Default;
    using AngleSharp.Services;
    using System.Linq;
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
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithRequesters(this IConfiguration configuration)
        {
            if (!configuration.Services.OfType<ILoaderService>().Any())
            {
                var requesters = new IRequester [] { new HttpClientRequester(), new DataRequester() };
                var service = new LoaderService(requesters);
                return configuration.With(service);
            }

            return configuration;
        }


        /// <summary>
        /// Adds a loader service that comes with all (improved) requesters.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <param name="httpMessageHandler">The HTTP handler stack to use for sending requests.</param>
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithHttpRequesters(this IConfiguration configuration, HttpMessageHandler httpMessageHandler)
        {
            if (!configuration.Services.OfType<ILoaderService>().Any())
            {
                var httpClient = new HttpClient(httpMessageHandler);
                var requesters = new IRequester[] { new HttpClientRequester(httpClient), new DataRequester() };
                var service = new LoaderService(requesters);
                return configuration.With(service);
            }

            return configuration;
        }
    }
}