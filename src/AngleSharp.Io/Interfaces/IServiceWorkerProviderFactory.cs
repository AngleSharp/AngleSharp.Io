namespace AngleSharp.Io.Dom
{
    using AngleSharp.Browser.Dom;

    /// <summary>
    /// Represents a factory for creating ServiceWorkerContainer views per navigator.
    /// </summary>
    public interface IServiceWorkerProviderFactory
    {
        /// <summary>
        /// Gets the ServiceWorkerContainer object for the provided navigator.
        /// </summary>
        /// <param name="navigator">The navigator requesting service worker access.</param>
        /// <returns>The ServiceWorkerContainer object or null.</returns>
        ServiceWorkerContainer GetServiceWorkerContainer(INavigator navigator);
    }
}
