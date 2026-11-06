namespace AngleSharp.Io.Dom
{
    using AngleSharp;
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the Web Locks API.
    /// </summary>
    [DomName("LockManager")]
    public sealed class LockManager
    {
        private const String ExclusiveMode = "exclusive";
        private const String SharedMode = "shared";

        private readonly String _scope;

        internal LockManager(String scope)
        {
            _scope = scope ?? String.Empty;
        }

        /// <summary>
        /// Requests a lock with the given name.
        /// </summary>
        /// <param name="name">The lock name.</param>
        /// <param name="callback">The callback to run while the lock is held.</param>
        /// <returns>The task representing the lock request.</returns>
        [DomName("request")]
        public Task Request(String name, Func<WebLock, Task> callback) => Request(name, null, callback);

        /// <summary>
        /// Requests a lock with the given name.
        /// </summary>
        /// <typeparam name="TResult">The callback result type.</typeparam>
        /// <param name="name">The lock name.</param>
        /// <param name="callback">The callback to run while the lock is held.</param>
        /// <returns>The task representing the lock request.</returns>
        public async Task<TResult> Request<TResult>(String name, Func<WebLock, Task<TResult>> callback)
        {
            return await RequestInternal(name, callback).ConfigureAwait(false);
        }

        /// <summary>
        /// Requests a lock with the given name in the way the options tell.
        /// </summary>
        /// <param name="name">The lock name.</param>
        /// <param name="options">The options of the request.</param>
        /// <param name="callback">
        /// The callback to run while the lock is held. It gets null if the options asked for the
        /// lock only if it is available, and it was not.
        /// </param>
        /// <returns>The task representing the lock request.</returns>
        [DomName("request")]
        public Task Request(String name, LockOptions options, Func<WebLock, Task> callback)
        {
            return RequestInternal<Object>(name, options, async lockInfo =>
            {
                var task = callback.Invoke(lockInfo);
                await task.ConfigureAwait(false);
                return (task as Task<Object>)?.Result;
            });
        }

        internal static String GetScopeKey(IBrowsingContext context)
        {
            if (context == null)
            {
                return String.Empty;
            }

            var window = context.Active?.DefaultView;
            var origin = GetOrigin(window);

            if (!String.IsNullOrEmpty(origin))
            {
                return origin;
            }

            return "context:" + RuntimeHelpers.GetHashCode(context).ToString(CultureInfo.InvariantCulture);
        }

        private Task<TResult> RequestInternal<TResult>(String name, Func<WebLock, Task<TResult>> callback) =>
            RequestInternal(name, null, callback);

        private async Task<TResult> RequestInternal<TResult>(String name, LockOptions options, Func<WebLock, Task<TResult>> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var isShared = Validate(options);
            var scope = _scope;
            var normalizedName = String.IsNullOrEmpty(name) ? String.Empty : name;
            var state = LockRegistry.GetState(scope, normalizedName);
            options?.Signal?.ThrowIfAborted();
            var holder = await state.AcquireAsync(isShared, options?.IfAvailable == true, options?.Steal == true, options?.Signal).ConfigureAwait(false);

            if (holder == null)
            {
                return await callback.Invoke(null).ConfigureAwait(false);
            }

            try
            {
                return await callback.Invoke(new WebLock(scope, normalizedName, isShared ? SharedMode : ExclusiveMode)).ConfigureAwait(false);
            }
            finally
            {
                state.Release(holder);
            }
        }

        private static Boolean Validate(LockOptions options)
        {
            var mode = options?.Mode ?? ExclusiveMode;

            if (mode != ExclusiveMode && mode != SharedMode)
            {
                throw new ArgumentException($"The mode '{mode}' is not known.", nameof(options));
            }

            var isShared = mode == SharedMode;
            var isInvalid = options?.Steal == true && (options.IfAvailable == true || isShared || options.Signal != null) ||
                options?.IfAvailable == true && options.Signal != null;

            if (isInvalid)
            {
                throw new DomException(DomError.NotSupported);
            }

            return isShared;
        }

        internal static String GetOrigin(IWindow window)
        {
            var href = window?.Location?.Href;

            if (String.IsNullOrEmpty(href))
            {
                return String.Empty;
            }

            var url = new Url(href);

            if (url.IsInvalid || url.IsRelative)
            {
                return String.Empty;
            }

            return url.Origin ?? String.Empty;
        }
    }

    /// <summary>
    /// Represents a granted Web Lock.
    /// </summary>
    [DomName("Lock")]
    public sealed class WebLock
    {
        private readonly String _scope;

        internal WebLock(String scope, String name, String mode)
        {
            _scope = scope ?? String.Empty;
            Name = name ?? String.Empty;
            Mode = mode;
        }

        /// <summary>
        /// Gets the lock name.
        /// </summary>
        [DomName("name")]
        public String Name { get; }

        /// <summary>
        /// Gets the mode the lock is held in, i.e. exclusive or shared.
        /// </summary>
        [DomName("mode")]
        public String Mode { get; }

        internal String Scope => _scope;
    }

    /// <summary>
    /// Represents the options of a request for a Web Lock.
    /// </summary>
    [DomName("LockOptions")]
    public sealed class LockOptions
    {
        /// <summary>
        /// Gets or sets the mode, i.e. exclusive (the default) or shared.
        /// </summary>
        [DomName("mode")]
        public String Mode { get; set; }

        /// <summary>
        /// Gets or sets if the lock is only wanted if it can be held right away.
        /// </summary>
        [DomName("ifAvailable")]
        public Boolean? IfAvailable { get; set; }

        /// <summary>
        /// Gets or sets if the lock is taken from whoever holds it. Whatever they
        /// are doing goes on, but they do not hold the lock any more.
        /// </summary>
        [DomName("steal")]
        public Boolean? Steal { get; set; }

        /// <summary>
        /// Gets or sets the signal to stop waiting for the lock.
        /// </summary>
        [DomName("signal")]
        public AbortSignal Signal { get; set; }
    }

    internal static class LockRegistry
    {
        private static readonly ConcurrentDictionary<String, ConcurrentDictionary<String, LockState>> Scopes = new ConcurrentDictionary<String, ConcurrentDictionary<String, LockState>>(StringComparer.Ordinal);

        public static LockState GetState(String scope, String name)
        {
            var locks = Scopes.GetOrAdd(scope ?? String.Empty, _ => new ConcurrentDictionary<String, LockState>(StringComparer.Ordinal));
            return locks.GetOrAdd(name ?? String.Empty, _ => new LockState());
        }
    }

    /// <summary>
    /// Who holds a lock and who waits for it, in the order of their requests.
    /// </summary>
    internal sealed class LockState
    {
        private readonly List<Holder> _held = new List<Holder>();
        private readonly LinkedList<Holder> _waiting = new LinkedList<Holder>();

        public Task<Holder> AcquireAsync(Boolean isShared, Boolean ifAvailable, Boolean steal, AbortSignal signal)
        {
            var holder = new Holder(isShared);

            lock (_held)
            {
                if (steal)
                {
                    _held.Clear();
                    _waiting.AddFirst(holder);
                }
                else if (ifAvailable && (_waiting.Count > 0 || !CanHold(holder)))
                {
                    return Task.FromResult<Holder>(null);
                }
                else
                {
                    _waiting.AddLast(holder);
                }

                GrantNext();
            }

            if (signal == null || holder.Granted.IsCompleted)
            {
                return holder.Granted;
            }

            return WaitAsync(holder, signal);
        }

        public void Release(Holder holder)
        {
            lock (_held)
            {
                //  What was stolen has no lock to give back anymore.
                if (_held.Remove(holder))
                {
                    GrantNext();
                }
            }
        }

        private async Task<Holder> WaitAsync(Holder holder, AbortSignal signal)
        {
            Action abort = () =>
            {
                lock (_held)
                {
                    if (_waiting.Remove(holder))
                    {
                        holder.Fail(signal.CreateException());
                        GrantNext();
                    }
                }
            };

            signal.OnAbort(abort);

            try
            {
                return await holder.Granted.ConfigureAwait(false);
            }
            finally
            {
                signal.RemoveOnAbort(abort);
            }
        }

        private Boolean CanHold(Holder holder) =>
            _held.Count == 0 || holder.IsShared && _held.TrueForAll(m => m.IsShared);

        private void GrantNext()
        {
            while (_waiting.Count > 0 && CanHold(_waiting.First.Value))
            {
                var next = _waiting.First.Value;
                _waiting.RemoveFirst();
                _held.Add(next);
                next.Grant();
            }
        }

        internal sealed class Holder
        {
            private readonly TaskCompletionSource<Holder> _granted = new TaskCompletionSource<Holder>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Holder(Boolean isShared)
            {
                IsShared = isShared;
            }

            public Boolean IsShared { get; }

            public Task<Holder> Granted => _granted.Task;

            public void Grant() => _granted.TrySetResult(this);

            public void Fail(Exception error) => _granted.TrySetException(error);
        }
    }
}
