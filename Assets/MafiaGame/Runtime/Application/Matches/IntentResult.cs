namespace MafiaGame.Application.Matches
{
    /// <summary>
    /// Outcome of submitting a night intent to the authority. Expected invalid player actions are
    /// modelled as explicit results (not exceptions) per the project rules; the reason is a safe,
    /// non-leaking <see cref="IntentRejection"/>.
    /// </summary>
    public readonly struct IntentResult
    {
        private IntentResult(bool accepted, IntentRejection reason)
        {
            Accepted = accepted;
            Reason = reason;
        }

        public bool Accepted { get; }

        public IntentRejection Reason { get; }

        public static IntentResult Accept() => new IntentResult(true, IntentRejection.None);

        public static IntentResult Reject(IntentRejection reason) => new IntentResult(false, reason);
    }
}
