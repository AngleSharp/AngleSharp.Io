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

AngleSharp.Io also exposes a lightweight IndexedDB surface that is shared per origin:

- `indexedDB` using in-memory databases and object stores scoped to the origin

Register IndexedDB by providing a provider factory:

```cs
var factory = new IndexedDbProviderFactory();
var config = Configuration.Default.WithIndexedDbProviderFactory(factory);
```

Use the service from a browsing context:

```cs
var context = BrowsingContext.New(config);
var document = await context.OpenAsync("https://example.org");
var database = document.DefaultView.IndexedDb().Open("app");
var store = database.CreateObjectStore("records");

store["persisted"] = "yes";
```

### Cache Storage

AngleSharp.Io also exposes a lightweight Cache Storage surface that is shared per origin:

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
