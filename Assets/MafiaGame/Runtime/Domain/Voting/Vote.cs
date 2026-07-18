using MafiaGame.Domain.Players;

namespace MafiaGame.Domain.Voting
{
    /// <summary>
    /// A single day-vote intent: <see cref="Voter"/> votes to eliminate <see cref="Target"/>.
    /// Abstaining is modelled by simply not submitting a vote.
    /// </summary>
    public sealed class Vote
    {
        public Vote(PlayerId voter, PlayerId target)
        {
            Voter = voter;
            Target = target;
        }

        public PlayerId Voter { get; }

        public PlayerId Target { get; }
    }
}
