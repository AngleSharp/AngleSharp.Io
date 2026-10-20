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
    Permissions = new PermissionPolicy(),
    ClipboardPlatform = myClipboardPlatform,
    GeolocationPlatform = myGeolocationPlatform,
    StorageManagerPlatform = myStorageManagerPlatform,
    ServiceWorkerPlatform = myServiceWorkerPlatform,
    WebRtcPlatform = new LoopbackRtcPlatform(),
};

var config = Configuration.Default
    .WithIo(options)
    .WithDefaultLoader();
```

`WithIo` wires navigator, cookies, requesters, storage, IndexedDB, and cache in one call.
Clipboard, geolocation, the storage manager, service workers, and WebRTC are only enabled when platforms are provided.
The [permissions](#permissions) are only checked when a policy is provided.

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

### Permissions

AngleSharp.Io provides `navigator.permissions` backed by a single permission policy. The same
policy decides whether the powerful features may be used, so there is one place to configure them:

- `permissions` exposing `query`, which returns a `PermissionStatus` with `name`, `state`, and `onchange`
- `clipboard.readText` / `clipboard.writeText` check `clipboard-read` / `clipboard-write`
- `geolocation.getCurrentPosition` checks `geolocation`
- `storage.persist` checks `persistent-storage` (and resolves with `false` instead of rejecting)

The names in `PermissionNames` are known by default (`notifications` can be queried, but nothing
uses it yet). A denied permission rejects with a `DomException` named `NotAllowedError`.

`PermissionPolicy` keeps the states in memory. Every permission starts as `prompt`, and as no user
can answer the prompt in a headless browser, the feature is denied until you grant the permission:

```cs
var policy = new PermissionPolicy();

// for all origins; entries for a single origin take precedence
policy.Set(PermissionNames.ClipboardWrite, PermissionState.Granted);
policy.Set("https://example.com", PermissionNames.Geolocation, PermissionState.Granted);

// optional: answer prompts in place of the user, the answer is remembered for the origin
policy.PromptHandler = (origin, name) => Task.FromResult(PermissionState.Denied);

// optional: make a permission of another library known
policy.Register("local-fonts");

var config = Configuration.Default
    .WithNavigator()
    .WithPermissions(policy)
    .WithClipboard(new MyClipboardPlatform());
```

Use `new PermissionPolicy(PermissionState.Granted)` to allow everything, or implement
`IPermissionPolicy` to take the decisions from somewhere else. Without `WithPermissions` there is
no `navigator.permissions` and the features are not gated, as in previous versions.

Statuses follow later changes of the policy and dispatch `change` when their state changes:

```cs
var permissions = document.DefaultView.Navigator.Permissions();
var status = await permissions.Query(new PermissionDescriptor { Name = "geolocation" });
status.Changed += (sender, ev) => Console.WriteLine(status.State);

policy.Set("https://example.com", PermissionNames.Geolocation, PermissionState.Denied);
```

APIs from other libraries can use the same check before touching a feature:

```cs
await permissions.EnsureGrantedAsync("local-fonts"); // throws NotAllowedError if not granted
var state = await permissions.RequestAsync("local-fonts"); // granted or denied, never prompt
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

### Storage Manager

AngleSharp.Io provides `navigator.storage` backed by a user-supplied platform implementation:

- `storage` exposing `estimate`, `persist`, and `persisted`

The platform receives the origin of the active document. `MemoryStorageManagerPlatform` reports
fixed values and keeps the persistence state in memory, which makes it a good fit for tests:

```cs
var platform = new MemoryStorageManagerPlatform
{
    Quota = 1024 * 1024 * 1024,
    Usage = 4096,
    AllowPersistence = true, // the default
};

var config = Configuration.Default
    .WithNavigator()
    .WithStorageManager(platform);
```

For host-specific behavior implement `IStorageManagerPlatform` instead. Resolve it from `INavigator`:

```cs
var storage = document.DefaultView.Navigator.Storage();
var estimate = await storage.Estimate(); // estimate.Usage, estimate.Quota
var granted = await storage.Persist();
var persisted = await storage.Persisted();
```

### Service Workers

AngleSharp.Io provides `navigator.serviceWorker` with a minimal registration surface:

- `serviceWorker` exposing `register` (with `scope`, `type`, and `updateViaCache`), `getRegistration`,
  `getRegistrations`, `ready`, `controller`, and the `controllerchange` and `message` events
- registrations exposing `scope`, `updateViaCache`, `installing`, `waiting`, `active`, `update`,
  `unregister`, and the `updatefound` event
- workers exposing `scriptURL`, `state`, `postMessage`, and the `statechange` event

As in browsers, `navigator.serviceWorker` is only available in secure contexts, i.e., for `https`
documents and for `http` documents served from `localhost` or a loopback address.

Running the worker script is up to an `IServiceWorkerPlatform`. The included `ServiceWorkerPlatform`
delegates to optional callbacks; without callbacks every worker installs and activates and all
requests go to the network:

