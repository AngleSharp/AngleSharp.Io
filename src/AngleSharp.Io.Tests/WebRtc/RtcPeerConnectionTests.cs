namespace AngleSharp.Io.Tests.WebRtc
{
    using AngleSharp;
    using AngleSharp.Browser;
    using AngleSharp.Common;
    using AngleSharp.Dom;
    using AngleSharp.Dom.Events;
    using AngleSharp.Io;
    using AngleSharp.Io.WebRtc;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    [TestFixture]
    public class RtcPeerConnectionTests
    {
        [Test]
        public void NewConnectionStartsInTheInitialStates()
        {
            var connection = new RtcPeerConnection(new LoopbackRtcPlatform());

            Assert.AreEqual(RtcSignalingState.Stable, connection.SignalingState);
            Assert.AreEqual(RtcIceGatheringState.New, connection.IceGatheringState);
            Assert.AreEqual(RtcIceConnectionState.New, connection.IceConnectionState);
            Assert.AreEqual(RtcPeerConnectionState.New, connection.ConnectionState);
            Assert.IsNull(connection.LocalDescription);
            Assert.IsNull(connection.RemoteDescription);
            Assert.IsNull(connection.CanTrickleIceCandidates);
            Assert.Throws<ArgumentNullException>(() => new RtcPeerConnection((IRtcPlatform)null));
        }

        [Test]
        public async Task CreateOfferReturnsAValidOfferShape()
        {
            var connection = new RtcPeerConnection(new LoopbackRtcPlatform());

            var empty = await connection.CreateOffer().ConfigureAwait(false);
            connection.CreateDataChannel("chat");
            var offer = await connection.CreateOffer().ConfigureAwait(false);

            Assert.AreEqual(RtcSdpType.Offer, empty.Type);
            StringAssert.StartsWith("v=0\r\no=- ", empty.Sdp);
            StringAssert.Contains("\r\ns=-\r\nt=0 0\r\n", empty.Sdp);
            StringAssert.DoesNotContain("m=", empty.Sdp);
            Assert.AreEqual(RtcSdpType.Offer, offer.Type);
            StringAssert.Contains("a=group:BUNDLE 0\r\n", offer.Sdp);
            StringAssert.Contains("m=application 9 UDP/DTLS/SCTP webrtc-datachannel\r\n", offer.Sdp);
            StringAssert.Contains("a=ice-ufrag:", offer.Sdp);
            StringAssert.Contains("a=ice-pwd:", offer.Sdp);
            StringAssert.Contains("a=fingerprint:sha-256 ", offer.Sdp);
            StringAssert.Contains("a=setup:actpass\r\n", offer.Sdp);
            StringAssert.Contains("a=mid:0\r\n", offer.Sdp);
            Assert.IsTrue(offer.Sdp.EndsWith("\r\n"));
            Assert.AreEqual(RtcSignalingState.Stable, connection.SignalingState);
        }

        [Test]
        public async Task OfferAnswerExchangeWalksThroughTheSignalingStates()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);
            var callerStates = RecordSignalingStates(caller);
            var calleeStates = RecordSignalingStates(callee);
            caller.CreateDataChannel("chat");

            var offer = await caller.CreateOffer().ConfigureAwait(false);
            await caller.SetLocalDescription(offer).ConfigureAwait(false);
            Assert.AreEqual(RtcSignalingState.HaveLocalOffer, caller.SignalingState);
            Assert.AreEqual(offer.Sdp, caller.PendingLocalDescription.Sdp);
            Assert.AreSame(caller.PendingLocalDescription, caller.LocalDescription);
            Assert.IsNull(caller.CurrentLocalDescription);

            await callee.SetRemoteDescription(offer).ConfigureAwait(false);
            Assert.AreEqual(RtcSignalingState.HaveRemoteOffer, callee.SignalingState);
            Assert.AreEqual(RtcSdpType.Offer, callee.PendingRemoteDescription.Type);

            var answer = await callee.CreateAnswer().ConfigureAwait(false);
            Assert.AreEqual(RtcSdpType.Answer, answer.Type);
            StringAssert.Contains("a=setup:active\r\n", answer.Sdp);
            StringAssert.Contains("m=application", answer.Sdp);

            await callee.SetLocalDescription(answer).ConfigureAwait(false);
            await caller.SetRemoteDescription(answer).ConfigureAwait(false);

            foreach (var connection in new[] { caller, callee })
            {
                Assert.AreEqual(RtcSignalingState.Stable, connection.SignalingState);
                Assert.IsNull(connection.PendingLocalDescription);
                Assert.IsNull(connection.PendingRemoteDescription);
                Assert.IsNotNull(connection.CurrentLocalDescription);
                Assert.IsNotNull(connection.CurrentRemoteDescription);
                Assert.IsTrue(connection.CanTrickleIceCandidates);
            }

            Assert.AreEqual(offer.Sdp, caller.CurrentLocalDescription.Sdp);
            Assert.AreEqual(answer.Sdp, caller.CurrentRemoteDescription.Sdp);
            CollectionAssert.AreEqual(new[] { RtcSignalingState.HaveLocalOffer, RtcSignalingState.Stable }, callerStates);
            CollectionAssert.AreEqual(new[] { RtcSignalingState.HaveRemoteOffer, RtcSignalingState.Stable }, calleeStates);
        }

        [Test]
        public async Task MethodsRejectInvalidSignalingStates()
        {
            var platform = new LoopbackRtcPlatform();
            var connection = new RtcPeerConnection(platform);
            var other = new RtcPeerConnection(platform);

            AssertDomError(DomError.InvalidState, () => connection.CreateAnswer());
            AssertDomError(DomError.InvalidState, () => connection.SetLocalDescription(new RtcSessionDescriptionInit { Type = RtcSdpType.Answer }));
            AssertDomError(DomError.InvalidState, () => connection.SetRemoteDescription(new RtcSessionDescriptionInit { Type = RtcSdpType.Answer, Sdp = "v=0\r\n" }));
            AssertDomError(DomError.InvalidState, () => connection.SetLocalDescription(new RtcSessionDescriptionInit { Type = RtcSdpType.Rollback }));

            var offer = await other.CreateOffer().ConfigureAwait(false);
            await connection.SetRemoteDescription(offer).ConfigureAwait(false);

            AssertDomError(DomError.InvalidState, () => connection.CreateOffer());
            AssertDomError(DomError.InvalidState, () => connection.SetLocalDescription(new RtcSessionDescriptionInit { Type = RtcSdpType.Offer }));
            Assert.AreEqual(RtcSignalingState.HaveRemoteOffer, connection.SignalingState);
        }

        [Test]
        public async Task SetLocalDescriptionRequiresTheLastCreatedDescription()
        {
            var connection = new RtcPeerConnection(new LoopbackRtcPlatform());
            var first = await connection.CreateOffer().ConfigureAwait(false);
            var second = await connection.CreateOffer().ConfigureAwait(false);

            Assert.AreNotEqual(first.Sdp, second.Sdp);
            AssertDomError(DomError.InvalidModification, () => connection.SetLocalDescription(first));
            AssertDomError(DomError.InvalidModification, () => connection.SetLocalDescription(new RtcSessionDescriptionInit { Type = RtcSdpType.Offer, Sdp = second.Sdp + "a=x\r\n" }));

            await connection.SetLocalDescription(new RtcSessionDescriptionInit { Type = RtcSdpType.Offer }).ConfigureAwait(false);

            Assert.AreEqual(second.Sdp, connection.LocalDescription.Sdp);
        }

        [Test]
        public async Task ParameterlessSetLocalDescriptionCreatesOffersAndAnswers()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);

            await caller.SetLocalDescription().ConfigureAwait(false);
            await callee.SetRemoteDescription(caller.LocalDescription).ConfigureAwait(false);
            await callee.SetLocalDescription().ConfigureAwait(false);
            await caller.SetRemoteDescription(callee.LocalDescription).ConfigureAwait(false);

            Assert.AreEqual(RtcSdpType.Offer, caller.CurrentLocalDescription.Type);
            Assert.AreEqual(RtcSdpType.Answer, callee.CurrentLocalDescription.Type);
            Assert.AreEqual(RtcSignalingState.Stable, caller.SignalingState);
            Assert.AreEqual(RtcSignalingState.Stable, callee.SignalingState);
        }

        [Test]
        public async Task RollbackReturnsToStable()
        {
            var platform = new LoopbackRtcPlatform();
            var connection = new RtcPeerConnection(platform);
            var other = new RtcPeerConnection(platform);
            var states = RecordSignalingStates(connection);

            await connection.SetLocalDescription().ConfigureAwait(false);
            await connection.SetLocalDescription(new RtcSessionDescriptionInit { Type = RtcSdpType.Rollback }).ConfigureAwait(false);

            Assert.AreEqual(RtcSignalingState.Stable, connection.SignalingState);
            Assert.IsNull(connection.LocalDescription);

            await connection.SetRemoteDescription(await other.CreateOffer().ConfigureAwait(false)).ConfigureAwait(false);
            await connection.SetRemoteDescription(new RtcSessionDescriptionInit { Type = RtcSdpType.Rollback }).ConfigureAwait(false);

            Assert.IsNull(connection.RemoteDescription);
            CollectionAssert.AreEqual(new[] { RtcSignalingState.HaveLocalOffer, RtcSignalingState.Stable, RtcSignalingState.HaveRemoteOffer, RtcSignalingState.Stable }, states);
        }

        [Test]
        public async Task RemoteOfferDuringALocalOfferRollsBackImplicitly()
        {
            var platform = new LoopbackRtcPlatform();
            var polite = new RtcPeerConnection(platform);
            var impolite = new RtcPeerConnection(platform);
            var states = RecordSignalingStates(polite);

            await polite.SetLocalDescription().ConfigureAwait(false);
            await impolite.SetLocalDescription().ConfigureAwait(false);
            await polite.SetRemoteDescription(impolite.LocalDescription).ConfigureAwait(false);

            Assert.AreEqual(RtcSignalingState.HaveRemoteOffer, polite.SignalingState);
            Assert.IsNull(polite.PendingLocalDescription);
            CollectionAssert.AreEqual(new[] { RtcSignalingState.HaveLocalOffer, RtcSignalingState.HaveRemoteOffer }, states);
        }

        [Test]
        public async Task ProvisionalAnswersKeepTheNegotiationOpen()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);
            await caller.SetLocalDescription().ConfigureAwait(false);
            await callee.SetRemoteDescription(caller.LocalDescription).ConfigureAwait(false);
            var answer = await callee.CreateAnswer().ConfigureAwait(false);

            await callee.SetLocalDescription(new RtcSessionDescriptionInit { Type = RtcSdpType.PrAnswer, Sdp = answer.Sdp }).ConfigureAwait(false);
            await caller.SetRemoteDescription(new RtcSessionDescriptionInit { Type = RtcSdpType.PrAnswer, Sdp = answer.Sdp }).ConfigureAwait(false);

            Assert.AreEqual(RtcSignalingState.HaveLocalPrAnswer, callee.SignalingState);
            Assert.AreEqual(RtcSignalingState.HaveRemotePrAnswer, caller.SignalingState);
            Assert.AreEqual(RtcSdpType.PrAnswer, callee.LocalDescription.Type);

            await callee.SetLocalDescription(answer).ConfigureAwait(false);
            await caller.SetRemoteDescription(answer).ConfigureAwait(false);

            Assert.AreEqual(RtcSignalingState.Stable, callee.SignalingState);
            Assert.AreEqual(RtcSignalingState.Stable, caller.SignalingState);
        }

        [Test]
        public void SetRemoteDescriptionValidatesTheDescription()
        {
            var connection = new RtcPeerConnection(new LoopbackRtcPlatform());

            Assert.ThrowsAsync<InvalidOperationException>(() => connection.SetRemoteDescription((RtcSessionDescriptionInit)null));
            Assert.ThrowsAsync<InvalidOperationException>(() => connection.SetRemoteDescription(new RtcSessionDescriptionInit { Sdp = "v=0\r\n" }));
            AssertDomError(DomError.Syntax, () => connection.SetRemoteDescription(new RtcSessionDescriptionInit { Type = RtcSdpType.Offer }));
            AssertDomError(DomError.Syntax, () => connection.SetRemoteDescription(new RtcSessionDescriptionInit { Type = RtcSdpType.Offer, Sdp = "garbage" }));
            Assert.AreEqual(RtcSignalingState.Stable, connection.SignalingState);
        }

        [Test]
        public async Task CloseEndsTheConnectionWithoutEvents()
        {
            var connection = new RtcPeerConnection(new LoopbackRtcPlatform());
            var channel = connection.CreateDataChannel("chat");
            var events = RecordAllEvents(connection);
            await connection.SetLocalDescription().ConfigureAwait(false);
            events.Clear();

            connection.Close();
            connection.Close();

            Assert.AreEqual(RtcSignalingState.Closed, connection.SignalingState);
            Assert.AreEqual(RtcIceConnectionState.Closed, connection.IceConnectionState);
            Assert.AreEqual(RtcPeerConnectionState.Closed, connection.ConnectionState);
            Assert.AreEqual(RtcDataChannelState.Closed, channel.ReadyState);
            Assert.IsEmpty(events);
            AssertDomError(DomError.InvalidState, () => connection.CreateOffer());
            AssertDomError(DomError.InvalidState, () => connection.SetLocalDescription());
            AssertDomError(DomError.InvalidState, () => connection.AddIceCandidate());
            Assert.Throws<DomException>(() => connection.CreateDataChannel("other"));
            Assert.Throws<DomException>(() => connection.SetConfiguration(new RtcConfiguration()));
        }

        [Test]
        public async Task SettingTheLocalDescriptionGathersCandidates()
        {
            var connection = new RtcPeerConnection(new LoopbackRtcPlatform());
            var events = RecordAllEvents(connection);
            var candidates = new List<RtcIceCandidate>();
            connection.IceCandidate += (s, e) => candidates.Add(((RtcPeerConnectionIceEvent)e).Candidate);
            connection.CreateDataChannel("chat");
            events.Clear();

            await connection.SetLocalDescription().ConfigureAwait(false);

            CollectionAssert.AreEqual(new[]
            {
                "signalingstatechange",
                "icegatheringstatechange:gathering",
                "icecandidate",
                "icegatheringstatechange:complete",
                "icecandidate:end",
            }, events);
            Assert.AreEqual(RtcIceGatheringState.Complete, connection.IceGatheringState);

            var candidate = candidates[0];
            var ufrag = connection.LocalDescription.Sdp.Split('\n').First(m => m.StartsWith("a=ice-ufrag:")).Substring(12).TrimEnd('\r');
            StringAssert.StartsWith("candidate:1 1 udp 2122260223 127.0.0.1 ", candidate.Candidate);
            Assert.AreEqual("0", candidate.SdpMid);
            Assert.AreEqual((UInt16)0, candidate.SdpMLineIndex);
            Assert.AreEqual("1", candidate.Foundation);
            Assert.AreEqual(RtcIceComponent.Rtp, candidate.Component);
            Assert.AreEqual(RtcIceProtocol.Udp, candidate.Protocol);
            Assert.AreEqual(2122260223u, candidate.Priority);
            Assert.AreEqual("127.0.0.1", candidate.Address);
            Assert.IsNotNull(candidate.Port);
            Assert.AreEqual(RtcIceCandidateType.Host, candidate.Type);
            Assert.AreEqual(ufrag, candidate.UsernameFragment);
            Assert.IsNull(candidates[1]);
        }

        [Test]
        public async Task CandidateEventsSeeTheAppliedDescription()
        {
            var connection = new RtcPeerConnection(new LoopbackRtcPlatform());
            var observed = default(RtcSignalingState?);
            var description = default(RtcSessionDescription);
            connection.IceCandidate += (s, e) =>
            {
                observed ??= connection.SignalingState;
                description ??= connection.LocalDescription;
            };
            connection.CreateDataChannel("chat");

            await connection.SetLocalDescription().ConfigureAwait(false);

            Assert.AreEqual(RtcSignalingState.HaveLocalOffer, observed);
            Assert.IsNotNull(description);
        }

        [Test]
        public async Task ConnectionsWithoutMediaDescriptionsDoNotGather()
        {
            var connection = new RtcPeerConnection(new LoopbackRtcPlatform());
            var candidates = 0;
            connection.IceCandidate += (s, e) => candidates++;

            await connection.SetLocalDescription().ConfigureAwait(false);

            Assert.AreEqual(RtcIceGatheringState.New, connection.IceGatheringState);
            Assert.AreEqual(0, candidates);
        }

        [Test]
        public async Task ExchangingCandidatesConnectsThePeers()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);
            var callerEvents = RecordAllEvents(caller);
            var calleeEvents = RecordAllEvents(callee);
            caller.CreateDataChannel("chat");

            await ConnectAsync(caller, callee).ConfigureAwait(false);

            foreach (var connection in new[] { caller, callee })
            {
                Assert.AreEqual(RtcIceConnectionState.Connected, connection.IceConnectionState);
                Assert.AreEqual(RtcPeerConnectionState.Connected, connection.ConnectionState);
            }

            var expected = new[]
            {
                "iceconnectionstatechange:checking",
                "connectionstatechange:connecting",
                "iceconnectionstatechange:connected",
                "connectionstatechange:connected",
            };
            CollectionAssert.AreEqual(expected, callerEvents.Where(m => m.Contains("connectionstatechange")));
            CollectionAssert.AreEqual(expected, calleeEvents.Where(m => m.Contains("connectionstatechange")));
        }

        [Test]
        public async Task CandidatesOfUnknownPeersDoNotConnect()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);
            caller.CreateDataChannel("chat");
            await caller.SetLocalDescription().ConfigureAwait(false);
            await callee.SetRemoteDescription(caller.LocalDescription).ConfigureAwait(false);
            await callee.SetLocalDescription().ConfigureAwait(false);
            await caller.SetRemoteDescription(callee.LocalDescription).ConfigureAwait(false);

            await caller.AddIceCandidate(new RtcIceCandidateInit { Candidate = "candidate:1 1 udp 2122260223 192.0.2.1 1234 typ host", SdpMid = "0" }).ConfigureAwait(false);
            await caller.AddIceCandidate().ConfigureAwait(false);

            Assert.AreEqual(RtcIceConnectionState.New, caller.IceConnectionState);
        }

        [Test]
        public async Task AddIceCandidateValidatesItsInput()
        {
            var platform = new LoopbackRtcPlatform();
            var connection = new RtcPeerConnection(platform);
            var candidate = new RtcIceCandidateInit { Candidate = "candidate:1 1 udp 1 127.0.0.1 9 typ host", SdpMid = "0" };

            AssertDomError(DomError.InvalidState, () => connection.AddIceCandidate(candidate));
            Assert.ThrowsAsync<InvalidOperationException>(() => connection.AddIceCandidate(new RtcIceCandidateInit { Candidate = candidate.Candidate }));

            await connection.SetRemoteDescription(await new RtcPeerConnection(platform).CreateOffer().ConfigureAwait(false)).ConfigureAwait(false);

            await connection.AddIceCandidate(candidate).ConfigureAwait(false);
            await connection.AddIceCandidate(new RtcIceCandidateInit()).ConfigureAwait(false);
            await connection.AddIceCandidate().ConfigureAwait(false);
        }

        [Test]
        public async Task RelayPolicyGathersNoHostCandidates()
        {
            var platform = new LoopbackRtcPlatform();
            var configuration = new RtcConfiguration { IceTransportPolicy = RtcIceTransportPolicy.Relay };
            var caller = new RtcPeerConnection(platform, configuration);
            var callee = new RtcPeerConnection(platform, configuration);
            var candidates = new List<RtcIceCandidate>();
            caller.IceCandidate += (s, e) => candidates.Add(((RtcPeerConnectionIceEvent)e).Candidate);
            caller.CreateDataChannel("chat");

            await ConnectAsync(caller, callee).ConfigureAwait(false);

            CollectionAssert.AreEqual(new RtcIceCandidate[] { null }, candidates);
            Assert.AreEqual(RtcIceGatheringState.Complete, caller.IceGatheringState);
            Assert.AreEqual(RtcIceConnectionState.New, caller.IceConnectionState);
        }

        [Test]
        public void IceCandidatesAreParsed()
        {
            var srflx = new RtcIceCandidate(new RtcIceCandidateInit
            {
                Candidate = "candidate:842163049 1 UDP 1677729535 203.0.113.7 61665 typ srflx raddr 192.168.1.2 rport 61665 generation 0 ufrag abcd",
                SdpMLineIndex = 0,
            });
            var tcp = new RtcIceCandidate(new RtcIceCandidateInit
            {
                Candidate = "candidate:3 2 tcp 1518280447 192.168.1.2 9 typ host tcptype active",
                SdpMid = "1",
                UsernameFragment = "given",
            });
            var broken = new RtcIceCandidate(new RtcIceCandidateInit { Candidate = "candidate:x y", SdpMid = "0" });

            Assert.AreEqual("842163049", srflx.Foundation);
            Assert.AreEqual(RtcIceProtocol.Udp, srflx.Protocol);
            Assert.AreEqual(RtcIceCandidateType.Srflx, srflx.Type);
            Assert.AreEqual("192.168.1.2", srflx.RelatedAddress);
            Assert.AreEqual((UInt16)61665, srflx.RelatedPort);
            Assert.AreEqual("abcd", srflx.UsernameFragment);
            Assert.IsNull(srflx.SdpMid);
            Assert.AreEqual(RtcIceComponent.Rtcp, tcp.Component);
            Assert.AreEqual(RtcIceProtocol.Tcp, tcp.Protocol);
            Assert.AreEqual(RtcIceTcpCandidateType.Active, tcp.TcpType);
            Assert.AreEqual("given", tcp.UsernameFragment);
            Assert.IsNull(broken.Foundation);
            Assert.IsNull(broken.Port);
            Assert.IsNull(broken.Type);
            Assert.Throws<InvalidOperationException>(() => new RtcIceCandidate(new RtcIceCandidateInit { Candidate = "candidate:1" }));
            Assert.Throws<InvalidOperationException>(() => new RtcIceCandidate(null));

            var json = tcp.ToJson();
            Assert.AreEqual(tcp.Candidate, json.Candidate);
            Assert.AreEqual("1", json.SdpMid);
            Assert.IsNull(json.SdpMLineIndex);
            Assert.AreEqual("given", json.UsernameFragment);
        }

        [Test]
        public void SessionDescriptionsRoundTrip()
        {
            var description = new RtcSessionDescription(new RtcSessionDescriptionInit { Type = RtcSdpType.Answer, Sdp = "v=0\r\n" });
            var json = description.ToJson();

            Assert.AreEqual(RtcSdpType.Answer, description.Type);
            Assert.AreEqual("v=0\r\n", description.Sdp);
            Assert.AreEqual(RtcSdpType.Answer, json.Type);
            Assert.AreEqual("v=0\r\n", json.Sdp);
            Assert.Throws<InvalidOperationException>(() => new RtcSessionDescription(new RtcSessionDescriptionInit()));
        }

        [Test]
        public async Task CreatingADataChannelNeedsNegotiationOnce()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);
            var needed = 0;
            caller.NegotiationNeeded += (s, e) => needed++;

            caller.CreateDataChannel("one");
            caller.CreateDataChannel("two");
            Assert.AreEqual(1, needed);

            await ConnectAsync(caller, callee).ConfigureAwait(false);
            caller.CreateDataChannel("three");

            Assert.AreEqual(1, needed);
        }

        [Test]
        public async Task NegotiationNeededWaitsForTheStableState()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);
            var needed = 0;
            callee.NegotiationNeeded += (s, e) => needed++;
            await caller.SetLocalDescription().ConfigureAwait(false);
            await callee.SetRemoteDescription(caller.LocalDescription).ConfigureAwait(false);

            callee.CreateDataChannel("late");
            Assert.AreEqual(0, needed);

            await callee.SetLocalDescription().ConfigureAwait(false);

            Assert.AreEqual(1, needed);
        }

        [Test]
        public async Task RestartIceUsesNewCredentials()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);
            caller.CreateDataChannel("chat");
            await ConnectAsync(caller, callee).ConfigureAwait(false);
            var needed = 0;
            var gathered = new List<String>();
            caller.NegotiationNeeded += (s, e) => needed++;
            caller.IceCandidate += (s, e) => gathered.Add(((RtcPeerConnectionIceEvent)e).Candidate?.UsernameFragment);
            var before = GetUfrag(caller.LocalDescription);
            var calleeBefore = GetUfrag(callee.LocalDescription);

            caller.RestartIce();
            Assert.AreEqual(1, needed);

            var offer = await caller.CreateOffer().ConfigureAwait(false);
            await caller.SetLocalDescription(offer).ConfigureAwait(false);
            await callee.SetRemoteDescription(offer).ConfigureAwait(false);
            var answer = await callee.CreateAnswer().ConfigureAwait(false);
            await callee.SetLocalDescription(answer).ConfigureAwait(false);
            await caller.SetRemoteDescription(answer).ConfigureAwait(false);

            Assert.AreNotEqual(before, GetUfrag(caller.LocalDescription));
            Assert.AreNotEqual(calleeBefore, GetUfrag(answer.Sdp));
            CollectionAssert.AreEqual(new[] { GetUfrag(caller.LocalDescription), null }, gathered);
            Assert.AreEqual(1, needed);
            Assert.AreEqual(RtcPeerConnectionState.Connected, caller.ConnectionState);

            var again = await caller.CreateOffer().ConfigureAwait(false);
            Assert.AreEqual(GetUfrag(caller.LocalDescription), GetUfrag(again.Sdp));
            Assert.AreEqual(GetUfrag(callee.LocalDescription), GetUfrag(caller.RemoteDescription));
        }

        [Test]
        public async Task AnnouncedDataChannelsOpenOnBothPeers()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);
            var local = caller.CreateDataChannel("chat", new RtcDataChannelInit { Ordered = false, MaxRetransmits = 3, Protocol = "json" });
            var opened = 0;
            var remote = default(RtcDataChannel);
            var remoteState = default(RtcDataChannelState?);
            local.Opened += (s, e) => opened++;
            callee.DataChannel += (s, e) =>
            {
                remote = ((RtcDataChannelEvent)e).Channel;
                remoteState = remote.ReadyState;
            };

            Assert.AreEqual(RtcDataChannelState.Connecting, local.ReadyState);
            Assert.IsNull(local.Id);

            await ConnectAsync(caller, callee).ConfigureAwait(false);

            Assert.AreEqual(1, opened);
            Assert.AreEqual(RtcDataChannelState.Open, local.ReadyState);
            Assert.IsNotNull(remote);
            Assert.AreEqual(RtcDataChannelState.Open, remoteState);
            Assert.AreEqual("chat", remote.Label);
            Assert.AreEqual("json", remote.Protocol);
            Assert.IsFalse(remote.Ordered);
            Assert.AreEqual((UInt16)3, remote.MaxRetransmits);
            Assert.IsFalse(remote.Negotiated);
            Assert.AreEqual(local.Id, remote.Id);
            Assert.AreEqual((UInt16)1, local.Id);
        }

        [Test]
        public async Task DataChannelsExchangeMessages()
        {
            var (local, remote) = await OpenChannelPairAsync().ConfigureAwait(false);
            var atRemote = RecordMessages(remote);
            var atLocal = RecordMessages(local);
            var bytes = new Byte[] { 1, 2, 3 };

            local.Send("hello");
            local.Send(bytes);
            bytes[0] = 9;
            remote.Send("back");

            Assert.AreEqual(2, atRemote.Count);
            Assert.AreEqual("hello", atRemote[0]);
            CollectionAssert.AreEqual(new Byte[] { 1, 2, 3 }, (Byte[])atRemote[1]);
            CollectionAssert.AreEqual(new Object[] { "back" }, atLocal);
            Assert.AreEqual(0u, local.BufferedAmount);
        }

        [Test]
        public async Task MessagesSentWhenOpeningAreNotLost()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);
            var local = caller.CreateDataChannel("chat");
            var received = new List<Object>();
            local.Opened += (s, e) => local.Send("first");
            callee.DataChannel += (s, e) =>
            {
                var channel = ((RtcDataChannelEvent)e).Channel;
                channel.Message += (s2, e2) => received.Add(((MessageEvent)e2).Data);
            };

            await ConnectAsync(caller, callee).ConfigureAwait(false);

            CollectionAssert.AreEqual(new Object[] { "first" }, received);
        }

        [Test]
        public async Task NegotiatedDataChannelsOpenWithoutAnnouncement()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);
            var local = caller.CreateDataChannel("pre", new RtcDataChannelInit { Negotiated = true, Id = 5 });
            var remote = callee.CreateDataChannel("pre", new RtcDataChannelInit { Negotiated = true, Id = 5 });
            var announced = 0;
            callee.DataChannel += (s, e) => announced++;

            await ConnectAsync(caller, callee).ConfigureAwait(false);
            var received = RecordMessages(remote);
            local.Send("ping");

            Assert.AreEqual(RtcDataChannelState.Open, local.ReadyState);
            Assert.AreEqual(RtcDataChannelState.Open, remote.ReadyState);
            Assert.AreEqual((UInt16)5, remote.Id);
            Assert.AreEqual(0, announced);
            CollectionAssert.AreEqual(new Object[] { "ping" }, received);
        }

        [Test]
        public async Task ChannelsCreatedOnAnEstablishedConnectionOpenRightAway()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);
            caller.CreateDataChannel("first");
            await ConnectAsync(caller, callee).ConfigureAwait(false);
            var announced = new List<RtcDataChannel>();
            caller.DataChannel += (s, e) => announced.Add(((RtcDataChannelEvent)e).Channel);

            var second = callee.CreateDataChannel("second");

            Assert.AreEqual(RtcDataChannelState.Open, second.ReadyState);
            Assert.AreEqual((UInt16)0, second.Id);
            Assert.AreEqual("second", announced.Single().Label);
        }

        [Test]
        public async Task ClosingAChannelClosesItOnBothPeers()
        {
            var (local, remote) = await OpenChannelPairAsync().ConfigureAwait(false);
            var localEvents = RecordChannelEvents(local);
            var remoteEvents = RecordChannelEvents(remote);

            local.Close();
            local.Close();

            Assert.AreEqual(RtcDataChannelState.Closed, local.ReadyState);
            Assert.AreEqual(RtcDataChannelState.Closed, remote.ReadyState);
            CollectionAssert.AreEqual(new[] { "close" }, localEvents);
            CollectionAssert.AreEqual(new[] { "closing", "close" }, remoteEvents);
            Assert.Throws<DomException>(() => local.Send("late"));
        }

        [Test]
        public void SendingBeforeTheChannelIsOpenFails()
        {
            var channel = new RtcPeerConnection(new LoopbackRtcPlatform()).CreateDataChannel("chat");

            var ex = Assert.Throws<DomException>(() => channel.Send("too early"));

            Assert.AreEqual((Int32)DomError.InvalidState, ex.Code);
        }

        [Test]
        public async Task ClosingAConnectionDisconnectsTheRemotePeer()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);
            var local = caller.CreateDataChannel("chat");
            var remote = default(RtcDataChannel);
            callee.DataChannel += (s, e) => remote = ((RtcDataChannelEvent)e).Channel;
            await ConnectAsync(caller, callee).ConfigureAwait(false);
            var remoteEvents = RecordChannelEvents(remote);

            caller.Close();

            Assert.AreEqual(RtcDataChannelState.Closed, local.ReadyState);
            Assert.AreEqual(RtcIceConnectionState.Disconnected, callee.IceConnectionState);
            Assert.AreEqual(RtcPeerConnectionState.Disconnected, callee.ConnectionState);
            Assert.AreEqual(RtcDataChannelState.Closed, remote.ReadyState);
            CollectionAssert.AreEqual(new[] { "closing", "close" }, remoteEvents);
        }

        [Test]
        public void CreateDataChannelValidatesItsOptions()
        {
            var connection = new RtcPeerConnection(new LoopbackRtcPlatform());

            Assert.Throws<InvalidOperationException>(() => connection.CreateDataChannel("x", new RtcDataChannelInit { Negotiated = true }));
            Assert.Throws<InvalidOperationException>(() => connection.CreateDataChannel("x", new RtcDataChannelInit { MaxPacketLifeTime = 1, MaxRetransmits = 1 }));
            Assert.Throws<InvalidOperationException>(() => connection.CreateDataChannel("x", new RtcDataChannelInit { Negotiated = true, Id = 65535 }));
            Assert.Throws<InvalidOperationException>(() => connection.CreateDataChannel(new String('x', 65536)));

            var channel = connection.CreateDataChannel(null, new RtcDataChannelInit { Id = 7 });
            Assert.AreEqual(String.Empty, channel.Label);
            Assert.IsNull(channel.Id);
            Assert.AreEqual("arraybuffer", channel.BinaryType);
            channel.BinaryType = "blob";
            channel.BinaryType = "invalid";
            Assert.AreEqual("blob", channel.BinaryType);
        }

        [Test]
        public void ConfigurationIsValidated()
        {
            var platform = new LoopbackRtcPlatform();

            AssertDomError(DomError.Syntax, () => new RtcPeerConnection(platform, Servers(new RtcIceServer { Urls = new[] { "http://stun.example" } })));
            AssertDomError(DomError.Syntax, () => new RtcPeerConnection(platform, Servers(new RtcIceServer())));
            AssertDomError(DomError.Syntax, () => new RtcPeerConnection(platform, Servers(new RtcIceServer { Urls = new[] { "stun:" } })));
            AssertDomError(DomError.InvalidAccess, () => new RtcPeerConnection(platform, Servers(new RtcIceServer { Urls = new[] { "turn:turn.example" } })));

            var configuration = Servers(
                new RtcIceServer { Urls = new[] { "stun:stun.example:3478", "STUNS:stun.example" } },
                new RtcIceServer { Urls = new[] { "turn:turn.example?transport=tcp" }, Username = "user", Credential = "secret" });
            var connection = new RtcPeerConnection(platform, configuration);
            configuration.IceServers[0].Urls[0] = "changed";

            var copy = connection.GetConfiguration();
            Assert.AreEqual("stun:stun.example:3478", copy.IceServers[0].Urls[0]);
            Assert.AreEqual("user", copy.IceServers[1].Username);
            copy.IceServers[0].Urls[0] = "changed";
            Assert.AreEqual("stun:stun.example:3478", connection.GetConfiguration().IceServers[0].Urls[0]);
        }

        [Test]
        public async Task SetConfigurationKeepsTheFixedPolicies()
        {
            var connection = new RtcPeerConnection(new LoopbackRtcPlatform(), new RtcConfiguration { IceCandidatePoolSize = 1 });

            AssertDomError(DomError.InvalidModification, () => connection.SetConfiguration(new RtcConfiguration { BundlePolicy = RtcBundlePolicy.MaxBundle, IceCandidatePoolSize = 1 }));
            connection.SetConfiguration(new RtcConfiguration { IceTransportPolicy = RtcIceTransportPolicy.Relay, IceCandidatePoolSize = 2 });
            Assert.AreEqual(RtcIceTransportPolicy.Relay, connection.GetConfiguration().IceTransportPolicy);

            await connection.SetLocalDescription().ConfigureAwait(false);

            AssertDomError(DomError.InvalidModification, () => connection.SetConfiguration(new RtcConfiguration { IceCandidatePoolSize = 3 }));
            AssertDomError(DomError.Syntax, () => connection.SetConfiguration(new RtcConfiguration { IceCandidatePoolSize = 2, IceServers = new[] { new RtcIceServer { Urls = new[] { "x" } } } }));
            Assert.AreEqual(2, connection.GetConfiguration().IceCandidatePoolSize);
        }

        [Test]
        public async Task TransportEventsAreQueuedOnTheEventLoop()
        {
            var loop = new ManualEventLoop();
            var connection = new RtcPeerConnection(new LoopbackRtcPlatform(), null, loop);
            var events = RecordAllEvents(connection);
            connection.CreateDataChannel("chat");

            Assert.IsEmpty(events);
            loop.RunAll();
            CollectionAssert.AreEqual(new[] { "negotiationneeded" }, events);
            events.Clear();

            await connection.SetLocalDescription().ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "signalingstatechange" }, events);
            Assert.AreEqual(RtcIceGatheringState.New, connection.IceGatheringState);

            loop.RunAll();

            Assert.AreEqual(RtcIceGatheringState.Complete, connection.IceGatheringState);
            CollectionAssert.AreEqual(new[]
            {
                "signalingstatechange",
                "icegatheringstatechange:gathering",
                "icecandidate",
                "icegatheringstatechange:complete",
                "icecandidate:end",
            }, events);
        }

        [Test]
        public async Task PeersConnectThroughTheirEventLoops()
        {
            var platform = new LoopbackRtcPlatform();
            var loop = new ManualEventLoop();
            var caller = new RtcPeerConnection(platform, null, loop);
            var callee = new RtcPeerConnection(platform, null, loop);
            var local = caller.CreateDataChannel("chat");
            var received = new List<Object>();
            callee.DataChannel += (s, e) => ((RtcDataChannelEvent)e).Channel.Message += (s2, e2) => received.Add(((MessageEvent)e2).Data);
            local.Opened += (s, e) => local.Send("over the loop");

            await ConnectAsync(caller, callee, loop.RunAll).ConfigureAwait(false);
            loop.RunAll();

            Assert.AreEqual(RtcPeerConnectionState.Connected, caller.ConnectionState);
            Assert.AreEqual(RtcPeerConnectionState.Connected, callee.ConnectionState);
            CollectionAssert.AreEqual(new Object[] { "over the loop" }, received);
        }

        [Test]
        public async Task WindowsUseTheRegisteredPlatformAndEventLoop()
        {
            var loop = new ManualEventLoop();
            var configuration = Configuration.Default.WithWebRtc().WithOnly<IEventLoop>(_ => loop);
            var context = BrowsingContext.New(configuration);
            var document = await context.OpenAsync(res => res.Address("https://rtc.example/").Content("<!doctype html>")).ConfigureAwait(false);
            loop.RunAll();
            var caller = new RtcPeerConnection(document.DefaultView);
            var callee = new RtcPeerConnection(document.DefaultView, new RtcConfiguration());
            caller.CreateDataChannel("chat");

            await ConnectAsync(caller, callee, loop.RunAll).ConfigureAwait(false);

            Assert.AreEqual(RtcPeerConnectionState.Connected, caller.ConnectionState);
            Assert.AreEqual(RtcPeerConnectionState.Connected, callee.ConnectionState);
        }

        [Test]
        public async Task WindowsWithoutPlatformCannotCreateConnections()
        {
            var context = BrowsingContext.New(Configuration.Default.WithWebRtc(null));
            var document = await context.OpenAsync(res => res.Content("<!doctype html>")).ConfigureAwait(false);

            var ex = Assert.Throws<DomException>(() => new RtcPeerConnection(document.DefaultView));

            Assert.AreEqual((Int32)DomError.NotSupported, ex.Code);
            Assert.Throws<ArgumentNullException>(() => new RtcPeerConnection((IWindow)null));
        }

        [Test]
        public void WithIoRegistersTheWebRtcPlatform()
        {
            var platform = new LoopbackRtcPlatform();

            var configuration = Configuration.Default.WithIo(new IoOptions { WebRtcPlatform = platform });

            Assert.AreSame(platform, configuration.Services.OfType<IRtcPlatform>().Single());
            Assert.IsEmpty(Configuration.Default.WithIo(new IoOptions()).Services.OfType<IRtcPlatform>());
        }

        private static async Task<(RtcDataChannel Local, RtcDataChannel Remote)> OpenChannelPairAsync()
        {
            var platform = new LoopbackRtcPlatform();
            var caller = new RtcPeerConnection(platform);
            var callee = new RtcPeerConnection(platform);
            var local = caller.CreateDataChannel("chat");
            var remote = default(RtcDataChannel);
            callee.DataChannel += (s, e) => remote = ((RtcDataChannelEvent)e).Channel;
            await ConnectAsync(caller, callee).ConfigureAwait(false);
            Assert.IsNotNull(remote);
            return (local, remote);
        }

        private static async Task ConnectAsync(RtcPeerConnection caller, RtcPeerConnection callee, Action spin = null)
        {
            // candidates are buffered until the remote description is set,
            // like signaling code has to do
            var fromCaller = new List<RtcIceCandidate>();
            var fromCallee = new List<RtcIceCandidate>();
            caller.IceCandidate += (s, e) => fromCaller.Add(((RtcPeerConnectionIceEvent)e).Candidate);
            callee.IceCandidate += (s, e) => fromCallee.Add(((RtcPeerConnectionIceEvent)e).Candidate);
            spin ??= () => { };

            var offer = await caller.CreateOffer().ConfigureAwait(false);
            await caller.SetLocalDescription(offer).ConfigureAwait(false);
            await callee.SetRemoteDescription(offer).ConfigureAwait(false);
            var answer = await callee.CreateAnswer().ConfigureAwait(false);
            await callee.SetLocalDescription(answer).ConfigureAwait(false);
            await caller.SetRemoteDescription(answer).ConfigureAwait(false);
            spin.Invoke();

            foreach (var candidate in fromCaller.ToArray())
            {
                await callee.AddIceCandidate(candidate).ConfigureAwait(false);
            }

            foreach (var candidate in fromCallee.ToArray())
            {
                await caller.AddIceCandidate(candidate).ConfigureAwait(false);
            }

            spin.Invoke();
        }

        private static String GetUfrag(RtcSessionDescription description) => GetUfrag(description?.Sdp);

        private static String GetUfrag(String sdp) =>
            sdp?.Split('\n').Where(m => m.StartsWith("a=ice-ufrag:")).Select(m => m.Substring(12).TrimEnd('\r')).FirstOrDefault();

        private static RtcConfiguration Servers(params RtcIceServer[] servers) => new RtcConfiguration { IceServers = servers };

        private static List<RtcSignalingState> RecordSignalingStates(RtcPeerConnection connection)
        {
            var states = new List<RtcSignalingState>();
            connection.SignalingStateChanged += (s, e) => states.Add(connection.SignalingState);
            return states;
        }

        private static List<String> RecordAllEvents(RtcPeerConnection connection)
        {
            var events = new List<String>();
            connection.NegotiationNeeded += (s, e) => events.Add(e.Type);
            connection.SignalingStateChanged += (s, e) => events.Add(e.Type);
            connection.IceGatheringStateChanged += (s, e) => events.Add(e.Type + ":" + Literal(connection.IceGatheringState));
            connection.IceConnectionStateChanged += (s, e) => events.Add(e.Type + ":" + Literal(connection.IceConnectionState));
            connection.ConnectionStateChanged += (s, e) => events.Add(e.Type + ":" + Literal(connection.ConnectionState));
            connection.IceCandidate += (s, e) => events.Add(((RtcPeerConnectionIceEvent)e).Candidate is null ? e.Type + ":end" : e.Type);
            connection.DataChannel += (s, e) => events.Add(e.Type);
            return events;
        }

        private static List<Object> RecordMessages(RtcDataChannel channel)
        {
            var messages = new List<Object>();
            channel.Message += (s, e) => messages.Add(((MessageEvent)e).Data);
            return messages;
        }

        private static List<String> RecordChannelEvents(RtcDataChannel channel)
        {
            var events = new List<String>();
            channel.Closing += (s, e) => events.Add(e.Type);
            channel.Closed += (s, e) => events.Add(e.Type);
            return events;
        }

        private static String Literal<T>(T value) => value.ToString().ToLowerInvariant();

        private static void AssertDomError(DomError error, AsyncTestDelegate action)
        {
            var ex = Assert.ThrowsAsync<DomException>(action);
            Assert.AreEqual((Int32)error, ex.Code);
        }

        private static void AssertDomError(DomError error, TestDelegate action)
        {
            var ex = Assert.Throws<DomException>(action);
            Assert.AreEqual((Int32)error, ex.Code);
        }

        private sealed class ManualEventLoop : IEventLoop
        {
            private readonly Queue<Action<CancellationToken>> _tasks = new Queue<Action<CancellationToken>>();

            public ICancellable Enqueue(Action<CancellationToken> action, TaskPriority priority)
            {
                _tasks.Enqueue(action);
                return null;
            }

            public void RunAll()
            {
                while (_tasks.Count > 0)
                {
                    _tasks.Dequeue().Invoke(CancellationToken.None);
                }
            }

            public void CancelAll() => _tasks.Clear();

            public void Spin() => RunAll();
        }
    }
}
