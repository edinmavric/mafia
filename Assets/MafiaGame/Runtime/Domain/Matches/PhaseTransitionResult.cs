namespace MafiaGame.Domain.Matches
{
    /// <summary>
    /// Explicit outcome of an attempted match-phase transition.
    /// Ordinary illegal transitions are expected gameplay/flow errors, so they
    /// are represented as a returned result rather than a thrown exception.
    /// (Exceptions are reserved for unexpected programmer/system failures.)
    /// </summary>
    public sealed class PhaseTransitionResult
    {
        private PhaseTransitionResult(bool isAllowed, MatchPhase fromPhase, MatchPhase toPhase, string rejectionReason)
        {
            IsAllowed = isAllowed;
            FromPhase = fromPhase;
            ToPhase = toPhase;
            RejectionReason = rejectionReason;
        }

        /// <summary>True when the transition is legal.</summary>
        public bool IsAllowed { get; }

        /// <summary>The phase the transition started from.</summary>
        public MatchPhase FromPhase { get; }

        /// <summary>The phase the transition targeted.</summary>
        public MatchPhase ToPhase { get; }

        /// <summary>
        /// A safe, human-readable reason when the transition was rejected;
        /// <c>null</c> when the transition is allowed. This message must never
        /// contain hidden-information (roles, private results, etc.).
        /// </summary>
        public string RejectionReason { get; }

        internal static PhaseTransitionResult Allowed(MatchPhase fromPhase, MatchPhase toPhase) =>
            new PhaseTransitionResult(true, fromPhase, toPhase, rejectionReason: null);

        internal static PhaseTransitionResult Rejected(MatchPhase fromPhase, MatchPhase toPhase, string reason) =>
            new PhaseTransitionResult(false, fromPhase, toPhase, reason);
    }
}
