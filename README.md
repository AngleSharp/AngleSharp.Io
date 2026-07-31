![logo](https://raw.githubusercontent.com/AngleSharp/AngleSharp.Io/main/header.png)

# AngleSharp.Io

[![CI](https://github.com/AngleSharp/AngleSharp.Io/actions/workflows/ci.yml/badge.svg)](https://github.com/AngleSharp/AngleSharp.Io/actions/workflows/ci.yml)
[![GitHub Tag](https://img.shields.io/github/tag/AngleSharp/AngleSharp.Io.svg?style=flat-square)](https://github.com/AngleSharp/AngleSharp.Io/releases)
[![NuGet Count](https://img.shields.io/nuget/dt/AngleSharp.Io.svg?style=flat-square)](https://www.nuget.org/packages/AngleSharp.Io/)
[![Issues Open](https://img.shields.io/github/issues/AngleSharp/AngleSharp.Io.svg?style=flat-square)](https://github.com/AngleSharp/AngleSharp.Io/issues)
[![Gitter Chat](http://img.shields.io/badge/gitter-AngleSharp/AngleSharp-blue.svg?style=flat-square)](https://gitter.im/AngleSharp/AngleSharp)
[![StackOverflow Questions](https://img.shields.io/stackexchange/stackoverflow/t/anglesharp.svg?style=flat-square)](https://stackoverflow.com/tags/anglesharp)
[![CLA Assistant](https://cla-assistant.io/readme/badge/AngleSharp/AngleSharp.Io?style=flat-square)](https://cla-assistant.io/AngleSharp/AngleSharp.Io)

AngleSharp.Io extends AngleSharp with powerful requesters, caching mechanisms, and storage systems. It is coupled more strongly to the underlying operating system than AngleSharp itself. Therefore it has stronger dependencies and demands and cannot be released for the standard framework (4.6). Nevertheless, it is released as a .NET Standard 2.0 library.

## Basic Configuration

### Requesters

If you just want to use *all* available requesters provided by AngleSharp.Io you can do the following:

```cs
var config = Configuration.Default
    .WithRequesters() // from AngleSharp.Io
    .WithDefaultLoader(); // from AngleSharp
```

This will register all requesters. Alternatively, the requesters can be provided explicitly. They are located in the `AngleSharp.Io.Network` namespace and have names such as `DataRequester`.

Requesters can make use of `HttpClientHandler` instances. Hence using it, e.g., with a proxy is as simple as the following snippet:

```cs
var handler = new HttpClientHandler
{
    Proxy = new WebProxy(myProxyHost, false),
    PreAuthenticate = true,
    UseDefaultCredentials = false,
};

var config = Configuration.Default
    .WithRequesters(handler) // from AngleSharp.Io with a handler config
    .WithDefaultLoader();
```

Alternatively, if you don't want to add all possible requesters, you can also just add a single requester from AngleSharp.Io:

```cs
var config = Configuration.Default
    .With(new HttpClientRequester()) // only requester
    .WithDefaultLoader();
```

In the code above we now only have a single requester - the `HttpClientRequester` coming from AngleSharp.Io. If we have an `HttpClient` already used somewhere we can actually re-use it:

```cs
var config = Configuration.Default
    .With(new HttpClientRequester(myHttpClient)) // re-using the HttpClient instance
    .WithDefaultLoader();
```

### Cookies

To get improved cookie support, e.g., do

```cs
var config = Configuration.Default
    .WithTemporaryCookies(); // Uses memory cookies
```

or if you want to have persistent cookies you can use:

```cs
var syncPath = $"Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)\\anglesharp.cookies";
var config = Configuration.Default
    .WithPersistentCookies(syncPath); // Uses sync cookies against the given path
```

Alternatively, the new overloads for the `WithCookies` extension method can be used.

### Storage

AngleSharp.Io provides a single storage engine with two storage modes:

- `localStorage` using persistent per-origin files
- `sessionStorage` using in-memory buckets scoped to the top-level browsing context and origin

To configure storage, create and register an `IStorageProviderFactory`:

```cs
var syncDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "anglesharp.storage");
var factory = new StorageProviderFactory();

factory.EnableLocalStorage(syncDirectoryPath);
factory.EnableSessionStorage();

var config = Configuration.Default.WithStorageProviderFactory(factory);
```

Once configured, storage services can be resolved from the browsing context:

```cs
var context = BrowsingContext.New(config);
var document = await context.OpenAsync("https://example.org");
var localStorage = document.DefaultView.LocalStorage();
var sessionStorage = document.DefaultView.SessionStorage();

localStorage["token"] = "abc";
sessionStorage["ephemeral"] = "42";
```

### IndexedDB

AngleSharp.Io also provides a lightweight IndexedDB surface that is shared per origin:

- `indexedDB` using in-memory databases and object stores scoped to the origin

To configure IndexedDB, create and register an `IIndexedDbProviderFactory`:

```cs
var factory = new IndexedDbProviderFactory();
var config = Configuration.Default.WithIndexedDbProviderFactory(factory);
```

Once configured, IndexedDB services can be resolved from the browsing context:

```cs
var context = BrowsingContext.New(config);
var document = await context.OpenAsync("https://example.org");
var database = document.DefaultView.IndexedDb().Open("app");
var store = database.CreateObjectStore("records");

store["token"] = "abc";
```

### Cache Storage

AngleSharp.Io also exposes a lightweight Cache Storage surface that is shared per origin:

- `caches` using in-memory caches and response snapshots scoped to the origin

To configure Cache Storage, create and register an `ICacheProviderFactory`:

```cs
var factory = new CacheProviderFactory();
var config = Configuration.Default.WithCacheProviderFactory(factory);
```

Once configured, caches can be resolved from the browsing context:

```cs
var context = BrowsingContext.New(config);
var document = await context.OpenAsync("https://example.org");
var cache = document.DefaultView.Caches().Open("app");

cache.Put("https://example.org/data", new DefaultResponse());
```

### BroadcastChannel

AngleSharp.Io also provides a lightweight BroadcastChannel surface that is shared per origin:

- `BroadcastChannel` delivering `message` events to same-origin channels with the same name

Create a channel from a DOM window:

```cs
var context = BrowsingContext.New();
var document = await context.OpenAsync("https://example.org");

using var channel = new BroadcastChannel(document.DefaultView, "app");
channel.Message += (s, e) => Console.WriteLine(((MessageEvent)e).Data);
channel.PostMessage("hello");
```

### Web Locks

AngleSharp.Io also provides a minimal Web Locks surface that serializes requests per origin and lock name:

- `locks` using in-memory request queues scoped to the origin

Create a lock manager from a navigator implementation:

```cs
var config = Configuration.Default.WithNavigator();
var context = BrowsingContext.New(config);
var document = await context.OpenAsync("https://example.org");
var navigator = document.DefaultView.Navigator;
var locks = navigator.Locks();

await locks.Request("app", async _ =>
{
    Console.WriteLine("inside the lock");
    await Task.CompletedTask;
});
```

### Clipboard

AngleSharp.Io provides a navigator clipboard surface backed by a user-supplied platform implementation:

- `clipboard` exposing `readText` / `writeText`

Register a platform implementation during configuration:

```cs
public sealed class MyClipboardPlatform : IClipboardPlatform
{
    public Task<string> ReadTextAsync() => Task.FromResult("sample");

    public Task WriteTextAsync(string text) => Task.CompletedTask;
}

var config = Configuration.Default.WithClipboard(new MyClipboardPlatform());
```

To expose `navigator`, include navigator registration in the configuration:

```cs
var config = Configuration.Default
    .WithNavigator()
    .WithClipboard(new MyClipboardPlatform());
```

Resolve it from `INavigator`:

```cs
var navigator = document.DefaultView.Navigator;
var clipboard = navigator.Clipboard();

await clipboard.WriteText("hello");
var value = await clipboard.ReadText();
```

### Geolocation

AngleSharp.Io provides a navigator geolocation surface backed by a user-supplied platform implementation:

- `geolocation` exposing `getCurrentPosition`

Register a platform implementation during configuration:

```cs
public sealed class MyGeolocationPlatform : IGeolocationPlatform
{
    public Task<GeolocationReading> GetCurrentPositionAsync(GeolocationOptions options)
    {
        return Task.FromResult(new GeolocationReading
        {
            Latitude = 52.52,
            Longitude = 13.405,
            Accuracy = 8.0,
        });
    }
}

var config = Configuration.Default.WithGeolocation(new MyGeolocationPlatform());
```

To expose `navigator`, include navigator registration in the configuration:

```cs
var config = Configuration.Default
    .WithNavigator()
    .WithGeolocation(new MyGeolocationPlatform());
```

Resolve it from `INavigator`:

```cs
var navigator = document.DefaultView.Navigator;
var geolocation = navigator.Geolocation();
var position = await geolocation.GetCurrentPosition();
```

### Fetch

AngleSharp.Io also exposes a `fetch` method on `window`:

- `fetch` delegates to the configured `IDocumentLoader`

Configure requesters and a default loader:

```cs
var config = Configuration.Default
    .WithRequesters()
    .WithDefaultLoader();
```

Use fetch from a DOM window:

```cs
var context = BrowsingContext.New(config);
var document = await context.OpenAsync("https://example.org");

var response = await document.DefaultView.Fetch("/api/data", new FetchOptions
{
    Method = "POST",
    Headers = new Dictionary<string, string>
    {
        ["X-Requested-With"] = "AngleSharp.Io"
    },
    Body = "payload"
});
```

### Downloads

AngleSharp.Io offers you the possibility of a simplified downloading experience. Just use `WithStandardDownload` to redirect resources to a callback.

In the simplest case you can write:

```cs
var config = Configuration.Default
    .WithStandardDownload((fileName, content) =>
    {
        // store fileName with the content stream ...
    });
```

Alternatively, use `WithDownload`, which allows you to distinguish also on the provided MIME type.

## DOM Extension Methods

The `IHtmlInputElement` interface now has `AppendFile` to easily allow appending files without much trouble.

```cs
document
    .QuerySelector<IHtmlInputElement>("input[type=file]")
    .AppendFile("c:\\example.jpg");
```

More overloads exist.

Furthermore, the `IUrlUtilities` interface now has `DownloadAsync`.

```cs
document
    .QuerySelector<IHtmlAnchorElement>("a#download-document")
    .DownloadAsync()
    .SaveToAsync("c:\\example.pdf");
```

The `SaveToAsync` (as well as the `CopyToAsync`) are extension methods for the `IResponse` interface.

## Features

- New requesters
  - HTTP (using `HttpClient`)
  - FTP
  - Supporting data URLs
  - Supporting file URLs
  - Enhanced support for about: URLs
- WebSockets (mostly interesting for scripting engines, e.g., JS)
- Storage support with a unified `Storage` implementation
- Improved cookie container (`AdvancedCookieContainer`)
- Enhanced download capabilities for resources / links
- Web Locks support for origin-scoped request serialization
- Clipboard support with injectable platform implementation
- Geolocation support with injectable platform implementation

## Participating

Participation in the project is highly welcome. For this project the same rules as for the AngleSharp core project may be applied.

If you have any question, concern, or spot an issue then please report it before opening a pull request. An initial discussion is appreciated regardless of the nature of the problem.

Live discussions can take place in our [Gitter chat](https://gitter.im/AngleSharp/AngleSharp), which supports using GitHub accounts.

This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.

For more information see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

## .NET Foundation

This project is supported by the [.NET Foundation](https://dotnetfoundation.org).

## License

AngleSharp.Io is released using the MIT license. For more information see the [license file](./LICENSE).
