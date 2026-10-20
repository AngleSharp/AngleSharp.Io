namespace AngleSharp.Io.WebRtc
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the options of a data channel.
    /// </summary>
    [DomName("RTCDataChannelInit")]
    public sealed class RtcDataChannelInit
    {
        /// <summary>
        /// Gets or sets if the messages are delivered in order.
        /// </summary>
        [DomName("ordered")]
        public Boolean Ordered { get; set; } = true;

        /// <summary>
        /// Gets or sets the time in milliseconds a message may be retransmitted.
        /// </summary>
        [DomName("maxPacketLifeTime")]
        public UInt16? MaxPacketLifeTime { get; set; }

        /// <summary>
        /// Gets or sets how often a message may be retransmitted.
        /// </summary>
        [DomName("maxRetransmits")]
        public UInt16? MaxRetransmits { get; set; }

        /// <summary>
        /// Gets or sets the subprotocol of the channel.
        /// </summary>
        [DomName("protocol")]
        public String Protocol { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets if the channel is negotiated by the application, i.e.,
        /// created on both peers with the same id instead of announced.
        /// </summary>
        [DomName("negotiated")]
        public Boolean Negotiated { get; set; }

        /// <summary>
        /// Gets or sets the id of the channel, required if negotiated.
        /// </summary>
        [DomName("id")]
        public UInt16? Id { get; set; }
    }

    /// <summary>
    /// Represents a data channel of a peer connection. Messages are
    /// delivered by the platform; reliability parameters are informational.
    /// </summary>
    [DomName("RTCDataChannel")]
    public sealed class RtcDataChannel : EventTarget
    {
        private readonly RtcPeerConnection _connection;
        private readonly Queue<Object> _early;
        private RtcDataChannelState _readyState;
        private String _binaryType;

        internal RtcDataChannel(RtcPeerConnection connection, String label, RtcDataChannelInit options)
        {
            _connection = connection;
            _early = new Queue<Object>();
            _readyState = RtcDataChannelState.Connecting;
            _binaryType = "arraybuffer";
            Label = label;
            Ordered = options.Ordered;
            MaxPacketLifeTime = options.MaxPacketLifeTime;
            MaxRetransmits = options.MaxRetransmits;
            Protocol = options.Protocol ?? String.Empty;
            Negotiated = options.Negotiated;
            Id = options.Id;
        }

        /// <summary>
        /// Gets the label of the channel.
        /// </summary>
        [DomName("label")]
        public String Label { get; }

        /// <summary>
        /// Gets if the messages are delivered in order.
        /// </summary>
        [DomName("ordered")]
        public Boolean Ordered { get; }

        /// <summary>
        /// Gets the time in milliseconds a message may be retransmitted.
        /// </summary>
        [DomName("maxPacketLifeTime")]
        public UInt16? MaxPacketLifeTime { get; }

        /// <summary>
        /// Gets how often a message may be retransmitted.
        /// </summary>
        [DomName("maxRetransmits")]
        public UInt16? MaxRetransmits { get; }

        /// <summary>
        /// Gets the subprotocol of the channel.
        /// </summary>
        [DomName("protocol")]
        public String Protocol { get; }

        /// <summary>
        /// Gets if the channel is negotiated by the application.
        /// </summary>
        [DomName("negotiated")]
        public Boolean Negotiated { get; }

        /// <summary>
        /// Gets the id of the channel, which is known once it is open unless
        /// it was given.
        /// </summary>
        [DomName("id")]
        public UInt16? Id { get; private set; }

        /// <summary>
        /// Gets the state of the channel.
        /// </summary>
        [DomName("readyState")]
        public RtcDataChannelState ReadyState => _readyState;

        /// <summary>
        /// Gets the number of queued bytes, which is always 0 as messages are
        /// handed to the platform right away.
        /// </summary>
        [DomName("bufferedAmount")]
        public UInt32 BufferedAmount => 0;

        /// <summary>
        /// Gets or sets the threshold for the bufferedamountlow event.
        /// </summary>
        [DomName("bufferedAmountLowThreshold")]
        public UInt32 BufferedAmountLowThreshold { get; set; }

        /// <summary>
        /// Gets or sets the type of binary messages. Binary messages are
        /// always delivered as byte arrays, i.e., array buffers.
        /// </summary>
        [DomName("binaryType")]
        public String BinaryType
        {
            get => _binaryType;
            set
            {
                if (value == "arraybuffer" || value == "blob")
                {
                    _binaryType = value;
                }
            }
        }

        /// <summary>
        /// Adds or removes the handler for the open event.
        /// </summary>
        [DomName("onopen")]
        public event DomEventHandler Opened
        {
            add { AddEventListener(RtcEventNames.Open, value, false); }
            remove { RemoveEventListener(RtcEventNames.Open, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the message event.
        /// </summary>
        [DomName("onmessage")]
        public event DomEventHandler Message
        {
            add { AddEventListener(RtcEventNames.Message, value, false); }
            remove { RemoveEventListener(RtcEventNames.Message, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the closing event.
        /// </summary>
        [DomName("onclosing")]
        public event DomEventHandler Closing
        {
            add { AddEventListener(RtcEventNames.Closing, value, false); }
            remove { RemoveEventListener(RtcEventNames.Closing, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the close event.
        /// </summary>
        [DomName("onclose")]
        public event DomEventHandler Closed
        {
            add { AddEventListener(RtcEventNames.Close, value, false); }
            remove { RemoveEventListener(RtcEventNames.Close, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the error event.
        /// </summary>
        [DomName("onerror")]
        public event DomEventHandler Error
        {
            add { AddEventListener(RtcEventNames.Error, value, false); }
            remove { RemoveEventListener(RtcEventNames.Error, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the bufferedamountlow event.
        /// </summary>
        [DomName("onbufferedamountlow")]
        public event DomEventHandler BufferedAmountLow
        {
            add { AddEventListener(RtcEventNames.BufferedAmountLow, value, false); }
            remove { RemoveEventListener(RtcEventNames.BufferedAmountLow, value, false); }
        }

        /// <summary>
        /// Sends a text message.
        /// </summary>
        /// <param name="data">The message to send.</param>
        [DomName("send")]
        public void Send(String data) => SendCore(data ?? String.Empty);

        /// <summary>
        /// Sends a binary message.
        /// </summary>
        /// <param name="data">The message to send.</param>
        [DomName("send")]
        public void Send(Byte[] data) => SendCore((Byte[])(data ?? Array.Empty<Byte>()).Clone());

        /// <summary>
        /// Closes the channel on both peers.
        /// </summary>
        [DomName("close")]
        public void Close()
        {
            if (_readyState == RtcDataChannelState.Closing || _readyState == RtcDataChannelState.Closed)
            {
                return;
            }

            _readyState = RtcDataChannelState.Closing;
            _connection.Peer?.CloseDataChannel(this);
        }

        internal void Open(UInt16 id)
        {
            if (_readyState == RtcDataChannelState.Connecting)
            {
                Id = id;
                _readyState = RtcDataChannelState.Open;
                Dispatch(new Event(RtcEventNames.Open));
                DeliverEarlyMessages();
            }
        }

        internal void Announce()
        {
            // a remote channel is already open when the datachannel event
            // announces it, so it has no open event
            if (_readyState == RtcDataChannelState.Connecting)
            {
                _readyState = RtcDataChannelState.Open;
            }
        }

        internal void Receive(Object data)
        {
            // the remote peer may send right after its channel opened, i.e.,
            // before the open or datachannel event of this channel ran
            if (_readyState == RtcDataChannelState.Connecting)
            {
                _early.Enqueue(data);
            }
            else if (_readyState == RtcDataChannelState.Open)
            {
                Dispatch(new MessageEvent(RtcEventNames.Message, false, false, data, String.Empty, String.Empty, null, Array.Empty<IMessagePort>()));
            }
        }

        internal void DeliverEarlyMessages()
        {
            while (_early.Count > 0)
            {
                Receive(_early.Dequeue());
            }
        }

        internal void Finish()
        {
            if (_readyState == RtcDataChannelState.Closed)
            {
                return;
            }

            // a channel closed by the remote peer or the transport passes the
            // closing state with an event, a local close already set it
            if (_readyState != RtcDataChannelState.Closing)
            {
                _readyState = RtcDataChannelState.Closing;
                Dispatch(new Event(RtcEventNames.Closing));
            }

            _readyState = RtcDataChannelState.Closed;
            Dispatch(new Event(RtcEventNames.Close));
        }

        internal void CloseSilently()
        {
            _early.Clear();
            _readyState = RtcDataChannelState.Closed;
        }

        private void SendCore(Object data)
        {
            if (_readyState != RtcDataChannelState.Open)
            {
                throw new DomException(DomError.InvalidState);
            }

            _connection.Peer?.Send(this, data);
        }
    }
}
