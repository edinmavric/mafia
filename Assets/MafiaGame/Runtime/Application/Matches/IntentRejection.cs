namespace MafiaGame.Application.Matches
{
    /// <summary>
    /// Reason a submitted night intent was rejected by the authority. This is a localization-friendly
    /// enum rather than a raw string so presentation can map it to player-facing text, and so the
    /// authority never leaks hidden information through an error message.
    /// </summary>
    public enum IntentRejection
    {
        /// <summary>The intent was accepted; no rejection.</summary>
        None,

        /// <summary>The match is not in a phase where this intent is legal.</summary>
        WrongPhase,

        /// <summary>The sender may not perform this action (dead, disconnected, or wrong role).</summary>
        NotAllowed,

        /// <summary>The chosen target is not a valid living player.</summary>
        InvalidTarget
    }
}
