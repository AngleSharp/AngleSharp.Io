namespace AngleSharp.Io.Dom
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the platform storage implementation used by the DOM storage manager.
    /// </summary>
    public interface IStorageManagerPlatform
    {
        /// <summary>
        /// Estimates the usage and quota of the storage for the given origin.
        /// </summary>
        /// <param name="origin">The origin owning the storage.</param>
        /// <returns>The storage estimate.</returns>
        Task<StorageEstimate> EstimateAsync(String origin);

        /// <summary>
        /// Requests to persist the storage of the given origin.
        /// </summary>
        /// <param name="origin">The origin owning the storage.</param>
        /// <returns>True if the storage is persisted after the request, otherwise false.</returns>
        Task<Boolean> PersistAsync(String origin);

        /// <summary>
        /// Checks if the storage of the given origin is persisted.
        /// </summary>
        /// <param name="origin">The origin owning the storage.</param>
        /// <returns>True if the storage is persisted, otherwise false.</returns>
        Task<Boolean> PersistedAsync(String origin);
    }
}
