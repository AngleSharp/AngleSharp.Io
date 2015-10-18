namespace AngleSharp.Io.Tests.Network.Mocks
{
    using AngleSharp.Network;
    using System;
    using System.Collections.Generic;
    using System.IO;

    class Request : IRequest
    {
        public HttpMethod Method
        {
            get;
            set;
        }

        public Url Address
        {
            get;
            set;
        }

        public Dictionary<String, String> Headers
        {
            get;
            set;
        }

        public Stream Content
        {
            get;
            set;
        }
    }
}
