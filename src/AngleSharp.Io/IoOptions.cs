namespace AngleSharp.Io;

using System;
using System.IO;
using System.Net.Http;
using AngleSharp.Io.Cache;
using AngleSharp.Io.Cookie;
using AngleSharp.Io.Dom;
using AngleSharp.Io.Http;
using AngleSharp.Io.IndexedDb;
using AngleSharp.Io.Storage;

/// <summary>
/// Options for setting up the Io services.
/// </summary>
public sealed class IoOptions
{
    /// <summary>
    /// Creates options keeping all data (cookies, storage, HTTP cache) in
    /// memory only.
    /// </summary>
    /// <returns>The new options.</returns>
    public static IoOptions CreateTemporary() => new IoOptions
    {
        HttpCache = new HttpCacheOptions(),
    };

    /// <summary>
    /// Creates options persisting the cookies (cookies.txt), the local storage
    /// (storage) and the HTTP cache (http-cache) below the given directory.
    /// </summary>
    /// <param name="rootDirectoryPath">The directory holding the data.</param>
    /// <returns>The new options.</returns>
    public static IoOptions CreatePersistent(String rootDirectoryPath)
    {
        if (rootDirectoryPath is null)
        {
            throw new ArgumentNullException(nameof(rootDirectoryPath));
        }

        Directory.CreateDirectory(rootDirectoryPath);

        return new IoOptions
        {
            CookieHandler = new LocalFileHandler(Path.Combine(rootDirectoryPath, "cookies.txt")),
            StorageFactory = StorageProviderFactory.CreatePersistent(Path.Combine(rootDirectoryPath, "storage")),
            HttpCache = new HttpCacheOptions
            {
                Handler = new PersistenceHttpCacheHandler(Path.Combine(rootDirectoryPath, "http-cache")),
            },
        };
    }

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
    /// Gets or sets the options for configuring the HTTP cache in front of the
    /// HTTP requester. By default, this is null, which means that responses
    /// are not cached.
    /// </summary>
    public HttpCacheOptions HttpCache { get; set; } = null;

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
