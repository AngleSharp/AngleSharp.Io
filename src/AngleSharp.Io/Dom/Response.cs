namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// The response of <c>fetch</c>, giving the status, the headers and the body
    /// (once) as text or JSON.
    /// </summary>
    [DomName("Response")]
    [DomExposed("Window")]
    [DomExposed("Worker")]
    public sealed class Response
    {
        private readonly IResponse _response;
        private readonly Boolean _isRedirected;
        private readonly AbortSignal _signal;
        private readonly String _statusText;
        private readonly String _type = "basic";
        private Lazy<Task<Byte[]>> _sharedBody;
        private ReadableStream _body;
        private Boolean _isBodyUsed;

        /// <summary>
        /// Creates a response that did not come from a server, as a script may do.
        /// </summary>
        /// <param name="body">The body, if any.</param>
        /// <param name="init">The status and headers, if any.</param>
        [DomConstructor]
        public Response(Object body = null, ResponseInit init = null)
            : this(CreateResponse(body, init), false, null)
        {
            _statusText = init?.StatusText ?? String.Empty;
            _type = "default";
        }

        /// <summary>
        /// Creates a response for the given one of a requester.
        /// </summary>
        /// <param name="response">The received response.</param>
        /// <param name="isRedirected">True if the request has been redirected.</param>
        public Response(IResponse response, Boolean isRedirected = false)
            : this(response, isRedirected, null)
        {
        }

        internal Response(IResponse response, Boolean isRedirected, AbortSignal signal)
        {
            _signal = signal;
            _response = response ?? throw new ArgumentNullException(nameof(response));
            _isRedirected = isRedirected;
            Headers = new Headers(response.Headers);
        }

        private Response(Response source)
        {
            _response = source._response;
            _isRedirected = source._isRedirected;
            _signal = source._signal;
            _statusText = source._statusText;
            _type = source._type;
            _sharedBody = source._sharedBody;
            Headers = new Headers(source.Headers);
        }

        /// <summary>
        /// Gets the status code, e.g. 200.
        /// </summary>
        [DomName("status")]
        public Int32 Status => (Int32)_response.StatusCode;

        /// <summary>
        /// Gets the text of the status, e.g. "Not Found".
        /// </summary>
        [DomName("statusText")]
        public String StatusText => _statusText ?? GetStatusText(_response.StatusCode);

        /// <summary>
        /// Gets if the status is a success (200 to 299).
        /// </summary>
        [DomName("ok")]
        public Boolean IsOk => Status >= 200 && Status <= 299;

        /// <summary>
        /// Gets the address the response came from.
        /// </summary>
        [DomName("url")]
        public String Url => _response.Address?.Href ?? String.Empty;

        /// <summary>
        /// Gets if the request has been redirected.
        /// </summary>
        [DomName("redirected")]
        public Boolean IsRedirected => _isRedirected;

        /// <summary>
        /// Gets the type of the response.
        /// </summary>
        [DomName("type")]
        public String Type => _type;

        /// <summary>
        /// Gets the headers of the response.
        /// </summary>
        [DomName("headers")]
        public Headers Headers { get; }

        /// <summary>
        /// Gets if the body has been read already.
        /// </summary>
        [DomName("bodyUsed")]
        public Boolean IsBodyUsed => _isBodyUsed;

        /// <summary>
        /// Gets the body as a stream, which delivers the data as it arrives.
        /// Reading it counts as using the body.
        /// </summary>
        [DomName("body")]
        public ReadableStream Body
        {
            get
            {
                if (_body == null)
                {
                    _body = CreateBodyStream();
                    _body.OnDisturbed(() => _isBodyUsed = true);
                }

                return _body;
            }
        }

        /// <summary>
        /// Reads the body as text, decoded with the charset of the content type
        /// (UTF-8 by default).
        /// </summary>
        /// <returns>The text of the body.</returns>
        [DomName("text")]
        public async Task<String> Text()
        {
            var bytes = await ReadBodyAsync().ConfigureAwait(false);

            using (var reader = new StreamReader(new MemoryStream(bytes), GetEncoding(), true))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Reads the body as bytes, which scripts receive as an ArrayBuffer.
        /// </summary>
        /// <returns>The bytes of the body.</returns>
        [DomName("arrayBuffer")]
        public Task<Byte[]> ArrayBuffer() => ReadBodyAsync();

        /// <summary>
        /// Reads the body as bytes, which scripts receive as a Uint8Array.
        /// </summary>
        /// <returns>The bytes of the body.</returns>
        [DomName("bytes")]
        public async Task<ArraySegment<Byte>> Bytes() =>
            new ArraySegment<Byte>(await ReadBodyAsync().ConfigureAwait(false));

        /// <summary>
        /// Reads the body into a blob typed with the content type.
        /// </summary>
        /// <returns>The blob holding the body.</returns>
        [DomName("blob")]
        public async Task<Blob> Blob()
        {
            var bytes = await ReadBodyAsync().ConfigureAwait(false);
            return new Blob(bytes, Headers.Get(HeaderNames.ContentType) ?? String.Empty);
        }

        private async Task<Byte[]> ReadBodyAsync()
        {
            _signal?.ThrowIfAborted();

            if (_isBodyUsed || _body != null && _body.Locked)
            {
                throw new InvalidOperationException("The body has already been read.");
            }

            _isBodyUsed = true;

            if (_sharedBody != null)
            {
                return await _sharedBody.Value.ConfigureAwait(false);
            }

            //  reading through the stream, when there is one already, keeps the two
            //  ways of consuming the body from racing over the same content
            if (_body != null)
            {
                using (var memory = new MemoryStream())
                using (var content = new ReadableStreamContentStream(_body))
                {
                    await content.CopyToAsync(memory).ConfigureAwait(false);
                    return memory.ToArray();
                }
            }

            return await ReadAllAsync(_response.Content).ConfigureAwait(false);
        }

        private ReadableStream CreateBodyStream()
        {
            if (_sharedBody != null)
            {
                var shared = _sharedBody;
                return new ReadableStream(
                    start: async controller =>
                    {
                        var bytes = await shared.Value.ConfigureAwait(false);

                        if (bytes.Length > 0)
                        {
                            controller.Enqueue(new ArraySegment<Byte>(bytes));
                        }

                        controller.Close();
                    },
                    pull: null,
                    cancel: null,
                    highWaterMark: 1.0,
                    size: null);
            }

            return ReadableStream.FromStream(_response.Content ?? Stream.Null);
        }

        /// <summary>
        /// Creates a copy of the response, whose body can be read independently. The body is
        /// kept in memory then, once one of them reads it.
        /// </summary>
        /// <returns>The copy.</returns>
        [DomName("clone")]
        public Response Clone()
        {
            if (_isBodyUsed || _body != null && _body.Locked)
            {
                throw new InvalidOperationException("The body has already been read.");
            }

            if (_sharedBody == null)
            {
                var content = _response.Content;
                _sharedBody = new Lazy<Task<Byte[]>>(() => ReadAllAsync(content));
            }

            return new Response(this);
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

        private static async Task<Byte[]> ReadAllAsync(Stream content)
        {
            if (content == null)
            {
                return Array.Empty<Byte>();
            }

            using (var memory = new MemoryStream())
            {
                await content.CopyToAsync(memory).ConfigureAwait(false);
                return memory.ToArray();
            }
        }

        private static IResponse CreateResponse(Object body, ResponseInit init)
        {
            var status = init?.Status ?? 200;

            if (status < 200 || status > 599)
            {
                throw new InvalidOperationException($"The status {status} is out of range.");
            }

            var response = new DefaultResponse { StatusCode = (HttpStatusCode)status };
            var headers = new Headers(init?.Headers);

            if (body != null)
            {
                var serialized = RequestBody.Serialize(body);
                var contentType = serialized.ContentType ?? (body is String ? "text/plain;charset=UTF-8" : null);
                response.Content = serialized.Content;

                if (contentType != null && !headers.Has(HeaderNames.ContentType))
                {
                    headers.Set(HeaderNames.ContentType, contentType);
                }
            }

            foreach (var header in headers.Pairs)
            {
                response.Headers[header.Key] = header.Value;
            }

            return response;
        }

        private Encoding GetEncoding()
        {
            var contentType = Headers.Get(HeaderNames.ContentType);

            if (contentType != null)
            {
                var charset = new MimeType(contentType).GetParameter("charset");

                if (!String.IsNullOrEmpty(charset) && EncodingSupport.IsSupported(charset))
                {
                    return EncodingSupport.Resolve(charset);
                }
            }

            return EncodingSupport.Utf8;
        }

        private static String GetStatusText(HttpStatusCode status)
        {
            var name = status.ToString();
            var text = new StringBuilder(name.Length + 4);

            // HttpStatusCode.NotFound becomes "Not Found"
            for (var i = 0; i < name.Length; i++)
            {
                if (i > 0 && Char.IsUpper(name[i]) && !Char.IsUpper(name[i - 1]))
                {
                    text.Append(' ');
                }

                text.Append(name[i]);
            }

            return text.ToString();
        }
    }

    /// <summary>
    /// Represents the options for creating a <see cref="Response"/>.
    /// </summary>
    [DomName("ResponseInit")]
    public sealed class ResponseInit
    {
        /// <summary>
        /// Gets or sets the status code, by default 200.
        /// </summary>
        [DomName("status")]
        public Int32? Status { get; set; }

        /// <summary>
        /// Gets or sets the text of the status.
        /// </summary>
        [DomName("statusText")]
        public String StatusText { get; set; }

        /// <summary>
        /// Gets or sets the headers.
        /// </summary>
        [DomName("headers")]
        public Headers Headers { get; set; }
    }
}
