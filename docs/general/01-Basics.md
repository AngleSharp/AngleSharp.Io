---
title: "Getting Started"
section: "AngleSharp.Io"
---
# Getting Started

## Requirements

AngleSharp.Io comes currently in two flavors: on Windows for .NET 4.6 and in general targetting .NET Standard 2.0 platforms.

Most of the features of the library do not require .NET 4.6, which means you could create your own fork and modify it to work with previous versions of the .NET-Framework.

You need to have AngleSharp installed already. This could be done via NuGet:

```ps1
Install-Package AngleSharp
```

## Getting AngleSharp.Io over NuGet

The simplest way of integrating AngleSharp.Io to your project is by using NuGet. You can install AngleSharp.Io by opening the package manager console (PM) and typing in the following statement:

```ps1
Install-Package AngleSharp.Io
```

You can also use the graphical library package manager ("Manage NuGet Packages for Solution"). Searching for "AngleSharp.Io" in the official NuGet online feed will find this library.

## Setting up AngleSharp.Io

To use AngleSharp.Io you need to add it to your `Configuration` coming from AngleSharp itself.

### All-In-One Setup

If you want to register all core AngleSharp.Io services in one step, use `WithIo` with `IoOptions`:

```cs
var options = new IoOptions
{
    // optional; defaults are already provided
    ClipboardPlatform = myClipboardPlatform,
    GeolocationPlatform = myGeolocationPlatform,
};

var config = Configuration.Default
    .WithIo(options)
    .WithDefaultLoader();
```

`WithIo` wires navigator, cookies, requesters, storage, IndexedDB, and cache in one call.
Clipboard and geolocation are only enabled when platforms are provided.

