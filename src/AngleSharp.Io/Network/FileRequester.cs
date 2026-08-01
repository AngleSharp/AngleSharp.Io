namespace AngleSharp.Io.Network
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Requester to perform file:// requests.
    /// </summary>
    public class FileRequester : BaseRequester
    {
        /// <summary>
        /// Performs an asynchronous request that can be cancelled.
        /// </summary>
        /// <param name="request">The options to consider.</param>
        /// <param name="cancel">The token for cancelling the task.</param>
        /// <returns>The task that will eventually give the response data.</returns>
        protected override Task<IResponse> PerformRequestAsync(Request request, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();

            try
            {
                var uri = new Uri(request.Address.Href, UriKind.Absolute);

                if (uri.IsFile)
                {
                    var content = File.OpenRead(uri.LocalPath);

                    return Task.FromResult<IResponse>(new DefaultResponse
                    {
                        Address = request.Address,
                        Content = content,
                        StatusCode = HttpStatusCode.OK
                    });
                }

                return Task.FromResult<IResponse>(default);
            }
            catch (FileNotFoundException)
            {
                return Task.FromResult<IResponse>(new DefaultResponse
                {
                    Address = request.Address,
                    StatusCode = HttpStatusCode.NotFound
                });
            }
            catch (DirectoryNotFoundException)
            {
                return Task.FromResult<IResponse>(new DefaultResponse
                {
                    Address = request.Address,
                    StatusCode = HttpStatusCode.NotFound
                });
            }
        }

        /// <summary>
        /// Checks if the given protocol is supported.
        /// </summary>
        /// <param name="protocol">The protocol to check for, e.g. file.</param>
        /// <returns>True if the protocol is supported, otherwise false.</returns>
        public override Boolean SupportsProtocol(String protocol) =>
            protocol.Equals(ProtocolNames.File, StringComparison.OrdinalIgnoreCase);
    }
}
