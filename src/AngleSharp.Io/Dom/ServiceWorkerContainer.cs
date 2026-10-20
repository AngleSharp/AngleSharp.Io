namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using AngleSharp.Io.ServiceWorkerApi;
    using System;
    using System.Net;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the ServiceWorkerContainer API exposed on navigator.serviceWorker.
    /// </summary>
    [DomName("ServiceWorkerContainer")]
    public sealed class ServiceWorkerContainer : EventTarget
    {
        private readonly ServiceWorkerRegistry _registry;
        private readonly Func<Url> _getClientUrl;

        /// <summary>
        /// Creates a new service worker container.
        /// </summary>
        /// <param name="registry">The registry holding the registrations.</param>
        /// <param name="getClientUrl">The callback for resolving the URL of the current document.</param>
        public ServiceWorkerContainer(ServiceWorkerRegistry registry, Func<Url> getClientUrl)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _getClientUrl = getClientUrl ?? throw new ArgumentNullException(nameof(getClientUrl));
            _registry.Track(this);
        }

        /// <summary>
        /// Gets the active worker of the registration matching the current
        /// document, if any. Unlike browsers, the match is determined on
        /// access and not when the document has been loaded.
        /// </summary>
        [DomName("controller")]
        public ServiceWorker Controller
        {
            get
            {
                var clientUrl = _getClientUrl.Invoke();

                if (!IsPotentiallyTrustworthy(clientUrl))
                {
                    return null;
                }

                var worker = _registry.Match(clientUrl)?.Active;
                return worker?.State == ServiceWorkerState.Activated ? worker : null;
            }
        }

        /// <summary>
        /// Gets the task completing with the registration matching the current
        /// document once it has an active worker. As in browsers, the task
        /// never fails, i.e., it stays pending without such a registration.
        /// </summary>
        [DomName("ready")]
        public Task<ServiceWorkerRegistration> Ready
        {
            get
            {
                var clientUrl = _getClientUrl.Invoke();

                if (!IsPotentiallyTrustworthy(clientUrl))
                {
                    return new TaskCompletionSource<ServiceWorkerRegistration>().Task;
                }

                return _registry.WaitForActiveAsync(clientUrl);
            }
        }

        /// <summary>
        /// Adds or removes the handler for the controllerchange event.
        /// </summary>
        [DomName("oncontrollerchange")]
        public event DomEventHandler ControllerChanged
        {
            add { AddEventListener(ServiceWorkerEventNames.ControllerChange, value, false); }
            remove { RemoveEventListener(ServiceWorkerEventNames.ControllerChange, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the message event, which receives
        /// the messages of the controlling worker.
        /// </summary>
        [DomName("onmessage")]
        public event DomEventHandler Message
        {
            add { AddEventListener(EventNames.Message, value, false); }
            remove { RemoveEventListener(EventNames.Message, value, false); }
        }

        /// <summary>
        /// Registers a service worker using the default options.
        /// </summary>
        /// <param name="scriptUrl">The URL of the worker's script.</param>
        /// <returns>The registration.</returns>
        [DomName("register")]
        public Task<ServiceWorkerRegistration> Register(String scriptUrl) => Register(scriptUrl, null);

        /// <summary>
        /// Registers a service worker. The returned task completes after the
        /// worker went through its lifecycle, i.e., it is either activated or,
        /// if its installation failed, redundant.
        /// </summary>
        /// <param name="scriptUrl">The URL of the worker's script.</param>
        /// <param name="options">The registration options.</param>
        /// <returns>The registration.</returns>
        [DomName("register")]
        public async Task<ServiceWorkerRegistration> Register(String scriptUrl, ServiceWorkerRegistrationOptions options)
        {
            var clientUrl = GetClientUrl();
            var script = ParseUrl(clientUrl, scriptUrl ?? String.Empty);
            var scope = String.IsNullOrEmpty(options?.Scope) ? new Url(script, "./") : ParseUrl(clientUrl, options.Scope);

            if (script.Origin != clientUrl.Origin || scope.Origin != clientUrl.Origin)
            {
                throw new DomException(DomError.Security);
            }

            // without fetching the script there is no Service-Worker-Allowed
            // header, which leaves the script's directory as maximum scope
            var maxScope = new Url(script, "./");

            if (!scope.PathName.StartsWith(maxScope.PathName, StringComparison.Ordinal))
            {
                throw new DomException(DomError.Security);
            }

            var type = options?.Type ?? WorkerType.Classic;
            var updateViaCache = options?.UpdateViaCache ?? ServiceWorkerUpdateViaCache.Imports;
            return await _registry.RegisterAsync(script, scope, type, updateViaCache).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the registration matching the current document.
        /// </summary>
        /// <returns>The registration or null.</returns>
        [DomName("getRegistration")]
        public Task<ServiceWorkerRegistration> GetRegistration() => GetRegistration(null);

        /// <summary>
        /// Gets the registration matching the given URL.
        /// </summary>
        /// <param name="clientUrl">The URL to match, relative to the current document.</param>
        /// <returns>The registration or null.</returns>
        [DomName("getRegistration")]
        public Task<ServiceWorkerRegistration> GetRegistration(String clientUrl)
        {
            var baseUrl = GetClientUrl();
            var url = String.IsNullOrEmpty(clientUrl) ? baseUrl : ParseUrl(baseUrl, clientUrl);

            if (url.Origin != baseUrl.Origin)
            {
                throw new DomException(DomError.Security);
            }

            return Task.FromResult(_registry.Match(url));
        }

        /// <summary>
        /// Gets all registrations of the current document's origin.
        /// </summary>
        /// <returns>The registrations.</returns>
        [DomName("getRegistrations")]
        public Task<ServiceWorkerRegistration[]> GetRegistrations()
        {
            var clientUrl = GetClientUrl();
            return Task.FromResult(_registry.GetRegistrations(clientUrl.Origin));
        }

        internal void RaiseControllerChange() => Dispatch(new Event(ServiceWorkerEventNames.ControllerChange));

        internal void Receive(ServiceWorker source, Object message)
        {
            // MessageEvent only knows windows as source, hence the worker is
            // only reflected in the origin
            var origin = new Url(source.ScriptUrl).Origin;
            Dispatch(new MessageEvent(EventNames.Message, false, false, message, origin, String.Empty, null, Array.Empty<IMessagePort>()));
        }

        /// <summary>
        /// Checks if the given URL belongs to a potentially trustworthy origin,
        /// i.e., to a secure context as far as service workers are concerned.
        /// </summary>
        /// <param name="url">The URL to check.</param>
        /// <returns>True if the URL is potentially trustworthy, otherwise false.</returns>
        internal static Boolean IsPotentiallyTrustworthy(Url url)
        {
            if (url is null || url.IsInvalid || !IsHttp(url))
            {
                return false;
            }

            if (url.Scheme == ProtocolNames.Https)
            {
                return true;
            }

            var host = (url.HostName ?? String.Empty).Trim('[', ']');

            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
        }

        private Url GetClientUrl()
        {
            var clientUrl = _getClientUrl.Invoke();

            if (!IsPotentiallyTrustworthy(clientUrl))
            {
                throw new DomException(DomError.Security);
            }

            return clientUrl;
        }

        private static Url ParseUrl(Url baseUrl, String value)
        {
            var url = new Url(baseUrl, value);

            // the spec rejects these with a TypeError
            if (url.IsInvalid || !IsHttp(url))
            {
                throw new InvalidOperationException("The URL must be a valid http or https URL.");
            }

            if (url.PathName.IndexOf("%2f", StringComparison.OrdinalIgnoreCase) >= 0 || url.PathName.IndexOf("%5c", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException("The URL path must not contain an escaped slash or backslash.");
            }

            url.Fragment = null;
            return url;
        }

        private static Boolean IsHttp(Url url) =>
            url.Scheme == ProtocolNames.Http || url.Scheme == ProtocolNames.Https;
    }

    /// <summary>
    /// Represents the options for registering a service worker.
    /// </summary>
    [DomName("RegistrationOptions")]
    public sealed class ServiceWorkerRegistrationOptions
    {
        /// <summary>
        /// Gets or sets the scope, relative to the current document.
        /// </summary>
        [DomName("scope")]
        public String Scope { get; set; }

        /// <summary>
        /// Gets or sets the type of the worker's script.
        /// </summary>
        [DomName("type")]
        public WorkerType Type { get; set; } = WorkerType.Classic;

        /// <summary>
        /// Gets or sets how the HTTP cache is used when checking for updates.
        /// </summary>
        [DomName("updateViaCache")]
        public ServiceWorkerUpdateViaCache UpdateViaCache { get; set; } = ServiceWorkerUpdateViaCache.Imports;
    }
}
