namespace AngleSharp.Io.WebRtc
{
    using AngleSharp.Attributes;
    using System;

    /// <summary>
    /// Represents the dictionary describing a session description.
    /// </summary>
    [DomName("RTCSessionDescriptionInit")]
    public sealed class RtcSessionDescriptionInit
    {
        /// <summary>
        /// Gets or sets the type of the description. May be omitted when
        /// setting the local description, which then picks the type.
        /// </summary>
        [DomName("type")]
        public RtcSdpType? Type { get; set; }

        /// <summary>
        /// Gets or sets the SDP of the description.
        /// </summary>
        [DomName("sdp")]
        public String Sdp { get; set; } = String.Empty;
    }

    /// <summary>
    /// Represents a session description, i.e., an SDP with its type.
    /// </summary>
    [DomName("RTCSessionDescription")]
    public sealed class RtcSessionDescription
    {
        /// <summary>
        /// Creates a new session description.
        /// </summary>
        /// <param name="description">The type and SDP of the description.</param>
        [DomConstructor]
        public RtcSessionDescription(RtcSessionDescriptionInit description)
        {
            if (description?.Type is null)
            {
                throw new InvalidOperationException("The type of the session description is required.");
            }

            Type = description.Type.Value;
            Sdp = description.Sdp ?? String.Empty;
        }

        internal RtcSessionDescription(RtcSdpType type, String sdp)
        {
            Type = type;
            Sdp = sdp ?? String.Empty;
        }

        /// <summary>
        /// Gets the type of the description.
        /// </summary>
        [DomName("type")]
        public RtcSdpType Type { get; }

        /// <summary>
        /// Gets the SDP of the description.
        /// </summary>
        [DomName("sdp")]
        public String Sdp { get; }

        /// <summary>
        /// Gets the dictionary form of the description, e.g., for sending it
        /// to the other peer.
        /// </summary>
        /// <returns>The type and SDP of the description.</returns>
        [DomName("toJSON")]
        public RtcSessionDescriptionInit ToJson() => new RtcSessionDescriptionInit
        {
            Type = Type,
            Sdp = Sdp,
        };
    }
}