```cs
var platform = new ServiceWorkerPlatform
{
    Install = worker => Task.CompletedTask,      // a faulted task makes the worker redundant
    CheckForUpdate = worker => Task.FromResult(false), // true installs a new worker
    Message = (worker, message) => { },          // receives worker.postMessage(...)
    Fetch = (worker, request, cancel) =>
    {
        // return null to continue with the network
        var response = VirtualResponse.Create(res => res.Address(request.Address).Content("offline"));
        return Task.FromResult(response);
    },
};

var config = Configuration.Default
    .WithNavigator()
    .WithRequesters()
    .WithServiceWorkers(platform) // after WithRequesters
    .WithDefaultLoader();
```

`WithServiceWorkers` wraps the registered HTTP(S) requesters, so it must be called after
`WithRequesters`. Requests (including navigations and `fetch`) whose URL lies within the scope of
an active worker are passed to the platform first; the HTTP cache, if any, stays behind the worker.

```cs
var container = document.DefaultView.Navigator.ServiceWorker();
var registration = await container.Register("/app/sw.js"); // scope defaults to /app/
var state = registration.Active.State;                       // ServiceWorkerState.Activated
var ready = await container.Ready;                           // registration controlling the document
await registration.Update();                                 // asks the platform's CheckForUpdate
```

Workers reply to documents via the registry, which dispatches a `message` event on the containers
of the documents they control:

```cs
var registry = context.GetService<ServiceWorkerRegistry>();
registry.PostMessageToClients(registration.Active, "hello");
```

Compared to browsers the lifecycle is simplified: `register` and `update` complete once the worker
is activated (or redundant if its installation failed), new workers are activated right away as if
`skipWaiting()` was called, and `unregister` drops the workers immediately. Since scripts are not
fetched, the platform decides whether a script changed (`CheckForUpdate`) and the scope cannot be
wider than the script's directory. Requests are matched by their own URL and the controller is
determined on access, as clients are not tracked.

### Beacon

AngleSharp.Io provides `navigator.sendBeacon` for fire-and-forget requests, e.g., for telemetry:

- `sendBeacon(url, data)` queues a `POST` request and returns right away

The data can be a string (sent as `text/plain;charset=UTF-8`), a blob (sent with its type), a byte
array or `ArraySegment<Byte>`, `FormDataSet` (sent as `multipart/form-data`), or `UrlSearchParams`
(sent as `application/x-www-form-urlencoded`). The request goes through the configured
`IDocumentLoader`, i.e., the requesters and cookies apply as for `fetch`.

```cs
var config = Configuration.Default
    .WithNavigator()
    .WithRequesters()
    .WithDefaultLoader();

var navigator = document.DefaultView.Navigator;
var queued = navigator.SendBeacon("/collect", "session ended");
```

`SendBeacon` returns `false` if the beacon was not queued: when no loader is registered, when the
data is not supported (e.g., a `Stream`, whose length is unknown upfront) or cannot be serialized,
or when the body would exceed the quota. As in browsers, the bodies of the pending beacons may not
exceed 64 KiB per browsing context. Once queued, failures (e.g., network errors) are not reported,
but release the quota. Beacons keep going when their document navigates away, closes, or its
browsing context is disposed.

`BeaconOptions` changes the quota and can limit the number of beacons sent at the same time; further
beacons are queued, count towards the quota, and are sent in order:

```cs
var config = Configuration.Default
    .WithNavigator()
    .WithRequesters()
    .WithDefaultLoader()
    .WithBeacon(new BeaconOptions
    {
        Quota = 256 * 1024,
        MaxConcurrentRequests = 4, // 0 (default) sends all right away
    });
```

Beacons carry an `Origin` header. Like in browsers, a beacon to another origin whose content type is
not `text/plain`, `application/x-www-form-urlencoded`, or `multipart/form-data` (e.g., a JSON blob)
is preceded by a CORS preflight (`OPTIONS`, without cookies). The beacon is only sent if the response
allows the origin (`Access-Control-Allow-Origin` with the exact origin), credentials
(`Access-Control-Allow-Credentials: true`), and the `content-type` header
(`Access-Control-Allow-Headers`). Preflight results are not cached.

An invalid or non-HTTP(S) URL throws an `InvalidOperationException`, standing in for the
`TypeError` of the spec.

### Reporting

AngleSharp.Io collects browser-like diagnostics as reports, following the shape of the Reporting
API and Network Error Logging (NEL):

- `WithReporting` registers a `ReportingService` that queues reports until they are flushed
- failed requests and responses with a status of 400 or above are captured as `network-error`
  reports from the requester pipeline
- `window.QueueReport(...)` emits reports about a document, e.g., deprecations
- `ReportingObserver` exposes the reports of a window (`observe`, `disconnect`, `takeRecords`)

Flushed reports go to the configured sinks, each with an optional filter by type and endpoint:

