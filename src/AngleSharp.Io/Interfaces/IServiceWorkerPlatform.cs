namespace AngleSharp.Io.Dom
{
    using AngleSharp.Io;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the platform running the service workers, e.g., a script
    /// engine or a test double.
    /// </summary>
    public interface IServiceWorkerPlatform
    {
        /// <summary>
        /// Installs the given worker. A faulted task marks the installation
        /// as failed and the worker becomes redundant.
        /// </summary>
        /// <param name="worker">The worker to install.</param>
        /// <returns>The task representing the installation.</returns>
        Task InstallAsync(ServiceWorker worker);

        /// <summary>
        /// Activates the given worker. As in browsers, a faulted task does
        /// not prevent the activation.
        /// </summary>
        /// <param name="worker">The worker to activate.</param>
        /// <returns>The task representing the activation.</returns>
        Task ActivateAsync(ServiceWorker worker);

        /// <summary>
        /// Checks if the script of the given worker changed, i.e., if a new
        /// worker with the same script URL and type should be installed.
        /// The registration's update via cache mode tells how the script
        /// should be fetched.
        /// </summary>
        /// <param name="worker">The newest worker of the registration.</param>
        /// <returns>True if the script changed, otherwise false.</returns>
        Task<Boolean> CheckForUpdateAsync(ServiceWorker worker);

        /// <summary>
        /// Delivers a message posted to the given worker. Replies can be sent
        /// via <see cref="ServiceWorkerApi.ServiceWorkerRegistry.PostMessageToClients"/>.
        /// </summary>
        /// <param name="worker">The worker receiving the message.</param>
        /// <param name="message">The posted message.</param>
        void PostMessage(ServiceWorker worker, Object message);

        /// <summary>
        /// Handles a request within the scope of the given active worker. The
        /// request content must not be consumed when null is returned.
        /// </summary>
        /// <param name="worker">The active worker controlling the request.</param>
        /// <param name="request">The intercepted request.</param>
        /// <param name="cancel">The token for cancelling the task.</param>
        /// <returns>The response, or null to continue with the network.</returns>
        Task<IResponse> FetchAsync(ServiceWorker worker, Request request, CancellationToken cancel);
    }
}
