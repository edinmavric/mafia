namespace MafiaGame.Application.Matches
{
    /// <summary>
    /// What the phase timer says is now due. The authority decides this; the transport only carries
    /// out the matching step (and ships the resulting public/private payloads). Keeping it a plain
    /// value means "what expires into what" is unit-tested without a network or a real clock.
    /// </summary>
    public enum PhaseAdvance
    {
        /// <summary>Nothing is due: the phase is untimed or time is still left.</summary>
        None,

        /// <summary>Role reveal ran out: enter the night.</summary>
        ConfirmRolesSeen,

        /// <summary>The night ran out: resolve it with whatever intents arrived.</summary>
        ResolveNight,

        /// <summary>The announcement ran out: open the discussion.</summary>
        ContinueToDiscussion,

        /// <summary>The discussion ran out: open voting.</summary>
        BeginVoting,

        /// <summary>Voting ran out: tally whatever votes were cast (abstention is allowed).</summary>
        ResolveVoting
    }
}
