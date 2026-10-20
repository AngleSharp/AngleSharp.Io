namespace AngleSharp;

using AngleSharp.Dom;
using AngleSharp.Io;
using AngleSharp.Io.Cookie;
using AngleSharp.Io.Dom;
using AngleSharp.Io.IndexedDb;
using AngleSharp.Io.GeolocationApi;
using AngleSharp.Io.Http;
using AngleSharp.Io.Network;
using AngleSharp.Io.PermissionsApi;
using AngleSharp.Io.Reporting;
using AngleSharp.Io.Storage;
using AngleSharp.Io.ClipboardApi;
using AngleSharp.Io.ServiceWorkerApi;
using AngleSharp.Io.StorageManagerApi;
using AngleSharp.Io.WebRtc;
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
            .WithBeacon(options.Beacon)
            .WithCookies(options.CookieHandler)
            .WithRequesters(options.HttpHandler)
            .WithHttpCache(options.HttpCache)
            .WithReporting(options.Reporting)
            .WithServiceWorkers(options.ServiceWorkerPlatform)
            .WithStorageProviderFactory(options.StorageFactory)
            .WithIndexedDbProviderFactory(options.IndexedDbFactory)
            .WithCacheProviderFactory(options.CacheFactory)
            .WithPermissions(options.Permissions)
            .WithClipboard(options.ClipboardPlatform)
            .WithGeolocation(options.GeolocationPlatform)
            .WithStorageManager(options.StorageManagerPlatform)
            .WithWebRtc(options.WebRtcPlatform);
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

    #region Beacon

    /// <summary>
    /// Registers the options for sending beacons. Without them, beacons are
    /// sent using the default quota. Passing null leaves the configuration
    /// unchanged.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="options">The beacon options to use.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithBeacon(this IConfiguration configuration, BeaconOptions options)
    {
        if (options is null)
        {
            return configuration;
        }

        return configuration.WithOnly(options);
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

    #region HTTP Cache

    /// <summary>
    /// Wraps the registered HTTP(S) requesters with an HTTP cache keeping
    /// its entries in memory. Needs to be called after the requesters have
    /// been registered.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithHttpCache(this IConfiguration configuration) =>
        configuration.WithHttpCache(new HttpCacheOptions());

    /// <summary>
    /// Wraps the registered HTTP(S) requesters with an HTTP cache persisting
    /// its entries in the given directory. Needs to be called after the
    /// requesters have been registered.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="directoryPath">The directory for storing the entries.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithHttpCache(this IConfiguration configuration, String directoryPath) =>
        configuration.WithHttpCache(new PersistenceHttpCacheHandler(directoryPath));

    /// <summary>
    /// Wraps the registered HTTP(S) requesters with an HTTP cache using the
    /// given handler. Needs to be called after the requesters have been
    /// registered.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="handler">The handler for storing the entries.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithHttpCache(this IConfiguration configuration, IHttpCacheHandler handler) =>
        configuration.WithHttpCache(new HttpCacheOptions { Handler = handler });

    /// <summary>
    /// Wraps the registered HTTP(S) requesters with an HTTP cache using the
    /// given options. Needs to be called after the requesters have been
    /// registered. Passing null leaves the configuration unchanged.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="options">The options of the cache.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithHttpCache(this IConfiguration configuration, HttpCacheOptions options)
    {
        if (options is null)
        {
            return configuration;
        }

        return configuration.WithHttpCache(new HttpCache(options));
    }

    /// <summary>
    /// Wraps the registered HTTP(S) requesters with the given HTTP cache,
    /// which is also registered as a service. Needs to be called after the
    /// requesters have been registered.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="cache">The cache to use.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithHttpCache(this IConfiguration configuration, IHttpCache cache)
    {
        // rebuilding the list keeps the order of the requesters, which
        // decides which requester handles a protocol; as in browsers the
        // HTTP cache stays behind the service workers
        var services = configuration.Services.Select(service => service switch
        {
            ServiceWorkerRequester serviceWorker => new ServiceWorkerRequester(WithHttpCache(serviceWorker.Inner, cache), serviceWorker.Registry),
            IRequester requester => WithHttpCache(requester, cache),
            _ => service,
        });

        return new Configuration(services).WithOnly<IHttpCache>(cache);
    }

    private static IRequester WithHttpCache(IRequester requester, IHttpCache cache) => requester switch
    {
        CachingRequester caching => new CachingRequester(caching.Inner, cache),
        _ when IsHttpRequester(requester) => new CachingRequester(requester, cache),
        _ => requester,
    };

    private static Boolean IsHttpRequester(IRequester requester) =>
        requester.SupportsProtocol(ProtocolNames.Http) || requester.SupportsProtocol(ProtocolNames.Https);

    #endregion

    #region Reporting

    /// <summary>
    /// Registers a reporting service with the default options, which keeps
    /// the reports until flushed. Needs to be called after the requesters
    /// have been registered to capture network errors.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithReporting(this IConfiguration configuration) =>
        configuration.WithReporting(new ReportingOptions());

    /// <summary>
    /// Registers a reporting service with the given options. Needs to be
    /// called after the requesters have been registered to capture network
    /// errors. Passing null leaves the configuration unchanged.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="options">The options of the service.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithReporting(this IConfiguration configuration, ReportingOptions options)
    {
        if (options is null)
        {
            return configuration;
        }

        return configuration.WithReporting(new ReportingService(options));
    }

    /// <summary>
    /// Registers the given reporting service and wraps the registered HTTP(S)
    /// requesters to report their network errors. The reporting sits closest
    /// to the network, i.e., behind the HTTP cache and the service workers.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="service">The service receiving the reports.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithReporting(this IConfiguration configuration, ReportingService service)
    {
        var services = configuration.Services.Select(item => item switch
        {
            IRequester requester => WithReporting(requester, service),
            _ => item,
        });

        return new Configuration(services).WithOnly(service);
    }

    private static IRequester WithReporting(IRequester requester, ReportingService service) => requester switch
    {
        ServiceWorkerRequester serviceWorker => new ServiceWorkerRequester(WithReporting(serviceWorker.Inner, service), serviceWorker.Registry),
        CachingRequester caching => new CachingRequester(WithReporting(caching.Inner, service), caching.Cache),
        ReportingRequester reporting => new ReportingRequester(reporting.Inner, service),
        _ when IsHttpRequester(requester) => new ReportingRequester(requester, service),
        _ => requester,
    };

    #endregion

    #region WebRTC

    /// <summary>
    /// Registers the loopback WebRTC platform, which connects the peer
    /// connections of the configuration with each other in-process.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithWebRtc(this IConfiguration configuration) =>
        configuration.WithWebRtc(new LoopbackRtcPlatform());

    /// <summary>
    /// Registers the given WebRTC platform, which provides the transport of
    /// RTCPeerConnection. Passing null leaves the configuration unchanged.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="platform">The platform to use.</param>
    /// <returns>The new configuration.</returns>
    public static IConfiguration WithWebRtc(this IConfiguration configuration, IRtcPlatform platform)
    {
        if (platform is null)
        {
            return configuration;
        }

        return configuration.WithOnly(platform);
    }

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

    #region Permissions

    /// <summary>
    /// Registers a permission policy where every permission requires a
    /// prompt, which is dismissed as no user can answer it. Hence, the
    /// gated features cannot be used until their permissions are granted
    /// via the policy found in navigator.permissions.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithPermissions(this IConfiguration configuration) =>
        configuration.WithPermissions(new PermissionPolicy());

    /// <summary>
    /// Registers the given permission policy, which backs
    /// navigator.permissions and gates the clipboard, the geolocation, and
    /// persisting the storage. Passing null leaves the configuration
    /// unchanged, i.e., these features are not gated.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="policy">The policy deciding about the permissions.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithPermissions(this IConfiguration configuration, IPermissionPolicy policy)
    {
        if (policy is null)
        {
            return configuration;
        }

        return configuration.WithOnly<IPermissionPolicy>(policy);
    }

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

    #region StorageManager

    /// <summary>
    /// Registers the storage manager service using the given platform implementation.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="platform">The platform storage implementation.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithStorageManager(this IConfiguration configuration, IStorageManagerPlatform platform)
    {
        if (platform is not null)
        {
            var factory = new StorageManagerProviderFactory(platform);
            return configuration.WithStorageManagerProviderFactory(factory);
        }

        return configuration;
    }

    /// <summary>
    /// Registers a storage manager provider factory.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="factory">The storage manager provider factory to use.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithStorageManagerProviderFactory(this IConfiguration configuration, IStorageManagerProviderFactory factory) =>
        configuration.WithOnly<IStorageManagerProviderFactory>(factory);

    #endregion

    #region Service Workers

    /// <summary>
    /// Registers the service worker container with a platform installing and
    /// activating every worker without intercepting any request. Needs to be
    /// called after the requesters have been registered.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithServiceWorkers(this IConfiguration configuration) =>
        configuration.WithServiceWorkers(new ServiceWorkerPlatform());

    /// <summary>
    /// Registers the service worker container using the given platform
    /// implementation. Needs to be called after the requesters have been
    /// registered. Passing null leaves the configuration unchanged.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="platform">The platform running the workers.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithServiceWorkers(this IConfiguration configuration, IServiceWorkerPlatform platform)
    {
        if (platform is not null)
        {
            return configuration.WithServiceWorkers(new ServiceWorkerRegistry(platform));
        }

        return configuration;
    }

    /// <summary>
    /// Registers the service worker container using the given registry, which
    /// is also registered as a service. The registered HTTP(S) requesters are
    /// wrapped to pass requests to the active workers. Needs to be called
    /// after the requesters have been registered.
    /// </summary>
    /// <param name="configuration">The configuration to extend.</param>
    /// <param name="registry">The registry holding the registrations.</param>
    /// <returns>The new instance with the service.</returns>
    public static IConfiguration WithServiceWorkers(this IConfiguration configuration, ServiceWorkerRegistry registry)
    {
        var services = configuration.Services.Select(service => service switch
        {
            ServiceWorkerRequester serviceWorker => new ServiceWorkerRequester(serviceWorker.Inner, registry),
            IRequester requester when IsHttpRequester(requester) => new ServiceWorkerRequester(requester, registry),
            _ => service,
        });

        return new Configuration(services)
            .WithOnly(registry)
            .WithOnly<IServiceWorkerProviderFactory>(new ServiceWorkerProviderFactory(registry));
    }

    #endregion
}
