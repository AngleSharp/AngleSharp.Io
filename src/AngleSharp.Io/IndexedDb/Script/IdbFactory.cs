namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the indexedDB object of a window, which opens and deletes databases.
    /// </summary>
    [DomName("IDBFactory")]
    [DomExposed("Window")]
    public sealed class IdbFactory
    {
        private static readonly ConditionalWeakTable<IWindow, IdbFactory> Factories = new ConditionalWeakTable<IWindow, IdbFactory>();
        private static readonly ConditionalWeakTable<Object, IdbStorage> Storages = new ConditionalWeakTable<Object, IdbStorage>();

        private readonly IWindow _window;
        private readonly IdbStorage _storage;
        private readonly IdbScheduler _scheduler;

        private IdbFactory(IWindow window, IdbStorage storage)
        {
            _window = window;
            _storage = storage;
            _scheduler = new IdbScheduler(window);
        }

        /// <summary>
        /// Requests a connection to a database, creating or upgrading it if needed.
        /// </summary>
        /// <param name="name">The name of the database.</param>
        /// <param name="version">The version to have; by default the one it has.</param>
        /// <returns>The request, whose result is the connection.</returns>
        [DomName("open")]
        public IdbOpenRequest Open(String name, Double? version = null)
        {
            if (version.HasValue && (Double.IsNaN(version.Value) || version.Value < 1))
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            var request = new IdbOpenRequest(_window);
            var data = _storage.GetOrCreate(Origin, name ?? String.Empty);
            data.EnqueueConnecting(_scheduler, done => Connect(request, data, (Int64?)version, done));
            return request;
        }

        /// <summary>
        /// Requests a database to be deleted.
        /// </summary>
        /// <param name="name">The name of the database.</param>
        /// <returns>The request, which is done when the database is gone.</returns>
        [DomName("deleteDatabase")]
        public IdbOpenRequest DeleteDatabase(String name)
        {
            var request = new IdbOpenRequest(_window);
            var data = _storage.GetOrCreate(Origin, name ?? String.Empty);
            data.EnqueueConnecting(_scheduler, done => Delete(request, data, done));
            return request;
        }

        /// <summary>
        /// Gets the names and versions of the databases of the origin.
        /// </summary>
        /// <returns>The task giving a record for each database.</returns>
        [DomName("databases")]
        public Task<Object[]> Databases()
        {
            var records = _storage.GetAll(Origin).Select(m => (Object)new Dictionary<String, Object>
            {
                ["name"] = m.Name,
                ["version"] = (Double)m.Version,
            });
            return Task.FromResult(records.ToArray());
        }

        /// <summary>
        /// Compares two keys.
        /// </summary>
        /// <param name="first">The first key.</param>
        /// <param name="second">The second key.</param>
        /// <returns>-1, 0 or 1 for the first being before, the same as or after the second.</returns>
        [DomName("cmp")]
        public Int32 Cmp(Object first, Object second) =>
            Math.Sign(IdbKeys.Compare(IdbKeys.ToKey(Clone(_window, first)), IdbKeys.ToKey(Clone(_window, second))));

        internal static IdbFactory For(IWindow window)
        {
            var context = window?.Document?.Context;

            if (context == null)
            {
                return null;
            }

            //  the windows of a profile share it; without one the ones of a context do
            var owner = (Object)context.GetService<IIndexedDbProviderFactory>() ?? context;
            return Factories.GetValue(window, key => new IdbFactory(key, Storages.GetValue(owner, _ => new IdbStorage())));
        }

        internal static Object Clone(IWindow window, Object value)
        {
            var cloner = window?.Document?.Context?.GetService<IScriptValueCloner>();
            return cloner != null ? cloner.Clone(value) : value;
        }

        internal static Object GetUndefined(IWindow window) =>
            window?.Document?.Context?.GetService<IScriptValueCloner>()?.Undefined;

        private String Origin => LockManager.GetOrigin(_window);

        private void Connect(IdbOpenRequest request, IdbDatabaseData data, Int64? version, Action done)
        {
            var target = version ?? Math.Max(data.Version, 1);

            if (target < data.Version)
            {
                Fail(request, new DomException("VersionError"), done);
                return;
            }

            var connection = new IdbDatabase(_window, _scheduler, data);
            data.Add(connection);

            if (target == data.Version)
            {
                Succeed(request, connection, done);
                return;
            }

            var oldVersion = data.Version;

            foreach (var other in data.GetOpenConnections(connection))
            {
                other.NotifyVersionChange(oldVersion, target);
            }

            //  a task later, as that is when they had their chance of closing
            _scheduler.Post(() =>
            {
                if (data.GetOpenConnections(connection).Length > 0)
                {
                    request.Dispatch(new IdbVersionChangeEvent("blocked", oldVersion, target));
                }

                data.WhenOthersClosed(connection, () => _scheduler.Post(() => Upgrade(request, data, connection, oldVersion, target, done)));
            });
        }

        private void Upgrade(IdbOpenRequest request, IdbDatabaseData data, IdbDatabase connection, Int64 oldVersion, Int64 version, Action done)
        {
            lock (data)
            {
                data.Version = version;
            }

            var transaction = connection.BeginUpgrade(request, () => data.Version = oldVersion, isCommitted =>
            {
                connection.EndUpgrade();
                request.Transaction = null;

                if (isCommitted)
                {
                    Succeed(request, connection, done);
                }
                else
                {
                    connection.Close();
                    Fail(request, new DomException(DomError.Abort), done);
                }
            });
            request.Transaction = transaction;
            request.SetResult(connection);
            transaction.RunDuringEvent(() => request.Dispatch(new IdbVersionChangeEvent("upgradeneeded", oldVersion, version)));
        }

        private void Delete(IdbOpenRequest request, IdbDatabaseData data, Action done)
        {
            var oldVersion = data.Version;

            foreach (var other in data.GetOpenConnections(null))
            {
                other.NotifyVersionChange(oldVersion, null);
            }

            _scheduler.Post(() =>
            {
                if (data.GetOpenConnections(null).Length > 0)
                {
                    request.Dispatch(new IdbVersionChangeEvent("blocked", oldVersion, null));
                }

                data.WhenOthersClosed(null, () => _scheduler.Post(() =>
                {
                    data.Clear();
                    request.SetResult(GetUndefined(_window));
                    Finish(done, () => request.Dispatch(new IdbVersionChangeEvent("success", oldVersion, null)));
                }));
            });
        }

        private static void Succeed(IdbOpenRequest request, IdbDatabase connection, Action done)
        {
            request.SetResult(connection);
            Finish(done, () => request.Dispatch(new AngleSharp.Dom.Events.Event("success")));
        }

        private static void Fail(IdbOpenRequest request, Exception error, Action done)
        {
            request.SetError(error);
            Finish(done, () => request.FireError());
        }

        private static void Finish(Action done, Action dispatch)
        {
            try
            {
                dispatch.Invoke();
            }
            finally
            {
                done.Invoke();
            }
        }
    }
}
