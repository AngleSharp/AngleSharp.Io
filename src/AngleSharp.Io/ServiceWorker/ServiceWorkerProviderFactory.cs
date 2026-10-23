namespace AngleSharp.Io.ServiceWorkerApi
{
    using AngleSharp.Browser.Dom;
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents the default ServiceWorkerContainer provider factory.
    /// </summary>
    public sealed class ServiceWorkerProviderFactory : IServiceWorkerProviderFactory
    {
        private readonly ServiceWorkerRegistry _registry;
        private readonly ConditionalWeakTable<INavigator, ServiceWorkerContainer> _containers;

        /// <summary>
        /// Creates a new ServiceWorkerContainer provider factory.
        /// </summary>
        /// <param name="registry">The registry holding the registrations.</param>
        public ServiceWorkerProviderFactory(ServiceWorkerRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _containers = new ConditionalWeakTable<INavigator, ServiceWorkerContainer>();
        }

        /// <inheritdoc />
        public ServiceWorkerContainer GetServiceWorkerContainer(INavigator navigator)
        {
            if (navigator == null)
            {
                return null;
            }

            // the client URL is resolved per call as the active document
            // changes while the navigator stays the same
            return _containers.GetValue(navigator, _ => new ServiceWorkerContainer(_registry, () => GetClientUrl(navigator)));
        }

        private static Url GetClientUrl(INavigator navigator)
        {
            var address = NavigatorExtensions.GetContext(navigator)?.Active?.Url;
            return String.IsNullOrEmpty(address) ? null : new Url(address);
        }
    }
}
