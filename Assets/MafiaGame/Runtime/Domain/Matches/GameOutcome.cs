namespace MafiaGame.Domain.Matches
{
    /// <summary>Result of evaluating the win condition at a point in the match.</summary>
    public enum GameOutcome
    {
        None,
        TownWins,
        MafiaWins,

        /// <summary>
        /// The match was called off because too few players were left to play it, not won by anyone.
        /// Appended rather than inserted: the values are replicated, so shifting them would silently
        /// mean a different outcome to anything holding an older value.
        /// </summary>
        Abandoned
    }
}
