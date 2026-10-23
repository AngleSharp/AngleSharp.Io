---
title: "Questions"
section: "AngleSharp.Io"
---
# Frequently Asked Questions

## How can the new requesters be used?

The integration of the new requesters can be done via the `WithRequesters` extension method. Don't forgot to add the loaders.

A quick example:

```cs
var config = Configuration.Default
    .WithRequesters() // from AngleSharp.Io
    .WithDefaultLoader(); // from AngleSharp
```

In this case the order matters. The `WithDefaultLoader` method will - by default - look for registered requesters. If no requester has been registered yet, default requesters will be added. This is not what you want. Therefore, add the custom requesters first.

## What requesters will be added?

There are 5 requesters:

- `http:` / `https:` requester (`HttpClientRequester`) using the `HttpClient`
- `data:` requester
- `ftp:` requester
- `file:` requester
- `about:` requester

If you would like to only add specific ones, then register them individually using the `With` extension method. Example:

```cs
var config = Configuration.Default
    .With(new HttpClientRequester()) // only requester
    .WithDefaultLoader();
```

## How can responses be cached?

Use the `WithHttpCache` extension method after the requesters have been registered:

```cs
var config = Configuration.Default
    .WithRequesters()  // from AngleSharp.Io
    .WithHttpCache()   // wraps the HTTP(S) requesters registered above
    .WithDefaultLoader();
```

Again, the order matters: `WithHttpCache` only wraps the requesters that are already registered. Pass a directory (`WithHttpCache(cacheDirectory)`) to keep the cache across runs.

## Why is a response not taken from the cache?

The HTTP cache follows the rules of a browser cache (RFC 9111). Common reasons for a response not being served from the cache are:

- the request is not a `GET` (or `HEAD`) request
- the response has `Cache-Control: no-store` or `Vary: *`
- the response has no `Cache-Control: max-age`, `Expires`, or `Last-Modified`, so it is stale immediately and revalidated (or fetched again) on the next request
- the request had an `Authorization` header and the response is not `public`
- the response is larger than `HttpCacheOptions.MaxEntrySize`

To use stored responses regardless of their freshness, set `Mode = HttpCacheMode.ForceCache` (or `OnlyIfCached` to never use the network) in the `HttpCacheOptions`.

