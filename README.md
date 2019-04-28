![logo](https://raw.githubusercontent.com/AngleSharp/AngleSharp.Io/master/header.png)

# AngleSharp.Io

[![Build Status](https://img.shields.io/appveyor/ci/FlorianRappl/AngleSharp-Io.svg?style=flat-square)](https://ci.appveyor.com/project/FlorianRappl/AngleSharp-Io)
[![GitHub Tag](https://img.shields.io/github/tag/AngleSharp/AngleSharp.Io.svg?style=flat-square)](https://github.com/AngleSharp/AngleSharp.Io/releases)
[![NuGet Count](https://img.shields.io/nuget/dt/AngleSharp.Io.svg?style=flat-square)](https://www.nuget.org/packages/AngleSharp.Io/)
[![Issues Open](https://img.shields.io/github/issues/AngleSharp/AngleSharp.Io.svg?style=flat-square)](https://github.com/AngleSharp/AngleSharp.Io/issues)
[![Gitter Chat](http://img.shields.io/badge/gitter-AngleSharp/AngleSharp-blue.svg?style=flat-square)](https://gitter.im/AngleSharp/AngleSharp)
[![StackOverflow Questions](https://img.shields.io/stackexchange/stackoverflow/t/anglesharp.svg?style=flat-square)](https://stackoverflow.com/tags/anglesharp)
[![CLA Assistant](https://cla-assistant.io/readme/badge/AngleSharp/AngleSharp.Io?style=flat-square)](https://cla-assistant.io/AngleSharp/AngleSharp.Io)

AngleSharp.Io extends AngleSharp with powerful requesters, caching mechanisms, and storage systems. It is coupled more strongly to the underlying operating system than AngleSharp itself. Therefore it has stronger dependencies and demands and cannot be released for the standard framework (4.6). Nevertheless, it is released as a .NET Standard 2.0 library.

## Basic Configuration

If you just want to use *all* available requesters provided by AngleSharp.Io you can do the following:

```cs
var config = Configuration.Default
    .WithRequesters() // from AngleSharp.Io
    .WithDefaultLoader(); // from AngleSharp
```

This will register all requesters. Alternatively, the requesters can be provided explicitly. They are located in the `AngleSharp.Io.Network` namespace and have names such as `DataRequester`.

## Features

- New requesters
  - HTTP (using `HttpClient`)
  - FTP
  - Supporting data URLs
  - Supporting file URLs
  - Enhanced support for about: URLs
- WebSockets (mostly interesting for scripting engines, e.g., JS)
- Storage support by providing the `IStorage` interface

## Participating

Participation in the project is highly welcome. For this project the same rules as for the AngleSharp core project may be applied.

If you have any question, concern, or spot an issue then please report it before opening a pull request. An initial discussion is appreciated regardless of the nature of the problem.

Live discussions can take place in our [Gitter chat](https://gitter.im/AngleSharp/AngleSharp), which supports using GitHub accounts.

This project has adopted the code of conduct defined by the Contributor Covenant to clarify expected behavior in our community.

For more information see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

## .NET Foundation

This project is supported by the [.NET Foundation](https://dotnetfoundation.org).

## License

The MIT License (MIT)

Copyright (c) 2015 - 2019 AngleSharp

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
