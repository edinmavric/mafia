using System;
using System.Collections.Generic;
using MafiaGame.Application.Matches;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Roles;
using MafiaGame.Domain.Voting;
using Unity.Netcode;

namespace MafiaGame.Infrastructure.Networking
{
    /// <summary>
    /// Thin NGO transport over the engine-free <see cref="NetworkedMatchAuthority"/>. The server owns
    /// the authority and every decision; this class only moves data:
    /// <list type="bullet">
    /// <item>clients send intent through <c>ServerRpc</c>s (identity taken from the connection, never
    /// from client claims);</item>
    /// <item>public state (phase, player count, night result) is broadcast;</item>
    /// <item>private data (each player's role, the Detective's result) is sent with a targeted
    /// <c>ClientRpc</c> to a single client — never broadcast.</item>
    /// </list>
    /// The view subscribes to the plain C# events below and never touches NGO types directly.
    /// This is a scene <c>NetworkObject</c>; the host drives the match through the Host* methods.
    /// </summary>
    public sealed class NetworkMatchController : NetworkBehaviour
    {
        // Server-only state.
        private readonly NetworkedMatchAuthority _authority = new NetworkedMatchAuthority();
        private readonly Dictionary<ulong, int> _seatByClient = new Dictionary<ulong, int>();
        private readonly Dictionary<int, ulong> _clientBySeat = new Dictionary<int, ulong>();

