namespace AngleSharp.Io.Dom
{
    using AngleSharp;
    using AngleSharp.Html;
    using AngleSharp.Io;
    using AngleSharp.Dom;
    using AngleSharp.Attributes;
    using AngleSharp.Io.FileSystemApi;
    using AngleSharp.Io.Reporting;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a set of extensions for the window object.
    /// </summary>
    [DomExposed("Window")]
    public static class WindowExtensions
    {
        /// <summary>
        /// Gets the localStorage object.
        /// </summary>
        [DomName("localStorage")]
        [DomAccessor(Accessors.Getter)]
        public static ILocalStorage LocalStorage(this IWindow window)
        {
            var factory = window?.Document?.Context?.GetService<IStorageProviderFactory>();
            return factory?.GetStorages(window)?.Local;
        }
        
        /// <summary>
        /// Gets the sessionStorage object.
        /// </summary>
        [DomName("sessionStorage")]
        [DomAccessor(Accessors.Getter)]
        public static ISessionStorage SessionStorage(this IWindow window)
        {
            var factory = window?.Document?.Context?.GetService<IStorageProviderFactory>();
            return factory?.GetStorages(window)?.Session;
        }

        /// <summary>
        /// Gets the indexedDB object.
        /// </summary>
        public static IIndexedDbFactory IndexedDb(this IWindow window)
        {
            var factory = window?.Document?.Context?.GetService<IIndexedDbProviderFactory>();
            return factory?.GetIndexedDb(window);
        }

        /// <summary>
        /// Gets the IndexedDB of script, which works with requests and events as in the standard.
        /// Its databases are not the ones of <see cref="IndexedDb"/>.
        /// </summary>
        [DomName("indexedDB")]
        [DomAccessor(Accessors.Getter)]
        public static IdbFactory IndexedDbOfScript(this IWindow window) => IdbFactory.For(window);

        /// <summary>
        /// Gets the caches object.
        /// </summary>
        [DomName("caches")]
        [DomAccessor(Accessors.Getter)]
        public static ICacheStorage Caches(this IWindow window)
        {
            var factory = window?.Document?.Context?.GetService<ICacheProviderFactory>();
            return factory?.GetCaches(window);
        }

        /// <summary>
        /// Lets the user pick existing files via the registered platform.
        /// </summary>
        /// <param name="window">The window showing the picker.</param>
        /// <param name="options">The optional options of the picker.</param>
        /// <returns>The handles of the picked files.</returns>
        [DomName("showOpenFilePicker")]
        public static Task<FileSystemFileHandle[]> ShowOpenFilePicker(this IWindow window, OpenFilePickerOptions options = null) =>
            FilePickers.ShowOpenFilePickerAsync(window, options);

        /// <summary>
        /// Lets the user pick the file to save to via the registered platform.
        /// </summary>
        /// <param name="window">The window showing the picker.</param>
        /// <param name="options">The optional options of the picker.</param>
        /// <returns>The handle of the picked file.</returns>
        [DomName("showSaveFilePicker")]
        public static Task<FileSystemFileHandle> ShowSaveFilePicker(this IWindow window, SaveFilePickerOptions options = null) =>
            FilePickers.ShowSaveFilePickerAsync(window, options);

        /// <summary>
        /// Queues a report about the window's document, e.g., a deprecation.
        /// The report is delivered to the window's reporting observers and,
        /// if a reporting service is registered, queued for its sinks.
        /// </summary>
        /// <param name="window">The window the report is about.</param>
        /// <param name="type">The type of the report.</param>
        /// <param name="body">The type specific details, if any.</param>
        /// <param name="destination">The name of the endpoint receiving the report.</param>
        /// <returns>The queued report.</returns>
        public static Report QueueReport(this IWindow window, String type, ReportBody body, String destination = null)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            // like browsers, the report omits credentials and the fragment
            var url = new Url(window.Document?.Url ?? String.Empty)
            {
                Fragment = null,
                UserName = null,
                Password = null,
            };

            var report = new Report(type, url.Href, body, destination)
            {
                UserAgent = window.Navigator?.UserAgent ?? String.Empty,
            };

            WindowReports.For(window).Add(report);
            window.Document?.Context?.GetService<ReportingService>()?.QueueReport(report);
            return report;
        }

