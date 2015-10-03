using System.Collections.Generic;
using System.IO;
using System.Net;
using AngleSharp;
using AngleSharp.Network;

namespace Knapcode.AngleSharp.NetHttp
{
    public class Response : IResponse
    {
        public void Dispose()
        {
            if (Content != null)
            {
                Content.Dispose();
            }
        }

        public HttpStatusCode StatusCode { get; set; }

        public Url Address { get; set; }

        public IDictionary<string, string> Headers { get; set; }

        public Stream Content { get; set; }
    }
}