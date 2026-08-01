namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;

    /// <summary>
    /// Represents an IndexedDB factory.
    /// </summary>
    [DomName("IDBFactory")]
    [DomExposed("Window")]
    public interface IIndexedDbFactory
    {
        /// <summary>
        /// Opens or creates the database with the given name.
        /// </summary>
        /// <param name="name">The database name.</param>
        /// <returns>The database instance.</returns>
        [DomName("open")]
        IIndexedDbDatabase Open(String name);

        /// <summary>
        /// Opens the database at the requested version.
        /// </summary>
        /// <param name="name">The database name.</param>
        /// <param name="version">The requested version (must be greater than 0).</param>
        /// <returns>The database instance.</returns>
        [DomName("open")]
        IIndexedDbDatabase Open(String name, Int64 version);

        /// <summary>
        /// Opens the database at the requested version and runs upgrade logic if needed.
        /// </summary>
        /// <param name="name">The database name.</param>
        /// <param name="version">The requested version (must be greater than 0).</param>
        /// <param name="upgrade">The callback used for upgrade changes.</param>
        /// <returns>The database instance.</returns>
        [DomName("open")]
        IIndexedDbDatabase Open(String name, Int64 version, Action<IIndexedDbUpgradeTransaction> upgrade);

        /// <summary>
        /// Opens or creates the database with the given name using a request wrapper.
        /// </summary>
        /// <param name="name">The database name.</param>
        /// <returns>The open request.</returns>
        [DomName("open")]
        IIndexedDbRequest<IIndexedDbDatabase> OpenRequest(String name);

        /// <summary>
        /// Opens the database at the requested version using a request wrapper.
        /// </summary>
        /// <param name="name">The database name.</param>
        /// <param name="version">The requested version (must be greater than 0).</param>
        /// <returns>The open request.</returns>
        [DomName("open")]
        IIndexedDbRequest<IIndexedDbDatabase> OpenRequest(String name, Int64 version);

        /// <summary>
        /// Opens the database at the requested version using a request wrapper and optional upgrade callback.
        /// </summary>
        /// <param name="name">The database name.</param>
        /// <param name="version">The requested version (must be greater than 0).</param>
        /// <param name="upgrade">The callback used for upgrade changes.</param>
        /// <returns>The open request.</returns>
        [DomName("open")]
        IIndexedDbRequest<IIndexedDbDatabase> OpenRequest(String name, Int64 version, Action<IIndexedDbUpgradeTransaction> upgrade);

        /// <summary>
        /// Opens the database at the requested version using a request wrapper and optional upgrade callback.
        /// If blocked by open connections, the request waits and retries automatically when connections close.
        /// </summary>
        /// <param name="name">The database name.</param>
        /// <param name="version">The requested version (must be greater than 0).</param>
        /// <param name="upgrade">The callback used for upgrade changes.</param>
        /// <param name="waitForUnblock">True to keep retrying when blocked until the request can proceed.</param>
        /// <returns>The open request.</returns>
        [DomName("open")]
        IIndexedDbRequest<IIndexedDbDatabase> OpenRequest(String name, Int64 version, Action<IIndexedDbUpgradeTransaction> upgrade, Boolean waitForUnblock);

        /// <summary>
        /// Deletes the database with the given name.
        /// </summary>
        /// <param name="name">The database name.</param>
        /// <returns>True if the database existed and was removed.</returns>
        [DomName("deleteDatabase")]
        Boolean DeleteDatabase(String name);

        /// <summary>
        /// Deletes the database with the given name using a request wrapper.
        /// </summary>
        /// <param name="name">The database name.</param>
        /// <returns>The delete request.</returns>
        [DomName("deleteDatabase")]
        IIndexedDbRequest<Boolean> DeleteDatabaseRequest(String name);

        /// <summary>
        /// Deletes the database with the given name using a request wrapper.
        /// If blocked by open connections, the request waits and retries automatically when connections close.
        /// </summary>
        /// <param name="name">The database name.</param>
        /// <param name="waitForUnblock">True to keep retrying when blocked until the delete can proceed.</param>
        /// <returns>The delete request.</returns>
        [DomName("deleteDatabase")]
        IIndexedDbRequest<Boolean> DeleteDatabaseRequest(String name, Boolean waitForUnblock);
    }
}