        // Public, replicated state.
        private readonly NetworkVariable<MatchPhase> _phase = new NetworkVariable<MatchPhase>(
            MatchPhase.Lobby, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _playerCount = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Bit per seat: 1 = alive. Public by the rules (everyone sees who is out) and cheap to sync.
        private readonly NetworkVariable<int> _aliveMask = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Bit per seat: 1 = may be voted for in the current round (all living seats, or only the tied
        // ones during a revote).
        private readonly NetworkVariable<int> _voteCandidateMask = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // How many peers are actually connected over Netcode. This is NOT the lobby roster: a player
        // can be in the Relay session while their transport connection failed, and only this number
        // decides whether a match can start.
        private readonly NetworkVariable<int> _connectedCount = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // How many living seats have voted in the current round. Only the count is public — never who
        // voted or for whom.
        private readonly NetworkVariable<int> _votesCast = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<GameOutcome> _outcome = new NetworkVariable<GameOutcome>(
            GameOutcome.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // When the current phase ends, expressed in NGO server time; NoDeadline when the phase is
        // untimed. Replicating the deadline instead of a ticking counter costs one message per phase
        // instead of one per frame, and every client counts down to the exact same instant.
        private readonly NetworkVariable<double> _phaseEndsAt = new NetworkVariable<double>(
            NoDeadline, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // The host's lobby setup, replicated so every player sees the agreed rules before the match.
        // Public information only: counts, flags and durations, never a role assignment.
        private readonly NetworkVariable<MatchSetupSnapshot> _setup =
            new NetworkVariable<MatchSetupSnapshot>(
                MatchSetupSnapshot.From(MatchSetup.Default),
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private const double NoDeadline = -1d;

        /// <summary>Server-side source of truth for the setup; clients only mirror it for display.</summary>
        private MatchSetup _hostSetup = MatchSetup.Default;

        /// <summary>True once the host has dealt roles; gates the server-side phase clock.</summary>
        private bool _matchRunning;

        /// <summary>This client's seat, or -1 until roles are dealt. Local (client-side) value.</summary>
        public int LocalSeat { get; private set; } = -1;

        /// <summary>True on the host/server peer.</summary>
        public bool IsHostPeer => IsServer;

        public MatchPhase CurrentPhase => _phase.Value;

        public int PlayerCount => _playerCount.Value;

        public GameOutcome Outcome => _outcome.Value;

        /// <summary>Peers actually connected over Netcode — the number a match start is checked against.</summary>
        public int ConnectedCount => _connectedCount.Value;

        /// <summary>Votes submitted so far in the current round.</summary>
        public int VotesCast => _votesCast.Value;

        /// <summary>Number of living seats — how many votes the round can still receive.</summary>
        public int AliveCount
        {
            get
            {
                int count = 0;
                for (int seat = 0; seat < _playerCount.Value; seat++)
                {
                    if (IsSeatAlive(seat))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>True while the current phase is counting down.</summary>
        public bool HasPhaseDeadline => _phaseEndsAt.Value > NoDeadline;

        /// <summary>
        /// Seconds left in the current phase, never negative. Computed locally from the replicated
        /// deadline and NGO server time, so it stays smooth without any per-frame traffic. This is
        /// display only: the host alone decides when a phase actually ends.
        /// </summary>
        public double RemainingSeconds
        {
            get
            {
                if (!HasPhaseDeadline || NetworkManager == null)
                {
                    return 0d;
                }

                double remaining = _phaseEndsAt.Value - NetworkManager.ServerTime.Time;
                return remaining > 0d ? remaining : 0d;
            }
        }

        /// <summary>The agreed lobby setup, as every player sees it.</summary>
        public MatchSetup Setup => IsServer ? _hostSetup : _setup.Value.ToSetup();

        /// <summary>
        /// Applies a lobby change. Host only, and only before the match starts — a setup change
        /// mid-match would rewrite rules the players are already playing under. The caller passes a
        /// transform so every edit goes through the same validation and replication path.
        /// </summary>
        public void HostChangeSetup(Func<MatchSetup, MatchSetup> change)
        {
            if (!IsServer || _matchRunning || change == null)
            {
                return;
            }

            MatchSetup updated = change(_hostSetup);
            if (updated == null)
            {
                return;
            }

            // Reject a setup the current lobby cannot play instead of starting and failing later.
            int count = NetworkManager != null ? NetworkManager.ConnectedClientsIds.Count : 0;
            if (count >= MatchConfiguration.MinPlayers)
            {
                MatchConfigurationResult check = updated.ToConfiguration(count);
                if (!check.IsValid)
                {
                    HostNotice?.Invoke("Ne mogu tako: " + check.Error);
                    return;
                }
            }

            _hostSetup = updated;
            _setup.Value = MatchSetupSnapshot.From(updated);
            StateChanged?.Invoke();
        }

        /// <summary>Public alive/dead status of a seat.</summary>
        public bool IsSeatAlive(int seat) => (_aliveMask.Value & (1 << seat)) != 0;

        /// <summary>Whether a seat may be voted for in the current round.</summary>
        public bool IsVoteCandidate(int seat) => (_voteCandidateMask.Value & (1 << seat)) != 0;

        // Local peer events for the view. Raised on the peer that should react.
        public event Action Ready;
        public event Action<PrivateRoleInfo> RoleReceived;
        public event Action<MatchPhase> PhaseChanged;
        public event Action<NightPublicResult> NightResolved;
        public event Action<VotingPublicResult> VotingResolved;
        public event Action<DetectivePrivateResult> DetectiveResultReceived;
        public event Action<int> ConnectedCountChanged;

        /// <summary>Raised whenever any replicated public state changes; the view re-renders on it.</summary>
        public event Action StateChanged;

        public event Action<IntentRejection> IntentRejected;
        public event Action<string> HostNotice;

        public override void OnNetworkSpawn()
        {
            _phase.OnValueChanged += HandlePhaseChanged;
            _connectedCount.OnValueChanged += HandleConnectedCountChanged;

            // Public state arrives as several independent variables. A client may apply the phase
            // before the alive/candidate masks, so the view must refresh on ANY of them — refreshing
            // only on the phase left clients without vote buttons and without the final outcome.
            _aliveMask.OnValueChanged += HandleStateChanged;
            _voteCandidateMask.OnValueChanged += HandleStateChanged;
            _votesCast.OnValueChanged += HandleStateChanged;
            _playerCount.OnValueChanged += HandleStateChanged;
            _outcome.OnValueChanged += HandleOutcomeChanged;
            _setup.OnValueChanged += HandleSetupChanged;
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnect;
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                PublishConnectedCount();
            }

            Ready?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _phase.OnValueChanged -= HandlePhaseChanged;
            _connectedCount.OnValueChanged -= HandleConnectedCountChanged;
            _aliveMask.OnValueChanged -= HandleStateChanged;
            _voteCandidateMask.OnValueChanged -= HandleStateChanged;
            _votesCast.OnValueChanged -= HandleStateChanged;
            _playerCount.OnValueChanged -= HandleStateChanged;
            _outcome.OnValueChanged -= HandleOutcomeChanged;
            _setup.OnValueChanged -= HandleSetupChanged;
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnect;
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            }
        }

        /// <summary>
        /// Runs the phase clock. Server only: a client never advances a phase, it just watches the
        /// replicated deadline. The authority decides what fell due; this only carries it out, which
        /// keeps the timing rules in the unit-tested class rather than in a MonoBehaviour.
        /// </summary>
        private void Update()
        {
            if (!IsServer || !_matchRunning)
            {
                return;
            }

            switch (_authority.Tick(UnityEngine.Time.deltaTime))
            {
                case PhaseAdvance.ConfirmRolesSeen: HostConfirmRolesSeen(); break;
                case PhaseAdvance.ResolveNight: HostResolveNight(); break;
                case PhaseAdvance.ContinueToDiscussion: HostContinueToDiscussion(); break;
                case PhaseAdvance.BeginVoting: HostBeginVoting(); break;
                case PhaseAdvance.ResolveVoting: HostResolveVoting(); break;
            }
        }

        /// <summary>
        /// Publishes the new phase together with its deadline. Both must move as one: a client that
        /// saw the phase but kept the old deadline would show a countdown for the wrong phase.
        /// </summary>
        private void PublishPhase()
        {
            _phaseEndsAt.Value = _authority.HasDeadline
                ? NetworkManager.ServerTime.Time + _authority.RemainingSeconds
                : NoDeadline;
            _phase.Value = _authority.CurrentPhase;

            if (_authority.CurrentPhase == MatchPhase.GameOver)
            {
                _matchRunning = false;
            }
        }

        // ----- Host controls (server authority) -----

        /// <summary>Deals roles to the currently connected clients and enters the role-reveal phase.</summary>
        public void HostStartMatch()
        {
            if (!IsServer)
            {
                return;
            }

            var clients = new List<ulong>(NetworkManager.ConnectedClientsIds);
            clients.Sort();
            int count = clients.Count;

            // Guard with a message that names the real problem: the lobby roster counts everyone who
            // joined the Relay session, but only peers whose transport connection succeeded can play.
            if (count < MatchConfiguration.MinPlayers)
            {
                HostNotice?.Invoke(
                    $"Ne mogu da počnem: mrežno je povezano {count} igrača, a treba " +
                    $"najmanje {MatchConfiguration.MinPlayers}. Sačekaj da se svi povežu ili neka " +
                    "onaj kome je veza pukla pokuša ponovo.");
                return;
            }

            MatchConfigurationResult config = _hostSetup.ToConfiguration(count);
            if (!config.IsValid)
            {
                HostNotice?.Invoke($"Ne mogu da počnem: {config.Error}");
                return;
            }

            _seatByClient.Clear();
            _clientBySeat.Clear();
            for (int seat = 0; seat < count; seat++)
            {
                _seatByClient[clients[seat]] = seat;
                _clientBySeat[seat] = clients[seat];
            }

            int seed = Guid.NewGuid().GetHashCode();
            IReadOnlyList<PrivateRoleInfo> payloads =
                _authority.StartMatch(count, config.Configuration, seed, _hostSetup.Timings);
            _playerCount.Value = count;

            foreach (PrivateRoleInfo payload in payloads)
            {
                ulong client = _clientBySeat[payload.Seat];
                ReceiveRoleClientRpc(
                    payload.Seat, (int)payload.Role, ToArray(payload.MafiaTeammateSeats), Target(client));
            }

            PublishAliveAndOutcome();
            _matchRunning = true;
            PublishPhase(); // RoleReveal
        }

        /// <summary>Advances from role reveal into the night.</summary>
        public void HostConfirmRolesSeen()
        {
            if (!IsServer || _authority.CurrentPhase != MatchPhase.RoleReveal)
            {
                return;
            }

            _authority.ConfirmRolesSeen();
            PublishPhase(); // Night
        }

        /// <summary>Resolves the night, broadcasts the public result, and privately answers the Detective.</summary>
        public void HostResolveNight()
        {
            if (!IsServer || _authority.CurrentPhase != MatchPhase.Night)
            {
                return;
            }

            NightOutcome outcome = _authority.ResolveNight();
            NightPublicResult publicResult = outcome.Public;
            NightResultClientRpc(
                publicResult.KilledSeat,
                publicResult.RevealedRole.HasValue,
                publicResult.RevealedRole.HasValue ? (int)publicResult.RevealedRole.Value : 0);

            if (outcome.DetectivePrivate != null &&
                _clientBySeat.TryGetValue(outcome.DetectivePrivate.DetectiveSeat, out ulong detectiveClient))
            {
                DetectiveResultClientRpc(
                    outcome.DetectivePrivate.TargetSeat,
                    outcome.DetectivePrivate.IsMafia,
                    Target(detectiveClient));
            }

            PublishAliveAndOutcome();
            PublishPhase(); // DayAnnouncement or GameOver
        }

        /// <summary>Opens the free discussion after the day announcement.</summary>
        public void HostContinueToDiscussion()
        {
            if (!IsServer || _authority.CurrentPhase != MatchPhase.DayAnnouncement)
            {
                return;
            }

            _authority.ContinueToDiscussion();
            PublishPhase(); // DayDiscussion
        }

        /// <summary>Opens the voting round; every living seat becomes a candidate.</summary>
        public void HostBeginVoting()
        {
            if (!IsServer || _authority.CurrentPhase != MatchPhase.DayDiscussion)
            {
                return;
            }

            _authority.BeginVoting();
            _voteCandidateMask.Value = ToMask(_authority.VoteCandidateSeats());
            _votesCast.Value = 0;
            PublishPhase(); // Voting
        }

        /// <summary>
        /// Tallies the votes and broadcasts the public result. A tie keeps the match in the voting
        /// phase and narrows the candidates to the tied seats, so the same button drives the revote.
        /// </summary>
        public void HostResolveVoting()
        {
            if (!IsServer || _authority.CurrentPhase != MatchPhase.Voting)
            {
                return;
            }

            VotingPublicResult result = _authority.ResolveVoting();
            _voteCandidateMask.Value = ToMask(_authority.VoteCandidateSeats());
            _votesCast.Value = 0;
            PublishAliveAndOutcome();

            VotingResultClientRpc(
                (int)result.Outcome,
                result.EliminatedSeat,
                result.RevealedRole.HasValue,
                result.RevealedRole.HasValue ? (int)result.RevealedRole.Value : 0,
                ToArray(result.TiedSeats));

            PublishPhase(); // Night, Voting (revote) or GameOver
        }

        // ----- Client intents (identity from the connection) -----

        [ServerRpc(RequireOwnership = false)]
        public void SubmitMafiaTargetServerRpc(int targetSeat, ServerRpcParams rpcParams = default) =>
            HandleIntent(rpcParams.Receive.SenderClientId, targetSeat, _authority.SubmitMafiaTarget);

        [ServerRpc(RequireOwnership = false)]
        public void SubmitDoctorProtectServerRpc(int targetSeat, ServerRpcParams rpcParams = default) =>
            HandleIntent(rpcParams.Receive.SenderClientId, targetSeat, _authority.SubmitDoctorProtect);

        [ServerRpc(RequireOwnership = false)]
        public void SubmitDetectiveInvestigateServerRpc(int targetSeat, ServerRpcParams rpcParams = default) =>
            HandleIntent(rpcParams.Receive.SenderClientId, targetSeat, _authority.SubmitDetectiveInvestigate);

        [ServerRpc(RequireOwnership = false)]
        public void SubmitVoteServerRpc(int targetSeat, ServerRpcParams rpcParams = default) =>
            HandleIntent(rpcParams.Receive.SenderClientId, targetSeat, _authority.SubmitVote);

        private void HandleIntent(ulong senderClientId, int targetSeat, Func<int, int, IntentResult> submit)
        {
            if (!IsServer || !_seatByClient.TryGetValue(senderClientId, out int senderSeat))
            {
                return;
            }

            IntentResult result = submit(senderSeat, targetSeat);
            if (result.Accepted && _authority.CurrentPhase == MatchPhase.Voting)
            {
                _votesCast.Value = _authority.SubmittedVoteCount;
            }

            if (!result.Accepted)
            {
                IntentRejectedClientRpc((int)result.Reason, Target(senderClientId));
            }
            else
            {
                IntentAcceptedClientRpc(Target(senderClientId));
            }
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            if (_seatByClient.TryGetValue(clientId, out int seat))
            {
                _authority.MarkDisconnected(seat);
            }

            PublishConnectedCount();
        }

        private void OnClientConnected(ulong clientId) => PublishConnectedCount();

        private void PublishConnectedCount()
        {
            if (IsServer && NetworkManager != null)
            {
                _connectedCount.Value = NetworkManager.ConnectedClientsIds.Count;
            }
        }

        // ----- Targeted / broadcast client RPCs -----

        [ClientRpc]
        private void ReceiveRoleClientRpc(int seat, int role, int[] teammateSeats, ClientRpcParams rpcParams = default)
        {
            LocalSeat = seat;
            RoleReceived?.Invoke(new PrivateRoleInfo(seat, (Role)role, teammateSeats ?? Array.Empty<int>()));
        }

        [ClientRpc]
        private void NightResultClientRpc(int killedSeat, bool hasReveal, int revealRole)
        {
            Role? revealed = hasReveal ? (Role?)(Role)revealRole : null;
            NightResolved?.Invoke(new NightPublicResult(killedSeat, revealed));
        }

        [ClientRpc]
        private void VotingResultClientRpc(
            int outcome, int eliminatedSeat, bool hasReveal, int revealRole, int[] tiedSeats)
        {
            Role? revealed = hasReveal ? (Role?)(Role)revealRole : null;
            VotingResolved?.Invoke(new VotingPublicResult(
                (VoteOutcome)outcome, eliminatedSeat, revealed, tiedSeats ?? Array.Empty<int>()));
        }

        [ClientRpc]
        private void DetectiveResultClientRpc(int targetSeat, bool isMafia, ClientRpcParams rpcParams = default)
        {
            DetectiveResultReceived?.Invoke(new DetectivePrivateResult(LocalSeat, targetSeat, isMafia));
        }

        [ClientRpc]
        private void IntentRejectedClientRpc(int reason, ClientRpcParams rpcParams = default) =>
            IntentRejected?.Invoke((IntentRejection)reason);

        [ClientRpc]
        private void IntentAcceptedClientRpc(ClientRpcParams rpcParams = default) =>
            IntentRejected?.Invoke(IntentRejection.None);

        private void HandlePhaseChanged(MatchPhase previous, MatchPhase current) => PhaseChanged?.Invoke(current);

        private void HandleConnectedCountChanged(int previous, int current) => ConnectedCountChanged?.Invoke(current);

        private void HandleStateChanged(int previous, int current) => StateChanged?.Invoke();

        private void HandleOutcomeChanged(GameOutcome previous, GameOutcome current) => StateChanged?.Invoke();

        private void HandleSetupChanged(MatchSetupSnapshot previous, MatchSetupSnapshot current) =>
            StateChanged?.Invoke();

        /// <summary>Publishes the public alive/dead status and the win outcome. Server only.</summary>
        private void PublishAliveAndOutcome()
        {
            _aliveMask.Value = ToMask(_authority.AliveSeats());
            _outcome.Value = _authority.Outcome;
        }

        private static int ToMask(IReadOnlyList<int> seats)
        {
            int mask = 0;
            foreach (int seat in seats)
            {
                mask |= 1 << seat;
            }

            return mask;
        }

        private static int[] ToArray(IReadOnlyList<int> source)
        {
            var list = new List<int>(source);
            return list.ToArray();
        }

        private static ClientRpcParams Target(ulong clientId) => new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };
    }
}
