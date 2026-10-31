namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Text;
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
    public sealed class Response
    {
        private readonly IResponse _response;
        private readonly Boolean _isRedirected;
        private Boolean _isBodyUsed;

        /// <summary>
        /// Creates a response for the given one of a requester.
        /// </summary>
        /// <param name="response">The received response.</param>
        /// <param name="isRedirected">True if the request has been redirected.</param>
        public Response(IResponse response, Boolean isRedirected = false)
        {
            _response = response ?? throw new ArgumentNullException(nameof(response));
            _isRedirected = isRedirected;
            Headers = new Headers(response.Headers);
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
        public String StatusText => GetStatusText(_response.StatusCode);

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
        public String Type => "basic";

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
        /// Reads the body as text, decoded with the charset of the content type
        /// (UTF-8 by default).
        /// </summary>
        /// <returns>The text of the body.</returns>
        [DomName("text")]
        public async Task<String> Text()
        {
            if (_isBodyUsed)
            {
                throw new InvalidOperationException("The body has already been read.");
            }

            _isBodyUsed = true;
            var content = _response.Content;

            if (content == null)
            {
                return String.Empty;
            }

            using (var reader = new StreamReader(content, GetEncoding(), true))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
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

        private Encoding GetEncoding()
        {
            var contentType = Headers.Get(HeaderNames.ContentType);

            if (contentType != null)
            {
                var charset = new MimeType(contentType).GetParameter("charset");

                if (!String.IsNullOrEmpty(charset) && TextEncoding.IsSupported(charset))
                {
                    return TextEncoding.Resolve(charset);
                }
            }

            return TextEncoding.Utf8;
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
}
