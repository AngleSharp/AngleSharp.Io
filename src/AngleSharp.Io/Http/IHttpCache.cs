namespace AngleSharp.Io.Http
{
    using AngleSharp.Dom;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the HTTP cache sitting in front of the HTTP(S) requesters.
    /// </summary>
    public interface IHttpCache
    {
        /// <summary>
        /// Gets the options of the cache.
        /// </summary>
        HttpCacheOptions Options { get; }

        /// <summary>
        /// Answers the request from the cache or by using the given network
        /// callback, storing the response if permitted.
        /// </summary>
        /// <param name="request">The request to answer.</param>
        /// <param name="network">The callback for sending a request.</param>
        /// <param name="cancel">The token for cancelling the operation.</param>
        /// <returns>The task resolving to the response.</returns>
        Task<IResponse> SendAsync(Request request, Func<Request, CancellationToken, Task<IResponse>> network, CancellationToken cancel);

        /// <summary>
        /// Looks up a stored response for the given request, regardless of
        /// its freshness.
        /// </summary>
        /// <param name="request">The request to match.</param>
        /// <param name="cancel">The token for cancelling the operation.</param>
        /// <returns>The task resolving to the stored response, if any.</returns>
        Task<IResponse> MatchAsync(Request request, CancellationToken cancel);

        /// <summary>
        /// Removes all stored responses for the given address.
        /// </summary>
        /// <param name="address">The address to remove.</param>
        /// <param name="cancel">The token for cancelling the operation.</param>
        /// <returns>The task resolving to true if something was removed.</returns>
        Task<Boolean> RemoveAsync(Url address, CancellationToken cancel);

        /// <summary>
        /// Removes all stored responses of the given origin.
        /// </summary>
        /// <param name="origin">The origin to clear, e.g., https://example.com.</param>
        /// <param name="cancel">The token for cancelling the operation.</param>
        /// <returns>The task completing when the origin has been cleared.</returns>
        Task ClearAsync(String origin, CancellationToken cancel);

        /// <summary>
        /// Removes all stored responses.
        /// </summary>
        /// <param name="cancel">The token for cancelling the operation.</param>
        /// <returns>The task completing when the cache has been cleared.</returns>
        Task ClearAsync(CancellationToken cancel);
    }
}
