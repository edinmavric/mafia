namespace MafiaGame.Domain.Matches
{
    /// <summary>Result of evaluating the win condition at a point in the match.</summary>
    public enum GameOutcome
    {
        None,
        TownWins,
        MafiaWins
    }
}
