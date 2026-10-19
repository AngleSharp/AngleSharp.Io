---
title: "Common Workflows"
section: "AngleSharp.Io"
---
# Common Workflows

This tutorial collects practical, copy-paste-friendly workflows for common AngleSharp.Io scenarios.

## 1. Register all core Io services in one call

Use this when you want a compact setup with sensible defaults and optional platform integrations.

```cs
var options = new IoOptions
{
    ClipboardPlatform = myClipboardPlatform,   // optional
    GeolocationPlatform = myGeolocationPlatform, // optional
};

var config = Configuration.Default
    .WithIo(options)
    .WithDefaultLoader();
```

Why this helps:

- You configure navigator, cookies, requesters, storage, IndexedDB, and cache together.
- You can still override any dependency through `IoOptions`.

To keep a whole browsing profile (cookies, local storage, HTTP cache) on disk, start from
`IoOptions.CreatePersistent(rootDirectory)` instead; `IoOptions.CreateTemporary()` does the same in
memory.

## 2. Use custom HttpClient behavior with requesters

Use this when you need proxying, custom decompression, headers, or controlled redirect behavior.

```cs
var handler = new HttpClientHandler
{
    Proxy = new WebProxy("http://my-proxy.local:8080", false),
    PreAuthenticate = true,
    UseDefaultCredentials = false,
    AutomaticDecompression = DecompressionMethods.Brotli |
                             DecompressionMethods.GZip |
                             DecompressionMethods.Deflate,
};

var config = Configuration.Default
    .WithRequesters(handler)
    .WithDefaultLoader();

var context = BrowsingContext.New(config);
var document = await context.OpenAsync("https://httpbingo.org/html");
```

Why this helps:

- You keep a single place for transport behavior.
- You still use AngleSharp's standard loading pipeline.

## 3. Persist cookies across runs

Use this for crawl sessions or login flows that should survive process restarts.

```cs
var cookiePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "anglesharp.cookies");

var config = Configuration.Default
    .WithPersistentCookies(cookiePath)
    .WithRequesters()
    .WithDefaultLoader();

var context = BrowsingContext.New(config);
await context.OpenAsync("https://httpbingo.org/cookies/set?k1=v1");
```

If you only need in-memory cookies for a single run, use `WithTemporaryCookies()`.

## 4. Simulate file upload input in tests

Use this when you need to test form/file handling without manual browser interaction.

```cs
var config = Configuration.Default;
var context = BrowsingContext.New(config);
var document = await context.OpenAsync(req => req.Content("<input type='file' id='f'>"));

var input = document.QuerySelector<IHtmlInputElement>("#f");
input.AppendFile("avatar.png", new MemoryStream(imageBytes), "image/png");

var selected = input.Files[0];
Console.WriteLine($"{selected.Name} ({selected.Type})");
```

Alternative:

- `AppendFile("/absolute/path/to/avatar.png")` to attach directly from disk.

## 5. Download linked resources directly

Use this if you want resource stream access from an anchor/link-like element.

```cs
var config = Configuration.Default
    .WithRequesters()
    .WithDefaultLoader();

var context = BrowsingContext.New(config);
var document = await context.OpenAsync(req => req.Content("<a href='https://example.org/file.bin'>Download</a>"));
var link = document.QuerySelector<IHtmlAnchorElement>("a");

var response = await link.DownloadAsync();
using var target = File.Create("file.bin");
await response.Content.CopyToAsync(target);
```

You can also combine this with `WithStandardDownload(...)` if you want callback-based download routing.

## 6. Open a WebSocket from a DOM context

Use this for integration tests or headless workflows that need live message channels.

```cs
var context = BrowsingContext.New();
var document = await context.OpenNewAsync("https://example.org");

var closed = new TaskCompletionSource<Boolean>();
var ws = new WebSocket(document.DefaultView, "wss://echo.websocket.events/");

ws.Opened += (s, e) => ws.Send("hello");
ws.Message += (s, e) => ws.Close();
ws.Closed += (s, e) => closed.TrySetResult(true);
ws.Error += (s, e) => closed.TrySetResult(false);

await closed.Task;
```

Tip: for tests that depend on external endpoints, use explicit timeouts to avoid indefinite waits.

## 7. Cache responses and replay them offline

Use this for crawlers that revisit pages, or for tests that should run against previously recorded
responses instead of the live site.

```cs
var cacheDirectory = Path.Combine(Path.GetTempPath(), "anglesharp.http-cache");

// first run: fill the cache while browsing as usual
var recording = Configuration.Default
    .WithRequesters()
    .WithHttpCache(new HttpCacheOptions
    {
        Handler = new PersistenceHttpCacheHandler(cacheDirectory),
        Mode = HttpCacheMode.ForceCache, // keep using what is stored, even if stale
    })
    .WithDefaultLoader();

await BrowsingContext.New(recording).OpenAsync("https://example.org");

// later runs: never touch the network
var replaying = Configuration.Default
    .WithRequesters()
    .WithHttpCache(new HttpCacheOptions
    {
        Handler = new PersistenceHttpCacheHandler(cacheDirectory),
        Mode = HttpCacheMode.OnlyIfCached, // 504 for anything not recorded
    })
    .WithDefaultLoader();

var document = await BrowsingContext.New(replaying).OpenAsync("https://example.org");
```

Notes:

- Only responses the server allows to be cached are recorded, e.g., `no-store` responses and `Vary: *`
  are never stored.
- With `HttpCacheMode.Default` the cache behaves like a browser and only uses fresh responses.

