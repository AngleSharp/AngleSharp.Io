namespace AngleSharp.Io.WebRtc
{
    using AngleSharp.Attributes;
    using System;
    using System.Globalization;

    /// <summary>
    /// Represents the dictionary describing an ICE candidate.
    /// </summary>
    [DomName("RTCIceCandidateInit")]
    public sealed class RtcIceCandidateInit
    {
        /// <summary>
        /// Gets or sets the candidate-attribute, e.g., "candidate:1 1 udp ...".
        /// An empty value signals the end of the candidates.
        /// </summary>
        [DomName("candidate")]
        public String Candidate { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the media stream identification of the candidate.
        /// </summary>
        [DomName("sdpMid")]
        public String SdpMid { get; set; }

        /// <summary>
        /// Gets or sets the index of the media description of the candidate.
        /// </summary>
        [DomName("sdpMLineIndex")]
        public UInt16? SdpMLineIndex { get; set; }

        /// <summary>
        /// Gets or sets the ICE username fragment of the candidate.
        /// </summary>
        [DomName("usernameFragment")]
        public String UsernameFragment { get; set; }
    }

    /// <summary>
    /// Represents an ICE candidate. The attributes are parsed from the
    /// candidate-attribute and stay null if it cannot be parsed.
    /// </summary>
    [DomName("RTCIceCandidate")]
    public sealed class RtcIceCandidate
    {
        /// <summary>
        /// Creates a new ICE candidate.
        /// </summary>
        /// <param name="candidate">The description of the candidate.</param>
        [DomConstructor]
        public RtcIceCandidate(RtcIceCandidateInit candidate)
        {
            if (candidate is null || (candidate.SdpMid is null && candidate.SdpMLineIndex is null))
            {
                throw new InvalidOperationException("Either sdpMid or sdpMLineIndex of the candidate is required.");
            }

            Candidate = candidate.Candidate ?? String.Empty;
            SdpMid = candidate.SdpMid;
            SdpMLineIndex = candidate.SdpMLineIndex;
            UsernameFragment = candidate.UsernameFragment;
            Parse();
        }

        /// <summary>
        /// Gets the candidate-attribute.
        /// </summary>
        [DomName("candidate")]
        public String Candidate { get; }

        /// <summary>
        /// Gets the media stream identification of the candidate.
        /// </summary>
        [DomName("sdpMid")]
        public String SdpMid { get; }

        /// <summary>
        /// Gets the index of the media description of the candidate.
        /// </summary>
        [DomName("sdpMLineIndex")]
        public UInt16? SdpMLineIndex { get; }

        /// <summary>
        /// Gets the foundation of the candidate.
        /// </summary>
        [DomName("foundation")]
        public String Foundation { get; private set; }

        /// <summary>
        /// Gets the component of the candidate.
        /// </summary>
        [DomName("component")]
        public RtcIceComponent? Component { get; private set; }

        /// <summary>
        /// Gets the priority of the candidate.
        /// </summary>
        [DomName("priority")]
        public UInt32? Priority { get; private set; }

        /// <summary>
        /// Gets the address of the candidate.
        /// </summary>
        [DomName("address")]
        public String Address { get; private set; }

        /// <summary>
        /// Gets the transport protocol of the candidate.
        /// </summary>
        [DomName("protocol")]
        public RtcIceProtocol? Protocol { get; private set; }

        /// <summary>
        /// Gets the port of the candidate.
        /// </summary>
        [DomName("port")]
        public UInt16? Port { get; private set; }

        /// <summary>
        /// Gets the type of the candidate.
        /// </summary>
        [DomName("type")]
        public RtcIceCandidateType? Type { get; private set; }

        /// <summary>
        /// Gets the TCP type of a TCP candidate.
        /// </summary>
        [DomName("tcpType")]
        public RtcIceTcpCandidateType? TcpType { get; private set; }

        /// <summary>
        /// Gets the related address of a non-host candidate.
        /// </summary>
        [DomName("relatedAddress")]
        public String RelatedAddress { get; private set; }

        /// <summary>
        /// Gets the related port of a non-host candidate.
        /// </summary>
        [DomName("relatedPort")]
        public UInt16? RelatedPort { get; private set; }

        /// <summary>
        /// Gets the ICE username fragment of the candidate.
        /// </summary>
        [DomName("usernameFragment")]
        public String UsernameFragment { get; private set; }

        /// <summary>
        /// Gets the dictionary form of the candidate, e.g., for sending it to
        /// the other peer.
        /// </summary>
        /// <returns>The description of the candidate.</returns>
        [DomName("toJSON")]
        public RtcIceCandidateInit ToJson() => new RtcIceCandidateInit
        {
            Candidate = Candidate,
            SdpMid = SdpMid,
            SdpMLineIndex = SdpMLineIndex,
            UsernameFragment = UsernameFragment,
        };

        private void Parse()
        {
            var value = Candidate.StartsWith("candidate:", StringComparison.Ordinal) ? Candidate.Substring(10) : Candidate;
            var parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 8 || parts[6] != "typ" ||
                !TryParseEnum<RtcIceProtocol>(parts[2].ToLowerInvariant(), out var protocol) ||
                !UInt32.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var priority) ||
                !UInt16.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
                !TryParseEnum<RtcIceCandidateType>(parts[7], out var type))
            {
                return;
            }

            Foundation = parts[0];
            Component = parts[1] == "1" ? RtcIceComponent.Rtp : parts[1] == "2" ? RtcIceComponent.Rtcp : (RtcIceComponent?)null;
            Protocol = protocol;
            Priority = priority;
            Address = parts[4];
            Port = port;
            Type = type;

            for (var i = 8; i + 1 < parts.Length; i += 2)
            {
                var name = parts[i];
                var argument = parts[i + 1];

                if (name == "raddr")
                {
                    RelatedAddress = argument;
                }
                else if (name == "rport" && UInt16.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var relatedPort))
                {
                    RelatedPort = relatedPort;
                }
                else if (name == "tcptype" && TryParseEnum<RtcIceTcpCandidateType>(argument, out var tcpType))
                {
                    TcpType = tcpType;
                }
                else if (name == "ufrag" && UsernameFragment is null)
                {
                    UsernameFragment = argument;
                }
            }
        }

        private static Boolean TryParseEnum<T>(String value, out T result)
            where T : struct
        {
            // the literals are lowercase words, which the enum names match
            // case-insensitively; numbers are not literals
            if (value.Length > 0 && Char.IsLetter(value[0]) && Enum.TryParse(value, true, out result))
            {
                return true;
            }

            result = default;
            return false;
        }
    }
}
