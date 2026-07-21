using System.Collections.Generic;

namespace MafiaGame.Domain.Matches
{
    /// <summary>
    /// Owns the single authoritative current <see cref="MatchPhase"/> and is the
    /// one place where phase transitions are validated. No other component may
    /// mutate the phase directly; they request a transition and receive an
    /// explicit <see cref="PhaseTransitionResult"/>.
    ///
    /// The allowed transitions model only the structural flow of a match. They do
    /// NOT decide *why* the game ends (win-condition evaluation) or *how long* a
    /// phase lasts (authoritative timers) — those are separate concerns handled by
    /// higher layers. Two resolution phases can lead to <see cref="MatchPhase.GameOver"/>
    /// because eliminations there may satisfy a win condition.
    /// </summary>
    public sealed class MatchPhaseMachine
    {
        private static readonly IReadOnlyDictionary<MatchPhase, MatchPhase[]> AllowedTransitions =
            new Dictionary<MatchPhase, MatchPhase[]>
            {
                { MatchPhase.Lobby, new[] { MatchPhase.RoleReveal } },
                { MatchPhase.RoleReveal, new[] { MatchPhase.Night } },
                { MatchPhase.Night, new[] { MatchPhase.NightResolution } },
                { MatchPhase.NightResolution, new[] { MatchPhase.DayAnnouncement, MatchPhase.GameOver } },
                { MatchPhase.DayAnnouncement, new[] { MatchPhase.DayDiscussion } },
                { MatchPhase.DayDiscussion, new[] { MatchPhase.Voting } },
                { MatchPhase.Voting, new[] { MatchPhase.VotingResolution, MatchPhase.TieBreaker } },

                // A tie sends the day into the defense window and back into voting for the revote.
                // The revote's own tie ends the day through VotingResolution instead, so the loop
                // cannot repeat: only the first tie of a day reaches TieBreaker.
                { MatchPhase.TieBreaker, new[] { MatchPhase.Voting } },
                { MatchPhase.VotingResolution, new[] { MatchPhase.Night, MatchPhase.GameOver } },
                { MatchPhase.GameOver, System.Array.Empty<MatchPhase>() },
            };

        /// <summary>
        /// Creates a phase machine positioned at <paramref name="startingPhase"/>.
        /// Defaults to <see cref="MatchPhase.Lobby"/>, the start of every match.
        /// </summary>
        public MatchPhaseMachine(MatchPhase startingPhase = MatchPhase.Lobby)
        {
            CurrentPhase = startingPhase;
        }

        /// <summary>The authoritative current phase.</summary>
        public MatchPhase CurrentPhase { get; private set; }

        /// <summary>
        /// Pure check of whether a direct transition from <paramref name="from"/> to
        /// <paramref name="to"/> is legal, without changing any state.
        /// </summary>
        public static bool CanTransition(MatchPhase from, MatchPhase to)
        {
            if (!AllowedTransitions.TryGetValue(from, out MatchPhase[] targets))
            {
                return false;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == to)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to advance the current phase to <paramref name="target"/>.
        /// On success, <see cref="CurrentPhase"/> is updated and an allowed result
        /// is returned. On failure, the phase is left unchanged and a rejected
        /// result carrying a safe reason is returned.
        /// </summary>
        public PhaseTransitionResult TryAdvanceTo(MatchPhase target)
        {
            MatchPhase from = CurrentPhase;

            if (!CanTransition(from, target))
            {
                return PhaseTransitionResult.Rejected(
                    from, target, $"Transition from {from} to {target} is not allowed.");
            }

            CurrentPhase = target;
            return PhaseTransitionResult.Allowed(from, target);
        }
    }
}
