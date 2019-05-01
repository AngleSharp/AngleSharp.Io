namespace AngleSharp
{
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Io.Network;
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Http;

    /// <summary>
    /// Additional extensions for improved requesters.
    /// </summary>
    public static class IoConfigurationExtensions
    {
        /// <summary>
        /// Adds capability to start a download when following some link to the
        /// configuration.
        /// </summary>
        /// <param name="configuration">The configuration to extend.</param>
        /// <param name="download">
        /// The callback to invoke when a download should be started. Returns true
        /// to signal an interest in downloading the response, otherwise false.
        /// </param>
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithDownload(this IConfiguration configuration, Func<MimeType, IResponse, Boolean> download)
        {
            var oldFactory = configuration.Services.OfType<IDocumentFactory>().FirstOrDefault();
            var newFactory = new DownloadFactory(oldFactory, download);
            return configuration.WithOnly<IDocumentFactory>(newFactory);
        }

        /// <summary>
        /// Adds the standard download capability, i.e., when a binary or attachment
        /// is received the download callback is triggered.
        /// </summary>
        /// <param name="configuration">The configuration to extend.</param>
        /// <param name="download">
        /// The callback with filename and stream as parameters. The stream must be
        /// disposed / cleaned up after use.
        /// </param>
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithStandardDownload(this IConfiguration configuration, Action<String, Stream> download)
        {
            var binary = new MimeType(MimeTypeNames.Binary);
            return configuration.WithDownload((type, response) =>
            {
                if (response.IsAttachment() || type == binary)
                {
                    var fileName = response.GetAttachedFileName();
                    download.Invoke(fileName, response.Content);
                    return true;
                }

                return false;
            });
        }

        /// <summary>
        /// Adds the requesters from the AngleSharp.Io package.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithRequesters(this IConfiguration configuration) =>
            configuration.WithRequesters(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });

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
                new AboutRequester(),
            });
        }
    }
}