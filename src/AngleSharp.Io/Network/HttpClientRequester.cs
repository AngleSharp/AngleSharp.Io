namespace AngleSharp.Io.Network
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using HttpMethod = System.Net.Http.HttpMethod;

    /// <summary>
    /// An HTTP requester based on <see cref="HttpClient"/>.
    /// </summary>
    public class HttpClientRequester : BaseRequester
    {
        #region Fields

        private readonly HttpClient _client;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new HTTP client request with a new HttpClient instance.
        /// </summary>
        public HttpClientRequester()
            : this(new HttpClient())
        {
        }

        /// <summary>
        /// Creates a new HTTP client request.
        /// </summary>
        /// <param name="client">The HTTP client to use for requests.</param>
        public HttpClientRequester(HttpClient client)
        {
            _client = client;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Checks if the given protocol is supported.
        /// </summary>
        /// <param name="protocol">
        /// The protocol to check for, e.g., http.
        /// </param>
        /// <returns>
        /// True if the protocol is supported, otherwise false.
        /// </returns>
        public override Boolean SupportsProtocol(String protocol) =>
            protocol.Equals(ProtocolNames.Http, StringComparison.OrdinalIgnoreCase) ||
            protocol.Equals(ProtocolNames.Https, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Performs an asynchronous request that can be cancelled.
        /// </summary>
        /// <param name="request">The options to consider.</param>
        /// <param name="cancel">The token for cancelling the task.</param>
        /// <returns>
        /// The task that will eventually give the response data.
        /// </returns>
        protected override async Task<IResponse> PerformRequestAsync(Request request, CancellationToken cancel)
        {
            // create the request message
            var method = new HttpMethod(request.Method.Stringify());
            var requestMessage = new HttpRequestMessage(method, request.Address);
            var contentHeaders = new List<KeyValuePair<String, String>>();

            foreach (var header in request.Headers)
            {
                // Source:
                // https://github.com/aspnet/Mvc/blob/02c36a1c4824936682b26b6c133d11bebee822a2/src/Microsoft.AspNet.Mvc.WebApiCompatShim/HttpRequestMessage/HttpRequestMessageFeature.cs
                if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    contentHeaders.Add(new KeyValuePair<String, String>(header.Key, header.Value));
                }
            }

            // set up the content
            if (request.Content != null && method != HttpMethod.Get && method != HttpMethod.Head)
            {
                requestMessage.Content = new StreamContent(request.Content);

                foreach (var header in contentHeaders)
                {
                    requestMessage.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // execute the request
            var responseMessage = await _client.SendAsync(requestMessage, cancel).ConfigureAwait(false);

            // convert the response
            var response = new DefaultResponse
            {
                Headers = responseMessage.Headers.ToDictionary(p => p.Key, p => String.Join(", ", p.Value)),
                Address = Url.Convert(responseMessage.RequestMessage.RequestUri),
                StatusCode = responseMessage.StatusCode
            };

            // get the anticipated content
            var content = responseMessage.Content;

            if (content != null)
            {
                response.Content = await content.ReadAsStreamAsync().ConfigureAwait(false);

                foreach (var pair in content.Headers)
                {
                    response.Headers[pair.Key] = String.Join(", ", pair.Value);
                }
            }

            if (IsRedirected(response) && !response.Headers.ContainsKey(HeaderNames.SetCookie))
            {
                response.Headers[HeaderNames.SetCookie] = String.Empty;
            }

            return response;
        }

        private static Boolean IsRedirected(IResponse response)
        {
            var status = response.StatusCode;

            return status == HttpStatusCode.Redirect || status == HttpStatusCode.RedirectKeepVerb ||
                   status == HttpStatusCode.RedirectMethod || status == HttpStatusCode.TemporaryRedirect ||
                   status == HttpStatusCode.MovedPermanently || status == HttpStatusCode.MultipleChoices;
        }

        #endregion
    }
}