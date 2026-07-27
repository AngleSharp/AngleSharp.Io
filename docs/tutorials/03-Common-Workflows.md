---
title: "Common Workflows"
section: "AngleSharp.Io"
---
# Common Workflows

This tutorial collects practical, copy-paste-friendly workflows for common AngleSharp.Io scenarios.

## 1. Use custom HttpClient behavior with requesters

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

## 2. Persist cookies across runs

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

## 3. Simulate file upload input in tests

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

## 4. Download linked resources directly

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

## 5. Open a WebSocket from a DOM context

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
