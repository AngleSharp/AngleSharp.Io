namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using AngleSharp.Io.ServiceWorkerApi;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a service worker registration, i.e., the workers bound to
    /// a scope.
    /// </summary>
    [DomName("ServiceWorkerRegistration")]
    public sealed class ServiceWorkerRegistration : EventTarget
    {
        private readonly ServiceWorkerRegistry _registry;

        internal ServiceWorkerRegistration(ServiceWorkerRegistry registry, String scope)
        {
            _registry = registry;
            Scope = scope;
        }

        /// <summary>
        /// Gets the scope URL of the registration.
        /// </summary>
        [DomName("scope")]
        public String Scope { get; }

        /// <summary>
        /// Gets how the HTTP cache is used when checking for updates.
        /// </summary>
        [DomName("updateViaCache")]
        public ServiceWorkerUpdateViaCache UpdateViaCache { get; internal set; }

        /// <summary>
        /// Gets the worker being installed, if any.
        /// </summary>
        [DomName("installing")]
        public ServiceWorker Installing { get; internal set; }

        /// <summary>
        /// Gets the installed worker waiting for its activation, if any.
        /// </summary>
        [DomName("waiting")]
        public ServiceWorker Waiting { get; internal set; }

        /// <summary>
        /// Gets the active worker, if any.
        /// </summary>
        [DomName("active")]
        public ServiceWorker Active { get; internal set; }

        /// <summary>
        /// Adds or removes the handler for the updatefound event.
        /// </summary>
        [DomName("onupdatefound")]
        public event DomEventHandler UpdateFound
        {
            add { AddEventListener(ServiceWorkerEventNames.UpdateFound, value, false); }
            remove { RemoveEventListener(ServiceWorkerEventNames.UpdateFound, value, false); }
        }

        /// <summary>
        /// Checks the newest worker for an update, which the platform decides.
        /// An update is installed and activated before the task completes.
        /// </summary>
        /// <returns>The registration.</returns>
        [DomName("update")]
        public Task<ServiceWorkerRegistration> Update() => _registry.UpdateAsync(this);

        /// <summary>
        /// Removes the registration. Its workers become redundant right away.
        /// </summary>
        /// <returns>True if the registration was removed, otherwise false.</returns>
        [DomName("unregister")]
        public Task<Boolean> Unregister() => _registry.UnregisterAsync(this);

        internal ServiceWorkerRegistry Registry => _registry;

        internal ServiceWorker NewestWorker => Installing ?? Waiting ?? Active;

        internal void RaiseUpdateFound() => Dispatch(new Event(ServiceWorkerEventNames.UpdateFound));
    }
}
