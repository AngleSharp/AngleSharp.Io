namespace AngleSharp.Io.Dom
{
    using AngleSharp;
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using System;
    using System.Collections.Concurrent;
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
        public Task Request(String name, Func<WebLock, Task> callback)
        {
            return RequestInternal(name, async lockInfo =>
            {
                await callback.Invoke(lockInfo).ConfigureAwait(false);
                return true;
            });
        }

        /// <summary>
        /// Requests a lock with the given name.
        /// </summary>
        /// <typeparam name="TResult">The callback result type.</typeparam>
        /// <param name="name">The lock name.</param>
        /// <param name="callback">The callback to run while the lock is held.</param>
        /// <returns>The task representing the lock request.</returns>
        [DomName("request")]
        public async Task<TResult> Request<TResult>(String name, Func<WebLock, Task<TResult>> callback)
        {
            return await RequestInternal(name, callback).ConfigureAwait(false);
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

        private async Task<TResult> RequestInternal<TResult>(String name, Func<WebLock, Task<TResult>> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var scope = _scope;
            var normalizedName = String.IsNullOrEmpty(name) ? String.Empty : name;
            var gate = LockRegistry.GetGate(scope, normalizedName);

            await gate.WaitAsync().ConfigureAwait(false);

            try
            {
                return await callback.Invoke(new WebLock(scope, normalizedName)).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        private static String GetOrigin(IWindow window)
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

        internal WebLock(String scope, String name)
        {
            _scope = scope ?? String.Empty;
            Name = name ?? String.Empty;
        }

        /// <summary>
        /// Gets the lock name.
        /// </summary>
        [DomName("name")]
        public String Name { get; }

        internal String Scope => _scope;
    }

    internal static class LockRegistry
    {
        private static readonly ConcurrentDictionary<String, ConcurrentDictionary<String, SemaphoreSlim>> Scopes = new ConcurrentDictionary<String, ConcurrentDictionary<String, SemaphoreSlim>>(StringComparer.Ordinal);

        public static SemaphoreSlim GetGate(String scope, String name)
        {
            var locks = Scopes.GetOrAdd(scope ?? String.Empty, _ => new ConcurrentDictionary<String, SemaphoreSlim>(StringComparer.Ordinal));
            return locks.GetOrAdd(name ?? String.Empty, _ => new SemaphoreSlim(1, 1));
        }
    }
}