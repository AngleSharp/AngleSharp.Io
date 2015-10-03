using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Network;
using HttpMethod = System.Net.Http.HttpMethod;

namespace Knapcode.AngleSharp.NetHttp
{
    public class HttpClientRequester : IRequester
    {
        private readonly HttpClient _client;

        public HttpClientRequester(HttpClient client)
        {
            _client = client;
        }

        public bool SupportsProtocol(string protocol)
        {
            return protocol.Equals("http", StringComparison.OrdinalIgnoreCase) || protocol.Equals("https", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<IResponse> RequestAsync(IRequest request, CancellationToken cancellationToken)
        {
            // create the request message
            var method = new HttpMethod(request.Method.ToString().ToUpper());
            var requestMessage = new HttpRequestMessage(method, request.Address);
            var contentHeaders = new List<KeyValuePair<string, string>>();
            foreach (var header in request.Headers)
            {
                // Source:
                // https://github.com/aspnet/Mvc/blob/02c36a1c4824936682b26b6c133d11bebee822a2/src/Microsoft.AspNet.Mvc.WebApiCompatShim/HttpRequestMessage/HttpRequestMessageFeature.cs
                if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    contentHeaders.Add(new KeyValuePair<string, string>(header.Key, header.Value));
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
            var responseMessage = await _client.SendAsync(requestMessage, cancellationToken);

            // convert the response
            var response = new Response
            {
                Headers = responseMessage.Headers.ToDictionary(p => p.Key, p => string.Join(", ", p.Value)),
                Address = Url.Convert(responseMessage.RequestMessage.RequestUri),
                StatusCode = responseMessage.StatusCode
            };

            if (responseMessage.Content != null)
            {
                response.Content = await responseMessage.Content.ReadAsStreamAsync();
                foreach (var pair in responseMessage.Content.Headers)
                {
                    response.Headers[pair.Key] = string.Join(", ", pair.Value);
                }
            }

            return response;
        }
    }
}
