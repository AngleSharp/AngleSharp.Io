namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using AngleSharp.Io.Extensions;
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the WebSocket interface. For more information see:
    /// http://www.w3.org/TR/2011/WD-websockets-20110419/#the-websocket-interface
    /// </summary>
    [DomName("WebSocket")]
    public class WebSocket : EventTarget, IDisposable
    {
        #region Fields

        const Int32 ReceiveChunkSize = 2048;
        const Int32 SendChunkSize = 1024;

        readonly Url _url;
        readonly CancellationTokenSource _cts;
        readonly ClientWebSocket _ws;

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
            _state = WebSocketReadyState.Connecting;
            _cts = new CancellationTokenSource();

            if (_url.IsInvalid || _url.IsRelative)
                throw new DomException(DomError.Syntax);

            var invalid = protocols.Length - protocols.Distinct().Where(IsValid).Count();

            if (invalid > 0)
                throw new DomException(DomError.Syntax);

            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            ConnectAsync(url).Forget();
        }

        async Task ConnectAsync(String url)
        {
            await _ws.ConnectAsync(new Uri(url), _cts.Token).ConfigureAwait(false);
            OnConnected();
            StartListen().Forget();
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
            get { return 0; }
        }

        /// <summary>
        /// Gets the chosen protocol for the connection.
        /// </summary>
        [DomName("protocol")]
        public String Protocol
        {
            get { return _ws.SubProtocol ?? String.Empty; }
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
            if (_state == WebSocketReadyState.Open)
            {
                SendAsync(data).Forget();
            } 
            else if (_state != WebSocketReadyState.Connecting)
            {
                throw new Exception("WebSocket is already in CLOSING or CLOSED state.");
            }
        }

        /// <summary>
        /// Closes the WebSocket connection, if open. Otherwise aborts.
        /// </summary>
        [DomName("close")]
        public void Close()
        {
            if (_state != WebSocketReadyState.Closed && _state != WebSocketReadyState.Closing)
            {
                _state = WebSocketReadyState.Closing;
                StopListen();
                OnDisconnected();
            }
        }

        void IDisposable.Dispose()
        {
            StopListen();
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

        async Task SendAsync(String message)
        {
            var messageBuffer = Encoding.UTF8.GetBytes(message);
            var remainder = 0;
            var messagesCount = Math.DivRem(messageBuffer.Length, SendChunkSize, out remainder);

            if (remainder > 0)
                messagesCount++;

            remainder = messageBuffer.Length;

            for (var i = 0; i < messagesCount; i++)
            {
                var offset = SendChunkSize * i;
                var lastMessage = (i + 1) == messagesCount;
                var count = lastMessage ? remainder : SendChunkSize;
                var segment = new ArraySegment<Byte>(messageBuffer, offset, count);
                await _ws.SendAsync(segment, WebSocketMessageType.Text, lastMessage, _cts.Token).ConfigureAwait(false);
                remainder -= SendChunkSize;
            }
        }

        async Task StartListen()
        {
            var buffer = new Byte[ReceiveChunkSize];
            var stringResult = new StringBuilder();

            try
            {
                while (_ws.State == WebSocketState.Open)
                {
                    var segment = new ArraySegment<Byte>(buffer);
                    var result = await _ws.ReceiveAsync(segment, _cts.Token).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, String.Empty, _cts.Token).ConfigureAwait(false);
                        OnDisconnected();
                        return;
                    }

                    stringResult.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        OnMessage(stringResult.ToString());
                        stringResult.Clear();
                    }
                }
            }
            catch
            {
                OnDisconnected();
            }
            finally
            {
                StopListen();
            }
        }

        void StopListen()
        {
            _cts.Cancel();
            _ws.Abort();
            _ws.Dispose();
        }

        void OnMessage(String message)
        {
            this.Dispatch(new MessageEvent(MessageEvent, data: message, origin: _url.Href));
        }

        void OnDisconnected()
        {
            _state = WebSocketReadyState.Closed;
            this.Dispatch(new Event(CloseEvent));
        }

        void OnConnected()
        {
            _state = WebSocketReadyState.Open;
            this.Dispatch(new Event(OpenEvent));
        }

        #endregion
    }
}
