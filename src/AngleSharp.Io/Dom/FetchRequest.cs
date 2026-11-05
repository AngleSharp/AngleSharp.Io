namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the Request of the fetch standard, which can be given to <c>fetch</c>.
    /// The members describing how to fetch (e.g. its mode) are kept, but not acted upon.
    /// </summary>
    [DomName("Request")]
    [DomExposed("Window")]
    [DomExposed("Worker")]
    public sealed class FetchRequest
    {
        private readonly Byte[] _body;
        private readonly String _mimeType;
        private Boolean _isBodyUsed;

        /// <summary>
        /// Creates a new request.
        /// </summary>
        /// <param name="window">The window a relative address is resolved against.</param>
        /// <param name="input">The address (e.g. a string or an URL) or the request to copy.</param>
        /// <param name="options">The options, which replace what is taken from a copied request.</param>
        [DomConstructor]
        public FetchRequest(IWindow window, Object input, FetchOptions options = null)
        {
            var source = input as FetchRequest;
            Url = source?.Url ?? Resolve(window, input);
            Method = Normalize(options?.Method ?? source?.Method);
            Headers = new Headers(options?.Headers ?? source?.Headers);
            Signal = options?.Signal ?? source?.Signal ?? new AbortSignal();
            Mode = options?.Mode ?? source?.Mode ?? "cors";
            Credentials = options?.Credentials ?? source?.Credentials ?? "same-origin";
            Cache = options?.Cache ?? source?.Cache ?? "default";
            Redirect = options?.Redirect ?? source?.Redirect ?? "follow";
            Referrer = options?.Referrer ?? source?.Referrer ?? "about:client";
            ReferrerPolicy = options?.ReferrerPolicy ?? source?.ReferrerPolicy ?? String.Empty;
            Integrity = options?.Integrity ?? source?.Integrity ?? String.Empty;
            IsKeepAlive = options?.KeepAlive ?? source?.IsKeepAlive ?? false;
            _mimeType = options?.MimeType ?? source?._mimeType;

            if (options?.Body != null)
            {
                var serialized = RequestBody.Serialize(options.Body);
                _body = Read(serialized.Content);
                var contentType = serialized.ContentType ?? (options.Body is String ? "text/plain;charset=UTF-8" : null);

                if (contentType != null && !Headers.Has(HeaderNames.ContentType))
                {
                    Headers.Set(HeaderNames.ContentType, contentType);
                }
            }
            else if (source?._body != null)
            {
                _body = source.TakeBody();
            }

            if (_body != null && (Method == "GET" || Method == "HEAD"))
            {
                throw new InvalidOperationException("A request with the method GET or HEAD can't have a body.");
            }
        }

        private FetchRequest(FetchRequest source)
        {
            Url = source.Url;
            Method = source.Method;
            Headers = new Headers(source.Headers);
            Signal = source.Signal;
            Mode = source.Mode;
            Credentials = source.Credentials;
            Cache = source.Cache;
            Redirect = source.Redirect;
            Referrer = source.Referrer;
            ReferrerPolicy = source.ReferrerPolicy;
            Integrity = source.Integrity;
            IsKeepAlive = source.IsKeepAlive;
            _mimeType = source._mimeType;
            _body = source._body;
        }

        /// <summary>
        /// Gets the absolute address.
        /// </summary>
        [DomName("url")]
        public String Url { get; }

        /// <summary>
        /// Gets the method, e.g. GET.
        /// </summary>
        [DomName("method")]
        public String Method { get; }

        /// <summary>
        /// Gets the headers.
        /// </summary>
        [DomName("headers")]
        public Headers Headers { get; }

        /// <summary>
        /// Gets the signal aborting the request.
        /// </summary>
        [DomName("signal")]
        public AbortSignal Signal { get; }

        /// <summary>
        /// Gets the mode, e.g. cors.
        /// </summary>
        [DomName("mode")]
        public String Mode { get; }

        /// <summary>
        /// Gets when credentials are sent, e.g. same-origin.
        /// </summary>
        [DomName("credentials")]
        public String Credentials { get; }

        /// <summary>
        /// Gets the cache mode, e.g. default.
        /// </summary>
        [DomName("cache")]
        public String Cache { get; }

        /// <summary>
        /// Gets how redirects are handled, e.g. follow.
        /// </summary>
        [DomName("redirect")]
        public String Redirect { get; }

        /// <summary>
        /// Gets the referrer.
        /// </summary>
        [DomName("referrer")]
        public String Referrer { get; }

        /// <summary>
        /// Gets the referrer policy.
        /// </summary>
        [DomName("referrerPolicy")]
        public String ReferrerPolicy { get; }

        /// <summary>
        /// Gets the expected hash of the response.
        /// </summary>
        [DomName("integrity")]
        public String Integrity { get; }

        /// <summary>
        /// Gets if the request may outlive the page.
        /// </summary>
        [DomName("keepalive")]
        public Boolean IsKeepAlive { get; }

        /// <summary>
        /// Gets if the body has been read already.
        /// </summary>
        [DomName("bodyUsed")]
        public Boolean IsBodyUsed => _isBodyUsed;

        /// <summary>
        /// Reads the body as text.
        /// </summary>
        /// <returns>The text of the body.</returns>
        [DomName("text")]
        public Task<String> Text()
        {
            var body = TakeBody();
            return Task.FromResult(body != null ? Encoding.UTF8.GetString(body) : String.Empty);
        }

        /// <summary>
        /// Reads the body as JSON: objects are dictionaries, arrays are object arrays,
        /// numbers are doubles.
        /// </summary>
        /// <returns>The parsed value of the body.</returns>
        [DomName("json")]
        public async Task<Object> Json()
        {
            var text = await Text().ConfigureAwait(false);
            return JsonReader.Parse(text);
        }

        /// <summary>
        /// Creates a copy of the request, whose body can be read independently.
        /// </summary>
        /// <returns>The copy.</returns>
        [DomName("clone")]
        public FetchRequest Clone()
        {
            if (_isBodyUsed)
            {
                throw new InvalidOperationException("The body has already been read.");
            }

            return new FetchRequest(this);
        }

        internal FetchOptions ToOptions() => new FetchOptions
        {
            Method = Method,
            Headers = Headers,
            Body = TakeBody(),
            MimeType = _mimeType,
            Signal = Signal,
        };

        private Byte[] TakeBody()
        {
            if (_body != null && _isBodyUsed)
            {
                throw new InvalidOperationException("The body has already been read.");
            }

            _isBodyUsed = _body != null;
            return _body;
        }

        private static String Resolve(IWindow window, Object input)
        {
            var address = input?.ToString();
            var documentUrl = window?.Document?.Url;
            var url = documentUrl != null ? new Url(new Url(documentUrl), address ?? String.Empty) : new Url(address ?? String.Empty);

            if (url.IsInvalid)
            {
                throw new InvalidOperationException($"The address '{address}' is invalid.");
            }

            return url.Href;
        }

        private static String Normalize(String method)
        {
            if (String.IsNullOrEmpty(method))
            {
                return "GET";
            }

            var upper = method.ToUpperInvariant();

            switch (upper)
            {
                case "DELETE":
                case "GET":
                case "HEAD":
                case "OPTIONS":
                case "POST":
                case "PUT":
                    return upper;
                case "CONNECT":
                case "TRACE":
                case "TRACK":
                    throw new InvalidOperationException($"The method '{method}' is not allowed.");
                default:
                    return method;
            }
        }

        private static Byte[] Read(Stream content)
        {
            using (var memory = new MemoryStream())
            {
                content.CopyTo(memory);
                return memory.ToArray();
            }
        }
    }
}
