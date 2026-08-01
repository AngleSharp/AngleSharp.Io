namespace AngleSharp.Io.Storage
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a handler for reading and writing storage data per origin.
    /// </summary>
    public interface IStorageHandler
    {
        /// <summary>
        /// Reads the storage entries for the given origin.
        /// </summary>
        /// <param name="origin">The origin to read.</param>
        /// <returns>The key-value pairs for the origin.</returns>
        IEnumerable<KeyValuePair<String, String>> Read(String origin);

        /// <summary>
        /// Writes the storage entries for the given origin.
        /// </summary>
        /// <param name="origin">The origin to write.</param>
        /// <param name="entries">The key-value pairs to write.</param>
        void Write(String origin, IEnumerable<KeyValuePair<String, String>> entries);
    }
}
