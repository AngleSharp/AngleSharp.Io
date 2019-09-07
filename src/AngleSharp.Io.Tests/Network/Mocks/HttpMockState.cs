namespace AngleSharp.Io.Tests.Network.Mocks
{
    using AngleSharp.Io.Network;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;

    sealed class HttpMockState
    {
        public HttpMockState()
        {
            // dependencies
            TestHandler = new HttpMockHandler(this);
            HttpClient = new HttpClient(TestHandler);

            // data
            Request = new Request
            {
                Method = AngleSharp.Io.HttpMethod.Post,
                Address = new Url("http://example/path?query=value"),
                Headers = new Dictionary<String, String>
                {
                    {"User-Agent", "Foo/2.0"},
                    {"Cookie", "foo=bar"},
                    {"Content-Type", "application/json"},
                    {"Content-Length", "9"}
                },
                Content = new MemoryStream(Encoding.UTF8.GetBytes("\"request\""))
            };
            HttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("\"response\"", Encoding.UTF8, "application/json"),
                Headers =
                {
                    {"Server", "Fake"},
                    {"X-Powered-By", "Magic"},
                    {"X-CSV", new[] {"foo", "bar"}}
                }
            };

            // setup
            Target = new HttpClientRequester(HttpClient);
        }

        public Request Request
        {
            get;
            private set;
        }

        public HttpClientRequester Target
        {
            get;
            private set;
        }

        public HttpClient HttpClient
        {
            get;
            private set;
        }

        public HttpMockHandler TestHandler
        {
            get;
            private set;
        }

        public HttpResponseMessage HttpResponseMessage
        {
            get;
            private set;
        }

        public HttpRequestMessage HttpRequestMessage
        {
            get;
            set;
        }

        public Byte[] HttpRequestMessageContent
        {
            get;
            set;
        }
    }
}
