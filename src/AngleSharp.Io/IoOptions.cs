namespace AngleSharp.Io;

using System.Net.Http;
using AngleSharp.Io.Cache;
using AngleSharp.Io.Cookie;
using AngleSharp.Io.Dom;
using AngleSharp.Io.IndexedDb;
using AngleSharp.Io.Storage;

/// <summary>
/// Options for setting up the Io services.
/// </summary>
public sealed class IoOptions
{
    /// <summary>
    /// Gets or sets the options for configuring the navigator.
    /// </summary>
    public NavigatorOptions Navigator { get; set; } = new NavigatorOptions();

    /// <summary>
    /// Gets or sets the handler for configuring the cookie jar.
    /// </summary>
    public ICookieFileHandler CookieHandler { get; set; } = new MemoryFileHandler();

    /// <summary>
    /// Gets or sets the handler for configuring the HTTP requester.
    /// </summary>
    public HttpClientHandler HttpHandler { get; set; } = new HttpClientHandler();

    /// <summary>
    /// Gets or sets the provider for configuring the web storage provider.
    /// </summary>
    public IStorageProviderFactory StorageFactory { get; set; } = StorageProviderFactory.CreateTemporary();

    /// <summary>
    /// Gets or sets the provider for configuring the storage provider.
    /// </summary>
    public IIndexedDbProviderFactory IndexedDbFactory { get; set; } = IndexedDbProviderFactory.CreateTemporary();

    /// <summary>
    /// Gets or sets the provider for configuring the cache storage provider.
    /// </summary>
    public ICacheProviderFactory CacheFactory { get; set; } = CacheProviderFactory.CreateTemporary();

    /// <summary>
    /// Gets or sets the provider for configuring the clipboard platform.
    /// By default, this is null, which means that the clipboard API will not be available.
    /// </summary>
    public IClipboardPlatform ClipboardPlatform { get; set; } = null;

    /// <summary>
    /// Gets or sets the provider for configuring the geolocation platform.
    /// By default, this is null, which means that the geolocation API will not be available.
    /// </summary>
    public IGeolocationPlatform GeolocationPlatform { get; set; } = null;
}
