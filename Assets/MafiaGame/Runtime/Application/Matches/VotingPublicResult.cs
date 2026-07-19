using System.Collections.Generic;
using MafiaGame.Domain.Roles;
using MafiaGame.Domain.Voting;

namespace MafiaGame.Application.Matches
{
    /// <summary>
    /// Public result of one voting round, expressed in seats so it can be broadcast to everyone.
    /// It contains nothing hidden: the eliminated seat is public by the rules, and the role is only
    /// present when the host enabled reveal-on-elimination.
    /// </summary>
    public sealed class VotingPublicResult
    {
        public VotingPublicResult(
            VoteOutcome outcome, int eliminatedSeat, Role? revealedRole, IReadOnlyList<int> tiedSeats)
        {
            Outcome = outcome;
            EliminatedSeat = eliminatedSeat;
            RevealedRole = revealedRole;
            TiedSeats = tiedSeats;
        }

        public VoteOutcome Outcome { get; }

        /// <summary>Seat eliminated by the vote, or -1 when nobody was eliminated.</summary>
        public int EliminatedSeat { get; }

        public bool SomeoneEliminated => EliminatedSeat >= 0;

        /// <summary>Set only when the host enabled role reveal on elimination.</summary>
        public Role? RevealedRole { get; }

        /// <summary>Tied seats when the round asks for a revote; empty otherwise.</summary>
        public IReadOnlyList<int> TiedSeats { get; }
    }
}
