namespace MafiaGame.Domain.Matches
{
    /// <summary>
    /// Outcome of validating host-provided match settings. Invalid host input is an expected
    /// case (the host is choosing options), so it is represented as a result rather than an
    /// exception. On success <see cref="Configuration"/> is non-null; on failure
    /// <see cref="Error"/> carries a safe, user-presentable reason.
    /// </summary>
    public sealed class MatchConfigurationResult
    {
        private MatchConfigurationResult(bool isValid, MatchConfiguration configuration, string error)
        {
            IsValid = isValid;
            Configuration = configuration;
            Error = error;
        }

        public bool IsValid { get; }

        public MatchConfiguration Configuration { get; }

        public string Error { get; }

        internal static MatchConfigurationResult Valid(MatchConfiguration configuration) =>
            new MatchConfigurationResult(true, configuration, error: null);

        internal static MatchConfigurationResult Invalid(string error) =>
            new MatchConfigurationResult(false, configuration: null, error);
    }
}
