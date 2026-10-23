namespace AngleSharp.Io.Http
{
    /// <summary>
    /// The modes of the HTTP cache. The names follow the RequestCache values
    /// of the Fetch specification.
    /// </summary>
    public enum HttpCacheMode
    {
        /// <summary>
        /// Uses fresh responses from the cache, revalidates stale ones, and
        /// stores new responses.
        /// </summary>
        Default,
        /// <summary>
        /// Bypasses the cache completely, i.e., neither reads nor writes.
        /// </summary>
        NoStore,
        /// <summary>
        /// Always goes to the network, but stores the response.
        /// </summary>
        Reload,
        /// <summary>
        /// Always revalidates stored responses with the server.
        /// </summary>
        NoCache,
        /// <summary>
        /// Uses any stored response, even if stale. Goes to the network only
        /// if nothing is stored.
        /// </summary>
        ForceCache,
        /// <summary>
        /// Uses any stored response, even if stale. Never goes to the network;
        /// answers with a 504 if nothing is stored.
        /// </summary>
        OnlyIfCached,
    }
}
