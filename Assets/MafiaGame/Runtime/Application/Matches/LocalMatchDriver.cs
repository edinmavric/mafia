using System;
using System.Collections.Generic;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Night;
using MafiaGame.Domain.Players;
using MafiaGame.Domain.Randomness;
using MafiaGame.Domain.Roles;
using MafiaGame.Domain.Voting;

namespace MafiaGame.Application.Matches
{
    /// <summary>
    /// Authoritative, engine-free use-case facade that sequences the pure domain services into a
    /// single playable match. It owns the match state, the phase transitions, and the win check;
    /// callers (the presentation flow) only submit intents. Every operation is phase-gated: calling
    /// one in the wrong phase is a caller bug and throws <see cref="InvalidOperationException"/>.
    ///
    /// Deterministic: the same seed reproduces the same match. This is the natural home for the
    /// phase-gating that Milestone 1 deferred.
    /// </summary>
    public sealed class LocalMatchDriver
    {
        private readonly RoleAssignmentService _roleAssignment = new RoleAssignmentService();
        private readonly NightResolutionService _night = new NightResolutionService();
        private readonly VotingService _voting = new VotingService();
        private readonly WinConditionEvaluator _win = new WinConditionEvaluator();

        private Match _match;
        private MatchConfiguration _config;
        private PlayerId? _previousDoctorProtect;

        public MatchPhase CurrentPhase => _match?.Phases.CurrentPhase ?? MatchPhase.Lobby;

        public GameOutcome Outcome { get; private set; } = GameOutcome.None;

        public bool RevealRoleOnElimination => _config != null && _config.RevealRoleOnElimination;

        public IReadOnlyList<PlayerState> Players =>
            _match?.Players ?? (IReadOnlyList<PlayerState>)Array.Empty<PlayerState>();

        public IEnumerable<PlayerState> AlivePlayers() =>
            _match != null ? _match.AlivePlayers() : System.Linq.Enumerable.Empty<PlayerState>();

        public Role RoleOf(PlayerId id)
        {
            PlayerState player = RequireMatch().Find(id);
            if (player == null)
            {
                throw new ArgumentException($"Unknown player {id}.", nameof(id));
            }

            return player.Role;
        }

        public IReadOnlyList<PlayerId> MafiaTeammates(PlayerId id)
        {
            Match match = RequireMatch();
            var teammates = new List<PlayerId>();
            PlayerState self = match.Find(id);
            if (self == null || self.Role != Role.Mafia)
            {
                return teammates;
            }

            foreach (PlayerState player in match.Players)
            {
                if (player.Id != id && player.Role == Role.Mafia)
                {
                    teammates.Add(player.Id);
                }
            }

            return teammates;
        }

        /// <summary>Assigns roles from a seed and enters the role-reveal phase.</summary>
        public void Start(MatchConfiguration configuration, IReadOnlyList<PlayerId> roster, int seed)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (roster == null)
            {
                throw new ArgumentNullException(nameof(roster));
            }

            IRandomSource random = new SeededRandomSource(seed);
            RoleAssignmentResult assignment = _roleAssignment.Assign(roster, configuration, random);
            if (!assignment.IsSuccess)
            {
                // The presentation validates the configuration before calling Start, so a failure
                // here is a programming error rather than expected player input.
                throw new InvalidOperationException($"Role assignment failed: {assignment.Error}");
            }

            _config = configuration;
            _match = new Match(assignment.Players);
            _previousDoctorProtect = null;
            Outcome = GameOutcome.None;
            Transition(MatchPhase.RoleReveal);
        }

        /// <summary>Acknowledges that every player has privately seen their role; moves to night.</summary>
        public void ConfirmRolesSeen()
        {
            Guard(MatchPhase.RoleReveal);
            Transition(MatchPhase.Night);
        }

        /// <summary>Validates and resolves the night, applies any death, then branches to day or game over.</summary>
        public NightResolution ResolveNight(NightActions actions)
        {
            Guard(MatchPhase.Night);
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            Transition(MatchPhase.NightResolution);
            NightResolution resolution = _night.Resolve(_match, actions, _previousDoctorProtect);
            _previousDoctorProtect = resolution.ProtectedTarget;

            if (resolution.KilledPlayer.HasValue)
            {
                _match.Eliminate(resolution.KilledPlayer.Value);
            }

            EvaluateAndBranch(MatchPhase.DayAnnouncement);
            return resolution;
        }

        public void ContinueToDiscussion()
        {
            Guard(MatchPhase.DayAnnouncement);
            Transition(MatchPhase.DayDiscussion);
        }

