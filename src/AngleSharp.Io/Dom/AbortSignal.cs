namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the AbortController of the DOM standard, which aborts what was given its signal.
    /// </summary>
    [DomName("AbortController")]
    [DomExposed("Window")]
    [DomExposed("Worker")]
    public sealed class AbortController
    {
        /// <summary>
        /// Creates a new controller.
        /// </summary>
        [DomConstructor]
        public AbortController()
        {
            Signal = new AbortSignal();
        }

        /// <summary>
        /// Gets the signal of the controller.
        /// </summary>
        [DomName("signal")]
        public AbortSignal Signal { get; }

        /// <summary>
        /// Aborts what was given the signal.
        /// </summary>
        /// <param name="reason">The reason; by default a DOMException named AbortError.</param>
        [DomName("abort")]
        public void Abort(Object reason = null) => Signal.SignalAbort(reason);
    }

    /// <summary>
    /// Represents the AbortSignal of the DOM standard. Scripts get signals from a controller or
    /// from the static members, as it has no constructor for them.
    /// </summary>
    [DomName("AbortSignal")]
    [DomExposed("Window")]
    [DomExposed("Worker")]
    public sealed class AbortSignal : EventTarget
    {
        //  AngleSharp.Js throws this instead of the exception itself: an exception can't be
        //  just any value, while the reason of a signal can be.
        private const String ScriptValueKey = "AngleSharp.ScriptValue";

        private readonly List<Action> _algorithms = new List<Action>();

        internal AbortSignal()
        {
        }

        /// <summary>
        /// Raised when the signal is aborted.
        /// </summary>
        [DomName("onabort")]
        public event DomEventHandler Aborted
        {
            add { AddEventListener(EventNames.Abort, value); }
            remove { RemoveEventListener(EventNames.Abort, value); }
        }

        /// <summary>
        /// Gets if the signal is aborted.
        /// </summary>
        [DomName("aborted")]
        public Boolean IsAborted { get; private set; }

        /// <summary>
        /// Gets the reason of the abort, if it is aborted.
        /// </summary>
        [DomName("reason")]
        public Object Reason { get; private set; }

        /// <summary>
        /// Creates a signal that is aborted already.
        /// </summary>
        /// <param name="reason">The reason; by default a DOMException named AbortError.</param>
        /// <returns>The signal.</returns>
        [DomName("abort")]
        public static AbortSignal Abort(Object reason = null)
        {
            var signal = new AbortSignal();
            signal.SetAborted(reason);
            return signal;
        }

        /// <summary>
        /// Creates a signal that aborts with a DOMException named TimeoutError after the given time.
        /// </summary>
        /// <param name="window">The window whose timers are used.</param>
        /// <param name="milliseconds">The time to wait.</param>
        /// <returns>The signal.</returns>
        [DomName("timeout")]
        public static AbortSignal Timeout(IWindow window, Int32 milliseconds)
        {
            var signal = new AbortSignal();
            window.SetTimeout(_ => signal.SignalAbort(new DomException(DomError.Timeout)), milliseconds);
            return signal;
        }

        /// <summary>
        /// Creates a signal that aborts as soon as one of the given signals does.
        /// </summary>
        /// <param name="signals">The signals to follow.</param>
        /// <returns>The signal.</returns>
        [DomName("any")]
        public static AbortSignal Any(AbortSignal[] signals)
        {
            var signal = new AbortSignal();

            foreach (var source in signals ?? new AbortSignal[0])
            {
                if (source.IsAborted)
                {
                    signal.SetAborted(source.Reason);
                    return signal;
                }
            }

            foreach (var source in signals ?? new AbortSignal[0])
            {
                source.OnAbort(() => signal.SignalAbort(source.Reason));
            }

            return signal;
        }

        /// <summary>
        /// Throws the reason, if the signal is aborted.
        /// </summary>
        [DomName("throwIfAborted")]
        public void ThrowIfAborted()
        {
            if (IsAborted)
            {
                throw CreateException();
            }
        }

        /// <summary>
        /// Runs the action when the signal is aborted, unless it is removed by then.
        /// </summary>
        /// <param name="algorithm">The action to run.</param>
        internal void OnAbort(Action algorithm) => _algorithms.Add(algorithm);

        internal void RemoveOnAbort(Action algorithm) => _algorithms.Remove(algorithm);

        /// <summary>
        /// Gets the exception telling the reason of the abort.
        /// </summary>
        internal Exception CreateException()
        {
            if (Reason is Exception exception)
            {
                return exception;
            }

            var thrown = new DomException(DomError.Abort);
            thrown.Data[ScriptValueKey] = Reason;
            return thrown;
        }

        internal void SignalAbort(Object reason)
        {
            if (!IsAborted)
            {
                SetAborted(reason);

                foreach (var algorithm in _algorithms.ToArray())
                {
                    algorithm.Invoke();
                }

                _algorithms.Clear();
                this.FireSimpleEvent(EventNames.Abort);
            }
        }

        private void SetAborted(Object reason)
        {
            IsAborted = true;
            Reason = reason ?? new DomException(DomError.Abort);
        }
    }
}
