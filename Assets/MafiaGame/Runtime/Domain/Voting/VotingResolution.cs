using System.Collections.Generic;
using MafiaGame.Domain.Players;

namespace MafiaGame.Domain.Voting
{
    /// <summary>Whether the vote produced an elimination, nothing, or an unresolved tie.</summary>
    public enum VoteOutcome
    {
        Eliminated,
        NoElimination,
        TieRequiresRevote
    }

    /// <summary>
    /// Outcome of tallying a day-vote round. On a first-round tie the outcome is
    /// <see cref="VoteOutcome.TieRequiresRevote"/> with the tied candidates; a revote round
    /// (candidates restricted) that ties again resolves to <see cref="VoteOutcome.NoElimination"/>.
    /// </summary>
    public sealed class VotingResolution
    {
        public VotingResolution(
            VoteOutcome outcome,
            PlayerId? eliminatedPlayer,
            IReadOnlyList<PlayerId> tiedCandidates,
            IReadOnlyList<string> rejections)
        {
            Outcome = outcome;
            EliminatedPlayer = eliminatedPlayer;
            TiedCandidates = tiedCandidates;
            Rejections = rejections;
        }

        public VoteOutcome Outcome { get; }

        public PlayerId? EliminatedPlayer { get; }

        public IReadOnlyList<PlayerId> TiedCandidates { get; }

        public IReadOnlyList<string> Rejections { get; }
    }
}
