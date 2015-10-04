namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;

    /// <summary>
    /// Represents the state of the connection.
    /// </summary>
    [DomName("WebSocket")]
    public enum WebSocketReadyState : ushort
    {
        /// <summary>
        /// The connection has not yet been established.
        /// </summary>
        [DomName("CONNECTING")]
        Connecting = 0,
        /// <summary>
        /// The connection is established and communication is possible.
        /// </summary>
        [DomName("OPEN")]
        Open = 1,
        /// <summary>
        /// The connection is going through the closing handshake.
        /// </summary>
        [DomName("CLOSING")]
        Closing = 2,
        /// <summary>
        /// The connection has been closed or could not be opened.
        /// </summary>
        [DomName("CLOSED")]
        Closed = 3
    }
}
