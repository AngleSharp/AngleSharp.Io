namespace AngleSharp.Io.Network
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Requester to perform about:// requests.
    /// </summary>
    public class AboutRequester : BaseRequester
    {
        private readonly Dictionary<String, Func<Request, CancellationToken, Task<IResponse>>> _routes;

        /// <summary>
        /// Creates a new about requester.
        /// </summary>
        public AboutRequester()
        {
            _routes = new Dictionary<String, Func<Request, CancellationToken, Task<IResponse>>>(StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Sets the route for the given address.
        /// </summary>
        /// <param name="address">The address of the route.</param>
        /// <param name="route">The route to use.</param>
        public void SetRoute(String address, Func<Request, CancellationToken, Task<IResponse>> route)
        {
            _routes[address] = route;
        }

        /// <summary>
        /// Gets the route for the given address, if any.
        /// </summary>
        /// <param name="address">The address of the route to obtain.</param>
        /// <returns>The route, if any.</returns>
        public Func<Request, CancellationToken, Task<IResponse>> GetRoute(String address)
        {
            _routes.TryGetValue(address, out var route);
            return route;
        }

        /// <summary>
        /// Performs an asynchronous request that can be cancelled.
        /// </summary>
        /// <param name="request">The options to consider.</param>
        /// <param name="cancel">The token for cancelling the task.</param>
        /// <returns>The task that will eventually give the response data.</returns>
        protected override Task<IResponse> PerformRequestAsync(Request request, CancellationToken cancel)
        {
            var address = GetAddress(request.Address.Data);
            var route = GetRoute(address);

            if (route != null)
            {
                return route.Invoke(request, cancel);
            }

            return Task.FromResult(default(IResponse));
        }

        /// <summary>
        /// Checks if the given protocol is supported.
        /// </summary>
        /// <param name="protocol">The protocol to check for, e.g. file.</param>
        /// <returns>True if the protocol is supported, otherwise false.</returns>
        public override Boolean SupportsProtocol(String protocol) =>
            protocol.Equals("about", StringComparison.OrdinalIgnoreCase);

        private static String GetAddress(String data)
        {
            var skip = 0;

            while (data.Length > skip && data[skip] == '/' && skip++ < 2) ;

            var query = data.IndexOf('?');

            if (query >= 0)
            {
                data = data.Remove(query);
            }

            return data.Remove(0, skip);
        }
    }
}
