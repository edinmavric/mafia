using System;
using System.Collections.Generic;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Night;
using MafiaGame.Domain.Players;
using MafiaGame.Domain.Roles;
using MafiaGame.Domain.Voting;

namespace MafiaGame.Application.Matches
{
    /// <summary>
    /// Host-authoritative brain for a networked match, kept engine-free so every security rule is
    /// unit-testable without a live network. It wraps <see cref="LocalMatchDriver"/> and adds the two
    /// things networking needs: a stable seat model (seat 0..N-1 ↔ <see cref="PlayerId"/> = seat + 1)
    /// and an explicit public/private split of every result.
    ///
    /// The transport (Infrastructure) only calls these methods and ships the returned payloads:
    /// broadcast the public parts, send each <see cref="PrivateRoleInfo"/> and any
    /// <see cref="DetectivePrivateResult"/> to a single seat. Clients submit intents; this validates
    /// them (phase, sender liveness/connection/role, target validity) and never trusts client claims.
    /// </summary>
    public sealed class NetworkedMatchAuthority
    {
        private readonly LocalMatchDriver _driver = new LocalMatchDriver();
        private readonly HashSet<int> _disconnected = new HashSet<int>();

        private int _playerCount;
        private bool _started;

        // Pending night intents, stored as seats (null when not submitted).
        private int? _mafiaTargetSeat;
        private int? _doctorProtectSeat;
        private int? _detectiveTargetSeat;

        // Pending day votes: voter seat -> target seat. A player may change their vote until the
        // host tallies, so the last submission per seat is the one that counts.
        private readonly Dictionary<int, int> _votesBySeat = new Dictionary<int, int>();

        // Seats a revote is restricted to after a tie; null when the round is a normal one.
        private List<int> _revoteCandidateSeats;

        public MatchPhase CurrentPhase => _driver.CurrentPhase;

        public GameOutcome Outcome => _driver.Outcome;

        public int PlayerCount => _playerCount;

        /// <summary>Maps a seat to its domain player id (seat 0 → Player(1)).</summary>
        public static PlayerId SeatToPlayerId(int seat) => new PlayerId(seat + 1);

        /// <summary>Maps a domain player id back to its seat.</summary>
        public static int PlayerIdToSeat(PlayerId id) => id.Value - 1;

        public bool IsDisconnected(int seat) => _disconnected.Contains(seat);

        /// <summary>Seats of all players still alive, in ascending order.</summary>
        public IReadOnlyList<int> AliveSeats()
        {
            var seats = new List<int>();
            foreach (PlayerState player in _driver.AlivePlayers())
            {
                seats.Add(PlayerIdToSeat(player.Id));
            }

            seats.Sort();
            return seats;
        }

        /// <summary>
        /// Starts the match for <paramref name="playerCount"/> seats and returns the per-seat private
        /// role payloads. The <paramref name="seed"/> is supplied by the host (never by a client), so
        /// role assignment stays authority-owned and unpredictable to clients.
        /// </summary>
        public IReadOnlyList<PrivateRoleInfo> StartMatch(
            int playerCount, MatchConfiguration configuration, int seed)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (playerCount != configuration.PlayerCount)
            {
                throw new ArgumentException(
                    $"Player count {playerCount} does not match configuration {configuration.PlayerCount}.",
                    nameof(playerCount));
            }

            var roster = new List<PlayerId>(playerCount);
            for (int seat = 0; seat < playerCount; seat++)
            {
                roster.Add(SeatToPlayerId(seat));
            }

            _driver.Start(configuration, roster, seed);
            _playerCount = playerCount;
            _started = true;
            _disconnected.Clear();
            ClearNightIntents();
            _votesBySeat.Clear();
            _revoteCandidateSeats = null;

            var payloads = new List<PrivateRoleInfo>(playerCount);
            for (int seat = 0; seat < playerCount; seat++)
            {
                PlayerId id = SeatToPlayerId(seat);
                Role role = _driver.RoleOf(id);
                IReadOnlyList<int> teammates = role == Role.Mafia
                    ? MafiaTeammateSeats(id)
                    : Array.Empty<int>();
                payloads.Add(new PrivateRoleInfo(seat, role, teammates));
            }

            return payloads;
        }

        /// <summary>Advances from role reveal into the night once every client has seen its role.</summary>
        public void ConfirmRolesSeen()
        {
            RequireStarted();
            _driver.ConfirmRolesSeen();
        }

        public IntentResult SubmitMafiaTarget(int senderSeat, int targetSeat) =>
            SubmitNightIntent(senderSeat, targetSeat, Role.Mafia, allowSelf: false,
                store: seat => _mafiaTargetSeat = seat);

        public IntentResult SubmitDoctorProtect(int senderSeat, int targetSeat) =>
            SubmitNightIntent(senderSeat, targetSeat, Role.Doctor, allowSelf: true,
                store: seat => _doctorProtectSeat = seat);

