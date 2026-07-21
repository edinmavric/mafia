using System;
using System.Collections.Generic;
using MafiaGame.Application.Matches;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Roles;
using MafiaGame.Domain.Voting;
using MafiaGame.Infrastructure.Services;
using Unity.Netcode;
using UnityEngine.SceneManagement;

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
        /// <summary>
        /// The match scene, loaded additively over the lobby scene while a match runs. Additive
        /// rather than a full scene switch: the lobby scene is the network root (it holds the
        /// NetworkManager and this scene-placed object), and replacing it would mean tearing down and
        /// re-creating the authority mid-session. Netcode loads and unloads it on every peer at once.
        /// </summary>
        public const string GameSceneName = "Game";

        // Server-only state. Not readonly: returning to the lobby replaces the authority with a fresh
        // one, which is the only way to be sure no scrap of the finished match survives into the next.
        private NetworkedMatchAuthority _authority = new NetworkedMatchAuthority();
        private readonly Dictionary<ulong, int> _seatByClient = new Dictionary<ulong, int>();
        private readonly Dictionary<int, ulong> _clientBySeat = new Dictionary<int, ulong>();

        // How a returning player is recognised across a full application restart. A reconnecting
        // client arrives with a brand-new connection id, so the seat cannot be read from the
        // connection. Instead each seat is tied to the player's Unity Authentication id, which is
        // stable per profile and survives the process dying. The client sends its own id on connect
        // (it already knows it from sign-in); the server never invents it.
        //
        // Security note (owner decision 2026-07-20): the id is a client-supplied claim, and it is
        // public in the lobby roster, so this is not spoof-proof — a lobby member could in theory
        // claim a *disconnected* member's id to take their seat and role. That is acceptable for the
        // private-friends MVP and is contained two ways: a seat is only handed over while it is
        // actually flagged disconnected, and never to a connection that already holds a seat.
        // Hardening (a server-issued secret, or a trusted transport-level identity) is deferred.
        private readonly Dictionary<string, int> _seatByPlayerId = new Dictionary<string, int>();
        private readonly Dictionary<ulong, string> _playerIdByClient = new Dictionary<ulong, string>();

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

        /// <summary>
        /// This client's own private role, once it has been dealt one. Remembered rather than only
        /// raised as an event because the match screen is created after the match starts — the scene
        /// it lives in is still loading when the role arrives — and it has to be able to ask for what
        /// it missed. Null until dealt. Local to this client: it is never replicated and never holds
        /// anyone else's role.
        /// </summary>
        public PrivateRoleInfo LocalRole { get; private set; }

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

            ApplySetup(updated, explainClamp: true);
        }

        /// <summary>
        /// Stores and replicates a setup, first trimming it to what the current lobby can play. The
        /// host is told when something was switched off for them, so a silently changed rule never
        /// surprises the table.
        /// </summary>
        private void ApplySetup(MatchSetup wanted, bool explainClamp)
        {
            MatchSetup allowed = wanted.ClampTo(LobbySizeForRules());
            if (explainClamp && !allowed.SameAs(wanted))
            {
                HostNotice?.Invoke(ClampNotice(allowed, wanted));
            }

            if (allowed.SameAs(_hostSetup))
            {
                return;
            }

            _hostSetup = allowed;
            _setup.Value = MatchSetupSnapshot.From(allowed);
            StateChanged?.Invoke();
        }

        /// <summary>
        /// The size the setup rules are judged against. Below the minimum the lobby is still filling
        /// up, so it is judged against the minimum — otherwise a lone host would see every option
        /// switch itself off before anyone had a chance to join.
        /// </summary>
        private int LobbySizeForRules()
        {
            int count = NetworkManager != null ? NetworkManager.ConnectedClientsIds.Count : 0;
            return count < MatchConfiguration.MinPlayers ? MatchConfiguration.MinPlayers : count;
        }

        private string ClampNotice(MatchSetup allowed, MatchSetup wanted)
        {
            if (wanted.IncludeDetective && !allowed.IncludeDetective)
            {
                return $"Detektiv je isključen: treba bar " +
                       $"{MatchConfiguration.MinPlayersForBothSpecialRoles} igrača za obe specijalne uloge " +
                       $"(a bar {MatchConfiguration.MinPlayersForSpecialRole} za jednu).";
            }

            if (wanted.IncludeDoctor && !allowed.IncludeDoctor)
            {
                return $"Doktor je isključen: specijalna uloga traži bar " +
                       $"{MatchConfiguration.MinPlayersForSpecialRole} igrača.";
            }

            if (wanted.MafiaCount != allowed.MafiaCount)
            {
                return $"Broj mafija je smanjen na {allowed.MafiaCount}: mafija mora da bude u manjini.";
            }

            return "Podešavanje je prilagođeno broju igrača.";
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

        /// <summary>A seat was removed after being absent too long. Carries the seat only.</summary>
        public event Action<int> PlayerLeft;


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

                // Record the host's own identity directly; the host never sends itself an RPC.
                _playerIdByClient[NetworkManager.LocalClientId] = GameServices.LocalPlayerId;
                PublishConnectedCount();
            }

            // Every client tells the server who it is as soon as it spawns. Before a match this just
            // registers the identity so the host can tie it to a seat when roles are dealt; during a
            // match it doubles as the rejoin claim — a returning client (even after a full restart)
            // is recognised by the same id and gets its seat and role back, with no button to find.
            if (!IsServer)
            {
                IdentifyServerRpc(GameServices.LocalPlayerId);
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

            // Leaving the session is a state change like any other, and the screens have to hear
            // about it: without this the host's own controls stayed on screen after they left,
            // because nothing else ever fires once the network is gone.
            StateChanged?.Invoke();
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

            float delta = UnityEngine.Time.deltaTime;

            // Absent players first: a seat that gives up changes who the phase is still waiting for,
            // so the phase clock should see the new picture in the same frame.
            IReadOnlyList<int> forfeited = _authority.TickAbsence(delta);
            if (forfeited.Count > 0)
            {
                PublishAliveAndOutcome();
                foreach (int seat in forfeited)
                {
                    PlayerLeftClientRpc(seat);
                }

                // Those departures may have emptied the table, in which case the authority has
                // already called the match off and the clients need to be told it is over.
                if (_authority.CurrentPhase == MatchPhase.GameOver)
                {
                    PublishPhase();
                    return;
                }
            }

            switch (_authority.Tick(delta))
            {
                case PhaseAdvance.ConfirmRolesSeen: HostConfirmRolesSeen(); break;
                case PhaseAdvance.ResolveNight: HostResolveNight(); break;
                case PhaseAdvance.ContinueToDiscussion: HostContinueToDiscussion(); break;
                case PhaseAdvance.BeginVoting: HostBeginVoting(); break;
                case PhaseAdvance.ResolveVoting: HostResolveVoting(); break;
                case PhaseAdvance.BeginRevote: HostBeginRevote(); break;
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

            // Trim once more against the real roster before dealing roles: the lobby may have shrunk
            // since the last change, and starting must not fail on a setting the host cannot see.
            ApplySetup(_hostSetup, explainClamp: false);

            MatchConfigurationResult config = _hostSetup.ToConfiguration(count);
            if (!config.IsValid)
            {
                HostNotice?.Invoke($"Ne mogu da počnem: {config.Error}");
                return;
            }

            _seatByClient.Clear();
            _clientBySeat.Clear();
            _seatByPlayerId.Clear();
            for (int seat = 0; seat < count; seat++)
            {
                ulong client = clients[seat];
                _seatByClient[client] = seat;
                _clientBySeat[seat] = client;

                // Tie the seat to the player's stable account id so a returning client can be
                // recognised after a restart. A client that has not identified yet is simply not
                // rejoinable — an empty id is never stored.
                if (_playerIdByClient.TryGetValue(client, out string playerId) &&
                    !string.IsNullOrEmpty(playerId))
                {
                    _seatByPlayerId[playerId] = seat;
                }
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

            // The players are now in a match, so move them out of the lobby scene. Loading after the
            // roles are dealt is deliberate: the role-reveal phase covers the load, and no client can
            // miss anything because RPCs arrive regardless of which scenes that client has loaded.
            LoadGameScene();
        }

        /// <summary>
        /// Ends the finished match and puts everyone back in the lobby with the same join code, ready
        /// to play again. Host only, and only once the match is actually over — a mid-match reset
        /// would silently discard a game the others are still playing.
        /// </summary>
        public void HostReturnToLobby()
        {
            if (!IsServer || _authority.CurrentPhase != MatchPhase.GameOver)
            {
                return;
            }

            UnloadGameScene();

            // A brand-new authority rather than a reset method: there is then no chance of a leftover
            // vote, night action or absence timer from the finished match leaking into the next one.
            _authority = new NetworkedMatchAuthority();
            _seatByClient.Clear();
            _clientBySeat.Clear();
            _seatByPlayerId.Clear();
            _matchRunning = false;

            _playerCount.Value = 0;
            _aliveMask.Value = 0;
            _voteCandidateMask.Value = 0;
            _votesCast.Value = 0;
            _outcome.Value = GameOutcome.None;
            _phaseEndsAt.Value = NoDeadline;
            _phase.Value = MatchPhase.Lobby;

            // The lobby may have changed size during the match, so trim the setup before it is shown.
            ApplySetup(_hostSetup, explainClamp: false);
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Asks Netcode to add the match scene on every peer. A failure is reported to the host and
        /// nothing else: the match itself is driven by the authority, not by the scene, so playing on
        /// against the lobby background is far better than refusing to start.
        /// </summary>
        private void LoadGameScene()
        {
            if (!IsServer || NetworkManager == null || NetworkManager.SceneManager == null)
            {
                return;
            }

            if (SceneManager.GetSceneByName(GameSceneName).isLoaded)
            {
                return;
            }

            SceneEventProgressStatus status =
                NetworkManager.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Additive);
            if (status != SceneEventProgressStatus.Started)
            {
                HostNotice?.Invoke(
                    $"Scena partije nije mogla da se učita ({status}). Partija radi normalno, " +
                    "samo bez 3D scene. Proveri da li je scena „Game\" u Build Settings.");
            }
        }

        private void UnloadGameScene()
        {
            if (!IsServer || NetworkManager == null || NetworkManager.SceneManager == null)
            {
                return;
            }

            Scene scene = SceneManager.GetSceneByName(GameSceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                NetworkManager.SceneManager.UnloadScene(scene);
            }
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
        /// Opens the revote once the tied players have had their defense. Nothing is re-broadcast:
        /// the candidate mask was already narrowed to the tied seats when the tie was tallied.
        /// </summary>
        public void HostBeginRevote()
        {
            if (!IsServer || _authority.CurrentPhase != MatchPhase.TieBreaker)
            {
                return;
            }

            _authority.BeginRevote();
            _votesCast.Value = 0;
            PublishPhase(); // Voting (revote)
        }

        /// <summary>
        /// Tallies the votes and broadcasts the public result. A tie moves the day into the
        /// tie-breaker defense and narrows the candidates to the tied seats before the revote.
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

            PublishPhase(); // Night, TieBreaker or GameOver
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

        /// <summary>
        /// A client announces its stable account id. Before a match this only registers the identity
        /// so the seat can be tied to it when roles are dealt. During a match it is also the rejoin
        /// claim: if that id owns a seat that is currently disconnected, the seat is handed back to
        /// this connection and the role is re-sent — privately, to this client alone.
        ///
        /// The seat is only restored while it is actually flagged disconnected and never to a
        /// connection that already holds one, so a live player's seat cannot be stolen out from under
        /// them (see the security note on <see cref="_seatByPlayerId"/>).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void IdentifyServerRpc(string playerId, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || string.IsNullOrEmpty(playerId))
            {
                return;
            }

            ulong client = rpcParams.Receive.SenderClientId;
            _playerIdByClient[client] = playerId;

            if (!_matchRunning ||
                _seatByClient.ContainsKey(client) ||
                !_seatByPlayerId.TryGetValue(playerId, out int seat) ||
                !_authority.IsDisconnected(seat))
            {
                return;
            }

            // The seat may still be mapped to the dead connection; drop that first so one seat is
            // never reachable from two connection ids.
            if (_clientBySeat.TryGetValue(seat, out ulong previous) && previous != client)
            {
                _seatByClient.Remove(previous);
            }

            _seatByClient[client] = seat;
            _clientBySeat[seat] = client;
            _authority.MarkReconnected(seat);

            PrivateRoleInfo role = _authority.RoleInfoFor(seat);
            ReceiveRoleClientRpc(seat, (int)role.Role, ToArray(role.MafiaTeammateSeats), Target(client));

            PublishConnectedCount();
            StateChanged?.Invoke();
        }

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
            if (!IsServer || NetworkManager == null)
            {
                return;
            }

            _connectedCount.Value = NetworkManager.ConnectedClientsIds.Count;

            // The lobby just changed size, so a setting that is no longer playable switches itself
            // off. Silent here: the host did not ask for this, and the summary shows the result.
            if (!_matchRunning)
            {
                ApplySetup(_hostSetup, explainClamp: false);
            }
        }

        // ----- Targeted / broadcast client RPCs -----

        [ClientRpc]
        private void ReceiveRoleClientRpc(
            int seat, int role, int[] teammateSeats, ClientRpcParams rpcParams = default)
        {
            LocalSeat = seat;
            LocalRole = new PrivateRoleInfo(seat, (Role)role, teammateSeats ?? Array.Empty<int>());
            RoleReceived?.Invoke(LocalRole);
        }

        /// <summary>
        /// Announces that a seat gave up after being absent too long. Only the seat is public — no
        /// role is attached, so a player leaving never reveals what they were.
        /// </summary>
        [ClientRpc]
        private void PlayerLeftClientRpc(int seat) => PlayerLeft?.Invoke(seat);

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

        private void HandlePhaseChanged(MatchPhase previous, MatchPhase current)
        {
            // Back in the lobby means this client no longer holds a seat. Clearing it here, on the
            // one authoritative signal, keeps a stale seat from the finished match out of the next.
            if (current == MatchPhase.Lobby)
            {
                LocalSeat = -1;
                LocalRole = null;
            }

            PhaseChanged?.Invoke(current);
        }

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
