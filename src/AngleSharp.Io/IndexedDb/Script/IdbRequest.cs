namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Runs what IndexedDB does in tasks of the event loop of the window, as events have to reach
    /// script there. Without an event loop they run in the same order on some other thread, but
    /// then nothing tells when the task of a script ended, so that a transaction may end early.
    /// </summary>
    sealed class IdbScheduler
    {
        private readonly IWindow _window;
        private readonly Object _sync = new Object();
        private Task _last = Task.CompletedTask;

        public IdbScheduler(IWindow window)
        {
            _window = window;
        }

        public void Post(Action action)
        {
            var loop = _window.Document?.Context?.GetService<AngleSharp.Browser.IEventLoop>();

            if (loop != null)
            {
                loop.Enqueue(_ => action.Invoke(), AngleSharp.Browser.TaskPriority.Normal);
            }
            else
            {
                lock (_sync)
                {
                    _last = _last.ContinueWith(_ => action.Invoke(), TaskScheduler.Default);
                }
            }
        }
    }

    /// <summary>
    /// Represents a request of IndexedDB, telling about its end by an event.
    /// </summary>
    [DomName("IDBRequest")]
    [DomExposed("Window")]
    public class IdbRequest : EventTarget
    {
        private Object _result;
        private Exception _error;
        private Boolean _isDone;

        internal IdbRequest(IWindow window, Object source, IdbTransaction transaction)
        {
            Window = window;
            Source = source;
            Transaction = transaction;
        }

        internal IWindow Window { get; }

        /// <summary>
        /// Gets the result, which is only there when the request is done.
        /// </summary>
        [DomName("result")]
        public Object Result => _isDone ? _result : throw new DomException(DomError.InvalidState);

        /// <summary>
        /// Gets what went wrong, if anything, which is only there when the request is done.
        /// </summary>
        [DomName("error")]
        public Object Error => _isDone ? _error : throw new DomException(DomError.InvalidState);

        /// <summary>
        /// Gets what the request was made on: an object store, index or cursor, or nothing.
        /// </summary>
        [DomName("source")]
        public Object Source { get; internal set; }

        /// <summary>
        /// Gets the transaction of the request, if any.
        /// </summary>
        [DomName("transaction")]
        public IdbTransaction Transaction { get; internal set; }

        /// <summary>
        /// Gets the state, i.e. pending or done.
        /// </summary>
        [DomName("readyState")]
        public String ReadyState => _isDone ? "done" : "pending";

        /// <summary>
        /// Raised when the request succeeded.
        /// </summary>
        [DomName("onsuccess")]
        public event DomEventHandler Succeeded
        {
            add { AddEventListener("success", value); }
            remove { RemoveEventListener("success", value); }
        }

        /// <summary>
        /// Raised when the request failed.
        /// </summary>
        [DomName("onerror")]
        public event DomEventHandler Failed
        {
            add { AddEventListener("error", value); }
            remove { RemoveEventListener("error", value); }
        }

        internal Exception Exception => _error;

        internal void Reset() => _isDone = false;

        internal void SetResult(Object result)
        {
            _result = result;
            _error = null;
            _isDone = true;
        }

        internal void SetError(Exception error)
        {
            _result = IdbFactory.GetUndefined(Window);
            _error = error;
            _isDone = true;
        }

        internal Boolean FireError()
        {
            var ev = new Event("error", true, true);
            this.Dispatch(ev);
            return ev.IsDefaultPrevented;
        }
    }

    /// <summary>
    /// Represents the request of opening or deleting a database.
    /// </summary>
    [DomName("IDBOpenDBRequest")]
    [DomExposed("Window")]
    public sealed class IdbOpenRequest : IdbRequest
    {
        internal IdbOpenRequest(IWindow window)
            : base(window, null, null)
        {
        }

        /// <summary>
        /// Raised when other connections keep the request waiting.
        /// </summary>
        [DomName("onblocked")]
        public event DomEventHandler Blocked
        {
            add { AddEventListener("blocked", value); }
            remove { RemoveEventListener("blocked", value); }
        }

        /// <summary>
        /// Raised when the database has to be brought to a newer version.
        /// </summary>
        [DomName("onupgradeneeded")]
        public event DomEventHandler UpgradeNeeded
        {
            add { AddEventListener("upgradeneeded", value); }
            remove { RemoveEventListener("upgradeneeded", value); }
        }
    }

    /// <summary>
    /// Represents the event of the version of a database changing.
    /// </summary>
    [DomName("IDBVersionChangeEvent")]
    [DomExposed("Window")]
    public sealed class IdbVersionChangeEvent : Event
    {
        internal IdbVersionChangeEvent(String type, Int64 oldVersion, Int64? newVersion)
            : base(type)
        {
            OldVersion = oldVersion;
            NewVersion = newVersion;
        }

        /// <summary>
        /// Gets the version the database has had.
        /// </summary>
        [DomName("oldVersion")]
        public Int64 OldVersion { get; }

        /// <summary>
        /// Gets the version the database gets; none if it is deleted.
        /// </summary>
        [DomName("newVersion")]
        public Int64? NewVersion { get; }
    }
}