        /// <summary>
        /// Performs a request using the configured document loader.
        /// </summary>
        /// <param name="window">The originating window.</param>
        /// <param name="url">The request URL.</param>
        /// <returns>The fetched response.</returns>
        public static Task<IResponse> Fetch(this IWindow window, String url)
        {
            return Fetch(window, url, null);
        }

        private static async Task<IResponse> GetResponseAsync(IDownload download, Url documentUrl)
        {
            var response = await download.Task.ConfigureAwait(false);

            if (response != null && !FileUrlPolicy.IsAllowed(documentUrl, response.Address))
            {
                response.Dispose();
                throw new InvalidOperationException("Failed to fetch.");
            }

            return response;
        }

        /// <summary>
        /// Performs a request using the configured document loader.
        /// </summary>
        /// <param name="window">The originating window.</param>
        /// <param name="url">The request URL.</param>
        /// <param name="options">The optional request options.</param>
        /// <returns>The fetched response.</returns>
        public static async Task<IResponse> Fetch(this IWindow window, String url, FetchOptions options)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (String.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException(nameof(url));
            }

            var context = window.Document?.Context;
            var loader = context?.GetService<IDocumentLoader>();

            if (loader == null)
            {
                throw new InvalidOperationException("No document loader service is registered.");
            }

            var documentUrl = new Url(window.Document.Url);
            var absoluteUrl = new Url(documentUrl, url);
            var signal = options?.Signal;

            if (!FileUrlPolicy.IsAllowed(documentUrl, absoluteUrl))
            {
                throw new InvalidOperationException("Failed to fetch.");
            }

            signal?.ThrowIfAborted();

            var request = CreateRequest(window, absoluteUrl, options);
            var download = loader.FetchAsync(request);
            var downloadTask = GetResponseAsync(download, documentUrl);
            
            if (signal is null)
            {
                return await downloadTask.ConfigureAwait(false);
            }

            var aborted = new TaskCompletionSource<IResponse>();

            Action abort = () =>
            {
                aborted.TrySetException(signal.CreateException());
                download.Cancel();
            };

            signal.OnAbort(abort);

