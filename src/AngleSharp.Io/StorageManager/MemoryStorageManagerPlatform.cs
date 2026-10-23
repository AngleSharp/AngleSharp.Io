namespace AngleSharp.Io.StorageManagerApi
{
    using AngleSharp.Io.Dom;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a storage manager platform with fixed values and the
    /// persistence state kept in memory, e.g., for deterministic tests.
    /// </summary>
    public sealed class MemoryStorageManagerPlatform : IStorageManagerPlatform
    {
        private readonly HashSet<String> _persisted;

        /// <summary>
        /// Creates a new memory storage manager platform.
        /// </summary>
        public MemoryStorageManagerPlatform()
        {
            _persisted = new HashSet<String>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Gets or sets the reported quota in bytes.
        /// </summary>
        public UInt64 Quota { get; set; }

        /// <summary>
        /// Gets or sets the reported usage in bytes.
        /// </summary>
        public UInt64 Usage { get; set; }

        /// <summary>
        /// Gets or sets if requests to persist the storage are granted. By
        /// default, they are granted.
        /// </summary>
        public Boolean AllowPersistence { get; set; } = true;

        /// <inheritdoc />
        public Task<StorageEstimate> EstimateAsync(String origin)
        {
            return Task.FromResult(new StorageEstimate
            {
                Usage = Usage,
                Quota = Quota,
            });
        }

        /// <inheritdoc />
        public Task<Boolean> PersistAsync(String origin)
        {
            lock (_persisted)
            {
                if (AllowPersistence)
                {
                    _persisted.Add(origin);
                }

                return Task.FromResult(_persisted.Contains(origin));
            }
        }

        /// <inheritdoc />
        public Task<Boolean> PersistedAsync(String origin)
        {
            lock (_persisted)
            {
                return Task.FromResult(_persisted.Contains(origin));
            }
        }
    }
}
