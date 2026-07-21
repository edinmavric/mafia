using System;

namespace MafiaGame.Application.Matches
{
    /// <summary>
    /// How long each timed phase lasts, in seconds. Plain data with no clock of its own: the
    /// authority counts elapsed time down, so the whole timer is testable without waiting.
    /// The host picks these before the match starts; clients never influence them.
    /// </summary>
    public sealed class MatchTimings
    {
        /// <summary>Shortest phase a host may configure. Below this nobody can react in time.</summary>
        public const double MinSeconds = 5d;

        /// <summary>Longest phase a host may configure (ten minutes), a sanity bound, not a rule.</summary>
        public const double MaxSeconds = 600d;

        /// <summary>
        /// How long the tied players get to defend themselves before the revote (confirmed rule:
        /// 30 seconds). A constant rather than a configurable duration, like the role-reveal and
        /// announcement times: it is not in the lobby UI and not replicated, so host and clients
        /// always agree on it without another value on the wire.
        /// </summary>
        public const double TieBreakerSeconds = 30d;

        private MatchTimings(
            double roleRevealSeconds,
            double nightSeconds,
            double announcementSeconds,
            double discussionSeconds,
            double votingSeconds)
        {
            RoleRevealSeconds = roleRevealSeconds;
            NightSeconds = nightSeconds;
            AnnouncementSeconds = announcementSeconds;
            DiscussionSeconds = discussionSeconds;
            VotingSeconds = votingSeconds;
        }

        /// <summary>Time every player gets to read their own role before the first night.</summary>
        public double RoleRevealSeconds { get; }

        public double NightSeconds { get; }

        /// <summary>Time to read who died before the free discussion opens.</summary>
        public double AnnouncementSeconds { get; }

        public double DiscussionSeconds { get; }

        public double VotingSeconds { get; }

        /// <summary>
        /// Starting point until a lobby settings screen exists. Chosen for playtesting: long enough
        /// to act, short enough that a match does not drag.
        /// </summary>
        public static MatchTimings Default { get; } =
            new MatchTimings(10d, 45d, 8d, 90d, 45d);

        /// <summary>
        /// Builds timings from host input, rejecting values outside the allowed bounds instead of
        /// silently clamping them, so a bad lobby setting is visible rather than mysterious.
        /// </summary>
        public static MatchTimingsResult Create(
            double roleRevealSeconds,
            double nightSeconds,
            double announcementSeconds,
            double discussionSeconds,
            double votingSeconds)
        {
            string error =
                Validate(roleRevealSeconds, "Prikaz uloga") ??
                Validate(nightSeconds, "Noć") ??
                Validate(announcementSeconds, "Objava") ??
                Validate(discussionSeconds, "Diskusija") ??
                Validate(votingSeconds, "Glasanje");

            if (error != null)
            {
                return MatchTimingsResult.Invalid(error);
            }

            return MatchTimingsResult.Valid(new MatchTimings(
                roleRevealSeconds, nightSeconds, announcementSeconds, discussionSeconds, votingSeconds));
        }

        private static string Validate(double seconds, string label)
        {
            if (double.IsNaN(seconds) || seconds < MinSeconds || seconds > MaxSeconds)
            {
                return $"{label}: trajanje mora biti između {MinSeconds:0} i {MaxSeconds:0} sekundi.";
            }

            return null;
        }
    }

    /// <summary>Result of validating host-supplied timings. Invalid input is an expected outcome.</summary>
    public sealed class MatchTimingsResult
    {
        private MatchTimingsResult(MatchTimings timings, string error)
        {
            Timings = timings;
            Error = error;
        }

        public MatchTimings Timings { get; }

        public string Error { get; }

        public bool IsValid => Timings != null;

        public static MatchTimingsResult Valid(MatchTimings timings) =>
            new MatchTimingsResult(timings ?? throw new ArgumentNullException(nameof(timings)), null);

        public static MatchTimingsResult Invalid(string error) => new MatchTimingsResult(null, error);
    }
}
