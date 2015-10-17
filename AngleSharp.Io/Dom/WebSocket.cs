namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using System;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Represents the WebSocket interface. For more information see:
    /// http://www.w3.org/TR/2011/WD-websockets-20110419/#the-websocket-interface
    /// </summary>
    [DomName("WebSocket")]
    public class WebSocket : EventTarget, IDisposable
    {
        #region Fields

        readonly Url _url;
        readonly MemoryStream _buffered;
        String _protocol;
        WebSocketReadyState _state;

        #endregion

        #region Event Names

        static readonly String OpenEvent = "open";
        static readonly String CloseEvent = "close";
        static readonly String MessageEvent = "message";
        static readonly String ErrorEvent = "error";

        #endregion

        #region Events

        [DomName("onopen")]
        public event DomEventHandler Opened
        {
            add { AddEventListener(OpenEvent, value, false); }
            remove { RemoveEventListener(OpenEvent, value, false); }
        }

        [DomName("onmessage")]
        public event DomEventHandler Message
        {
            add { AddEventListener(MessageEvent, value, false); }
            remove { RemoveEventListener(MessageEvent, value, false); }
        }

        [DomName("onerror")]
        public event DomEventHandler Error
        {
            add { AddEventListener(ErrorEvent, value, false); }
            remove { RemoveEventListener(ErrorEvent, value, false); }
        }

        [DomName("onclose")]
        public event DomEventHandler Closed
        {
            add { AddEventListener(CloseEvent, value, false); }
            remove { RemoveEventListener(CloseEvent, value, false); }
        }

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new WebSocket instance.
        /// </summary>
        /// <param name="url">The URL to connect to.</param>
        /// <param name="protocols">The protocols to allow.</param>
        [DomConstructor]
        public WebSocket(String url, params String[] protocols)
        {
            _url = new Url(url);
            _protocol = String.Empty;
            _state = WebSocketReadyState.Connecting;
            _buffered = new MemoryStream();

            if (_url.IsInvalid || _url.IsRelative)
                throw new DomException(DomError.Syntax);

            var invalid = protocols.Length - protocols.Distinct().Where(IsValid).Count();

            if (invalid > 0)
                throw new DomException(DomError.Syntax);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the URL the connection is made to.
        /// </summary>
        [DomName("url")]
        public String Url
        {
            get { return _url.Href; }
        }

        /// <summary>
        /// Gets the current state of the connection.
        /// </summary>
        [DomName("readyState")]
        public WebSocketReadyState ReadyState
        {
            get { return _state; }
        }

        /// <summary>
        /// Gets the number of bytes of UTF-8 text that have been queued using
        /// Send() but that, as of the last time the event loop started
        /// executing a task, had not yet been transmitted to the network.
        /// </summary>
        [DomName("bufferedAmount")]
        public Int64 Buffered
        {
            get { return _buffered.Length; }
        }

        /// <summary>
        /// Gets the chosen protocol for the connection.
        /// </summary>
        [DomName("protocol")]
        public String Protocol
        {
            get { return _protocol; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Transmits data using the connection.
        /// </summary>
        /// <param name="data">The data to transmit.</param>
        [DomName("send")]
        public void Send(String data)
        {
            //TODO
        }

        /// <summary>
        /// Closes the WebSocket connection, if open. Otherwise aborts.
        /// </summary>
        [DomName("close")]
        public void Close()
        {
            //TODO
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        #endregion

        #region Helpers

        static Boolean IsValid(String protocol)
        {
            for (int i = 0; i < protocol.Length; i++)
            {
                if (protocol[i] < 0x21 || protocol[i] > 0x7e)
                    return false;
            }

            return true;
        }

        #endregion
    }
}
