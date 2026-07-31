namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Io;
    using System;

    /// <summary>
    /// Represents a Cache object.
    /// </summary>
    [DomName("Cache")]
    [DomExposed("Window")]
    public interface ICache
    {
        /// <summary>
        /// Stores a response for the given request address.
        /// </summary>
        /// <param name="requestAddress">The request address.</param>
        /// <param name="response">The response to store.</param>
        [DomName("put")]
        void Put(String requestAddress, IResponse response);

        /// <summary>
        /// Retrieves the stored response for the given request address.
        /// </summary>
        /// <param name="requestAddress">The request address.</param>
        /// <returns>The stored response or null.</returns>
        [DomName("match")]
        IResponse Match(String requestAddress);

        /// <summary>
        /// Deletes the stored response for the given request address.
        /// </summary>
        /// <param name="requestAddress">The request address.</param>
        /// <returns>True if a response was removed.</returns>
        [DomName("delete")]
        Boolean Delete(String requestAddress);

        /// <summary>
        /// Gets the stored request addresses.
        /// </summary>
        /// <returns>The stored request addresses.</returns>
        [DomName("keys")]
        String[] Keys();

        /// <summary>
        /// Removes all stored responses.
        /// </summary>
        [DomName("clear")]
        void Clear();
    }
}