namespace AngleSharp.Io.Network
{
    using AngleSharp.Network;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Requester to perform about:// requests.
    /// </summary>
    public class AboutRequester : IRequester
    {
        private readonly Dictionary<String, Func<IRequest, CancellationToken, Task<IResponse>>> _routes;

        /// <summary>
        /// Creates a new about requester.
        /// </summary>
        public AboutRequester()
        {
            _routes = new Dictionary<String, Func<IRequest, CancellationToken, Task<IResponse>>>(StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Sets the route for the given address.
        /// </summary>
        /// <param name="address">The address of the route.</param>
        /// <param name="route">The route to use.</param>
        public void SetRoute(String address, Func<IRequest, CancellationToken, Task<IResponse>> route)
        {
            _routes[address] = route;
        }

        /// <summary>
        /// Gets the route for the given address, if any.
        /// </summary>
        /// <param name="address">The address of the route to obtain.</param>
        /// <returns>The route, if any.</returns>
        public Func<IRequest, CancellationToken, Task<IResponse>> GetRoute(String address)
        {
            var route = default(Func<IRequest, CancellationToken, Task<IResponse>>);
            _routes.TryGetValue(address, out route);
            return route;
        }

        /// <summary>
        /// Performs an asynchronous request that can be cancelled.
        /// </summary>
        /// <param name="request">The options to consider.</param>
        /// <param name="cancel">The token for cancelling the task.</param>
        /// <returns>The task that will eventually give the response data.</returns>
        public Task<IResponse> RequestAsync(IRequest request, CancellationToken cancel)
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
        public Boolean SupportsProtocol(String protocol)
        {
            return !String.IsNullOrEmpty(protocol) && protocol.Equals("about");
        }

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
