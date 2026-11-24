namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
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

        private const Int32 ReceiveChunkSize = 2048;
        private const Int32 SendChunkSize = 1024;

        private readonly Url _url;
        private readonly CancellationTokenSource _cts;
        private readonly ClientWebSocket _ws;
        private readonly IWindow _window;

        private WebSocketReadyState _state;
        private BinaryType _binaryType;

        #endregion

        #region Event Names

        /// <summary>
        /// The open event name.
        /// </summary>
        public static readonly String OpenEvent = "open";

        /// <summary>
        /// The close event name.
        /// </summary>
        public static readonly String CloseEvent = "close";

        #endregion

        #region Events

        /// <summary>
        /// Adds or removes the handler for the open event.
        /// </summary>
        [DomName("onopen")]
        public event DomEventHandler Opened
        {
            add { AddEventListener(OpenEvent, value, false); }
            remove { RemoveEventListener(OpenEvent, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the message event.
        /// </summary>
        [DomName("onmessage")]
        public event DomEventHandler Message
        {
            add { AddEventListener(EventNames.Message, value, false); }
            remove { RemoveEventListener(EventNames.Message, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the error event.
        /// </summary>
        [DomName("onerror")]
        public event DomEventHandler Error
        {
            add { AddEventListener(EventNames.Error, value, false); }
            remove { RemoveEventListener(EventNames.Error, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the close event.
        /// </summary>
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
        /// <param name="window">The parent context.</param>
        /// <param name="url">The URL to connect to.</param>
        /// <param name="protocols">The protocols to allow.</param>
        [DomConstructor]
        public WebSocket(IWindow window, String url, params String[] protocols)
        {
            _url = new Url(url);
            _state = WebSocketReadyState.Connecting;
            _cts = new CancellationTokenSource();
            _window = window;

            if (_url.IsInvalid || _url.IsRelative)
            {
                throw new DomException(DomError.Syntax);
            }

            var invalid = protocols.Length - protocols.Distinct().Where(IsValid).Count();

            if (invalid > 0)
            {
                throw new DomException(DomError.Syntax);
            }

            _ws = new ClientWebSocket();

            foreach (var protocol in protocols)
            {
                _ws.Options.AddSubProtocol(protocol);
            }

            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            ConnectAsync(url).Forget();
            _window.Unloaded += OnUnload;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the URL the connection is made to.
        /// </summary>
        [DomName("url")]
        public String Url => _url.Href;

        /// <summary>
        /// Gets the current state of the connection.
        /// </summary>
        [DomName("readyState")]
        public WebSocketReadyState ReadyState => _state;

        /// <summary>
        /// Gets the number of bytes of UTF-8 text that have been queued using
        /// Send() but that, as of the last time the event loop started
        /// executing a task, had not yet been transmitted to the network.
        /// </summary>
        [DomName("bufferedAmount")]
        public Int64 Buffered => 0;

        /// <summary>
        /// Gets the chosen protocol for the connection.
        /// </summary>
        [DomName("protocol")]
        public String Protocol => _ws.SubProtocol ?? String.Empty;

        /// <summary>
        /// Gets or sets the type binary messages are given as: blobs (the default) or
        /// array buffers.
        /// </summary>
        [DomName("binaryType")]
        public BinaryType BinaryType
        {
            get => _binaryType;
            set => _binaryType = value;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Transmits data using the connection.
        /// </summary>
        /// <param name="data">The data to transmit.</param>
        [DomName("send")]
        public void Send(String data) =>
            Send(Encoding.UTF8.GetBytes(data ?? String.Empty), WebSocketMessageType.Text);

        /// <summary>
        /// Transmits binary data (an ArrayBuffer) using the connection.
        /// </summary>
        /// <param name="data">The data to transmit.</param>
        [DomName("send")]
        public void Send(Byte[] data) =>
            Send(data ?? Array.Empty<Byte>(), WebSocketMessageType.Binary);

        /// <summary>
        /// Transmits the contents of a blob using the connection.
        /// </summary>
        /// <param name="data">The data to transmit.</param>
        [DomName("send")]
        public void Send(Blob data) =>
            Send(data?.GetBytes() ?? Array.Empty<Byte>(), WebSocketMessageType.Binary);

        private void Send(Byte[] data, WebSocketMessageType type)
        {
            if (_state == WebSocketReadyState.Open)
            {
                SendAsync(data, type).Forget();
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
                CloseAsync().Forget();
            }
        }

        void IDisposable.Dispose()
        {
            CancelListener();
            _ws.Dispose();
        }

        #endregion

        #region Helpers

        private async Task ConnectAsync(String url)
        {
            try
            {
                await _ws.ConnectAsync(new Uri(url), _cts.Token).ConfigureAwait(false);
                _state = WebSocketReadyState.Open;
                OnConnected();
                ListenAsync().Forget();
            }
            catch (Exception ex)
            {
                _state = WebSocketReadyState.Closed;
                OnError(ex);
            }
        }

        private void OnUnload(Object sender, Event ev)
        {
            RemoveEventListeners();

            if (_state != WebSocketReadyState.Closed && _state != WebSocketReadyState.Closing)
            {
                CloseAsync().Wait();
                _ws.Dispose();
            }
        }

        private static Boolean IsValid(String protocol)
        {
            for (var i = 0; i < protocol.Length; i++)
            {
                if (protocol[i] < 0x21 || protocol[i] > 0x7e)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task SendAsync(Byte[] messageBuffer, WebSocketMessageType type)
        {
            var remainder = 0;
            var messagesCount = Math.DivRem(messageBuffer.Length, SendChunkSize, out remainder);

            if (remainder > 0)
            {
                messagesCount++;
            }

            remainder = messageBuffer.Length;

            for (var i = 0; i < messagesCount; i++)
            {
                var offset = SendChunkSize * i;
                var lastMessage = (i + 1) == messagesCount;
                var count = lastMessage ? remainder : SendChunkSize;
                var segment = new ArraySegment<Byte>(messageBuffer, offset, count);
                await _ws.SendAsync(segment, type, lastMessage, _cts.Token).ConfigureAwait(false);
                remainder -= SendChunkSize;
            }
        }

        private async Task CloseAsync()
        {
            _state = WebSocketReadyState.Closing;
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, String.Empty, _cts.Token).ConfigureAwait(false);
            CancelListener();
            OnDisconnected();
        }

        private async Task ListenAsync()
        {
            var buffer = new Byte[ReceiveChunkSize];
            var message = new MemoryStream();

            try
            {
                while (_ws.State == WebSocketState.Open)
                {
                    var segment = new ArraySegment<Byte>(buffer);
                    var result = await _ws.ReceiveAsync(segment, _cts.Token).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await CloseAsync().ConfigureAwait(false);
                        break;
                    }

                    message.Write(buffer, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        var bytes = message.ToArray();
                        message.SetLength(0);

                        if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            OnMessage(_binaryType == BinaryType.ArrayBuffer ? bytes : new Blob(bytes));
                        }
                        else
                        {
                            OnMessage(Encoding.UTF8.GetString(bytes));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnError(ex);
                CancelListener();
                OnDisconnected();
            }
        }

        private void CancelListener()
        {
            _cts.Cancel();
            _ws.Abort();
            _state = WebSocketReadyState.Closed;
        }

        private void OnMessage(Object message)
        {
            var evt = new MessageEvent();
            evt.Init(EventNames.Message, false, false, message, _url.Origin, String.Empty, _window);
            Dispatch(evt);
        }

        private void OnError(Exception ex)
        {
            var evt = new ErrorEvent();
            evt.Init(null, 0, 0, ex);
            evt.Init(EventNames.Error, false, false);
            Dispatch(evt);
        }

        private void OnDisconnected()
        {
            var evt = new Event();
            evt.Init(CloseEvent, false, false);
            Dispatch(evt);
        }

        private void OnConnected()
        {
            var evt = new Event();
            evt.Init(OpenEvent, false, false);
            Dispatch(evt);
        }

        #endregion
    }
}
