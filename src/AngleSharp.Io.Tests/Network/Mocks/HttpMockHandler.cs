namespace AngleSharp.Io.Tests.Network.Mocks
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    class HttpMockHandler : DelegatingHandler
    {
        readonly HttpMockState _testState;

        public HttpMockHandler(HttpMockState testState)
        {
            _testState = testState;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _testState.HttpRequestMessage = request;

            if (request.Content != null)
                _testState.HttpRequestMessageContent = await request.Content.ReadAsByteArrayAsync();

            _testState.HttpResponseMessage.RequestMessage = request;
            return _testState.HttpResponseMessage;
        }
    }
}
