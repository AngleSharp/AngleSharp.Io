namespace AngleSharp.Io.WebRtc
{
    using AngleSharp.Attributes;
    using AngleSharp.Browser;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the RTCPeerConnection interface, i.e., the signaling state
    /// machine of WebRTC. Transport and media are provided by an
    /// <see cref="IRtcPlatform"/>.
    /// </summary>
    [DomName("RTCPeerConnection")]
    [DomExposed("Window")]
    public sealed class RtcPeerConnection : EventTarget, IDisposable
    {
        #region Fields

        private readonly IEventLoop _eventLoop;
        private readonly SemaphoreSlim _operations;
        private readonly List<RtcDataChannel> _channels;
        private readonly Host _host;
        private readonly IRtcPeer _peer;

        private RtcConfiguration _configuration;
        private RtcSessionDescription _currentLocal;
        private RtcSessionDescription _pendingLocal;
        private RtcSessionDescription _currentRemote;
        private RtcSessionDescription _pendingRemote;
        private RtcSignalingState _signalingState;
        private RtcIceGatheringState _iceGatheringState;
        private RtcIceConnectionState _iceConnectionState;
        private RtcPeerConnectionState _connectionState;
        private Boolean? _canTrickle;
        private String _lastCreatedOffer;
        private String _lastCreatedAnswer;
        private Boolean _lastCreatedOfferRestartsIce;
        private Boolean _iceRestartPending;
        private Boolean _negotiationNeeded;
        private Boolean _closed;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new peer connection using the platform registered for the
        /// window's browsing context.
        /// </summary>
        /// <param name="window">The window creating the connection.</param>
        [DomConstructor]
        public RtcPeerConnection(IWindow window)
            : this(window, null)
        {
        }

        /// <summary>
        /// Creates a new peer connection using the platform registered for the
        /// window's browsing context.
        /// </summary>
        /// <param name="window">The window creating the connection.</param>
        /// <param name="configuration">The configuration of the connection.</param>
        [DomConstructor]
        public RtcPeerConnection(IWindow window, RtcConfiguration configuration)
            : this(GetPlatform(window), configuration, window.Document?.Context?.GetService<IEventLoop>())
        {
        }

        /// <summary>
        /// Creates a new peer connection using the given platform.
        /// </summary>
        /// <param name="platform">The platform providing the transport.</param>
        /// <param name="configuration">The configuration of the connection.</param>
        /// <param name="eventLoop">
        /// The event loop for dispatching the events of the transport. Without
        /// one, they are dispatched once the running operation completes.
        /// </param>
        public RtcPeerConnection(IRtcPlatform platform, RtcConfiguration configuration = null, IEventLoop eventLoop = null)
        {
            if (platform is null)
            {
                throw new ArgumentNullException(nameof(platform));
            }

            _configuration = (configuration ?? new RtcConfiguration()).Clone();
            _configuration.Validate();
            _eventLoop = eventLoop;
            _operations = new SemaphoreSlim(1, 1);
            _channels = new List<RtcDataChannel>();
            _host = new Host(this);
            _peer = platform.CreatePeer(_host, _configuration.Clone()) ?? throw new InvalidOperationException("The platform did not create a peer.");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the pending local description, or the current one if there is
        /// no pending one.
        /// </summary>
        [DomName("localDescription")]
        public RtcSessionDescription LocalDescription => _pendingLocal ?? _currentLocal;

        /// <summary>
        /// Gets the local description of the last completed negotiation.
        /// </summary>
        [DomName("currentLocalDescription")]
        public RtcSessionDescription CurrentLocalDescription => _currentLocal;

        /// <summary>
        /// Gets the local description of the running negotiation.
        /// </summary>
        [DomName("pendingLocalDescription")]
        public RtcSessionDescription PendingLocalDescription => _pendingLocal;

        /// <summary>
        /// Gets the pending remote description, or the current one if there
        /// is no pending one.
        /// </summary>
        [DomName("remoteDescription")]
        public RtcSessionDescription RemoteDescription => _pendingRemote ?? _currentRemote;

        /// <summary>
        /// Gets the remote description of the last completed negotiation.
        /// </summary>
        [DomName("currentRemoteDescription")]
        public RtcSessionDescription CurrentRemoteDescription => _currentRemote;

        /// <summary>
        /// Gets the remote description of the running negotiation.
        /// </summary>
        [DomName("pendingRemoteDescription")]
        public RtcSessionDescription PendingRemoteDescription => _pendingRemote;

        /// <summary>
        /// Gets the state of the signaling.
        /// </summary>
        [DomName("signalingState")]
        public RtcSignalingState SignalingState => _signalingState;

        /// <summary>
        /// Gets the state of gathering ICE candidates.
        /// </summary>
        [DomName("iceGatheringState")]
        public RtcIceGatheringState IceGatheringState => _iceGatheringState;

        /// <summary>
        /// Gets the state of the ICE connection.
        /// </summary>
        [DomName("iceConnectionState")]
        public RtcIceConnectionState IceConnectionState => _iceConnectionState;

        /// <summary>
        /// Gets the aggregated state of the connection.
        /// </summary>
        [DomName("connectionState")]
        public RtcPeerConnectionState ConnectionState => _connectionState;

        /// <summary>
        /// Gets if the remote peer accepts trickled candidates, or null while
        /// there is no remote description.
        /// </summary>
        [DomName("canTrickleIceCandidates")]
        public Boolean? CanTrickleIceCandidates => _canTrickle;

        internal IRtcPeer Peer => _closed ? null : _peer;

        #endregion

        #region Events

        /// <summary>
        /// Adds or removes the handler for the negotiationneeded event.
        /// </summary>
        [DomName("onnegotiationneeded")]
        public event DomEventHandler NegotiationNeeded
        {
            add { AddEventListener(RtcEventNames.NegotiationNeeded, value, false); }
            remove { RemoveEventListener(RtcEventNames.NegotiationNeeded, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the icecandidate event.
        /// </summary>
        [DomName("onicecandidate")]
        public event DomEventHandler IceCandidate
        {
            add { AddEventListener(RtcEventNames.IceCandidate, value, false); }
            remove { RemoveEventListener(RtcEventNames.IceCandidate, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the signalingstatechange event.
        /// </summary>
        [DomName("onsignalingstatechange")]
        public event DomEventHandler SignalingStateChanged
        {
            add { AddEventListener(RtcEventNames.SignalingStateChange, value, false); }
            remove { RemoveEventListener(RtcEventNames.SignalingStateChange, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the icegatheringstatechange event.
        /// </summary>
        [DomName("onicegatheringstatechange")]
        public event DomEventHandler IceGatheringStateChanged
        {
            add { AddEventListener(RtcEventNames.IceGatheringStateChange, value, false); }
            remove { RemoveEventListener(RtcEventNames.IceGatheringStateChange, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the iceconnectionstatechange event.
        /// </summary>
        [DomName("oniceconnectionstatechange")]
        public event DomEventHandler IceConnectionStateChanged
        {
            add { AddEventListener(RtcEventNames.IceConnectionStateChange, value, false); }
            remove { RemoveEventListener(RtcEventNames.IceConnectionStateChange, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the connectionstatechange event.
        /// </summary>
        [DomName("onconnectionstatechange")]
        public event DomEventHandler ConnectionStateChanged
        {
            add { AddEventListener(RtcEventNames.ConnectionStateChange, value, false); }
            remove { RemoveEventListener(RtcEventNames.ConnectionStateChange, value, false); }
        }

        /// <summary>
        /// Adds or removes the handler for the datachannel event.
        /// </summary>
        [DomName("ondatachannel")]
        public event DomEventHandler DataChannel
        {
            add { AddEventListener(RtcEventNames.DataChannel, value, false); }
            remove { RemoveEventListener(RtcEventNames.DataChannel, value, false); }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates an offer for starting a negotiation.
        /// </summary>
        /// <returns>The offer.</returns>
        [DomName("createOffer")]
        public Task<RtcSessionDescriptionInit> CreateOffer() => CreateOffer(null);

        /// <summary>
        /// Creates an offer for starting a negotiation.
        /// </summary>
        /// <param name="options">The options of the offer.</param>
        /// <returns>The offer.</returns>
        [DomName("createOffer")]
        public Task<RtcSessionDescriptionInit> CreateOffer(RtcOfferOptions options) => RunAsync(async () =>
        {
            var sdp = await CreateOfferCoreAsync(options).ConfigureAwait(false);
            return new RtcSessionDescriptionInit { Type = RtcSdpType.Offer, Sdp = sdp };
        });

        /// <summary>
        /// Creates an answer to the remote offer.
        /// </summary>
        /// <returns>The answer.</returns>
        [DomName("createAnswer")]
        public Task<RtcSessionDescriptionInit> CreateAnswer() => CreateAnswer(null);

        /// <summary>
        /// Creates an answer to the remote offer.
        /// </summary>
        /// <param name="options">The options of the answer.</param>
        /// <returns>The answer.</returns>
        [DomName("createAnswer")]
        public Task<RtcSessionDescriptionInit> CreateAnswer(RtcAnswerOptions options) => RunAsync(async () =>
        {
            var sdp = await CreateAnswerCoreAsync().ConfigureAwait(false);
            return new RtcSessionDescriptionInit { Type = RtcSdpType.Answer, Sdp = sdp };
        });

        /// <summary>
        /// Creates and applies an offer or answer, depending on the signaling
        /// state.
        /// </summary>
        /// <returns>The task representing the operation.</returns>
        [DomName("setLocalDescription")]
        public Task SetLocalDescription() => SetLocalDescription((RtcSessionDescriptionInit)null);

        /// <summary>
        /// Applies the given local description.
        /// </summary>
        /// <param name="description">The description to apply.</param>
        /// <returns>The task representing the operation.</returns>
        public Task SetLocalDescription(RtcSessionDescription description) => SetLocalDescription(description?.ToJson());

        /// <summary>
        /// Applies the given local description. Without type, an offer or
        /// answer is picked depending on the signaling state; without SDP, the
        /// last created offer or answer is used.
        /// </summary>
        /// <param name="description">The description to apply.</param>
        /// <returns>The task representing the operation.</returns>
        [DomName("setLocalDescription")]
        public Task SetLocalDescription(RtcSessionDescriptionInit description) => RunAsync(async () =>
        {
            var type = description?.Type ?? GetImplicitType();
            var sdp = description?.Sdp;

            if (type == RtcSdpType.Rollback)
            {
                await RollbackAsync().ConfigureAwait(false);
                return true;
            }

            if (type == RtcSdpType.Offer)
            {
                if (String.IsNullOrEmpty(sdp))
                {
                    sdp = _lastCreatedOffer ?? await CreateOfferCoreAsync(null).ConfigureAwait(false);
                }
                else if (sdp != _lastCreatedOffer)
                {
                    throw new DomException(DomError.InvalidModification);
                }

                EnsureSignalingState(RtcSignalingState.Stable, RtcSignalingState.HaveLocalOffer);
            }
            else
            {
                EnsureSignalingState(RtcSignalingState.HaveRemoteOffer, RtcSignalingState.HaveLocalPrAnswer);

                if (String.IsNullOrEmpty(sdp))
                {
                    sdp = _lastCreatedAnswer ?? await CreateAnswerCoreAsync().ConfigureAwait(false);
                }
                else if (sdp != _lastCreatedAnswer)
                {
                    throw new DomException(DomError.InvalidModification);
                }
            }

            var applied = new RtcSessionDescription(type, sdp);
            await _peer.SetLocalDescriptionAsync(applied).ConfigureAwait(false);
            EnsureOpen();

            if (type == RtcSdpType.Offer)
            {
                _pendingLocal = applied;

                if (_lastCreatedOfferRestartsIce)
                {
                    _iceRestartPending = false;
                }

                SetSignalingState(RtcSignalingState.HaveLocalOffer);
            }
            else if (type == RtcSdpType.PrAnswer)
            {
                _pendingLocal = applied;
                SetSignalingState(RtcSignalingState.HaveLocalPrAnswer);
            }
            else
            {
                _currentLocal = applied;
                _currentRemote = _pendingRemote;
                _pendingLocal = null;
                _pendingRemote = null;
                SetSignalingState(RtcSignalingState.Stable);
            }

            return true;
        });

        /// <summary>
        /// Applies the given remote description.
        /// </summary>
        /// <param name="description">The description to apply.</param>
        /// <returns>The task representing the operation.</returns>
        public Task SetRemoteDescription(RtcSessionDescription description) => SetRemoteDescription(description?.ToJson());

        /// <summary>
        /// Applies the given remote description. A remote offer while a local
        /// offer is pending rolls the local offer back.
        /// </summary>
        /// <param name="description">The description to apply.</param>
        /// <returns>The task representing the operation.</returns>
        [DomName("setRemoteDescription")]
        public Task SetRemoteDescription(RtcSessionDescriptionInit description) => RunAsync(async () =>
        {
            // the spec rejects a missing type with a TypeError
            if (description?.Type is null)
            {
                throw new InvalidOperationException("The type of the remote description is required.");
            }

            var type = description.Type.Value;

            if (type == RtcSdpType.Rollback)
            {
                await RollbackAsync().ConfigureAwait(false);
                return true;
            }

            if (String.IsNullOrEmpty(description.Sdp))
            {
                throw new DomException(DomError.Syntax);
            }

            if (type == RtcSdpType.Offer)
            {
                if (_signalingState == RtcSignalingState.HaveLocalOffer)
                {
                    await _peer.RollbackAsync().ConfigureAwait(false);
                    _pendingLocal = null;
                    _signalingState = RtcSignalingState.Stable;
                }

                EnsureSignalingState(RtcSignalingState.Stable, RtcSignalingState.HaveRemoteOffer);
            }
            else
            {
                EnsureSignalingState(RtcSignalingState.HaveLocalOffer, RtcSignalingState.HaveRemotePrAnswer);
            }

            var applied = new RtcSessionDescription(type, description.Sdp);
            await _peer.SetRemoteDescriptionAsync(applied).ConfigureAwait(false);
            EnsureOpen();
            _canTrickle = applied.Sdp.IndexOf("a=ice-options:trickle", StringComparison.Ordinal) >= 0;

            if (type == RtcSdpType.Offer)
            {
                _pendingRemote = applied;
                _lastCreatedAnswer = null;
                SetSignalingState(RtcSignalingState.HaveRemoteOffer);
            }
            else if (type == RtcSdpType.PrAnswer)
            {
                _pendingRemote = applied;
                SetSignalingState(RtcSignalingState.HaveRemotePrAnswer);
            }
            else
            {
                _currentRemote = applied;
                _currentLocal = _pendingLocal;
                _pendingLocal = null;
                _pendingRemote = null;
                SetSignalingState(RtcSignalingState.Stable);
            }

            return true;
        });

        /// <summary>
        /// Signals the end of the remote candidates.
        /// </summary>
        /// <returns>The task representing the operation.</returns>
        [DomName("addIceCandidate")]
        public Task AddIceCandidate() => AddIceCandidate((RtcIceCandidateInit)null);

        /// <summary>
        /// Adds a remote candidate.
        /// </summary>
        /// <param name="candidate">The candidate to add.</param>
        /// <returns>The task representing the operation.</returns>
        public Task AddIceCandidate(RtcIceCandidate candidate) => AddIceCandidate(candidate?.ToJson());

        /// <summary>
        /// Adds a remote candidate. A null candidate or an empty
        /// candidate-attribute signals the end of the remote candidates.
        /// </summary>
        /// <param name="candidate">The candidate to add.</param>
        /// <returns>The task representing the operation.</returns>
        [DomName("addIceCandidate")]
        public Task AddIceCandidate(RtcIceCandidateInit candidate) => RunAsync(async () =>
        {
            var end = candidate is null || String.IsNullOrEmpty(candidate.Candidate);

            // the spec rejects this with a TypeError
            if (!end && candidate.SdpMid is null && candidate.SdpMLineIndex is null)
            {
                throw new InvalidOperationException("Either sdpMid or sdpMLineIndex of the candidate is required.");
            }

            if (RemoteDescription is null)
            {
                throw new DomException(DomError.InvalidState);
            }

            await _peer.AddIceCandidateAsync(end ? null : new RtcIceCandidate(candidate)).ConfigureAwait(false);
            return true;
        });

        /// <summary>
        /// Requests new ICE credentials with the next offer.
        /// </summary>
        [DomName("restartIce")]
        public void RestartIce()
        {
            if (!_closed)
            {
                _iceRestartPending = true;
                UpdateNegotiationNeeded(false);
            }
        }

        /// <summary>
        /// Gets the configuration of the connection.
        /// </summary>
        /// <returns>A copy of the configuration.</returns>
        [DomName("getConfiguration")]
        public RtcConfiguration GetConfiguration() => _configuration.Clone();

        /// <summary>
        /// Changes the configuration of the connection. The bundle and RTCP
        /// multiplexing policies cannot be changed, the candidate pool size
        /// only before a local description has been applied.
        /// </summary>
        /// <param name="configuration">The new configuration.</param>
        [DomName("setConfiguration")]
        public void SetConfiguration(RtcConfiguration configuration)
        {
            EnsureOpen();
            var updated = (configuration ?? new RtcConfiguration()).Clone();

            if (updated.BundlePolicy != _configuration.BundlePolicy ||
                updated.RtcpMuxPolicy != _configuration.RtcpMuxPolicy ||
                (updated.IceCandidatePoolSize != _configuration.IceCandidatePoolSize && LocalDescription is not null))
            {
                throw new DomException(DomError.InvalidModification);
            }

            updated.Validate();
            _configuration = updated;
            _peer.SetConfiguration(updated.Clone());
        }

        /// <summary>
        /// Creates a data channel, which opens once the connection is
        /// established.
        /// </summary>
        /// <param name="label">The label of the channel.</param>
        /// <returns>The new channel.</returns>
        [DomName("createDataChannel")]
        public RtcDataChannel CreateDataChannel(String label) => CreateDataChannel(label, null);

        /// <summary>
        /// Creates a data channel, which opens once the connection is
        /// established.
        /// </summary>
        /// <param name="label">The label of the channel.</param>
        /// <param name="options">The options of the channel.</param>
        /// <returns>The new channel.</returns>
        [DomName("createDataChannel")]
        public RtcDataChannel CreateDataChannel(String label, RtcDataChannelInit options)
        {
            EnsureOpen();
            label = label ?? String.Empty;
            options = options ?? new RtcDataChannelInit();

            // the spec throws a TypeError for these
            if (Encoding.UTF8.GetByteCount(label) > 65535 || Encoding.UTF8.GetByteCount(options.Protocol ?? String.Empty) > 65535)
            {
                throw new InvalidOperationException("The label and protocol of a data channel must not exceed 65535 bytes.");
            }

            if (options.MaxPacketLifeTime is not null && options.MaxRetransmits is not null)
            {
                throw new InvalidOperationException("Only one of maxPacketLifeTime and maxRetransmits may be set.");
            }

            if (options.Negotiated && options.Id is null)
            {
                throw new InvalidOperationException("A negotiated data channel requires an id.");
            }

            if (options.Id == 65535)
            {
                throw new InvalidOperationException("The id of a data channel must be below 65535.");
            }

            var channel = new RtcDataChannel(this, label, new RtcDataChannelInit
            {
                Ordered = options.Ordered,
                MaxPacketLifeTime = options.MaxPacketLifeTime,
                MaxRetransmits = options.MaxRetransmits,
                Protocol = options.Protocol,
                Negotiated = options.Negotiated,
                Id = options.Negotiated ? options.Id : null,
            });

            lock (_channels)
            {
                _channels.Add(channel);
            }

            _peer.CreateDataChannel(channel);
            UpdateNegotiationNeeded(false);
            return channel;
        }

        /// <summary>
        /// Closes the connection and its data channels without raising any
        /// events.
        /// </summary>
        [DomName("close")]
        public void Close()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            _signalingState = RtcSignalingState.Closed;
            _iceConnectionState = RtcIceConnectionState.Closed;
            _connectionState = RtcPeerConnectionState.Closed;

            foreach (var channel in GetChannels())
            {
                channel.CloseSilently();
            }

            _peer.Dispose();
        }

        void IDisposable.Dispose() => Close();

        #endregion

        #region Helpers

        private static IRtcPlatform GetPlatform(IWindow window)
        {
            if (window is null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            return window.Document?.Context?.GetService<IRtcPlatform>() ?? throw new DomException(DomError.NotSupported);
        }

        private async Task<T> RunAsync<T>(Func<Task<T>> operation)
        {
            // like the spec's operations chain, one operation runs at a time
            EnsureOpen();
            await _operations.WaitAsync().ConfigureAwait(false);
            _host.BeginOperation();

            try
            {
                EnsureOpen();
                return await operation.Invoke().ConfigureAwait(false);
            }
            finally
            {
                _operations.Release();
                _host.EndOperation();
            }
        }

        private async Task<String> CreateOfferCoreAsync(RtcOfferOptions options)
        {
            EnsureSignalingState(RtcSignalingState.Stable, RtcSignalingState.HaveLocalOffer);
            var restart = _iceRestartPending || (options?.IceRestart ?? false);
            var request = new RtcSdpRequest(restart, GetChannels().Length > 0, LocalDescription, RemoteDescription);
            var sdp = await _peer.CreateOfferAsync(request).ConfigureAwait(false);
            EnsureOpen();
            _lastCreatedOffer = sdp;
            _lastCreatedOfferRestartsIce = restart;
            return sdp;
        }

        private async Task<String> CreateAnswerCoreAsync()
        {
            EnsureSignalingState(RtcSignalingState.HaveRemoteOffer, RtcSignalingState.HaveLocalPrAnswer);
            var request = new RtcSdpRequest(false, GetChannels().Length > 0, LocalDescription, RemoteDescription);
            var sdp = await _peer.CreateAnswerAsync(request).ConfigureAwait(false);
            EnsureOpen();
            _lastCreatedAnswer = sdp;
            return sdp;
        }

        private async Task RollbackAsync()
        {
            EnsureSignalingState(RtcSignalingState.HaveLocalOffer, RtcSignalingState.HaveRemoteOffer);
            await _peer.RollbackAsync().ConfigureAwait(false);
            EnsureOpen();
            _pendingLocal = null;
            _pendingRemote = null;
            SetSignalingState(RtcSignalingState.Stable);
        }

        private RtcSdpType GetImplicitType() =>
            _signalingState == RtcSignalingState.Stable ||
            _signalingState == RtcSignalingState.HaveLocalOffer ||
            _signalingState == RtcSignalingState.HaveRemotePrAnswer ? RtcSdpType.Offer : RtcSdpType.Answer;

        private void EnsureOpen()
        {
            if (_closed)
            {
                throw new DomException(DomError.InvalidState);
            }
        }

        private void EnsureSignalingState(RtcSignalingState first, RtcSignalingState second)
        {
            EnsureOpen();

            if (_signalingState != first && _signalingState != second)
            {
                throw new DomException(DomError.InvalidState);
            }
        }

        private void SetSignalingState(RtcSignalingState state)
        {
            var changed = _signalingState != state;
            _signalingState = state;

            if (changed)
            {
                Dispatch(new Event(RtcEventNames.SignalingStateChange));
            }

            if (state == RtcSignalingState.Stable)
            {
                UpdateNegotiationNeeded(true);
            }
        }

        private void UpdateNegotiationNeeded(Boolean negotiated)
        {
            if (_closed || _signalingState != RtcSignalingState.Stable)
            {
                return;
            }

            var hasApplication = _currentLocal?.Sdp.IndexOf("m=application", StringComparison.Ordinal) >= 0;
            var needed = _iceRestartPending || (GetChannels().Length > 0 && !hasApplication);

            if (!needed)
            {
                _negotiationNeeded = false;
            }
            else if (!_negotiationNeeded || negotiated)
            {
                // a negotiation that did not satisfy the need raises the
                // event again, like the spec's check after reaching stable
                _negotiationNeeded = true;
                _host.Post(() =>
                {
                    if (!_closed && _negotiationNeeded && _signalingState == RtcSignalingState.Stable)
                    {
                        Dispatch(new Event(RtcEventNames.NegotiationNeeded));
                    }
                });
            }
        }

        private RtcDataChannel[] GetChannels()
        {
            lock (_channels)
            {
                return _channels.ToArray();
            }
        }

        private void OnLocalCandidate(RtcIceCandidateInit candidate)
        {
            var value = candidate is null ? null : new RtcIceCandidate(candidate);
            Dispatch(new RtcPeerConnectionIceEvent(RtcEventNames.IceCandidate, value));
        }

        private void OnIceGatheringState(RtcIceGatheringState state)
        {
            if (_iceGatheringState != state)
            {
                _iceGatheringState = state;
                Dispatch(new Event(RtcEventNames.IceGatheringStateChange));
            }
        }

        private void OnIceConnectionState(RtcIceConnectionState state)
        {
            if (_iceConnectionState == state)
            {
                return;
            }

            _iceConnectionState = state;
            Dispatch(new Event(RtcEventNames.IceConnectionStateChange));

            // without DTLS transports of its own, the connection state follows
            // the ICE connection state
            var connectionState = state switch
            {
                RtcIceConnectionState.Checking => RtcPeerConnectionState.Connecting,
                RtcIceConnectionState.Connected => RtcPeerConnectionState.Connected,
                RtcIceConnectionState.Completed => RtcPeerConnectionState.Connected,
                RtcIceConnectionState.Disconnected => RtcPeerConnectionState.Disconnected,
                RtcIceConnectionState.Failed => RtcPeerConnectionState.Failed,
                RtcIceConnectionState.Closed => RtcPeerConnectionState.Closed,
                _ => RtcPeerConnectionState.New,
            };

            if (_connectionState != connectionState)
            {
                _connectionState = connectionState;
                Dispatch(new Event(RtcEventNames.ConnectionStateChange));
            }
        }

        #endregion

        #region Host

        /// <summary>
        /// Turns the calls of the transport into tasks of the event loop, or,
        /// without one, defers them until the running operation completed,
        /// so the state of the operation is applied before.
        /// </summary>
        private sealed class Host : IRtcPeerHost
        {
            private readonly RtcPeerConnection _connection;
            private readonly List<Action> _deferred;
            private Int32 _operations;

            public Host(RtcPeerConnection connection)
            {
                _connection = connection;
                _deferred = new List<Action>();
            }

            public IReadOnlyList<RtcDataChannel> DataChannels => _connection.GetChannels();

            public void BeginOperation()
            {
                lock (_deferred)
                {
                    _operations++;
                }
            }

            public void EndOperation()
            {
                Action[] actions;

                lock (_deferred)
                {
                    if (--_operations > 0)
                    {
                        return;
                    }

                    actions = _deferred.ToArray();
                    _deferred.Clear();
                }

                foreach (var action in actions)
                {
                    action.Invoke();
                }
            }

            public void Post(Action action)
            {
                void Run()
                {
                    if (!_connection._closed)
                    {
                        action.Invoke();
                    }
                }

                if (_connection._eventLoop is IEventLoop eventLoop)
                {
                    eventLoop.Enqueue(_ => Run(), TaskPriority.Normal);
                    return;
                }

                lock (_deferred)
                {
                    if (_operations > 0)
                    {
                        _deferred.Add(Run);
                        return;
                    }
                }

                Run();
            }

            public void AddLocalCandidate(RtcIceCandidateInit candidate) =>
                Post(() => _connection.OnLocalCandidate(candidate));

            public void SetIceGatheringState(RtcIceGatheringState state) =>
                Post(() => _connection.OnIceGatheringState(state));

            public void SetIceConnectionState(RtcIceConnectionState state) =>
                Post(() => _connection.OnIceConnectionState(state));

            public RtcDataChannel CreateRemoteDataChannel(String label, RtcDataChannelInit options)
            {
                var channel = new RtcDataChannel(_connection, label ?? String.Empty, options ?? new RtcDataChannelInit());

                lock (_connection._channels)
                {
                    _connection._channels.Add(channel);
                }

                return channel;
            }

            public void AnnounceDataChannel(RtcDataChannel channel) => Post(() =>
            {
                channel.Announce();
                _connection.Dispatch(new RtcDataChannelEvent(RtcEventNames.DataChannel, channel));
                channel.DeliverEarlyMessages();
            });

            public void OpenDataChannel(RtcDataChannel channel, UInt16 id) =>
                Post(() => channel.Open(id));

            public void ReceiveMessage(RtcDataChannel channel, Object data) =>
                Post(() => channel.Receive(data));

            public void CloseDataChannel(RtcDataChannel channel) =>
                Post(() => channel.Finish());
        }

        #endregion
    }
}