            try
            {
                var first = await Task.WhenAny(downloadTask, aborted.Task).ConfigureAwait(false);
                return await first.ConfigureAwait(false);
            }
            catch (Exception) when (signal.IsAborted)
            {
                // cancelling the download is a failure of its own, but the abort is what happened
                throw signal.CreateException();
            }
            finally
            {
                signal.RemoveOnAbort(abort);
            }
        }

        /// <summary>
        /// Performs a request like the fetch API (<c>window.fetch</c>), i.e., giving a response
        /// whose body can be read as text or JSON.
        /// </summary>
        /// <param name="window">The originating window.</param>
        /// <param name="url">The request URL.</param>
        /// <param name="options">The optional request options.</param>
        /// <returns>The fetched response.</returns>
        public static Task<Response> FetchAsync(this IWindow window, String url, FetchOptions options = null) =>
            FetchResponseAsync(window, url, options);

        /// <summary>
        /// Performs a request like the fetch API (<c>window.fetch</c>), i.e., giving a response
        /// whose body can be read as text or JSON.
        /// </summary>
        /// <param name="window">The originating window.</param>
        /// <param name="input">The request or its address, e.g. a string or an URL.</param>
        /// <param name="options">The optional request options.</param>
        /// <returns>The fetched response.</returns>
        [DomName("fetch")]
        public static Task<Response> FetchAsync(this IWindow window, Object input, FetchOptions options = null)
        {
            if (input is FetchRequest)
            {
                var request = new FetchRequest(window, input, options);
                return FetchResponseAsync(window, request.Url, request.ToOptions());
            }

            return FetchResponseAsync(window, input?.ToString(), options);
        }

        private static async Task<Response> FetchResponseAsync(IWindow window, String url, FetchOptions options)
        {
            var response = await Fetch(window, url, options).ConfigureAwait(false);
            var target = new Url(new Url(window.Document.Url), url);
            var isRedirected = response.Address != null && !response.Address.Equals(target);
            return new Response(response, isRedirected, options?.Signal);
        }

        private static DocumentRequest CreateRequest(IWindow window, Url url, FetchOptions options)
        {
            var requestOptions = options ?? new FetchOptions();
            var serializedBody = RequestBody.Serialize(requestOptions.Body);
            var request = new DocumentRequest(url)
            {
                Body = serializedBody.Content,
                Method = ParseMethod(requestOptions.Method),
                MimeType = requestOptions.MimeType,
                Referer = window.Document?.DocumentUri,
            };

            // not the documents a navigation asks for: servers answering by Accept would give
            // their web page instead of the data (fetch takes any type unless the script says so)
            request.Headers[HeaderNames.Accept] = "*/*";

            if (requestOptions.Headers != null)
            {
                foreach (var pair in requestOptions.Headers.Pairs)
                {
                    request.Headers[pair.Key] = pair.Value;
                }
            }

            if (!String.IsNullOrEmpty(serializedBody.ContentType) && !HasHeaderValue(request.Headers, HeaderNames.ContentType))
            {
                request.Headers[HeaderNames.ContentType] = serializedBody.ContentType;
            }

            return request;
        }

        private static HttpMethod ParseMethod(String method)
        {
            if (String.IsNullOrEmpty(method))
            {
                return HttpMethod.Get;
            }

            return Enum.TryParse(method, true, out HttpMethod parsed) ? parsed : HttpMethod.Get;
        }

        private static Boolean ContainsHeader(Dictionary<String, String> headers, String name)
        {
            foreach (var pair in headers)
            {
                if (String.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static Boolean HasHeaderValue(Dictionary<String, String> headers, String name)
        {
            foreach (var pair in headers)
            {
                if (String.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return !String.IsNullOrEmpty(pair.Value);
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Represents request options for <c>fetch</c>.
    /// </summary>
    [DomName("RequestInit")]
    public sealed class FetchOptions
    {
        /// <summary>
        /// Gets or sets the HTTP method.
        /// </summary>
        [DomName("method")]
        public String Method { get; set; }

        /// <summary>
        /// Gets or sets custom request headers.
        /// </summary>
        [DomName("headers")]
        public Headers Headers { get; set; }

        /// <summary>
        /// Gets or sets the request body.
        /// </summary>
        [DomName("body")]
        public Object Body { get; set; }

        /// <summary>
        /// Gets or sets an explicit mime type for the request.
        /// </summary>
        [DomName("mimeType")]
        public String MimeType { get; set; }

        /// <summary>
        /// Gets or sets the signal that aborts the request.
        /// </summary>
        [DomName("signal")]
        public AbortSignal Signal { get; set; }

        /// <summary>
        /// Gets or sets the mode, which is only kept by a <see cref="FetchRequest"/>.
        /// </summary>
        [DomName("mode")]
        public String Mode { get; set; }

        /// <summary>
        /// Gets or sets when credentials are sent, which is only kept by a <see cref="FetchRequest"/>.
        /// </summary>
        [DomName("credentials")]
        public String Credentials { get; set; }

        /// <summary>
        /// Gets or sets the cache mode, which is only kept by a <see cref="FetchRequest"/>.
        /// </summary>
        [DomName("cache")]
        public String Cache { get; set; }

        /// <summary>
        /// Gets or sets how redirects are handled, which is only kept by a <see cref="FetchRequest"/>.
        /// </summary>
        [DomName("redirect")]
        public String Redirect { get; set; }

        /// <summary>
        /// Gets or sets the referrer, which is only kept by a <see cref="FetchRequest"/>.
        /// </summary>
        [DomName("referrer")]
        public String Referrer { get; set; }

        /// <summary>
        /// Gets or sets the referrer policy, which is only kept by a <see cref="FetchRequest"/>.
        /// </summary>
        [DomName("referrerPolicy")]
        public String ReferrerPolicy { get; set; }

        /// <summary>
        /// Gets or sets the expected hash, which is only kept by a <see cref="FetchRequest"/>.
        /// </summary>
        [DomName("integrity")]
        public String Integrity { get; set; }

        /// <summary>
        /// Gets or sets if the request may outlive the page, which is only kept by a <see cref="FetchRequest"/>.
        /// </summary>
        [DomName("keepalive")]
        public Boolean? KeepAlive { get; set; }
    }
}
