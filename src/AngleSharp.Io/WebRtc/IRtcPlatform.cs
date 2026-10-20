namespace AngleSharp.Io.WebRtc
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the platform providing the transport behind peer
    /// connections, e.g., a WebRTC stack or a test double.
    /// </summary>
    public interface IRtcPlatform
    {
        /// <summary>
        /// Creates the transport of a new peer connection.
        /// </summary>
        /// <param name="host">The connection receiving the transport's events.</param>
        /// <param name="configuration">The configuration of the connection.</param>
        /// <returns>The transport of the connection.</returns>
        IRtcPeer CreatePeer(IRtcPeerHost host, RtcConfiguration configuration);
    }

    /// <summary>
    /// Represents the transport of a peer connection. The connection checks
    /// the signaling state before calling it, so the transport only needs to
    /// produce and apply the descriptions. A faulted task rejects the call.
    /// </summary>
    public interface IRtcPeer : IDisposable
    {
        /// <summary>
        /// Creates the SDP of an offer.
        /// </summary>
        /// <param name="request">The state of the connection.</param>
        /// <returns>The SDP of the offer.</returns>
        Task<String> CreateOfferAsync(RtcSdpRequest request);

        /// <summary>
        /// Creates the SDP of an answer to the remote offer.
        /// </summary>
        /// <param name="request">The state of the connection.</param>
        /// <returns>The SDP of the answer.</returns>
        Task<String> CreateAnswerAsync(RtcSdpRequest request);

        /// <summary>
        /// Applies the local description, which usually starts gathering
        /// candidates.
        /// </summary>
        /// <param name="description">The description to apply.</param>
        /// <returns>The task representing the operation.</returns>
        Task SetLocalDescriptionAsync(RtcSessionDescription description);

        /// <summary>
        /// Applies the remote description.
        /// </summary>
        /// <param name="description">The description to apply.</param>
        /// <returns>The task representing the operation.</returns>
        Task SetRemoteDescriptionAsync(RtcSessionDescription description);

        /// <summary>
        /// Reverts the pending descriptions to the last stable state.
        /// </summary>
        /// <returns>The task representing the operation.</returns>
        Task RollbackAsync();

        /// <summary>
        /// Adds a remote candidate, or signals the end of the remote
        /// candidates when null.
        /// </summary>
        /// <param name="candidate">The candidate to add, if any.</param>
        /// <returns>The task representing the operation.</returns>
        Task AddIceCandidateAsync(RtcIceCandidate candidate);

        /// <summary>
        /// Applies a changed configuration.
        /// </summary>
        /// <param name="configuration">The new configuration.</param>
        void SetConfiguration(RtcConfiguration configuration);

        /// <summary>
        /// Registers a data channel created locally. The transport opens it
        /// via <see cref="IRtcPeerHost.OpenDataChannel"/> once possible.
        /// </summary>
        /// <param name="channel">The created channel.</param>
        void CreateDataChannel(RtcDataChannel channel);

        /// <summary>
        /// Sends a message, i.e., a string or a byte array, on an open channel.
        /// </summary>
        /// <param name="channel">The channel sending the message.</param>
        /// <param name="data">The message.</param>
        void Send(RtcDataChannel channel, Object data);

        /// <summary>
        /// Closes a data channel. The transport finishes the closing via
        /// <see cref="IRtcPeerHost.CloseDataChannel"/>.
        /// </summary>
        /// <param name="channel">The channel to close.</param>
        void CloseDataChannel(RtcDataChannel channel);
    }

    /// <summary>
    /// Represents the peer connection as seen by its transport. The calls may
    /// come from any thread; their events are dispatched as tasks of the
    /// connection's event loop.
    /// </summary>
    public interface IRtcPeerHost
    {
        /// <summary>
        /// Gets the data channels of the connection.
        /// </summary>
        IReadOnlyList<RtcDataChannel> DataChannels { get; }

        /// <summary>
        /// Surfaces a gathered local candidate, or the end of the candidates
        /// when null.
        /// </summary>
        /// <param name="candidate">The gathered candidate, if any.</param>
        void AddLocalCandidate(RtcIceCandidateInit candidate);

        /// <summary>
        /// Updates the ICE gathering state.
        /// </summary>
        /// <param name="state">The new state.</param>
        void SetIceGatheringState(RtcIceGatheringState state);

        /// <summary>
        /// Updates the ICE connection state, which also determines the
        /// connection state.
        /// </summary>
        /// <param name="state">The new state.</param>
        void SetIceConnectionState(RtcIceConnectionState state);

        /// <summary>
        /// Creates the local counterpart of a channel the remote peer
        /// created. It is announced via <see cref="AnnounceDataChannel"/>.
        /// </summary>
        /// <param name="label">The label of the channel.</param>
        /// <param name="options">The options of the channel, including its id.</param>
        /// <returns>The new channel.</returns>
        RtcDataChannel CreateRemoteDataChannel(String label, RtcDataChannelInit options);

        /// <summary>
        /// Opens a channel created via <see cref="CreateRemoteDataChannel"/>
        /// and raises the datachannel event for it.
        /// </summary>
        /// <param name="channel">The channel to announce.</param>
        void AnnounceDataChannel(RtcDataChannel channel);

        /// <summary>
        /// Opens a locally created channel.
        /// </summary>
        /// <param name="channel">The channel to open.</param>
        /// <param name="id">The id of the channel.</param>
        void OpenDataChannel(RtcDataChannel channel, UInt16 id);

        /// <summary>
        /// Delivers a received message, i.e., a string or a byte array.
        /// </summary>
        /// <param name="channel">The channel receiving the message.</param>
        /// <param name="data">The message.</param>
        void ReceiveMessage(RtcDataChannel channel, Object data);

        /// <summary>
        /// Finishes closing a channel, which may have been closed by either
        /// peer.
        /// </summary>
        /// <param name="channel">The closed channel.</param>
        void CloseDataChannel(RtcDataChannel channel);
    }

    /// <summary>
    /// Represents the state of a connection when creating a description.
    /// </summary>
    public sealed class RtcSdpRequest
    {
        internal RtcSdpRequest(Boolean iceRestart, Boolean hasDataChannels, RtcSessionDescription local, RtcSessionDescription remote)
        {
            IceRestart = iceRestart;
            HasDataChannels = hasDataChannels;
            LocalDescription = local;
            RemoteDescription = remote;
        }

        /// <summary>
        /// Gets if the description should use new ICE credentials.
        /// </summary>
        public Boolean IceRestart { get; }

        /// <summary>
        /// Gets if the connection has data channels, i.e., needs an
        /// application media description.
        /// </summary>
        public Boolean HasDataChannels { get; }

        /// <summary>
        /// Gets the current local description, if any.
        /// </summary>
        public RtcSessionDescription LocalDescription { get; }

        /// <summary>
        /// Gets the current remote description, if any. For an answer, this
        /// is the offer to answer.
        /// </summary>
        public RtcSessionDescription RemoteDescription { get; }
    }
}
