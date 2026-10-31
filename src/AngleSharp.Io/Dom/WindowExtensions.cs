namespace AngleSharp.Io.Dom
{
    using AngleSharp;
    using AngleSharp.Html;
    using AngleSharp.Io;
    using AngleSharp.Dom;
    using AngleSharp.Attributes;
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
        [DomName("indexedDB")]
        [DomAccessor(Accessors.Getter)]
        public static IIndexedDbFactory IndexedDb(this IWindow window)
        {
            var factory = window?.Document?.Context?.GetService<IIndexedDbProviderFactory>();
            return factory?.GetIndexedDb(window);
        }

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

            var absoluteUrl = new Url(new Url(window.Document.Url), url);
            var request = CreateRequest(window, absoluteUrl, options);
            var download = loader.FetchAsync(request);

            return await download.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a request like the fetch API (<c>window.fetch</c>), i.e., giving a response
        /// whose body can be read as text or JSON.
        /// </summary>
        /// <param name="window">The originating window.</param>
        /// <param name="url">The request URL.</param>
        /// <param name="options">The optional request options.</param>
        /// <returns>The fetched response.</returns>
        [DomName("fetch")]
        public static async Task<Response> FetchAsync(this IWindow window, String url, FetchOptions options = null)
        {
            var response = await Fetch(window, url, options).ConfigureAwait(false);
            var target = new Url(new Url(window.Document.Url), url);
            var isRedirected = response.Address != null && !response.Address.Equals(target);
            return new Response(response, isRedirected);
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

            if (requestOptions.Headers != null)
            {
                foreach (var pair in requestOptions.Headers)
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
        public Dictionary<String, String> Headers { get; set; }

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
    }
}
