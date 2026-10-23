namespace AngleSharp.Io.ServiceWorkerApi
{
    using AngleSharp.Io.Dom;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a service worker platform delegating to optional callbacks.
    /// Without callbacks, workers install and activate successfully, scripts
    /// never change, messages are dropped, and all requests continue with
    /// the network.
    /// </summary>
    public sealed class ServiceWorkerPlatform : IServiceWorkerPlatform
    {
        /// <summary>
        /// Gets or sets the callback invoked when a worker is installed.
        /// </summary>
        public Func<ServiceWorker, Task> Install { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when a worker is activated.
        /// </summary>
        public Func<ServiceWorker, Task> Activate { get; set; }

        /// <summary>
        /// Gets or sets the callback deciding if the script of a worker
        /// changed. Returning true installs a new worker.
        /// </summary>
        public Func<ServiceWorker, Task<Boolean>> CheckForUpdate { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked for messages posted to a worker.
        /// </summary>
        public Action<ServiceWorker, Object> Message { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked for requests within the scope of
        /// an active worker. Returning null continues with the network.
        /// </summary>
        public Func<ServiceWorker, Request, CancellationToken, Task<IResponse>> Fetch { get; set; }

        /// <inheritdoc />
        public Task InstallAsync(ServiceWorker worker) => Install?.Invoke(worker) ?? Task.CompletedTask;

        /// <inheritdoc />
        public Task ActivateAsync(ServiceWorker worker) => Activate?.Invoke(worker) ?? Task.CompletedTask;

        /// <inheritdoc />
        public Task<Boolean> CheckForUpdateAsync(ServiceWorker worker) => CheckForUpdate?.Invoke(worker) ?? Task.FromResult(false);

        /// <inheritdoc />
        public void PostMessage(ServiceWorker worker, Object message) => Message?.Invoke(worker, message);

        /// <inheritdoc />
        public Task<IResponse> FetchAsync(ServiceWorker worker, Request request, CancellationToken cancel) =>
            Fetch?.Invoke(worker, request, cancel) ?? Task.FromResult<IResponse>(null);
    }
}