Instead of `new IoOptions()` you can start from one of the factory methods, which also turn on
the [HTTP cache](#http-cache):

```cs
// everything in memory, e.g., for tests or private sessions
var temporary = IoOptions.CreateTemporary();

// cookies, local storage, and HTTP cache persisted below one directory:
//   <root>/cookies.txt, <root>/storage, <root>/http-cache
var persistent = IoOptions.CreatePersistent(rootDirectory);

var config = Configuration.Default
    .WithIo(persistent)
    .WithDefaultLoader();
```

A plain `new IoOptions()` leaves the HTTP cache off. IndexedDB and Cache Storage are always kept in
memory, also with `CreatePersistent`.

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

### HTTP Cache

The HTTP(S) requesters can be wrapped with a private HTTP cache that follows RFC 9111, just like
the cache of a browser. Cached responses are answered without touching the network; stale ones are
revalidated with the server.

```cs
var config = Configuration.Default
    .WithRequesters()  // the cache wraps the requesters registered so far ...
    .WithHttpCache()   // ... so it needs to come after them
    .WithDefaultLoader();
```

The entries can be kept in memory (default) or persisted in a directory, which is the same choice
as for cookies and storage:

```cs
config.WithHttpCache();                                        // memory
config.WithHttpCache(cacheDirectory);                          // persisted files
config.WithHttpCache(new MemoryHttpCacheHandler());            // explicit handler
config.WithHttpCache(new HttpCacheOptions { /* see below */ }); // full control
```

What gets cached:

- Only `GET` responses are stored; a `HEAD` request is answered from a stored `GET` response.
- Unsafe methods (`POST`, `PUT`, `DELETE`, ...) are never cached. A successful one removes the stored
  responses for its address as well as for same-origin `Location` / `Content-Location` targets.
- `Cache-Control` (`no-store`, `no-cache`, `max-age`, `must-revalidate`, `public`, ...) and `Expires`
  of the response decide how long a response is fresh. Without them, responses with `Last-Modified`
  stay fresh for 10% of their age (at most 7 days).
- Request headers are respected, too: `Cache-Control: no-store`, `no-cache`, `max-age`, `min-fresh`,
  `max-stale`, `only-if-cached`, and `Pragma: no-cache`.
- `Vary` keeps separate variants, e.g., per `Accept-Language`. `Vary: *` is never stored.
- Stale responses with `ETag` or `Last-Modified` are revalidated using `If-None-Match` /
  `If-Modified-Since`. A `304` refreshes the stored response.
- Requests with an `Authorization` header are only stored if the response allows it (`public`,
  `must-revalidate`, or `s-maxage`). `private` responses are stored, since the cache is private.
- `Set-Cookie` is never stored, so cookies are only applied for responses coming from the network.
- If the network fails, a stale response is used instead, unless it requires revalidation.

The `HttpCacheOptions` give you more control:

```cs
var options = new HttpCacheOptions
{
    Handler = new PersistenceHttpCacheHandler(cacheDirectory),
    Mode = HttpCacheMode.Default,
    MaxEntrySize = 10 * 1024 * 1024,  // larger responses are passed through, but not stored
    UseHeuristicFreshness = true,     // the Last-Modified rule from above
    Bypass = request => request.Address.Path.StartsWith("api/"), // never cache these
};

var config = Configuration.Default
    .WithRequesters()
    .WithHttpCache(options)
    .WithDefaultLoader();
```

The `Mode` uses the names of the `cache` option of `fetch`:

| Mode | Uses stored responses | Goes to the network | Stores responses |
| --- | --- | --- | --- |
| `Default` | if fresh (stale ones are revalidated) | if needed | yes |
| `NoStore` | no | always | no |
| `Reload` | no | always | yes |
| `NoCache` | after revalidation | always | yes |
| `ForceCache` | always, even if stale | only if nothing is stored | yes |
| `OnlyIfCached` | always, even if stale | never (`504` if nothing is stored) | no |

The cache is registered as `IHttpCache`, which can be used to look into or clear the entries. The
entries are kept per origin of the requested address:

```cs
var context = BrowsingContext.New(config);
var cache = context.GetService<IHttpCache>();

var stored = await cache.MatchAsync(new Request { Address = new Url("https://example.org/") }, CancellationToken.None);
await cache.RemoveAsync(new Url("https://example.org/"), CancellationToken.None);
await cache.ClearAsync("https://example.org", CancellationToken.None); // one origin
await cache.ClearAsync(CancellationToken.None);                        // everything
```

Note that the HTTP cache is not the same as the [Cache Storage](#cache-storage) (`caches`), which is
a DOM API that is only filled explicitly.

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

AngleSharp.Io contains a single `Storage` implementation that is exposed as
`localStorage` and `sessionStorage` depending on the configured handler:

- local storage uses persistent per-origin files
- session storage uses in-memory buckets scoped to the top-level browsing context and origin

Register storage by providing a storage provider factory:

```cs
var syncDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "anglesharp.storage");
var factory = new StorageProviderFactory();

factory.EnableLocalStorage(syncDirectoryPath);
factory.EnableSessionStorage();

var config = Configuration.Default.WithStorageProviderFactory(factory);
```

Use the services from a browsing context:

```cs
var context = BrowsingContext.New(config);
var document = await context.OpenAsync("https://example.org");
var localStorage = document.DefaultView.LocalStorage();
var sessionStorage = document.DefaultView.SessionStorage();

localStorage["persisted"] = "yes";
sessionStorage["transient"] = "yes";
```

### IndexedDB

AngleSharp.Io exposes an in-memory IndexedDB surface that is shared per origin:

- `indexedDB` using in-memory databases and object stores scoped to the origin
- versioned open with upgrade callback support
- explicit transactions (`ReadOnly` / `ReadWrite`) with commit and abort
- request-style open wrappers (`OpenRequest(...)`)
- object store record methods (`Add`, `Put`, `Get`, `Delete`, `Count`)
- key-range queries (`IndexedDbKeyRange`)
- indexes with optional uniqueness (`CreateIndex`, `GetIndex`, `DeleteIndex`)
- cursors for store and index iteration (`OpenCursor`)
- request state (`Pending` / `Success` / `Error` / `Blocked`) and close lifecycle
- version-change notifications on open connections
- optional auto-unblock open requests (`OpenRequest(..., waitForUnblock: true)`)
- request-based delete with optional auto-unblock (`DeleteDatabaseRequest(..., waitForUnblock: true)`)
- transaction lifecycle state/events (`IsCompleted`, `IsAborted`, `Completed`, `Aborted`)
- single active read-write transaction per database

Register IndexedDB by providing a provider factory:

```cs
var factory = new IndexedDbProviderFactory();
var config = Configuration.Default.WithIndexedDbProviderFactory(factory);
```

Use the service from a browsing context:

```cs
var context = BrowsingContext.New(config);
var document = await context.OpenAsync("https://example.org");
var database = document.DefaultView.IndexedDb().Open("app", 1, tx =>
{
    tx.CreateObjectStore("records");
});

using (var transaction = database.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
{
    var store = transaction.GetObjectStore("records");
    var byType = store.CreateIndex("byType", "type");

    store.Add("persisted", "yes");
    store.Put("persisted", "yes-updated");

    var notes = byType.GetAll("note");
    var range = store.GetAll(IndexedDbKeyRange.Bound("a", "m"));

    transaction.Commit();
}

var openRequest = document.DefaultView.IndexedDb().OpenRequest("app", 2, tx =>
{
    tx.CreateObjectStore("events");
});

var upgraded = await openRequest.WaitAsync();

var cursor = byType.OpenCursor(IndexedDbKeyRange.Bound("note", "task"));

while (cursor != null)
{
    Console.WriteLine(cursor.Value);
    cursor = cursor.Continue();
}

database.Close();

var waitingRequest = document.DefaultView.IndexedDb().OpenRequest(
    "app",
    3,
    tx => tx.CreateObjectStore("logs"),
    waitForUnblock: true);

var upgradedAgain = await waitingRequest.WaitAsync();

var deleteRequest = document.DefaultView.IndexedDb().DeleteDatabaseRequest("app", waitForUnblock: true);
var deleted = await deleteRequest.WaitAsync();

using (var writeTx = upgradedAgain.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
{
    writeTx.Completed += () => Console.WriteLine("transaction committed");
    writeTx.Aborted += () => Console.WriteLine("transaction aborted");
    writeTx.GetObjectStore("records").Put("k", "v");
    writeTx.Commit();
}
```

### Cache Storage

AngleSharp.Io also exposes a lightweight Cache Storage surface that is shared per origin. This is
the DOM API (`caches`) that scripts fill explicitly; for caching the responses of the requesters
see [HTTP Cache](#http-cache).

- `caches` using in-memory caches and response snapshots scoped to the origin

Register Cache Storage by providing a provider factory:

```cs
var factory = new CacheProviderFactory();
var config = Configuration.Default.WithCacheProviderFactory(factory);
```

Use the service from a browsing context:

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

AngleSharp.Io also provides a lightweight Web Locks surface that serializes requests per origin and lock name:

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