```cs
var memory = new MemoryReportSink();
var options = new ReportingOptions()
    .AddSink(memory)
    .AddSink(new EndpointReportSink(new HttpClientRequester()), ReportFilter.ForTypes(ReportTypes.NetworkError))
    .AddSink((reports, cancel) => LogAsync(reports), ReportFilter.ForDestinations("default"));

options.Endpoints["default"] = "https://collector.example/reports";

var config = Configuration.Default
    .WithRequesters()
    .WithReporting(options) // after WithRequesters
    .WithDefaultLoader();
```

The service can be resolved from the configuration or a browsing context to enumerate, emit, and
flush the reports:

```cs
var reporting = context.GetService<ReportingService>();
var errors = reporting.GetReports(ReportFilter.ForTypes(ReportTypes.NetworkError));
reporting.QueueReport(new Report("custom", "https://example.org/", new GenericReportBody(members)));
await reporting.FlushAsync(); // removes the queued reports and hands them to the sinks
```

Reports serialize to JSON via `ToJson()` (`type`, `url`, `body`) and `ToDeliveryJson(now)`, the
format posted by `EndpointReportSink` as `application/reports+json` (adding `age` and `user_agent`).
A `network-error` report carries a `NetworkErrorReportBody` with the NEL members, e.g., `phase`
(`dns`, `connection`, `application`), `type` (e.g., `dns.name_not_resolved`, `tcp.refused`,
`tcp.timed_out`, `tls.failed`, `abandoned`, `http.error`), `elapsed_time`, `method`, `referrer`,
and `status_code`. Report URLs exclude credentials and fragments.

Further options:

- `Filter` decides which reports are queued at all
- `CaptureNetworkErrors` / `CaptureHttpErrors` turn the captured network errors on or off
- `NetworkErrorDestination` names the endpoint of network errors (`default` by default)
- `MaxQueueSize` limits the queue, dropping the oldest reports (1000 by default)
- `FlushInterval` flushes automatically, otherwise reports are only flushed by `FlushAsync`

Endpoint names are resolved to URLs via the `Reporting-Endpoints` header of responses from the
report's origin, falling back to `Endpoints`. The reporting sits closest to the network, i.e.,
behind the HTTP cache and service workers, so only requests reaching the network are reported.

As in browsers, a `ReportingObserver` callback runs as a task of the browsing context's event loop
(`IEventLoop`, e.g., provided by a scripting engine), receiving all reports added until the task
runs; without an event loop the callback is invoked right away.

Compared to browsers this is a lightweight subset: a flush removes the reports even if a sink fails
(the failure is rethrown), and `NEL` policy headers are not interpreted, i.e., network errors of
every origin are captured.

### WebRTC

AngleSharp.Io provides the signaling part of WebRTC via `RTCPeerConnection`, with the transport
provided by a pluggable `IRtcPlatform`:

- `createOffer`, `createAnswer`, `setLocalDescription` (also without arguments), and
  `setRemoteDescription`, including provisional answers and (implicit) rollbacks
- `addIceCandidate`, `restartIce`, `getConfiguration`, `setConfiguration`, and `close`
- the `signalingState`, `iceGatheringState`, `iceConnectionState`, and `connectionState`
  attributes with their change events, plus `negotiationneeded` and `icecandidate`
- `createDataChannel` with `RTCDataChannel` (`send`, `close`, and the `open`, `message`,
  `closing`, `close` events) and the `datachannel` event

`WithWebRtc()` registers the `LoopbackRtcPlatform`, which connects the peer connections of the
configuration with each other in-process: its SDP is deterministic, each peer gathers a single host
candidate on 127.0.0.1, and a peer connects once both descriptions are applied and it received a
candidate of the other peer. Data channel messages are delivered between the connected peers.

```cs
var config = Configuration.Default.WithWebRtc();
var context = BrowsingContext.New(config);
var document = await context.OpenAsync("https://example.org");

var caller = new RtcPeerConnection(document.DefaultView);
var callee = new RtcPeerConnection(document.DefaultView);
var channel = caller.CreateDataChannel("chat");

var offer = await caller.CreateOffer();
await caller.SetLocalDescription(offer);
await callee.SetRemoteDescription(offer);
var answer = await callee.CreateAnswer();
await callee.SetLocalDescription(answer);
await caller.SetRemoteDescription(answer);
// then exchange the candidates of the icecandidate events via addIceCandidate
```

A custom `IRtcPlatform` (e.g., backed by a WebRTC stack or a test double) creates an `IRtcPeer` per
connection, which produces the SDP, applies the descriptions, and reports candidates, state changes,
and data channel events through the given `IRtcPeerHost`. Without a registered platform,
constructing an `RTCPeerConnection` from a window fails with `NotSupportedError`; the constructor
taking an `IRtcPlatform` works without any configuration.

As in browsers, the events of the transport (candidates, ICE states, data channels) are dispatched as
tasks of the browsing context's event loop. Without an event loop they are dispatched once the
running operation completed, i.e., always after the state change they belong to. Media (tracks,
transceivers, codecs) and statistics are not supported; data channels deliver messages in order
without modeling the reliability parameters, and binary messages are always byte arrays.

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
