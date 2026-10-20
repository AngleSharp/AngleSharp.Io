namespace AngleSharp.Io.WebRtc
{
    using AngleSharp.Attributes;
    using AngleSharp.Dom;
    using System;
    using System.Linq;

    /// <summary>
    /// Represents the configuration of a peer connection.
    /// </summary>
    [DomName("RTCConfiguration")]
    public sealed class RtcConfiguration
    {
        /// <summary>
        /// Gets or sets the STUN and TURN servers the ICE agent may use.
        /// </summary>
        [DomName("iceServers")]
        public RtcIceServer[] IceServers { get; set; } = Array.Empty<RtcIceServer>();

        /// <summary>
        /// Gets or sets the candidates the ICE agent may use.
        /// </summary>
        [DomName("iceTransportPolicy")]
        public RtcIceTransportPolicy IceTransportPolicy { get; set; } = RtcIceTransportPolicy.All;

        /// <summary>
        /// Gets or sets the policy for bundling media.
        /// </summary>
        [DomName("bundlePolicy")]
        public RtcBundlePolicy BundlePolicy { get; set; } = RtcBundlePolicy.Balanced;

        /// <summary>
        /// Gets or sets the policy for multiplexing RTCP.
        /// </summary>
        [DomName("rtcpMuxPolicy")]
        public RtcRtcpMuxPolicy RtcpMuxPolicy { get; set; } = RtcRtcpMuxPolicy.Require;

        /// <summary>
        /// Gets or sets the number of candidates to gather in advance.
        /// </summary>
        [DomName("iceCandidatePoolSize")]
        public Byte IceCandidatePoolSize { get; set; }

        internal RtcConfiguration Clone() => new RtcConfiguration
        {
            IceServers = (IceServers ?? Array.Empty<RtcIceServer>()).Where(m => m is not null).Select(m => m.Clone()).ToArray(),
            IceTransportPolicy = IceTransportPolicy,
            BundlePolicy = BundlePolicy,
            RtcpMuxPolicy = RtcpMuxPolicy,
            IceCandidatePoolSize = IceCandidatePoolSize,
        };

        internal void Validate()
        {
            foreach (var server in IceServers ?? Array.Empty<RtcIceServer>())
            {
                server?.Validate();
            }
        }
    }

    /// <summary>
    /// Represents a STUN or TURN server.
    /// </summary>
    [DomName("RTCIceServer")]
    public sealed class RtcIceServer
    {
        /// <summary>
        /// Gets or sets the URLs of the server, e.g., stun:stun.example.org.
        /// </summary>
        [DomName("urls")]
        public String[] Urls { get; set; } = Array.Empty<String>();

        /// <summary>
        /// Gets or sets the username for a TURN server.
        /// </summary>
        [DomName("username")]
        public String Username { get; set; }

        /// <summary>
        /// Gets or sets the credential for a TURN server.
        /// </summary>
        [DomName("credential")]
        public String Credential { get; set; }

        internal RtcIceServer Clone() => new RtcIceServer
        {
            Urls = (Urls ?? Array.Empty<String>()).ToArray(),
            Username = Username,
            Credential = Credential,
        };

        internal void Validate()
        {
            if (Urls is null || Urls.Length == 0)
            {
                throw new DomException(DomError.Syntax);
            }

            foreach (var url in Urls)
            {
                var separator = url?.IndexOf(':') ?? -1;
                var scheme = separator > 0 ? url.Substring(0, separator).ToLowerInvariant() : String.Empty;

                if ((scheme != "stun" && scheme != "stuns" && scheme != "turn" && scheme != "turns") || separator == url.Length - 1)
                {
                    throw new DomException(DomError.Syntax);
                }

                if ((scheme == "turn" || scheme == "turns") && (Username is null || Credential is null))
                {
                    throw new DomException(DomError.InvalidAccess);
                }
            }
        }
    }

    /// <summary>
    /// Represents the options for creating an offer.
    /// </summary>
    [DomName("RTCOfferOptions")]
    public sealed class RtcOfferOptions
    {
        /// <summary>
        /// Gets or sets if the offer restarts ICE, i.e., uses new credentials.
        /// </summary>
        [DomName("iceRestart")]
        public Boolean IceRestart { get; set; }
    }

    /// <summary>
    /// Represents the options for creating an answer.
    /// </summary>
    [DomName("RTCAnswerOptions")]
    public sealed class RtcAnswerOptions
    {
    }
}
