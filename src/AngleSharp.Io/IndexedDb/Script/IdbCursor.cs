namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using System;

    /// <summary>
    /// Represents a cursor over the records of an object store or index. It is told where to go
    /// next, and its request gets another event when it got there.
    /// </summary>
    [DomName("IDBCursor")]
    [DomExposed("Window")]
    public class IdbCursor
    {
        private const String Next = "next";
        private const String NextUnique = "nextunique";
        private const String Prev = "prev";
        private const String PrevUnique = "prevunique";

        private readonly IdbObjectStore _store;
        private readonly IdbIndex _index;
        private readonly IdbKeyRange _range;
        private readonly Boolean _withValue;
        private IdbRecord _current;
        private Boolean _isWaiting = true;

        internal IdbCursor(IdbObjectStore store, IdbIndex index, IdbKeyRange range, String direction, Boolean withValue)
        {
            _store = store;
            _index = index;
            _range = range;
            _withValue = withValue;
            Direction = direction;
        }

        /// <summary>
        /// Gets the object store or index the cursor goes over.
        /// </summary>
        [DomName("source")]
        public Object Source => (Object)_index ?? _store;

        /// <summary>
        /// Gets the direction: next, nextunique, prev or prevunique.
        /// </summary>
        [DomName("direction")]
        public String Direction { get; }

        /// <summary>
        /// Gets the key the cursor is at.
        /// </summary>
        [DomName("key")]
        public Object Key => _current?.Key;

        /// <summary>
        /// Gets the primary key the cursor is at.
        /// </summary>
        [DomName("primaryKey")]
        public Object PrimaryKey => _current?.PrimaryKey;

        /// <summary>
        /// Gets the request of the cursor.
        /// </summary>
        [DomName("request")]
        public IdbRequest Request { get; private set; }

        private IWindow Window => _store.Transaction.Window;

        private IdbRecordList Records => _index != null ? _index.Data.Records : _store.Data.Records;

        /// <summary>
        /// Goes to the next record, or the next one at or past the key.
        /// </summary>
        /// <param name="key">The key to go to, if any.</param>
        [DomName("continue")]
        public void Continue(Object key = null)
        {
            EnsureMovable();
            var target = key != null ? IdbKeys.ToKey(IdbFactory.Clone(Window, key)) : null;

            if (target != null && _current != null)
            {
                var order = IdbKeys.Compare(target, _current.Key);

                if (IsForward ? order <= 0 : order >= 0)
                {
                    throw new DomException(IdbKeys.DataError);
                }
            }

            Move(() => FindNext(target, null));
        }

        /// <summary>
        /// Goes on to the record of the key and primary key, or the next one after them.
        /// </summary>
        /// <param name="key">The key to go to.</param>
        /// <param name="primaryKey">The primary key to go to.</param>
        [DomName("continuePrimaryKey")]
        public void ContinuePrimaryKey(Object key, Object primaryKey)
        {
            EnsureMovable();

            if (_index == null || Direction == NextUnique || Direction == PrevUnique)
            {
                throw new DomException(DomError.InvalidAccess);
            }

            var target = IdbKeys.ToKey(IdbFactory.Clone(Window, key));
            var targetPrimary = IdbKeys.ToKey(IdbFactory.Clone(Window, primaryKey));
            var order = _current != null ? IdbKeys.Compare(target, _current.Key) : 1;

            if (IsForward ? order < 0 : order > 0)
            {
                throw new DomException(IdbKeys.DataError);
            }

            if (order == 0)
            {
                var primaryOrder = IdbKeys.Compare(targetPrimary, _current.PrimaryKey);

                if (IsForward ? primaryOrder <= 0 : primaryOrder >= 0)
                {
                    throw new DomException(IdbKeys.DataError);
                }
            }

            Move(() => FindNext(target, targetPrimary));
        }

        /// <summary>
        /// Goes forward by a number of records.
        /// </summary>
        /// <param name="count">The number of records, at least 1.</param>
        [DomName("advance")]
        public void Advance(Double count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            EnsureMovable();
            Move(() =>
            {
                for (var i = 0; i < count && _current != null; i++)
                {
                    _current = FindNext(null, null);
                }

                return _current;
            });
        }

        /// <summary>
        /// Requests the record the cursor is at to be deleted.
        /// </summary>
        /// <returns>The request.</returns>
        [DomName("delete")]
        public IdbRequest Delete()
        {
            EnsureWritableValue();
            var primaryKey = _current.PrimaryKey;
            return _store.Transaction.Request(this, () =>
            {
                _store.Transaction.AddUndo(_store.RemoveRecords(IdbKeyRange.Only(null, primaryKey)));
                return IdbFactory.GetUndefined(Window);
            });
        }

        /// <summary>
        /// Requests the record the cursor is at to get a new value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The request, whose result is the key.</returns>
        [DomName("update")]
        public IdbRequest Update(Object value = null)
        {
            EnsureWritableValue();
            var clone = IdbFactory.Clone(Window, value);
            var primaryKey = _current.PrimaryKey;

            if (_store.Data.KeyPath != null &&
                (!IdbKeys.TryEvaluate(clone, _store.Data.KeyPath, out var found) ||
                 !IdbKeys.TryToKey(found, out var key) ||
                 IdbKeys.Compare(key, primaryKey) != 0))
            {
                throw new DomException(IdbKeys.DataError);
            }

            return _store.Transaction.Request(this, () => _store.Write(clone, primaryKey, false));
        }

        internal Object CurrentValue => _withValue && _current != null
            ? _store.Data.Records.Find(_current.PrimaryKey, _current.PrimaryKey)?.Value
            : null;

        internal static IdbRequest Open(IdbObjectStore store, IdbIndex index, IdbKeyRange range, String direction, Boolean withValue)
        {
            var kind = direction ?? Next;

            if (kind != Next && kind != NextUnique && kind != Prev && kind != PrevUnique)
            {
                throw new ArgumentException($"The direction '{kind}' can't be used.", nameof(direction));
            }

            var cursor = withValue
                ? new IdbCursorWithValue(store, index, range, kind)
                : new IdbCursor(store, index, range, kind, false);
            var request = new IdbRequest(store.Transaction.Window, (Object)index ?? store, store.Transaction);
            cursor.Request = request;
            store.Transaction.Enqueue(request, () => cursor.Arrive(cursor.FindNext(null, null)));
            return request;
        }

        private Boolean IsForward => Direction == Next || Direction == NextUnique;

        private Object Arrive(IdbRecord record)
        {
            _current = record;
            _isWaiting = false;
            return record != null ? this : null;
        }

        private void Move(Func<IdbRecord> find)
        {
            _isWaiting = true;
            Request.Reset();
            _store.Transaction.Enqueue(Request, () => Arrive(find.Invoke()));
        }

        private void EnsureMovable()
        {
            _store.EnsureActive();

            if (_isWaiting || _current == null)
            {
                throw new DomException(DomError.InvalidState);
            }
        }

        private void EnsureWritableValue()
        {
            _store.EnsureWritable();

            if (_isWaiting || _current == null || !_withValue)
            {
                throw new DomException(DomError.InvalidState);
            }
        }

        private IdbRecord FindNext(Object key, Object primaryKey)
        {
            var records = Records;
            records.GetBounds(_range, out var start, out var end);
            Int32 position;

            if (IsForward)
            {
                if (key != null)
                {
                    position = records.FindFirst(key, primaryKey);
                }
                else if (_current == null)
                {
                    position = start;
                }
                else if (Direction == NextUnique)
                {
                    position = records.FindAfter(_current.Key);
                }
                else
                {
                    position = records.FindAfter(_current.Key, _current.PrimaryKey);
                }

                position = Math.Max(position, start);
                return position < end ? records[position] : null;
            }

            if (key != null)
            {
                position = (primaryKey != null ? records.FindAfter(key, primaryKey) : records.FindAfter(key)) - 1;
            }
            else if (_current == null)
            {
                position = end - 1;
            }
            else if (Direction == PrevUnique)
            {
                position = records.FindFirst(_current.Key) - 1;
            }
            else
            {
                position = records.FindFirst(_current.Key, _current.PrimaryKey) - 1;
            }

            position = Math.Min(position, end - 1);

            if (position < start)
            {
                return null;
            }

            //  the same key once is the record the other direction would find first
            return Direction == PrevUnique ? records[Math.Max(records.FindFirst(records[position].Key), start)] : records[position];
        }
    }

    /// <summary>
    /// Represents a cursor that also gives the values of the records.
    /// </summary>
    [DomName("IDBCursorWithValue")]
    [DomExposed("Window")]
    public sealed class IdbCursorWithValue : IdbCursor
    {
        internal IdbCursorWithValue(IdbObjectStore store, IdbIndex index, IdbKeyRange range, String direction)
            : base(store, index, range, direction, true)
        {
        }

        /// <summary>
        /// Gets the value of the record the cursor is at.
        /// </summary>
        [DomName("value")]
        public Object Value => CurrentValue;
    }
}
