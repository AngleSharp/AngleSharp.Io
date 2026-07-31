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
        /// Deletes the database with the given name.
        /// </summary>
        /// <param name="name">The database name.</param>
        /// <returns>True if the database existed and was removed.</returns>
        [DomName("deleteDatabase")]
        Boolean DeleteDatabase(String name);
    }
}