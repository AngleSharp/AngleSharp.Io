namespace AngleSharp.Io.WebRtc
{
    using AngleSharp.Dom;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A platform connecting the peer connections it created with each other
    /// in-process, e.g., for signaling tests. The SDP is deterministic and
    /// only describes data channels; each peer gathers a single host
    /// candidate on 127.0.0.1. A peer connects once it applied both
    /// descriptions and received a candidate of the remote peer, which it
    /// finds via the ICE username fragment of the remote description.
    /// </summary>
    public sealed class LoopbackRtcPlatform : IRtcPlatform
    {
        private readonly Object _sync = new Object();
        private readonly Dictionary<String, Peer> _peers = new Dictionary<String, Peer>(StringComparer.Ordinal);
        private Int32 _lastId;

        /// <inheritdoc />
        public IRtcPeer CreatePeer(IRtcPeerHost host, RtcConfiguration configuration)
        {
            if (host is null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            var id = Interlocked.Increment(ref _lastId);
            return new Peer(this, host, configuration ?? new RtcConfiguration(), id);
        }

        private sealed class Peer : IRtcPeer
        {
            private readonly LoopbackRtcPlatform _platform;
            private readonly IRtcPeerHost _host;
            private readonly Int32 _id;
            private readonly List<RtcDataChannel> _channels = new List<RtcDataChannel>();
            private readonly Dictionary<RtcDataChannel, UInt16> _ids = new Dictionary<RtcDataChannel, UInt16>();
            private readonly Dictionary<RtcDataChannel, (Peer Peer, RtcDataChannel Channel)> _links = new Dictionary<RtcDataChannel, (Peer, RtcDataChannel)>();

            private Boolean _relayOnly;
            private Int32 _generation;
            private Int32 _version;
            private String _localUfrag;
            private String _gatheredUfrag;
            private Boolean _localHasApplication;
            private Boolean _stableLocalHasApplication;
            private Boolean _isClient;
            private String _remoteUfrag;
            private String _stableRemoteUfrag;
            private String _restartedFor;
            private Peer _remotePeer;
            private Peer _stableRemotePeer;
            private Boolean _remoteCandidate;
            private Boolean _connected;
            private Boolean _sctp;
            private Boolean _closed;

            public Peer(LoopbackRtcPlatform platform, IRtcPeerHost host, RtcConfiguration configuration, Int32 id)
            {
                _platform = platform;
                _host = host;
                _id = id;
                _relayOnly = configuration.IceTransportPolicy == RtcIceTransportPolicy.Relay;
            }

            private UInt16 Port => (UInt16)(50000 + _id % 10000);

            private String Ufrag => String.Format(CultureInfo.InvariantCulture, "lb{0:x4}{1:x2}", _id, _generation);

            private String Password => String.Format(CultureInfo.InvariantCulture, "loopbackpwd{0:x6}{1:x6}", _id, _generation);

            public Task<String> CreateOfferAsync(RtcSdpRequest request)
            {
                lock (_platform._sync)
                {
                    if (request.IceRestart)
                    {
                        _generation++;
                    }

                    // media descriptions cannot be removed once negotiated
                    var hasApplication = request.HasDataChannels || HasApplication(request.LocalDescription) || HasApplication(request.RemoteDescription);
                    return Task.FromResult(BuildSdp("actpass", hasApplication));
                }
            }

            public Task<String> CreateAnswerAsync(RtcSdpRequest request)
            {
                lock (_platform._sync)
                {
                    var offer = request.RemoteDescription?.Sdp ?? String.Empty;
                    var offerUfrag = GetAttribute(offer, "a=ice-ufrag:");

                    // an offer restarting ICE requires new credentials for the
                    // answer, too
                    if (_stableRemoteUfrag is not null && offerUfrag != _stableRemoteUfrag && offerUfrag != _restartedFor)
                    {
                        _restartedFor = offerUfrag;
                        _generation++;
                    }

                    return Task.FromResult(BuildSdp("active", HasApplication(request.RemoteDescription)));
                }
            }

            public Task SetLocalDescriptionAsync(RtcSessionDescription description)
            {
                var effects = new List<Action>();

                lock (_platform._sync)
                {
                    var sdp = description.Sdp;
                    var ufrag = GetAttribute(sdp, "a=ice-ufrag:");
                    _localHasApplication = HasApplication(description);
                    _isClient = sdp.IndexOf("a=setup:active", StringComparison.Ordinal) >= 0;

                    if (ufrag is not null)
                    {
                        _localUfrag = ufrag;
                        _platform._peers[ufrag] = this;
                    }

                    if (description.Type == RtcSdpType.Answer)
                    {
                        MarkStable();
                    }

                    if (_localHasApplication && ufrag is not null && _gatheredUfrag != ufrag)
                    {
                        _gatheredUfrag = ufrag;
                        Gather(ufrag, effects);
                    }

                    TryConnect(effects);
                }

                Run(effects);
                return Task.CompletedTask;
            }

            public Task SetRemoteDescriptionAsync(RtcSessionDescription description)
            {
                if (!description.Sdp.StartsWith("v=0", StringComparison.Ordinal))
                {
                    throw new DomException(DomError.Syntax);
                }

                var effects = new List<Action>();

                lock (_platform._sync)
                {
                    var ufrag = GetAttribute(description.Sdp, "a=ice-ufrag:");

                    if (ufrag != _remoteUfrag)
                    {
                        _remoteUfrag = ufrag;
                        _remoteCandidate = false;
                    }

                    _remotePeer = Resolve();

                    if (description.Type == RtcSdpType.Answer)
                    {
                        MarkStable();
                    }

                    TryConnect(effects);
                }

                Run(effects);
                return Task.CompletedTask;
            }

            public Task RollbackAsync()
            {
                lock (_platform._sync)
                {
                    _localHasApplication = _stableLocalHasApplication;
                    _remoteUfrag = _stableRemoteUfrag;
                    _remotePeer = _stableRemotePeer;
                }

                return Task.CompletedTask;
            }

            public Task AddIceCandidateAsync(RtcIceCandidate candidate)
            {
                var effects = new List<Action>();

                lock (_platform._sync)
                {
                    if (candidate is not null)
                    {
                        _remotePeer ??= Resolve();
                        var remote = _remotePeer;

                        if (remote is not null && candidate.Address == "127.0.0.1" && candidate.Port == remote.Port &&
                            (candidate.UsernameFragment is null || candidate.UsernameFragment == _remoteUfrag))
                        {
                            _remoteCandidate = true;
                            TryConnect(effects);
                        }
                    }
                }

                Run(effects);
                return Task.CompletedTask;
            }

            public void SetConfiguration(RtcConfiguration configuration)
            {
                lock (_platform._sync)
                {
                    _relayOnly = configuration?.IceTransportPolicy == RtcIceTransportPolicy.Relay;
                }
            }

            public void CreateDataChannel(RtcDataChannel channel)
            {
                var effects = new List<Action>();

                lock (_platform._sync)
                {
                    _channels.Add(channel);

                    if (_sctp && _remotePeer is not null)
                    {
                        OpenChannel(this, _remotePeer, channel, effects);
                    }
                }

                Run(effects);
            }

            public void Send(RtcDataChannel channel, Object data)
            {
                var effects = new List<Action>();

                lock (_platform._sync)
                {
                    if (_links.TryGetValue(channel, out var link))
                    {
                        var host = link.Peer._host;
                        effects.Add(() => host.ReceiveMessage(link.Channel, data));
                    }
                }

                Run(effects);
            }

            public void CloseDataChannel(RtcDataChannel channel)
            {
                var effects = new List<Action>();

                lock (_platform._sync)
                {
                    var host = _host;
                    effects.Add(() => host.CloseDataChannel(channel));

                    if (_links.TryGetValue(channel, out var link))
                    {
                        var remoteHost = link.Peer._host;
                        effects.Add(() => remoteHost.CloseDataChannel(link.Channel));
                        link.Peer._links.Remove(link.Channel);
                        _links.Remove(channel);
                    }
                }

                Run(effects);
            }

            public void Dispose()
            {
                var effects = new List<Action>();

                lock (_platform._sync)
                {
                    if (_closed)
                    {
                        return;
                    }

                    _closed = true;

                    foreach (var key in _platform._peers.Where(m => m.Value == this).Select(m => m.Key).ToArray())
                    {
                        _platform._peers.Remove(key);
                    }

                    var remote = _remotePeer;

                    // the remote peer notices the closed connection like a
                    // lost transport
                    if (remote is not null && !remote._closed && remote._remotePeer == this && remote._connected)
                    {
                        var remoteHost = remote._host;
                        remote._sctp = false;
                        effects.Add(() => remoteHost.SetIceConnectionState(RtcIceConnectionState.Disconnected));
                    }

                    foreach (var link in _links.Values)
                    {
                        var linkedHost = link.Peer._host;
                        effects.Add(() => linkedHost.CloseDataChannel(link.Channel));
                        link.Peer._links.Remove(link.Channel);
                    }

                    _links.Clear();
                }

                Run(effects);
            }

            private void MarkStable()
            {
                _stableLocalHasApplication = _localHasApplication;
                _stableRemoteUfrag = _remoteUfrag;
                _stableRemotePeer = _remotePeer;
            }

            private Peer Resolve() =>
                _remoteUfrag is not null && _platform._peers.TryGetValue(_remoteUfrag, out var peer) && peer != this && !peer._closed ? peer : null;

            private void Gather(String ufrag, List<Action> effects)
            {
                var host = _host;
                var relayOnly = _relayOnly;
                var candidate = new RtcIceCandidateInit
                {
                    Candidate = String.Format(CultureInfo.InvariantCulture, "candidate:1 1 udp 2122260223 127.0.0.1 {0} typ host generation 0 ufrag {1}", Port, ufrag),
                    SdpMid = "0",
                    SdpMLineIndex = 0,
                    UsernameFragment = ufrag,
                };

                effects.Add(() => host.SetIceGatheringState(RtcIceGatheringState.Gathering));

                // there is no relay server, hence no candidate for the relay policy
                if (!relayOnly)
                {
                    effects.Add(() => host.AddLocalCandidate(candidate));
                }

                effects.Add(() => host.SetIceGatheringState(RtcIceGatheringState.Complete));
                effects.Add(() => host.AddLocalCandidate(null));
            }

            private void TryConnect(List<Action> effects)
            {
                if (_closed)
                {
                    return;
                }

                if (!_connected)
                {
                    _remotePeer ??= Resolve();

                    if (!_localHasApplication || _gatheredUfrag is null || _remotePeer is null || !_remoteCandidate)
                    {
                        return;
                    }

                    var host = _host;
                    _connected = true;
                    effects.Add(() => host.SetIceConnectionState(RtcIceConnectionState.Checking));
                    effects.Add(() => host.SetIceConnectionState(RtcIceConnectionState.Connected));
                }

                var remote = _remotePeer;

                // the SCTP association needs both peers connected to each other
                if (!_sctp && remote is not null && remote._connected && remote._remotePeer == this && !remote._closed)
                {
                    _sctp = true;
                    remote._sctp = true;
                    OpenChannels(this, remote, effects);
                    OpenChannels(remote, this, effects);
                }
            }

            private static void OpenChannels(Peer source, Peer target, List<Action> effects)
            {
                foreach (var channel in source._channels.ToArray())
                {
                    if (!source._links.ContainsKey(channel))
                    {
                        OpenChannel(source, target, channel, effects);
                    }
                }
            }

            private static void OpenChannel(Peer source, Peer target, RtcDataChannel channel, List<Action> effects)
            {
                if (channel.ReadyState != RtcDataChannelState.Connecting)
                {
                    return;
                }

                var sourceHost = source._host;
                var targetHost = target._host;

                if (channel.Negotiated)
                {
                    var id = channel.Id.Value;
                    var other = target._channels.FirstOrDefault(m => m.Negotiated && m.Id == id && !target._links.ContainsKey(m) && m.ReadyState == RtcDataChannelState.Connecting);
                    source._ids[channel] = id;

                    // the channel waits until the remote peer creates its
                    // counterpart with the same id
                    if (other is not null)
                    {
                        target._ids[other] = id;
                        Link(source, channel, target, other);
                        effects.Add(() => sourceHost.OpenDataChannel(channel, id));
                        effects.Add(() => targetHost.OpenDataChannel(other, id));
                    }
                }
                else
                {
                    var id = AllocateId(source, target);
                    var remote = targetHost.CreateRemoteDataChannel(channel.Label, new RtcDataChannelInit
                    {
                        Ordered = channel.Ordered,
                        MaxPacketLifeTime = channel.MaxPacketLifeTime,
                        MaxRetransmits = channel.MaxRetransmits,
                        Protocol = channel.Protocol,
                        Negotiated = false,
                        Id = id,
                    });

                    source._ids[channel] = id;
                    target._channels.Add(remote);
                    target._ids[remote] = id;
                    Link(source, channel, target, remote);
                    effects.Add(() => targetHost.AnnounceDataChannel(remote));
                    effects.Add(() => sourceHost.OpenDataChannel(channel, id));
                }
            }

            private static UInt16 AllocateId(Peer source, Peer target)
            {
                // like the spec, the DTLS client uses even ids and the server
                // odd ones, which avoids collisions between the peers
                var id = source._isClient ? 0 : 1;

                while (source._ids.ContainsValue((UInt16)id) || target._ids.ContainsValue((UInt16)id))
                {
                    id += 2;
                }

                return (UInt16)id;
            }

            private static void Link(Peer source, RtcDataChannel channel, Peer target, RtcDataChannel other)
            {
                source._links[channel] = (target, other);
                target._links[other] = (source, channel);
            }

            private String BuildSdp(String setup, Boolean hasApplication)
            {
                var sessionId = (4000000000000000000L + _id).ToString(CultureInfo.InvariantCulture);
                var fingerprint = String.Join(":", Enumerable.Range(0, 32).Select(i => ((_id * 31 + i * 7) & 0xFF).ToString("X2", CultureInfo.InvariantCulture)));
                var sdp = new StringBuilder();
                _version++;

                sdp.Append("v=0\r\n");
                sdp.Append("o=- ").Append(sessionId).Append(' ').Append(_version.ToString(CultureInfo.InvariantCulture)).Append(" IN IP4 127.0.0.1\r\n");
                sdp.Append("s=-\r\n");
                sdp.Append("t=0 0\r\n");

                if (hasApplication)
                {
                    sdp.Append("a=group:BUNDLE 0\r\n");
                }

                sdp.Append("a=ice-options:trickle\r\n");
                sdp.Append("a=msid-semantic: WMS\r\n");

                if (hasApplication)
                {
                    sdp.Append("m=application 9 UDP/DTLS/SCTP webrtc-datachannel\r\n");
                    sdp.Append("c=IN IP4 0.0.0.0\r\n");
                    sdp.Append("a=ice-ufrag:").Append(Ufrag).Append("\r\n");
                    sdp.Append("a=ice-pwd:").Append(Password).Append("\r\n");
                    sdp.Append("a=fingerprint:sha-256 ").Append(fingerprint).Append("\r\n");
                    sdp.Append("a=setup:").Append(setup).Append("\r\n");
                    sdp.Append("a=mid:0\r\n");
                    sdp.Append("a=sctp-port:5000\r\n");
                    sdp.Append("a=max-message-size:262144\r\n");
                }

                return sdp.ToString();
            }

            private static Boolean HasApplication(RtcSessionDescription description) =>
                description?.Sdp.IndexOf("m=application", StringComparison.Ordinal) >= 0;

            private static String GetAttribute(String sdp, String prefix)
            {
                foreach (var line in sdp.Split('\n'))
                {
                    var value = line.TrimEnd('\r');

                    if (value.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return value.Substring(prefix.Length);
                    }
                }

                return null;
            }

            private static void Run(List<Action> effects)
            {
                // the host is called outside of the lock, as it may dispatch
                // events right away, whose handlers may call the platform
                foreach (var effect in effects)
                {
                    effect.Invoke();
                }
            }
        }
    }
}
