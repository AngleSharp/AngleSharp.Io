namespace AngleSharp.Io.ServiceWorkerApi
{
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Keeps the service worker registrations in memory, keyed by their
    /// scope URL, and runs the lifecycle of their workers.
    /// </summary>
    public sealed class ServiceWorkerRegistry
    {
        private readonly IServiceWorkerPlatform _platform;
        private readonly Dictionary<String, ServiceWorkerRegistration> _registrations;
        private readonly List<ReadyWaiter> _readyWaiters;
        private readonly List<WeakReference<ServiceWorkerContainer>> _containers;
        private readonly SemaphoreSlim _jobs;

        /// <summary>
        /// Creates a new registry using the given platform.
        /// </summary>
        /// <param name="platform">The platform running the workers.</param>
        public ServiceWorkerRegistry(IServiceWorkerPlatform platform)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _registrations = new Dictionary<String, ServiceWorkerRegistration>(StringComparer.Ordinal);
            _readyWaiters = new List<ReadyWaiter>();
            _containers = new List<WeakReference<ServiceWorkerContainer>>();
            _jobs = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Gets the platform running the workers.
        /// </summary>
        public IServiceWorkerPlatform Platform => _platform;

        /// <summary>
        /// Registers the given script for the given scope. A registration
        /// with the same script and type is returned as is, unless the update
        /// via cache mode changed, which triggers an update check. Otherwise
        /// a new worker is installed and activated right away, replacing the
        /// active one.
        /// </summary>
        /// <param name="scriptUrl">The absolute URL of the script.</param>
        /// <param name="scopeUrl">The absolute URL of the scope.</param>
        /// <param name="type">The type of the script.</param>
        /// <param name="updateViaCache">How the HTTP cache is used when checking for updates.</param>
        /// <returns>The registration.</returns>
        public async Task<ServiceWorkerRegistration> RegisterAsync(Url scriptUrl, Url scopeUrl, WorkerType type = WorkerType.Classic, ServiceWorkerUpdateViaCache updateViaCache = ServiceWorkerUpdateViaCache.Imports)
        {
            if (scriptUrl is null)
            {
                throw new ArgumentNullException(nameof(scriptUrl));
            }

            if (scopeUrl is null)
            {
                throw new ArgumentNullException(nameof(scopeUrl));
            }

            // jobs are serialized like the spec's job queue, which keeps the
            // lifecycle transitions of concurrent registrations consistent
            await _jobs.WaitAsync().ConfigureAwait(false);

            try
            {
                var scope = scopeUrl.Href;
                var script = scriptUrl.Href;
                var registration = Find(scope);
                var newest = registration?.NewestWorker;

                if (newest?.ScriptUrl == script && newest.Type == type)
                {
                    if (registration.UpdateViaCache != updateViaCache)
                    {
                        registration.UpdateViaCache = updateViaCache;
                        await UpdateIfChangedAsync(registration, newest).ConfigureAwait(false);
                    }

                    return registration;
                }

                if (registration is null)
                {
                    registration = new ServiceWorkerRegistration(this, scope);

                    lock (_registrations)
                    {
                        _registrations[scope] = registration;
                    }
                }

                registration.UpdateViaCache = updateViaCache;
                await InstallAsync(registration, new ServiceWorker(registration, script, type)).ConfigureAwait(false);
                return registration;
            }
            finally
            {
                _jobs.Release();
            }
        }

        /// <summary>
        /// Gets the registration whose scope is the longest prefix of the
        /// given URL.
        /// </summary>
        /// <param name="url">The URL to match.</param>
        /// <returns>The registration or null.</returns>
        public ServiceWorkerRegistration Match(Url url)
        {
            if (url is null)
            {
                return null;
            }

            var href = url.Href;
            var match = default(ServiceWorkerRegistration);

            lock (_registrations)
            {
                foreach (var registration in _registrations.Values)
                {
                    if (href.StartsWith(registration.Scope, StringComparison.Ordinal) && (match is null || registration.Scope.Length > match.Scope.Length))
                    {
                        match = registration;
                    }
                }
            }

            return match;
        }

        /// <summary>
        /// Gets the registrations of the given origin.
        /// </summary>
        /// <param name="origin">The origin of the registrations.</param>
        /// <returns>The registrations ordered by their scope.</returns>
        public ServiceWorkerRegistration[] GetRegistrations(String origin)
        {
            lock (_registrations)
            {
                return _registrations.Values
                    .Where(registration => new Url(registration.Scope).Origin == origin)
                    .OrderBy(registration => registration.Scope, StringComparer.Ordinal)
                    .ToArray();
            }
        }

        /// <summary>
        /// Sends a message from the given worker to the containers of the
        /// documents it controls, which receive it as message event.
        /// </summary>
        /// <param name="source">The worker sending the message.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>The number of containers that received the message.</returns>
        public Int32 PostMessageToClients(ServiceWorker source, Object message)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var clients = GetContainers().Where(container => container.Controller == source).ToArray();

            foreach (var client in clients)
            {
                client.Receive(source, message);
            }

            return clients.Length;
        }

        internal void Track(ServiceWorkerContainer container)
        {
            lock (_containers)
            {
                _containers.RemoveAll(reference => !reference.TryGetTarget(out _));
                _containers.Add(new WeakReference<ServiceWorkerContainer>(container));
            }
        }

        internal Task<ServiceWorkerRegistration> WaitForActiveAsync(Url clientUrl)
        {
            lock (_readyWaiters)
            {
                var registration = Match(clientUrl);

                if (registration?.Active?.State == ServiceWorkerState.Activated)
                {
                    return Task.FromResult(registration);
                }

                var waiter = new ReadyWaiter(clientUrl);
                _readyWaiters.Add(waiter);
                return waiter.Completion.Task;
            }
        }

        internal async Task<ServiceWorkerRegistration> UpdateAsync(ServiceWorkerRegistration registration)
        {
            await _jobs.WaitAsync().ConfigureAwait(false);

            try
            {
                var newest = registration.NewestWorker;

                if (newest is null || Find(registration.Scope) != registration)
                {
                    throw new DomException(DomError.InvalidState);
                }

                await UpdateIfChangedAsync(registration, newest).ConfigureAwait(false);
                return registration;
            }
            finally
            {
                _jobs.Release();
            }
        }

        internal async Task<Boolean> UnregisterAsync(ServiceWorkerRegistration registration)
        {
            await _jobs.WaitAsync().ConfigureAwait(false);

            try
            {
                if (!Remove(registration))
                {
                    return false;
                }

                // browsers keep the active worker until no client uses it,
                // without tracking clients it is dropped right away
                foreach (var worker in new[] { registration.Installing, registration.Waiting, registration.Active })
                {
                    worker?.SetState(ServiceWorkerState.Redundant);
                }

                registration.Installing = null;
                registration.Waiting = null;
                registration.Active = null;
                return true;
            }
            finally
            {
                _jobs.Release();
            }
        }

        private async Task UpdateIfChangedAsync(ServiceWorkerRegistration registration, ServiceWorker newest)
        {
            // the platform stands in for fetching the script and comparing
            // it byte by byte with the one of the newest worker
            if (await _platform.CheckForUpdateAsync(newest).ConfigureAwait(false))
            {
                await InstallAsync(registration, new ServiceWorker(registration, newest.ScriptUrl, newest.Type)).ConfigureAwait(false);
            }
        }

        private async Task InstallAsync(ServiceWorkerRegistration registration, ServiceWorker worker)
        {
            registration.Installing = worker;
            worker.SetState(ServiceWorkerState.Installing);
            registration.RaiseUpdateFound();

            try
            {
                await _platform.InstallAsync(worker).ConfigureAwait(false);
            }
            catch
            {
                registration.Installing = null;
                worker.SetState(ServiceWorkerState.Redundant);

                if (registration.NewestWorker is null)
                {
                    Remove(registration);
                }

                return;
            }

            registration.Installing = null;
            registration.Waiting = worker;
            worker.SetState(ServiceWorkerState.Installed);

            // without tracking clients there is nobody to wait for, hence the
            // worker is activated right away as if skipWaiting() was called
            var previous = registration.Active;
            registration.Waiting = null;
            registration.Active = worker;
            previous?.SetState(ServiceWorkerState.Redundant);
            worker.SetState(ServiceWorkerState.Activating);

            try
            {
                await _platform.ActivateAsync(worker).ConfigureAwait(false);
            }
            catch
            {
                // a failing activate event does not prevent the activation
            }

            worker.SetState(ServiceWorkerState.Activated);
            NotifyActivated(registration, worker);
        }

        private void NotifyActivated(ServiceWorkerRegistration registration, ServiceWorker worker)
        {
            var ready = new List<ReadyWaiter>();

            lock (_readyWaiters)
            {
                foreach (var waiter in _readyWaiters)
                {
                    if (Match(waiter.ClientUrl) == registration)
                    {
                        ready.Add(waiter);
                    }
                }

                _readyWaiters.RemoveAll(ready.Contains);
            }

            foreach (var waiter in ready)
            {
                waiter.Completion.TrySetResult(registration);
            }

            foreach (var container in GetContainers())
            {
                if (container.Controller == worker)
                {
                    container.RaiseControllerChange();
                }
            }
        }

        private ServiceWorkerContainer[] GetContainers()
        {
            lock (_containers)
            {
                var containers = new List<ServiceWorkerContainer>();

                foreach (var reference in _containers)
                {
                    if (reference.TryGetTarget(out var container))
                    {
                        containers.Add(container);
                    }
                }

                return containers.ToArray();
            }
        }

        private ServiceWorkerRegistration Find(String scope)
        {
            lock (_registrations)
            {
                return _registrations.TryGetValue(scope, out var registration) ? registration : null;
            }
        }

        private Boolean Remove(ServiceWorkerRegistration registration)
        {
            lock (_registrations)
            {
                return _registrations.TryGetValue(registration.Scope, out var current) && current == registration && _registrations.Remove(registration.Scope);
            }
        }

        private sealed class ReadyWaiter
        {
            public ReadyWaiter(Url clientUrl)
            {
                ClientUrl = clientUrl;
                Completion = new TaskCompletionSource<ServiceWorkerRegistration>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public Url ClientUrl { get; }

            public TaskCompletionSource<ServiceWorkerRegistration> Completion { get; }
        }
    }
}
