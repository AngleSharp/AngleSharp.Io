namespace AngleSharp.Io.Dom
{
    /// <summary>
    /// Represents the storage views available for a single window.
    /// </summary>
    public sealed class StorageViews
    {
        /// <summary>
        /// Creates a new set of storage views.
        /// </summary>
        /// <param name="local">The local storage view.</param>
        /// <param name="session">The session storage view.</param>
        public StorageViews(ILocalStorage local, ISessionStorage session)
        {
            Local = local;
            Session = session;
        }

        /// <summary>
        /// Gets the local storage view.
        /// </summary>
        public ILocalStorage Local { get; }

        /// <summary>
        /// Gets the session storage view.
        /// </summary>
        public ISessionStorage Session { get; }
    }
}
