namespace AngleSharp.Io.ServiceWorkerApi
{
    using AngleSharp.Io.Dom;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A requester passing HTTP(S) requests within the scope of an active
    /// service worker to the platform before delegating to the wrapped
    /// requester.
    /// </summary>
    public class ServiceWorkerRequester : BaseRequester
    {
        #region Fields

        private readonly IRequester _inner;
        private readonly ServiceWorkerRegistry _registry;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new service worker requester.
        /// </summary>
        /// <param name="inner">The requester used for network requests.</param>
        /// <param name="registry">The registry holding the registrations.</param>
        public ServiceWorkerRequester(IRequester inner, ServiceWorkerRegistry registry)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the wrapped requester.
        /// </summary>
        public IRequester Inner => _inner;

        /// <summary>
        /// Gets the used registry.
        /// </summary>
        public ServiceWorkerRegistry Registry => _registry;

        #endregion

        #region Methods

        /// <summary>
        /// Checks if the given protocol is supported by the wrapped requester.
        /// </summary>
        /// <param name="protocol">The protocol to check for, e.g., http.</param>
        /// <returns>True if the protocol is supported, otherwise false.</returns>
        public override Boolean SupportsProtocol(String protocol) => _inner.SupportsProtocol(protocol);

        /// <summary>
        /// Performs the request using the matching service worker, if any.
        /// </summary>
        /// <param name="request">The options to consider.</param>
        /// <param name="cancel">The token for cancelling the task.</param>
        /// <returns>The task that will eventually give the response data.</returns>
        protected override async Task<IResponse> PerformRequestAsync(Request request, CancellationToken cancel)
        {
            // browsers route a client's requests to the worker controlling
            // the client; requests carry no client here, so they are matched
            // by their own URL like navigations are
            var worker = _registry.Match(request.Address)?.Active;

            if (worker?.State == ServiceWorkerState.Activated)
            {
                var response = await _registry.Platform.FetchAsync(worker, request, cancel).ConfigureAwait(false);

                if (response is not null)
                {
                    return response;
                }
            }

            return await _inner.RequestAsync(request, cancel).ConfigureAwait(false);
        }

        #endregion
    }
}