        public IntentResult SubmitDetectiveInvestigate(int senderSeat, int targetSeat) =>
            SubmitNightIntent(senderSeat, targetSeat, Role.Detective, allowSelf: false,
                store: seat => _detectiveTargetSeat = seat);

        /// <summary>
        /// Marks a seat as disconnected. The player stays in the roster but is treated as absent:
        /// any pending night intent that only they could have supplied is dropped so the night still
        /// resolves. Votes for this seat are handled by the caller (not counted).
        /// </summary>
        public void MarkDisconnected(int seat)
        {
            if (!_started)
            {
                return;
            }

            _disconnected.Add(seat);
            DropOrphanedIntents();
        }

        /// <summary>
        /// Resolves the night from the stored intents and returns the public/private split. Intents
        /// from disconnected sole-actors have already been dropped, so only valid ones apply.
        /// </summary>
        public NightOutcome ResolveNight()
        {
            RequireStarted();

            var actions = new NightActions(
                _mafiaTargetSeat.HasValue ? SeatToPlayerId(_mafiaTargetSeat.Value) : (PlayerId?)null,
                _doctorProtectSeat.HasValue ? SeatToPlayerId(_doctorProtectSeat.Value) : (PlayerId?)null,
                _detectiveTargetSeat.HasValue ? SeatToPlayerId(_detectiveTargetSeat.Value) : (PlayerId?)null);

            NightResolution resolution = _driver.ResolveNight(actions);

            int killedSeat = resolution.KilledPlayer.HasValue
                ? PlayerIdToSeat(resolution.KilledPlayer.Value)
                : -1;
            Role? revealedRole = null;
            if (resolution.KilledPlayer.HasValue && _driver.RevealRoleOnElimination)
            {
                revealedRole = _driver.RoleOf(resolution.KilledPlayer.Value);
            }

            var publicResult = new NightPublicResult(killedSeat, revealedRole);

            DetectivePrivateResult detectivePrivate = null;
            if (resolution.DetectiveResult != null && _detectiveTargetSeat.HasValue)
            {
                int detectiveSeat = FindDetectiveSeat();
                if (detectiveSeat >= 0)
                {
                    detectivePrivate = new DetectivePrivateResult(
                        detectiveSeat,
                        PlayerIdToSeat(resolution.DetectiveResult.Target),
                        resolution.DetectiveResult.IsMafia);
                }
            }

            ClearNightIntents();
            return new NightOutcome(publicResult, detectivePrivate);
        }

        /// <summary>Moves from the day announcement into the free discussion.</summary>
        public void ContinueToDiscussion()
        {
            RequireStarted();
            _driver.ContinueToDiscussion();
        }

        /// <summary>Opens the voting round and clears any votes left over from a previous round.</summary>
        public void BeginVoting()
        {
            RequireStarted();
            _driver.BeginVoting();
            _votesBySeat.Clear();
        }

        /// <summary>How many seats have submitted a vote in the current round (public, safe to show).</summary>
        public int SubmittedVoteCount => _votesBySeat.Count;

        /// <summary>True while the current round is a revote restricted to the tied seats.</summary>
        public bool IsRevote => _revoteCandidateSeats != null;

        /// <summary>Seats that may be voted for right now: the tied ones in a revote, all living otherwise.</summary>
        public IReadOnlyList<int> VoteCandidateSeats() =>
            _revoteCandidateSeats != null ? new List<int>(_revoteCandidateSeats) : AliveSeats();

        /// <summary>
        /// Records a day vote. Validated exactly like a night intent: right phase, sender alive and
        /// connected, living target, and — during a revote — a target that is still a candidate.
        /// </summary>
        public IntentResult SubmitVote(int senderSeat, int targetSeat)
        {
            RequireStarted();

            if (CurrentPhase != MatchPhase.Voting)
            {
                return IntentResult.Reject(IntentRejection.WrongPhase);
            }

            if (!IsActingSeat(senderSeat))
            {
                return IntentResult.Reject(IntentRejection.NotAllowed);
            }

            if (!IsValidTarget(targetSeat) ||
                (_revoteCandidateSeats != null && !_revoteCandidateSeats.Contains(targetSeat)))
            {
                return IntentResult.Reject(IntentRejection.InvalidTarget);
            }

            _votesBySeat[senderSeat] = targetSeat;
            return IntentResult.Accept();
        }

