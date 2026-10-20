namespace AngleSharp.Io.WebRtc
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom.Events;
    using System;

    /// <summary>
    /// Represents the event surfacing a gathered ICE candidate.
    /// </summary>
    [DomName("RTCPeerConnectionIceEvent")]
    public sealed class RtcPeerConnectionIceEvent : Event
    {
        /// <summary>
        /// Creates a new ICE candidate event.
        /// </summary>
        /// <param name="type">The type of the event.</param>
        /// <param name="candidate">The candidate, or null at the end of the candidates.</param>
        /// <param name="url">The URL of the server that found the candidate.</param>
        public RtcPeerConnectionIceEvent(String type, RtcIceCandidate candidate, String url = null)
            : base(type)
        {
            Candidate = candidate;
            Url = url;
        }

        /// <summary>
        /// Gets the candidate, or null at the end of the candidates.
        /// </summary>
        [DomName("candidate")]
        public RtcIceCandidate Candidate { get; }

        /// <summary>
        /// Gets the URL of the server that found the candidate, if any.
        /// </summary>
        [DomName("url")]
        public String Url { get; }
    }

    /// <summary>
    /// Represents the event announcing a data channel opened by the remote
    /// peer.
    /// </summary>
    [DomName("RTCDataChannelEvent")]
    public sealed class RtcDataChannelEvent : Event
    {
        /// <summary>
        /// Creates a new data channel event.
        /// </summary>
        /// <param name="type">The type of the event.</param>
        /// <param name="channel">The announced channel.</param>
        public RtcDataChannelEvent(String type, RtcDataChannel channel)
            : base(type)
        {
            Channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        /// <summary>
        /// Gets the announced channel.
        /// </summary>
        [DomName("channel")]
        public RtcDataChannel Channel { get; }
    }

    static class RtcEventNames
    {
        public static readonly String NegotiationNeeded = "negotiationneeded";
        public static readonly String IceCandidate = "icecandidate";
        public static readonly String SignalingStateChange = "signalingstatechange";
        public static readonly String IceGatheringStateChange = "icegatheringstatechange";
        public static readonly String IceConnectionStateChange = "iceconnectionstatechange";
        public static readonly String ConnectionStateChange = "connectionstatechange";
        public static readonly String DataChannel = "datachannel";
        public static readonly String Open = "open";
        public static readonly String Message = "message";
        public static readonly String Closing = "closing";
        public static readonly String Close = "close";
        public static readonly String Error = "error";
        public static readonly String BufferedAmountLow = "bufferedamountlow";
    }
}
