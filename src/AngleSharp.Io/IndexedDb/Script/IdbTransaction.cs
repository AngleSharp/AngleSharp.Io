namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a transaction of IndexedDB. It takes requests while it is active, which is in
    /// the task creating it and while the event of a request of it is dispatched, including the
    /// promise jobs following that. It commits by itself once it has nothing left to do.
    /// </summary>
    [DomName("IDBTransaction")]
    [DomExposed("Window")]
    public sealed class IdbTransaction : EventTarget
    {
        internal const String ReadOnly = "readonly";
        internal const String ReadWrite = "readwrite";
        internal const String VersionChange = "versionchange";
        internal const String InactiveError = "TransactionInactiveError";

        private readonly Queue<KeyValuePair<IdbRequest, Func<Object>>> _operations = new Queue<KeyValuePair<IdbRequest, Func<Object>>>();
        private readonly Dictionary<String, IdbObjectStore> _stores = new Dictionary<String, IdbObjectStore>(StringComparer.Ordinal);
        private readonly List<Action> _undo = new List<Action>();
        private readonly HashSet<String> _scope;
        private readonly Action<Boolean> _finished;
        private Boolean _isActive = true;
        private Boolean _isCreating = true;
        private Boolean _isRunning;
        private Boolean _isFinished;
        private Exception _error;

        internal IdbTransaction(IdbDatabase database, String[] names, String mode, Action<Boolean> finished)
        {
            Database = database;
            Mode = mode;
            _scope = new HashSet<String>(names, StringComparer.Ordinal);
            _finished = finished;
            database.Add(this);

            if (mode == VersionChange)
            {
                //  alone by definition, and it stays inactive until it gets its event
                IsStarted = true;
                _isActive = false;
                _isCreating = false;
                database.Data.Add(this);
            }
            else
            {
                database.Data.Add(this);
                database.Scheduler.Post(EndCreation);
            }
        }

        /// <summary>
        /// Gets the connection of the transaction.
        /// </summary>
        [DomName("db")]
        public IdbDatabase Database { get; }

        /// <summary>
        /// Gets the mode: readonly, readwrite or versionchange.
        /// </summary>
        [DomName("mode")]
        public String Mode { get; }

        /// <summary>
        /// Gets why the transaction was aborted, if it was for a reason.
        /// </summary>
        [DomName("error")]
        public Object Error => _error;

        /// <summary>
        /// Gets the names of the object stores the transaction can reach.
        /// </summary>
        [DomName("objectStoreNames")]
        public IStringList ObjectStoreNames => new IdbStringList(_scope);

        /// <summary>
        /// Raised when the transaction is committed.
        /// </summary>
        [DomName("oncomplete")]
        public event DomEventHandler Completed
        {
            add { AddEventListener("complete", value); }
            remove { RemoveEventListener("complete", value); }
        }

        /// <summary>
        /// Raised when the transaction is aborted.
        /// </summary>
        [DomName("onabort")]
        public event DomEventHandler Aborted
        {
            add { AddEventListener("abort", value); }
            remove { RemoveEventListener("abort", value); }
        }

        /// <summary>
        /// Raised when a request of the transaction failed.
        /// </summary>
        [DomName("onerror")]
        public event DomEventHandler Failed
        {
            add { AddEventListener("error", value); }
            remove { RemoveEventListener("error", value); }
        }

        internal IWindow Window => Database.Window;

        internal IdbDatabaseData Data => Database.Data;

        internal Boolean IsActive => _isActive;

        internal Boolean IsStarted { get; private set; }

        internal Boolean IsWritable => Mode != ReadOnly;

        /// <summary>
        /// Gets an object store of the transaction.
        /// </summary>
        /// <param name="name">The name of the store.</param>
        /// <returns>The store.</returns>
        [DomName("objectStore")]
        public IdbObjectStore ObjectStore(String name)
        {
            if (_isFinished)
            {
                throw new DomException(DomError.InvalidState);
            }

            if (!_scope.Contains(name))
            {
                throw new DomException(DomError.NotFound);
            }

            return GetStore(name);
        }

        /// <summary>
        /// Aborts the transaction, undoing what it did.
        /// </summary>
        [DomName("abort")]
        public void Abort()
        {
            if (_isFinished)
            {
                throw new DomException(DomError.InvalidState);
            }

            Fail(null);
        }

        /// <summary>
        /// Commits the transaction when its requests are done, not taking any more.
        /// </summary>
        [DomName("commit")]
        public void Commit()
        {
            if (_isFinished || !_isActive)
            {
                throw new DomException(DomError.InvalidState);
            }

            _isActive = false;
            _isCreating = false;
            Next();
        }

        internal IdbObjectStore GetStore(String name)
        {
            if (!_stores.TryGetValue(name, out var store))
            {
                lock (Data)
                {
                    if (!Data.Stores.TryGetValue(name, out var data))
                    {
                        throw new DomException(DomError.NotFound);
                    }

                    store = new IdbObjectStore(this, data);
                }

                _stores[name] = store;
            }

            return store;
        }

        internal void AddToScope(String name) => _scope.Add(name);

        internal void RemoveFromScope(String name)
        {
            _scope.Remove(name);

            if (_stores.TryGetValue(name, out var store))
            {
                store.MarkDeleted();
                _stores.Remove(name);
            }
        }

        internal void AddUndo(Action undo) => _undo.Add(undo);

        internal Boolean ConflictsWith(IdbTransaction other)
        {
            if (!IsWritable && !other.IsWritable)
            {
                return false;
            }

            return _scope.Overlaps(other._scope);
        }

        internal void Start()
        {
            IsStarted = true;
            Database.Scheduler.Post(Next);
        }

        /// <summary>
        /// Makes a request of the transaction, whose operation runs when it is its turn.
        /// </summary>
        internal IdbRequest Request(Object source, Func<Object> operation)
        {
            var request = new IdbRequest(Window, source, this);
            Enqueue(request, operation);
            return request;
        }

        internal void Enqueue(IdbRequest request, Func<Object> operation)
        {
            _operations.Enqueue(new KeyValuePair<IdbRequest, Func<Object>>(request, operation));
            Next();
        }

        /// <summary>
        /// Lets script use the transaction while the action dispatches an event.
        /// </summary>
        internal void RunDuringEvent(Action dispatch)
        {
            _isActive = !_isFinished;

            try
            {
                dispatch.Invoke();
            }
            finally
            {
                _isActive = false;
            }

            Next();
        }

        internal void Fail(Exception error)
        {
            if (_isFinished)
            {
                return;
            }

            _isFinished = true;
            _isActive = false;
            _error = error;

            lock (Data)
            {
                for (var i = _undo.Count - 1; i >= 0; i--)
                {
                    _undo[i].Invoke();
                }
            }

            _undo.Clear();

            while (_operations.Count > 0)
            {
                var request = _operations.Dequeue().Key;
                Database.Scheduler.Post(() =>
                {
                    request.SetError(new DomException(DomError.Abort));
                    request.FireError();
                });
            }

            Database.Scheduler.Post(() => Finish(false, "abort"));
        }

        private void EndCreation()
        {
            _isCreating = false;
            _isActive = false;
            Next();
        }

        private void Next()
        {
            if (_isFinished || _isRunning || _isCreating && _operations.Count == 0)
            {
                return;
            }

            if (_operations.Count > 0)
            {
                if (IsStarted)
                {
                    _isRunning = true;
                    var operation = _operations.Dequeue();
                    Database.Scheduler.Post(() => Run(operation.Key, operation.Value));
                }
            }
            else if (!_isActive)
            {
                _isFinished = true;
                _undo.Clear();
                Database.Scheduler.Post(() => Finish(true, "complete"));
            }
        }

        private void Run(IdbRequest request, Func<Object> operation)
        {
            if (_isFinished)
            {
                _isRunning = false;
                request.SetError(new DomException(DomError.Abort));
                request.FireError();
                return;
            }

            Exception failure = null;

            try
            {
                lock (Data)
                {
                    request.SetResult(operation.Invoke());
                }
            }
            catch (DomException error)
            {
                failure = error;
                request.SetError(error);
            }

            var isHandled = false;
            _isActive = true;

            try
            {
                if (failure == null)
                {
                    request.Dispatch(new Event("success"));
                }
                else
                {
                    isHandled = request.FireError();
                }
            }
            finally
            {
                _isActive = false;
                _isRunning = false;
            }

            if (failure != null)
            {
                isHandled = PassOn("error", true, true) | isHandled;

                if (!isHandled)
                {
                    Fail(failure);
                    return;
                }
            }

            Next();
        }

        //  What happens to a request or transaction is told further up, as a new event: they have
        //  no tree for an event to bubble through.
        private Boolean PassOn(String type, Boolean bubbles, Boolean cancelable)
        {
            var own = new Event(type, bubbles, cancelable);
            this.Dispatch(own);
            var parent = new Event(type, bubbles, cancelable);
            Database.Dispatch(parent);
            return own.IsDefaultPrevented || parent.IsDefaultPrevented;
        }

        private void Finish(Boolean isCommitted, String type)
        {
            try
            {
                if (isCommitted)
                {
                    this.Dispatch(new Event(type));
                }
                else
                {
                    PassOn(type, true, false);
                }
            }
            finally
            {
                Data.Remove(this);
                Database.Remove(this);
                _finished?.Invoke(isCommitted);
            }
        }
    }
}
