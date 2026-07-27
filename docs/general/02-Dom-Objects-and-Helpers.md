---
title: "DOM Objects and Helpers"
section: "AngleSharp.Io"
---
# DOM Objects and Helpers

AngleSharp.Io extends AngleSharp with additional DOM-related building blocks and convenience methods. This page summarizes what they are and when to use them.

## Overview

The package adds the following DOM-oriented types:

- `InputFile` in `AngleSharp.Io.Dom`
- `WebSocket` in `AngleSharp.Io.Dom`
- `IStorage` interface in `AngleSharp.Io.Dom`
- `ElementExtensions` helper methods in `AngleSharp.Io.Dom`

## InputFile

`InputFile` implements `IFile` and is meant for programmatic file input scenarios, e.g., test automation for `<input type="file">`.

### Constructors

- `InputFile(String fileName, String type, Stream content, DateTime modified)`
- `InputFile(String fileName, String type, Stream content)`
- `InputFile(String fileName, String type, Byte[] content)`

### Key members

- `Name`: the file name as exposed to DOM consumers
- `Type`: MIME type
- `Length`: stream length
- `LastModified`: last modification timestamp
- `Body`: underlying content stream
- `Slice(...)`: returns an `IBlob` over a selected byte range

### Notes

- `InputFile` uses the provided stream directly. Ownership and disposal rules should be considered in tests and long-running automation.
- The `Slice` method returns a new `InputFile`-backed blob over copied bytes.

## ElementExtensions

`ElementExtensions` provides utility methods for `IHtmlInputElement` and URL-capable DOM elements.

### File attachment helpers

You can attach files to a file input in multiple ways:

- `AppendFile(input, InputFile file)`
- `AppendFile(input, String filePath)`
- `AppendFile(input, String fileName, Stream content, String mimeType = null)`

Example:

```cs
var input = document.QuerySelector<IHtmlInputElement>("input[type=file]");

input
    .AppendFile("avatar.png", new MemoryStream(imageBytes), "image/png")
    .AppendFile("contract.pdf");
```

If the input element is not of type `file`, no file is appended.

### Download helper

`DownloadAsync` allows direct download from a link-like element (`IUrlUtilities` + `IElement`) via the configured loader.

```cs
var link = document.QuerySelector<IHtmlAnchorElement>("a.download");
var response = await link.DownloadAsync();
```

Requirements:

- The element must belong to a browsing context.
- A document loader must be registered (usually via `WithDefaultLoader`).

## WebSocket

`WebSocket` is a DOM-facing wrapper around `ClientWebSocket` with familiar browser-style events.

### Events

- `Opened` (`open`)
- `Message` (`message`)
- `Error` (`error`)
- `Closed` (`close`)

### Methods

- `Send(String data)`
- `Close()`

### Properties

- `Url`
- `ReadyState`
- `Protocol`
- `Buffered`

Example:

```cs
var ws = new WebSocket(document.DefaultView, "wss://echo.websocket.events/");

ws.Opened += (s, e) => ws.Send("hello");
ws.Message += (s, e) => Console.WriteLine(e.Type);
ws.Error += (s, e) => Console.WriteLine("socket error");
ws.Closed += (s, e) => Console.WriteLine("socket closed");
```

## IStorage

`IStorage` models the Web Storage API shape (`Storage`) and exposes:

- `Length`
- `Key(Int32 index)`
- indexer `this[String key]`
- `Remove(String key)`
- `Clear()`

It exists as a DOM contract abstraction, suitable for integration and host-specific implementations.
