namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Io.PermissionsApi;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the StorageManager API.
    /// </summary>
    [DomName("StorageManager")]
    public sealed class StorageManager
    {
        private readonly IStorageManagerPlatform _platform;
        private readonly Func<String> _getOrigin;
        private readonly Permissions _permissions;

        /// <summary>
        /// Creates a new storage manager using the provided platform implementation.
        /// </summary>
        /// <param name="platform">The platform storage implementation.</param>
        /// <param name="getOrigin">The callback for resolving the current origin.</param>
        public StorageManager(IStorageManagerPlatform platform, Func<String> getOrigin)
            : this(platform, getOrigin, null)
        {
        }

        /// <summary>
        /// Creates a new storage manager using the provided platform
        /// implementation, which only persists the storage when the
        /// permissions allow it.
        /// </summary>
        /// <param name="platform">The platform storage implementation.</param>
        /// <param name="getOrigin">The callback for resolving the current origin.</param>
        /// <param name="permissions">The permissions to check, or null to allow every request.</param>
        public StorageManager(IStorageManagerPlatform platform, Func<String> getOrigin, Permissions permissions)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _getOrigin = getOrigin ?? throw new ArgumentNullException(nameof(getOrigin));
            _permissions = permissions;
        }

        /// <summary>
        /// Estimates the usage and quota of the storage.
        /// </summary>
        /// <returns>The storage estimate.</returns>
        [DomName("estimate")]
        public async Task<StorageEstimate> Estimate()
        {
            var origin = GetOrigin();
            var estimate = await _platform.EstimateAsync(origin).ConfigureAwait(false);
            return estimate ?? new StorageEstimate();
        }

        /// <summary>
        /// Requests to persist the storage, i.e., to exclude it from eviction.
        /// </summary>
        /// <returns>True if the storage is persisted, otherwise false.</returns>
        [DomName("persist")]
        public async Task<Boolean> Persist()
        {
            var origin = GetOrigin();

            // unlike other APIs a denied permission does not reject, the
            // spec just resolves with false
            if (_permissions != null && await _permissions.RequestAsync(PermissionNames.PersistentStorage).ConfigureAwait(false) != PermissionState.Granted)
            {
                return false;
            }

            return await _platform.PersistAsync(origin).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if the storage is persisted.
        /// </summary>
        /// <returns>True if the storage is persisted, otherwise false.</returns>
        [DomName("persisted")]
        public async Task<Boolean> Persisted()
        {
            var origin = GetOrigin();
            return await _platform.PersistedAsync(origin).ConfigureAwait(false);
        }

        private String GetOrigin()
        {
            var origin = _getOrigin.Invoke();

            // the spec rejects with a TypeError when no storage shelf can be
            // obtained, which is the case for opaque origins
            if (String.IsNullOrEmpty(origin))
            {
                throw new InvalidOperationException("The storage is not available for an opaque origin.");
            }

            return origin;
        }
    }

    /// <summary>
    /// Represents a storage estimate.
    /// </summary>
    [DomName("StorageEstimate")]
    public sealed class StorageEstimate
    {
        /// <summary>
        /// Gets or sets the used storage in bytes.
        /// </summary>
        [DomName("usage")]
        public UInt64 Usage { get; set; }

        /// <summary>
        /// Gets or sets the available storage in bytes.
        /// </summary>
        [DomName("quota")]
        public UInt64 Quota { get; set; }
    }
}
