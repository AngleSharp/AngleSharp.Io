namespace AngleSharp.Io.Network
{
    using AngleSharp.Network;
    using AngleSharp.Network.Default;
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Requester to perform file:// requests.
    /// </summary>
    public class FileRequester : IRequester
    {
        /// <summary>
        /// Performs an asynchronous request that can be cancelled.
        /// </summary>
        /// <param name="request">The options to consider.</param>
        /// <param name="cancel">The token for cancelling the task.</param>
        /// <returns>The task that will eventually give the response data.</returns>
        public async Task<IResponse> RequestAsync(IRequest request, CancellationToken cancel)
        {
            var requester = FileWebRequest.Create(request.Address.Href) as FileWebRequest;

            if (requester != null)
            {
                var response = await requester.GetResponseAsync().ConfigureAwait(false);
                var content = response.GetResponseStream();

                return new Response
                {
                    Address = request.Address,
                    Content = content,
                    StatusCode = HttpStatusCode.OK
                };
            }

            return default(IResponse);
        }

        /// <summary>
        /// Checks if the given protocol is supported.
        /// </summary>
        /// <param name="protocol">The protocol to check for, e.g. file.</param>
        /// <returns>True if the protocol is supported, otherwise false.</returns>
        public Boolean SupportsProtocol(String protocol)
        {
            return !String.IsNullOrEmpty(protocol) && protocol.Equals(ProtocolNames.File);
        }
    }
}
