namespace MafiaGame.Domain.Matches
{
    /// <summary>
    /// The authoritative phases a Mafia match progresses through.
    /// This enum is the single source of truth for match progression.
    /// Scattered boolean flags (isNight, isVoting, canTalk, hasStarted) are
    /// deliberately avoided; any such flag must be derived from this phase.
    /// </summary>
    public enum MatchPhase
    {
        Lobby,
        RoleReveal,
        Night,
        NightResolution,
        DayAnnouncement,
        DayDiscussion,
        Voting,
        VotingResolution,
        GameOver
    }
}