        /// <summary>
        /// Tallies the votes and returns the public result. Votes from seats that disconnected are
        /// dropped (confirmed rule). A first-round tie keeps the match in the voting phase and arms a
        /// revote restricted to the tied seats; a tie in the revote ends the day with no elimination.
        /// </summary>
        public VotingPublicResult ResolveVoting()
        {
            RequireStarted();

            var votes = new List<Vote>();
            foreach (KeyValuePair<int, int> entry in _votesBySeat)
            {
                if (_disconnected.Contains(entry.Key))
                {
                    continue;
                }

                votes.Add(new Vote(SeatToPlayerId(entry.Key), SeatToPlayerId(entry.Value)));
            }

            IReadOnlyCollection<PlayerId> restriction = null;
            if (_revoteCandidateSeats != null)
            {
                var candidates = new List<PlayerId>(_revoteCandidateSeats.Count);
                foreach (int seat in _revoteCandidateSeats)
                {
                    candidates.Add(SeatToPlayerId(seat));
                }

                restriction = candidates;
            }

            VotingResolution resolution = _driver.ResolveVoting(votes, restriction);
            _votesBySeat.Clear();

            var tiedSeats = new List<int>();
            foreach (PlayerId candidate in resolution.TiedCandidates)
            {
                tiedSeats.Add(PlayerIdToSeat(candidate));
            }

            tiedSeats.Sort();
            _revoteCandidateSeats =
                resolution.Outcome == VoteOutcome.TieRequiresRevote ? tiedSeats : null;

            int eliminatedSeat = resolution.EliminatedPlayer.HasValue
                ? PlayerIdToSeat(resolution.EliminatedPlayer.Value)
                : -1;
            Role? revealedRole = null;
            if (resolution.EliminatedPlayer.HasValue && _driver.RevealRoleOnElimination)
            {
                revealedRole = _driver.RoleOf(resolution.EliminatedPlayer.Value);
            }

            return new VotingPublicResult(resolution.Outcome, eliminatedSeat, revealedRole, tiedSeats);
        }

        private IntentResult SubmitNightIntent(
            int senderSeat, int targetSeat, Role requiredRole, bool allowSelf, Action<int> store)
        {
            RequireStarted();

            if (CurrentPhase != MatchPhase.Night)
            {
                return IntentResult.Reject(IntentRejection.WrongPhase);
            }

            if (!IsActingSeat(senderSeat) || _driver.RoleOf(SeatToPlayerId(senderSeat)) != requiredRole)
            {
                return IntentResult.Reject(IntentRejection.NotAllowed);
            }

            if (!IsValidTarget(targetSeat) || (!allowSelf && targetSeat == senderSeat))
            {
                return IntentResult.Reject(IntentRejection.InvalidTarget);
            }

            store(targetSeat);
            return IntentResult.Accept();
        }

        /// <summary>A seat that may act: in range, alive, and connected.</summary>
        private bool IsActingSeat(int seat) => IsInRange(seat) && IsAlive(seat) && !_disconnected.Contains(seat);

        /// <summary>A legal target: in range and alive (targets may be disconnected).</summary>
        private bool IsValidTarget(int seat) => IsInRange(seat) && IsAlive(seat);

        private bool IsInRange(int seat) => seat >= 0 && seat < _playerCount;

        private bool IsAlive(int seat)
        {
            PlayerId id = SeatToPlayerId(seat);
            foreach (PlayerState player in _driver.Players)
            {
                if (player.Id == id)
                {
                    return player.IsAlive;
                }
            }

            return false;
        }

        private IReadOnlyList<int> MafiaTeammateSeats(PlayerId id)
        {
            var seats = new List<int>();
            foreach (PlayerId teammate in _driver.MafiaTeammates(id))
            {
                seats.Add(PlayerIdToSeat(teammate));
            }

            return seats;
        }

        private int FindDetectiveSeat()
        {
            for (int seat = 0; seat < _playerCount; seat++)
            {
                if (_driver.RoleOf(SeatToPlayerId(seat)) == Role.Detective)
                {
                    return seat;
                }
            }

            return -1;
        }

        /// <summary>
        /// Drops a pending intent when the only living player who could have supplied it has
        /// disconnected. For Mafia (a shared target) this keeps the target while any Mafia remains.
        /// </summary>
        private void DropOrphanedIntents()
        {
            if (_doctorProtectSeat.HasValue && !HasLivingConnectedRole(Role.Doctor))
            {
                _doctorProtectSeat = null;
            }

            if (_detectiveTargetSeat.HasValue && !HasLivingConnectedRole(Role.Detective))
            {
                _detectiveTargetSeat = null;
            }

            if (_mafiaTargetSeat.HasValue && !HasLivingConnectedRole(Role.Mafia))
            {
                _mafiaTargetSeat = null;
            }
        }

        private bool HasLivingConnectedRole(Role role)
        {
            for (int seat = 0; seat < _playerCount; seat++)
            {
                if (IsActingSeat(seat) && _driver.RoleOf(SeatToPlayerId(seat)) == role)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearNightIntents()
        {
            _mafiaTargetSeat = null;
            _doctorProtectSeat = null;
            _detectiveTargetSeat = null;
        }

        private void RequireStarted()
        {
            if (!_started)
            {
                throw new InvalidOperationException("The match has not been started.");
            }
        }
    }
}
