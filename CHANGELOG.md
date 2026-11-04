# 1.3.1

- Fixed a cookie `Expires` date with a day the month does not have failing the page load @michaelthyregod

# 1.3.0

Released on Friday, November 6 2026.

- Updated `RtcIceServer.Urls` to `RtcIceServerUrls`, as `urls` takes a single URL or several
- Improved `XMLHttpRequest` to read the body in chunks: `progress` fires about every 50 ms with `loaded` and `total` from `Content-Length`, and `responseText` grows while `LOADING`
- Improved `json()` with a nesting limit of 512 for deeply nested documents (#55) @michaelthyregod
- Fixed the order of the final `XMLHttpRequest` events: `readystatechange` to `DONE`, then `load` (or `error`, `abort`, `timeout`), then `loadend`
- Fixed the `ErrorEvent` of a `WebSocket` not telling the problem, so that its `Message` and `Error` were of no use
- Fixed `fetch` and `XMLHttpRequest` reading `file:` URLs from documents that are not `file:` themselves (#53) @michaelthyregod
- Fixed `navigator.clipboard` being null for script without a platform, when pages test for its members; it denies access now
- Fixed a cookie `Path` with control characters being accepted, which could add lines to the cookie file (#54) @michaelthyregod
- Added `Request`, also taken by `fetch`, the constructors of `Headers` and `Response` and the members of `Headers` changing it
- Added `entries`, `keys`, `values` and `forEach` to `Headers` and `FormData`
- Added `clone` to `Response`
- Added the options of `navigator.locks.request`, which are the `mode`, `ifAvailable`, `steal` and `signal`
- Added the IndexedDB of the standard for scripts (requests with events, transactions, structured values, keys of all kinds, key ranges, indexes and cursors), as `indexedDB` was a synchronous API for strings that the usual ways of using it never got an event from; the API for .NET code remains, without the names for scripts
- Added `ProgressEvent` with its script constructor, fired by `XMLHttpRequest` for `loadstart`, `progress`, `load`, `error`, `abort`, `timeout` and `loadend`
- Added `upload` to `XMLHttpRequest` with its `loadstart`, `progress`, `load` and `loadend` for a request with a body
- Added `Blob` and `File` with their script constructors, `text`, `arrayBuffer`, `bytes`, `stream` and a `slice` taking negative indices
- Added `FileReader` with `readAsText`, `readAsDataURL`, `readAsArrayBuffer`, `readAsBinaryString`, `abort` and the progress events of the File API
- Added the stream classes: `ReadableStream` (with `getReader`, `cancel`, `tee`, `pipeTo` and `pipeThrough`), `WritableStream` with its writer, `TransformStream`, and the `CountQueuingStrategy` and `ByteLengthQueuingStrategy`
- Added `TextEncoderStream` and `TextDecoderStream`, and `CompressionStream` and `DecompressionStream` for gzip, deflate and deflate-raw (deflate needs .NET 8)
- Added `body`, `arrayBuffer`, `bytes` and `blob` to `Response`, delivering the data as it arrives, and `body` to `Request`; a `ReadableStream` can be the body of both
- Added `cookieEnabled`, `hardwareConcurrency`, `maxTouchPoints` and `webdriver` to `navigator`
- Added `AbortController` and `AbortSignal`, also taken by `fetch` as its `signal`, which stops a download and rejects reading the body afterwards
- Added `Notification` with `requestPermission` and an injected delivery platform (#41)
- Added `showOpenFilePicker` and `showSaveFilePicker` with writable file handles (#42)
- Added `sync` and `periodicSync` on service worker registrations with an injected scheduler (#44)
- Added `pushManager` on service worker registrations with an injected transport (#45)

# 1.2.2

Released on Sunday, November 1 2026.

- Fixed `fetch` giving the response of the requester; it gives a `Response` with `status`, `statusText`, `ok`, `url`, `redirected`, `headers`, `text()`, and `json()`
- Added `FormData` (from a form or set by script) to send with `fetch` and `XMLHttpRequest`

# 1.2.1

Released on Friday, October 30 2026.

- Fixed `getItem` and `setItem` missing on `localStorage` and `sessionStorage`

# 1.2.0

Released on Friday, October 23 2026.

- Added an HTTP cache for the requesters (`WithHttpCache`)
- Added `IoOptions.CreateTemporary` and `IoOptions.CreatePersistent`
- Added `StorageManager` on `navigator.storage` (#38)
- Added minimal service workers on `navigator.serviceWorker` (#39)
- Added `navigator.sendBeacon` (#43)
- Added reporting with network error capture and `ReportingObserver` (#46)
- Added `RTCPeerConnection` with data channels and a loopback platform (#40)
- Added `navigator.permissions` with a policy gating clipboard, geolocation, and `persist` (#47)
- Added `navigator.language` and `navigator.languages` (`NavigatorOptions.Languages`) (#49)

# 1.1.0

Released on Friday, July 31 2026.

- Updated to use a minimum of AngleSharp 1.5
- Fixed domain handling in cookies #36
- Added the `Navigator` from *AngleSharp.Js*
- Added the `XmlHttpRequester` from *AngleSharp.Js*
- Added `fetch` for requesting resources
- Added `localStorage` and `sessionStorage` for web storages
- Added `indexedDB` using an IndexedDB implementation
- Added `caches` using a Cache API implementation
- Added `BroadcastChannel`
- Added `locks` using the `LockManager` web locks spec
- Added `Clipboard` providing the web clipboard API
- Added `Geolocation` providing the web geolocation API

# 1.0.0

Released on Sunday, January 15 2023.

- Updated to use AngleSharp 1.0

# 0.17.0

Released on Tuesday, May 31 2022.

- Updated to use AngleSharp 0.17
- Dropped .NET Framework 4.6

# 0.16.0

Released on Sunday, June 6 2021.

- Updated to use AngleSharp 0.16

# 0.15.0

Released on Friday, April 23 2021.

- Added option to specify default request headers (#29)
- Fixed unhandled exceptions that may break the requester (#28)

# 0.14.0

Released on Tuesday, March 31 2020.

- Added support for .NET framework 4.6
- Added support for .NET framework 4.6.1
- Added support for .NET framework 4.7.2
- Added shallow support for source link

# 0.13.1

Released on Tuesday, September 24 2019.

- Ignore non-existing cookie file when reading
- Write cookies after every add operation

# 0.13.0

Released on Friday, September 6 2019.

- Added additional configuration extensions

# 0.12.1

Released on Wednesday, May 15 2019.

- Binary version fix

# 0.12.0

Released on Thursday, May 2 2019.

- Reference latest AngleSharp
- Added `InputFile` class as a standard `IFile` implementation
- Added `AppendFile` extensions for `IHtmlInputElement`
- Included `WithDownload` and `WithStandardDownload` configuration
- Added ability for binary data restore on assets (#19)
- Added `DownloadAsync` extension method to `IUrlUtilities` elements
- Added more extension methods to `IResponse` (e.g., `SaveToAsync`)
- Introduced improved cookie container (#15)

# 0.10.1

Released on Monday, January 7 2019.

- Reference latest AngleSharp
- Updated reference to `System.Encoding.CodePages`

# 0.10.0

Released on Friday, January 4 2019.

- Updated to fit the latest version of AngleSharp

# 0.3.2

Released on Saturday, September 3 2016.

- Updated to latest version
- Dispose WebSocket on unload
- Added `about:` handler

# 0.3.1

Released on Sunday, July 17 2016.

- Adjusted to service changes

# 0.3.0

Released on Friday, May 6 2016.

- Added a File requester (#11)
- Added a FTP requester (#10)

# 0.2.0

Released on Tuesday, January 5 2016.

- Fixed compatibility bug (#7)
- Added WebSocket interface (#5)
- `IRequester` flexibility (#1)

# 0.1.0

Released on Sunday, October 18 2015.

- Initial release (#4, #3)
