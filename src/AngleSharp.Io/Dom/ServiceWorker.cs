namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using System;

    /// <summary>
    /// Represents a service worker. Only its lifecycle is modeled, running
    /// the script is up to the <see cref="IServiceWorkerPlatform"/>.
    /// </summary>
    [DomName("ServiceWorker")]
    public sealed class ServiceWorker : EventTarget
    {
        private ServiceWorkerState _state;

        internal ServiceWorker(ServiceWorkerRegistration registration, String scriptUrl, WorkerType type)
        {
            Registration = registration;
            ScriptUrl = scriptUrl;
            Type = type;
            _state = ServiceWorkerState.Parsed;
        }

        /// <summary>
        /// Gets the URL of the worker's script.
        /// </summary>
        [DomName("scriptURL")]
        public String ScriptUrl { get; }

        /// <summary>
        /// Gets the lifecycle state of the worker.
        /// </summary>
        [DomName("state")]
        public ServiceWorkerState State => _state;

        /// <summary>
        /// Gets the type of the worker's script.
        /// </summary>
        public WorkerType Type { get; }

        /// <summary>
        /// Gets the registration owning the worker.
        /// </summary>
        public ServiceWorkerRegistration Registration { get; }

        /// <summary>
        /// Adds or removes the handler for the statechange event.
        /// </summary>
        [DomName("onstatechange")]
        public event DomEventHandler StateChanged
        {
            add { AddEventListener(ServiceWorkerEventNames.StateChange, value, false); }
            remove { RemoveEventListener(ServiceWorkerEventNames.StateChange, value, false); }
        }

        /// <summary>
        /// Sends a message to the worker. Messages to a redundant worker are
        /// dropped.
        /// </summary>
        /// <param name="message">The message to send.</param>
        [DomName("postMessage")]
        public void PostMessage(Object message)
        {
            if (_state != ServiceWorkerState.Redundant)
            {
                Registration.Registry.Platform.PostMessage(this, message);
            }
        }

        internal void SetState(ServiceWorkerState state)
        {
            if (_state != state)
            {
                _state = state;
                Dispatch(new Event(ServiceWorkerEventNames.StateChange));
            }
        }
    }

    static class ServiceWorkerEventNames
    {
        public static readonly String StateChange = "statechange";

        public static readonly String UpdateFound = "updatefound";

        public static readonly String ControllerChange = "controllerchange";
    }
}
