namespace AngleSharp.Io.Storage
{
    using AngleSharp.Io.Dom;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents an in-memory session storage.
    /// </summary>
    public class SessionStorage : ISessionStorage
    {
        private readonly Dictionary<String, String> _entries;
        private readonly List<String> _keys;

        /// <summary>
        /// Creates a new in-memory session storage.
        /// </summary>
        public SessionStorage()
        {
            _entries = new Dictionary<String, String>(StringComparer.Ordinal);
            _keys = new List<String>();
        }

        /// <inheritdoc />
        public virtual Int32 Length => _keys.Count;

        /// <inheritdoc />
        public virtual String Key(Int32 index)
        {
            if (index < 0 || index >= _keys.Count)
            {
                return null;
            }

            return _keys[index];
        }

        /// <inheritdoc />
        public virtual String this[String key]
        {
            get
            {
                if (String.IsNullOrEmpty(key))
                {
                    return null;
                }

                return _entries.TryGetValue(key, out var value) ? value : null;
            }
            set
            {
                if (String.IsNullOrEmpty(key))
                {
                    throw new ArgumentNullException(nameof(key));
                }

                if (!_entries.ContainsKey(key))
                {
                    _keys.Add(key);
                }

                _entries[key] = value;
            }
        }

        /// <inheritdoc />
        public virtual void Remove(String key)
        {
            if (String.IsNullOrEmpty(key))
            {
                return;
            }

            if (_entries.Remove(key))
            {
                _keys.Remove(key);
            }
        }

        /// <inheritdoc />
        public virtual void Clear()
        {
            _entries.Clear();
            _keys.Clear();
        }
    }
}
