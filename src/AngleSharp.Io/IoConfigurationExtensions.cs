namespace AngleSharp
{
    using AngleSharp.Dom;
    using AngleSharp.Io;
    using AngleSharp.Io.Cookie;
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
        #region Download

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
            return configuration.WithDefaultLoader(new LoaderOptions
            {
                Filter = req => false,
            }).WithOnly<IDocumentFactory>(newFactory);
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

        #endregion

        #region Requesters

        /// <summary>
        /// Adds the requesters from the AngleSharp.Io package.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithRequesters(this IConfiguration configuration) =>
            configuration.WithRequesters(new HttpClientHandler());

        /// <summary>
        /// Adds the requesters from the AngleSharp.Io package.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <param name="httpClientHandler">
        /// The HTTP client handler to use for sending requests.
        /// </param>
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithRequesters(this IConfiguration configuration, HttpClientHandler httpClientHandler)
        {
            httpClientHandler.UseCookies = false;
            httpClientHandler.AllowAutoRedirect = false;
            return configuration.WithRequesters((HttpMessageHandler)httpClientHandler);
        }

        /// <summary>
        /// Adds the requesters from the AngleSharp.Io package.
        /// </summary>
        /// <param name="configuration">The configuration to use.</param>
        /// <param name="httpMessageHandler">
        /// The HTTP message handler to use for sending requests.
        /// </param>
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithRequesters(this IConfiguration configuration, HttpMessageHandler httpMessageHandler)
        {
            var httpClient = new HttpClient(httpMessageHandler);
            return configuration.With(new IRequester[]
            {
                new HttpClientRequester(httpClient),
                new DataRequester(),
                new FtpRequester(),
                new FileRequester(),
                new AboutRequester(),
            });
        }

        /// <summary>
        /// Adds the given requester to the configuration.
        /// </summary>
        /// <typeparam name="T">The type of the requester to add.</typeparam>
        /// <param name="configuration">The configuration to use.</param>
        /// <param name="requester">The requester instance to add.</param>
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithRequester<T>(this IConfiguration configuration, T requester)
            where T : IRequester => configuration.With(requester);

        /// <summary>
        /// Adds a new requester of the provided type to the configuration.
        /// </summary>
        /// <typeparam name="T">The type of the requester to add.</typeparam>
        /// <param name="configuration">The configuration to use.</param>
        /// <returns>The new configuration.</returns>
        public static IConfiguration WithRequester<T>(this IConfiguration configuration)
            where T: IRequester, new() => configuration.WithRequester(new T());

        #endregion

        #region Cookies

        /// <summary>
        /// Registers a persistent advanced cookie container using the local file handler.
        /// </summary>
        /// <param name="configuration">The configuration to extend.</param>
        /// <param name="syncFilePath">The path to the required sync file.</param>
        /// <returns>The new instance with the service.</returns>
        public static IConfiguration WithPersistentCookies(this IConfiguration configuration, String syncFilePath) =>
            configuration.WithCookies(new LocalFileHandler(syncFilePath));

        /// <summary>
        /// Registers a non-persistent advanced cookie container using the memory-only file
        /// handler.
        /// </summary>
        /// <param name="configuration">The configuration to extend.</param>
        /// <returns>The new instance with the service.</returns>
        public static IConfiguration WithTemporaryCookies(this IConfiguration configuration) =>
            configuration.WithCookies(new MemoryFileHandler());

        /// <summary>
        /// Registers a non-persistent advanced cookie container using the memory-only file
        /// handler.
        /// Alias for WithTemporaryCookies().
        /// </summary>
        /// <param name="configuration">The configuration to extend.</param>
        /// <returns>The new instance with the service.</returns>
        public static IConfiguration WithCookies(this IConfiguration configuration) =>
            configuration.WithTemporaryCookies();

        /// <summary>
        /// Registers the advanced cookie service.
        /// </summary>
        /// <param name="configuration">The configuration to extend.</param>
        /// <param name="fileHandler">The handler for the cookie source.</param>
        /// <returns>The new instance with the service.</returns>
        public static IConfiguration WithCookies(this IConfiguration configuration, ICookieFileHandler fileHandler) =>
            configuration.WithCookies(new AdvancedCookieProvider(fileHandler));

        /// <summary>
        /// Registers a cookie service with the given provider.
        /// </summary>
        /// <param name="configuration">The configuration to extend.</param>
        /// <param name="provider">The provider for cookie interactions.</param>
        /// <returns>The new instance with the service.</returns>
        public static IConfiguration WithCookies(this IConfiguration configuration, ICookieProvider provider) =>
            configuration.WithOnly(provider);

        #endregion
    }
}