        public void BeginVoting()
        {
            Guard(MatchPhase.DayDiscussion);
            Transition(MatchPhase.Voting);
        }

        /// <summary>Opens the revote once the tied players have had their defense.</summary>
        public void BeginRevote()
        {
            Guard(MatchPhase.TieBreaker);
            Transition(MatchPhase.Voting);
        }

        /// <summary>
        /// Tallies a voting round. A first-round tie returns <see cref="VoteOutcome.TieRequiresRevote"/>
        /// and moves to <see cref="MatchPhase.TieBreaker"/>, where the tied players defend themselves
        /// before the revote (restricted to those candidates) opens.
        /// A terminal outcome applies any elimination and branches to night or game over.
        /// </summary>
        public VotingResolution ResolveVoting(
            IReadOnlyCollection<Vote> votes,
            IReadOnlyCollection<PlayerId> candidateRestriction = null)
        {
            Guard(MatchPhase.Voting);
            if (votes == null)
            {
                throw new ArgumentNullException(nameof(votes));
            }

            var aliveIds = new List<PlayerId>();
            foreach (PlayerState player in _match.AlivePlayers())
            {
                aliveIds.Add(player.Id);
            }

            VotingResolution resolution = _voting.Resolve(votes, aliveIds, candidateRestriction);
            if (resolution.Outcome == VoteOutcome.TieRequiresRevote)
            {
                Transition(MatchPhase.TieBreaker);
                return resolution;
            }

            Transition(MatchPhase.VotingResolution);
            if (resolution.Outcome == VoteOutcome.Eliminated && resolution.EliminatedPlayer.HasValue)
            {
                _match.Eliminate(resolution.EliminatedPlayer.Value);
            }

            EvaluateAndBranch(MatchPhase.Night);
            return resolution;
        }

        /// <summary>
        /// Removes a player who is gone for good. Deliberately does NOT end the match on the spot:
        /// the phase machine only allows a win to be declared from a resolution phase, and — more
        /// importantly — announcing "town wins" the moment the last Mafia's connection drops would
        /// tell everyone what that player's role was. The win condition is evaluated at the next
        /// natural resolution instead, at most one phase later.
        /// Returns false when there is nobody to remove.
        /// </summary>
        public bool ForfeitPlayer(PlayerId id)
        {
            Match match = RequireMatch();
            if (CurrentPhase == MatchPhase.Lobby || CurrentPhase == MatchPhase.GameOver)
            {
                return false;
            }

            foreach (PlayerState player in match.Players)
            {
                if (player.Id == id && player.IsAlive)
                {
                    match.Eliminate(id);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Ends the match now because too few players are left to carry it on. The winner is still
        /// decided by the normal win condition — with two players left the game is already settled
        /// either way (two villagers means no Mafia remain; a Mafia against a villager is parity), so
        /// stopping early must not rob anyone of a win they had earned.
        ///
        /// Only an empty table is recorded as <see cref="GameOutcome.Abandoned"/>: with nobody alive
        /// the evaluator still reports a Town win, because no Mafia are alive — an artefact of the
        /// rule rather than a result. Does nothing once the match is over.
        /// </summary>
        public void EndMatchEarly()
        {
            RequireMatch();
            if (CurrentPhase == MatchPhase.GameOver)
            {
                return;
            }

            bool anyoneAlive = false;
            foreach (PlayerState player in _match.AlivePlayers())
            {
                anyoneAlive = true;
                break;
            }

            GameOutcome decided = _win.Evaluate(_match);
            Outcome = anyoneAlive && decided != GameOutcome.None ? decided : GameOutcome.Abandoned;
            Transition(MatchPhase.GameOver);
        }

        private void EvaluateAndBranch(MatchPhase continuePhase)
        {
            Outcome = _win.Evaluate(_match);
            Transition(Outcome != GameOutcome.None ? MatchPhase.GameOver : continuePhase);
        }

        private void Transition(MatchPhase target)
        {
            PhaseTransitionResult result = _match.Phases.TryAdvanceTo(target);
            if (!result.IsAllowed)
            {
                throw new InvalidOperationException(result.RejectionReason);
            }
        }

        private void Guard(MatchPhase expected)
        {
            if (CurrentPhase != expected)
            {
                throw new InvalidOperationException(
                    $"Operation requires phase {expected} but the match is in {CurrentPhase}.");
            }
        }

        private Match RequireMatch() =>
            _match ?? throw new InvalidOperationException("The match has not been started.");
    }
}
