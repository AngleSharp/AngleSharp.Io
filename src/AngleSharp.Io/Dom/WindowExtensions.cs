namespace AngleSharp.Io.Dom
{
    using AngleSharp;
    using AngleSharp.Html;
    using AngleSharp.Io;
    using AngleSharp.Dom;
    using AngleSharp.Attributes;
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
        /// Performs a request using the configured document loader.
        /// </summary>
        /// <param name="window">The originating window.</param>
        /// <param name="url">The request URL.</param>
        /// <returns>The fetched response.</returns>
        [DomName("fetch")]
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
        [DomName("fetch")]
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

        private static DocumentRequest CreateRequest(IWindow window, Url url, FetchOptions options)
        {
            var requestOptions = options ?? new FetchOptions();
            var serializedBody = SerializeBody(requestOptions.Body);
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

        private static SerializedBody SerializeBody(Object body)
        {
            if (body == null)
            {
                return SerializedBody.Empty;
            }

            switch (body)
            {
                case Stream stream:
                    if (stream.CanSeek)
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                    }

                    return new SerializedBody(stream);
                case Byte[] bytes:
                    return new SerializedBody(new MemoryStream(bytes));
                case ArraySegment<Byte> segment when segment.Array != null:
                    return new SerializedBody(new MemoryStream(segment.Array, segment.Offset, segment.Count, false));
                case FormDataSet formDataSet:
                    var formDataBody = formDataSet.AsMultipart(null, Encoding.UTF8);
                    var formDataType = String.Concat("multipart/form-data; boundary=", formDataSet.Boundary);
                    return new SerializedBody(formDataBody, formDataType);
                case UrlSearchParams searchParams:
                    var query = searchParams.ToString();
                    var queryBytes = Encoding.UTF8.GetBytes(query);
                    return new SerializedBody(new MemoryStream(queryBytes), "application/x-www-form-urlencoded; charset=UTF-8");
                case IBlob blob:
                    var blobBody = blob.Body;

                    if (blobBody != null)
                    {
                        if (blobBody.CanSeek)
                        {
                            blobBody.Seek(0, SeekOrigin.Begin);
                        }

                        return new SerializedBody(blobBody, blob.Type);
                    }

                    break;
            }

            var content = body.ToString();
            var contentBytes = Encoding.UTF8.GetBytes(content);
            return new SerializedBody(new MemoryStream(contentBytes));
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

        private readonly struct SerializedBody
        {
            public static readonly SerializedBody Empty = new SerializedBody(Stream.Null);

            public SerializedBody(Stream content, String contentType = null)
            {
                Content = content ?? Stream.Null;
                ContentType = contentType;
            }

            public Stream Content { get; }

            public String ContentType { get; }
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
