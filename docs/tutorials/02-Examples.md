---
title: "Examples"
section: "AngleSharp.Io"
---
# Example Code

This is a (growing) list of examples for every-day usage of AngleSharp.Io.

## Some Example

AngleSharp.Io makes it easy to use the `HttpClient` for, e.g., utilizing a proxy to make HTTP requests.

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

var context = BrowsingContext.New(config);

// this will load the document using the given requester, i.e., using the proxy
var document = await context.OpenAsync("https://some-page.com/foo");
```
