namespace AngleSharp.Io.Network
{
    using AngleSharp.Network;
    using AngleSharp.Network.Default;
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    public class FtpRequester : IRequester
    {
        public async Task<IResponse> RequestAsync(IRequest request, CancellationToken cancel)
        {
            var requester = FtpWebRequest.Create(request.Address.Href) as FtpWebRequest;

            if (requester != null)
            {
                requester.Method = WebRequestMethods.Ftp.DownloadFile;
                requester.Credentials = new NetworkCredential("anonymous", String.Empty);

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

        public Boolean SupportsProtocol(String protocol)
        {
            return !String.IsNullOrEmpty(protocol) && protocol.Equals(ProtocolNames.Ftp);
        }
    }
}
