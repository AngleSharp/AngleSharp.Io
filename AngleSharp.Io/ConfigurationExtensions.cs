namespace AngleSharp
{
    using AngleSharp.Io.Network;
    using AngleSharp.Io.Services;
    using AngleSharp.Network;
    using AngleSharp.Network.Default;
    using AngleSharp.Services;
    using System.Linq;

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
    }
}
