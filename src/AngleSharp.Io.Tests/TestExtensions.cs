namespace AngleSharp.Io.Tests
{
    using AngleSharp.Io.Tests.Mocks;
    using System;

    static class TestExtensions
    {
        public static IConfiguration WithMockRequester(this IConfiguration config, Action<Request> onRequest = null)
        {
            var mockRequester = new MockRequester { OnRequest = onRequest };
            return config.WithMockRequester(mockRequester);
        }

        public static IConfiguration WithMockRequester(this IConfiguration config, IRequester mockRequester)
        {
            return config.With(mockRequester).WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = true });
        }

        public static IConfiguration WithVirtualRequester(this IConfiguration config, Func<Request, IResponse> onRequest = null)
        {
            var mockRequester = new VirtualRequester(onRequest);
            return config.WithMockRequester(mockRequester);
        }
    }
}
