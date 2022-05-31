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
