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
        private readonly Object _features;

        private SyncManager _sync;
        private PeriodicSyncManager _periodicSync;
        private PushManager _pushManager;

        internal ServiceWorkerRegistration(ServiceWorkerRegistry registry, String scope)
        {
            _registry = registry;
            _features = new Object();
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
        /// Gets the manager for one-off background tasks, which is only
        /// available if a sync platform has been registered.
        /// </summary>
        [DomName("sync")]
        public SyncManager Sync
        {
            get
            {
                var platform = _registry.SyncPlatform;

                if (platform is null)
                {
                    return null;
                }

                lock (_features)
                {
                    return _sync ??= new SyncManager(this, platform);
                }
            }
        }

        /// <summary>
        /// Gets the manager for periodic background tasks, which is only
        /// available if a sync platform has been registered.
        /// </summary>
        [DomName("periodicSync")]
        public PeriodicSyncManager PeriodicSync
        {
            get
            {
                var platform = _registry.SyncPlatform;

                if (platform is null)
                {
                    return null;
                }

                lock (_features)
                {
                    return _periodicSync ??= new PeriodicSyncManager(this, platform);
                }
            }
        }

        /// <summary>
        /// Gets the manager for the push subscription, which is only available
        /// if a push platform has been registered.
        /// </summary>
        [DomName("pushManager")]
        public PushManager PushManager
        {
            get
            {
                var platform = _registry.PushPlatform;

                if (platform is null)
                {
                    return null;
                }

                lock (_features)
                {
                    return _pushManager ??= new PushManager(this, platform);
                }
            }
        }

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

        // registrations outlive documents and are shared by the contexts of a
        // registry, so the permissions are the ones of the registration's own
        // origin instead of the origin of some active document
        internal Permissions Permissions
        {
            get
            {
                var policy = _registry.PermissionPolicy;
                return policy is null ? null : new Permissions(policy, () => new Url(Scope).Origin);
            }
        }

        internal Boolean HasPushEndpoint(String endpoint)
        {
            lock (_features)
            {
                return _pushManager?.HasEndpoint(endpoint) ?? false;
            }
        }

        internal Task DispatchPushAsync(ServiceWorker worker, PushMessageData data) => PushManager.DispatchAsync(worker, data);

        internal async Task TerminateFeaturesAsync()
        {
            SyncManager sync;
            PeriodicSyncManager periodicSync;
            PushManager pushManager;

            lock (_features)
            {
                sync = _sync;
                periodicSync = _periodicSync;
                pushManager = _pushManager;
            }

            if (sync != null)
            {
                await sync.TerminateAsync().ConfigureAwait(false);
            }

            if (periodicSync != null)
            {
                await periodicSync.TerminateAsync().ConfigureAwait(false);
            }

            if (pushManager != null)
            {
                await pushManager.TerminateAsync().ConfigureAwait(false);
            }
        }

        internal void RaiseUpdateFound() => Dispatch(new Event(ServiceWorkerEventNames.UpdateFound));
    }
}
