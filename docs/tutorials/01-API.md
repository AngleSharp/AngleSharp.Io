---
title: "API Documentation"
section: "AngleSharp.Io"
---
# API Documentation

The AngleSharp.Io package brings a new DOM interface `IStorage` into AngleSharp. Furthermore, the `InputFile` and `WebSocket` classes have been implemented in AngleSharp.Io.

The `InputFile` makes it possible to append files to, e.g., `<input type=file>` elements. This way you could write:

```cs
var element = document.QuerySelector("input[type=file]");
var file = new InputFile("foo.png", "image/png", new Byte[0] {});
element.AppendFile(file);
```

The content could be either a byte array or `Stream`.

Most importantly, the `AdvancedCookieProvider` is a much better way of handling cookies. While AngleSharp itself uses the cookie jar provided by .NET, AngleSharp.Io has reimplemented a cookie provider following the official specification. This way, the cookie handling just works.

Cookies can be handled temporarily (`MemoryFileHandler`) or persistently (`LocalFileHandler`). Custom file handlers for actually storing or retrieving the cookie content are possible. The crucial interface for this is `ICookieFileHandler`.
