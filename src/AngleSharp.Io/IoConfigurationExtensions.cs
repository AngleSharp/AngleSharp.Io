namespace AngleSharp;

using AngleSharp.Dom;
using AngleSharp.Io;
using AngleSharp.Io.Cookie;
using AngleSharp.Io.Dom;
using AngleSharp.Io.IndexedDb;
using AngleSharp.Io.GeolocationApi;
using AngleSharp.Io.Network;
using AngleSharp.Io.Storage;
using AngleSharp.Io.ClipboardApi;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using AngleSharp.Browser.Dom;

/// <summary>
/// Additional extensions for improved requesters.
/// </summary>
public static class IoConfigurationExtensions
{
    #region Combined

    /// <summary>
    /// Registers the IO services all in one go.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="options">The options to use.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithIo(this IConfiguration configuration, IoOptions options)
    {
        if (options == null)
        {
            options = new IoOptions();
        }

        return configuration
            .WithNavigator(options.Navigator)
            .WithCookies(options.CookieHandler)
            .WithRequesters(options.HttpHandler)
            .WithStorageProviderFactory(options.StorageFactory)
            .WithIndexedDbProviderFactory(options.IndexedDbFactory)
            .WithCacheProviderFactory(options.CacheFactory)
            .WithClipboard(options.ClipboardPlatform)
            .WithGeolocation(options.GeolocationPlatform);
    }

    #endregion

    #region Navigator

    /// <summary>
    /// Registers the navigator service using the default implementation.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithNavigator(this IConfiguration configuration)
    {
        return configuration.WithNavigator(new NavigatorOptions());
    }

    /// <summary>
    /// Registers the navigator service using the default implementation.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="options">The navigator options to use.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithNavigator(this IConfiguration configuration, NavigatorOptions options)
    {
        return configuration.WithOnly<INavigator>(context => new Navigator(context, options));
    }

    #endregion

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

    #region Storage

    /// <summary>
    /// Registers a storage provider factory.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="syncDirectoryPath">The directory path for local storage synchronization.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithStorageProviderFactory(this IConfiguration configuration, String syncDirectoryPath)
    {
        var factory = new StorageProviderFactory();
        factory.EnableLocalStorage(syncDirectoryPath);
        factory.EnableSessionStorage();
        return configuration.WithStorageProviderFactory(factory);
    }

    /// <summary>
    /// Registers a storage provider factory.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="factory">The storage provider factory to use.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithStorageProviderFactory(this IConfiguration configuration, IStorageProviderFactory factory) =>
        configuration.WithOnly<IStorageProviderFactory>(factory);

    #endregion

    #region IndexedDb

    /// <summary>
    /// Registers an IndexedDB provider factory.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="factory">The IndexedDB provider factory to use.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithIndexedDbProviderFactory(this IConfiguration configuration, IIndexedDbProviderFactory factory) =>
        configuration.WithOnly<IIndexedDbProviderFactory>(factory);

    #endregion

    #region Cache

    /// <summary>
    /// Registers a Cache Storage provider factory.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="factory">The Cache Storage provider factory to use.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithCacheProviderFactory(this IConfiguration configuration, ICacheProviderFactory factory) =>
        configuration.WithOnly<ICacheProviderFactory>(factory);

    #endregion

    #region Clipboard

    /// <summary>
    /// Registers the clipboard service using the given platform implementation.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="platform">The platform clipboard implementation.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithClipboard(this IConfiguration configuration, IClipboardPlatform platform)
    {
        if (platform is not null)
        {
            var factory = new ClipboardProviderFactory(platform);
            return configuration.WithClipboardProviderFactory(factory);
        }

        return configuration;
    }

    /// <summary>
    /// Registers a clipboard provider factory.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="factory">The clipboard provider factory to use.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithClipboardProviderFactory(this IConfiguration configuration, IClipboardProviderFactory factory) =>
        configuration.WithOnly<IClipboardProviderFactory>(factory);

    #endregion

    #region Geolocation

    /// <summary>
    /// Registers the geolocation service using the given platform implementation.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="platform">The platform geolocation implementation.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithGeolocation(this IConfiguration configuration, IGeolocationPlatform platform)
    {
        if (platform is not null)
        {
            var factory = new GeolocationProviderFactory(platform);
            return configuration.WithGeolocationProviderFactory(factory);
        }

        return configuration;
    }

    /// <summary>
    /// Registers a geolocation provider factory.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="factory">The geolocation provider factory to use.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithGeolocationProviderFactory(this IConfiguration configuration, IGeolocationProviderFactory factory) =>
        configuration.WithOnly<IGeolocationProviderFactory>(factory);

    #endregion
}
