namespace AngleSharp.Io.Http
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A requester answering HTTP(S) requests from an HTTP cache before
    /// delegating to the wrapped requester.
    /// </summary>
    public class CachingRequester : BaseRequester
    {
        #region Fields

        private readonly IRequester _inner;
        private readonly IHttpCache _cache;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new caching requester.
        /// </summary>
        /// <param name="inner">The requester used for network requests.</param>
        /// <param name="cache">The cache to use.</param>
        public CachingRequester(IRequester inner, IHttpCache cache)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the wrapped requester.
        /// </summary>
        public IRequester Inner => _inner;

        /// <summary>
        /// Gets the used cache.
        /// </summary>
        public IHttpCache Cache => _cache;

        #endregion

        #region Methods

        /// <summary>
        /// Checks if the given protocol is supported by the wrapped requester.
        /// </summary>
        /// <param name="protocol">The protocol to check for, e.g., http.</param>
        /// <returns>True if the protocol is supported, otherwise false.</returns>
        public override Boolean SupportsProtocol(String protocol) => _inner.SupportsProtocol(protocol);

        /// <summary>
        /// Performs the request using the cache.
        /// </summary>
        /// <param name="request">The options to consider.</param>
        /// <param name="cancel">The token for cancelling the task.</param>
        /// <returns>The task that will eventually give the response data.</returns>
        protected override Task<IResponse> PerformRequestAsync(Request request, CancellationToken cancel) =>
            _cache.SendAsync(request, _inner.RequestAsync, cancel);

        #endregion
    }
}
