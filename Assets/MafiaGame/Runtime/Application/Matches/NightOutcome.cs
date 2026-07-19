namespace MafiaGame.Application.Matches
{
    /// <summary>
    /// The full result of resolving a night, split into what is public and what is private. The
    /// transport (Infrastructure) broadcasts <see cref="Public"/> to everyone and sends
    /// <see cref="DetectivePrivate"/> only to the Detective's seat. Keeping the split here means the
    /// authority — not the networking code — decides what is hidden.
    /// </summary>
    public sealed class NightOutcome
    {
        public NightOutcome(NightPublicResult publicResult, DetectivePrivateResult detectivePrivate)
        {
            Public = publicResult;
            DetectivePrivate = detectivePrivate;
        }

        /// <summary>Safe-to-broadcast result.</summary>
        public NightPublicResult Public { get; }

        /// <summary>Detective-only result, or null when no Detective acted.</summary>
        public DetectivePrivateResult DetectivePrivate { get; }
    }
}
