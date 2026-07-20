using MafiaGame.Domain.Matches;

namespace MafiaGame.Application.Matches
{
    /// <summary>
    /// The choices the host makes in the lobby before a match starts: how many Mafia, which special
    /// roles are in play, whether an eliminated role is revealed, and how long each phase lasts.
    /// Immutable — every change produces a new instance, so a half-applied setup can never exist.
    /// Engine-free, so the "is this a legal setup" question is answered by unit tests rather than by
    /// clicking around the lobby. The authoritative rules still live in
    /// <see cref="MatchConfiguration"/>; this only carries the host's intent to them.
    /// </summary>
    public sealed class MatchSetup
    {
        /// <summary>Step used by the lobby's − / + buttons, in seconds.</summary>
        public const double SecondsStep = 5d;

        private MatchSetup(
            int mafiaCount,
            bool includeDoctor,
            bool includeDetective,
            bool revealRoleOnElimination,
            MatchTimings timings)
        {
            MafiaCount = mafiaCount;
            IncludeDoctor = includeDoctor;
            IncludeDetective = includeDetective;
            RevealRoleOnElimination = revealRoleOnElimination;
            Timings = timings;
        }

        public int MafiaCount { get; }

        public bool IncludeDoctor { get; }

        public bool IncludeDetective { get; }

        public bool RevealRoleOnElimination { get; }

        public MatchTimings Timings { get; }

        /// <summary>What a fresh lobby starts from: one Mafia, both special roles, role reveal on.</summary>
        public static MatchSetup Default { get; } =
            new MatchSetup(1, true, true, true, MatchTimings.Default);

        public MatchSetup WithMafiaCount(int mafiaCount) =>
            new MatchSetup(mafiaCount, IncludeDoctor, IncludeDetective, RevealRoleOnElimination, Timings);

        public MatchSetup WithDoctor(bool include) =>
            new MatchSetup(MafiaCount, include, IncludeDetective, RevealRoleOnElimination, Timings);

        public MatchSetup WithDetective(bool include) =>
            new MatchSetup(MafiaCount, IncludeDoctor, include, RevealRoleOnElimination, Timings);

        public MatchSetup WithRoleReveal(bool reveal) =>
            new MatchSetup(MafiaCount, IncludeDoctor, IncludeDetective, reveal, Timings);

        public MatchSetup WithTimings(MatchTimings timings) =>
            new MatchSetup(MafiaCount, IncludeDoctor, IncludeDetective, RevealRoleOnElimination, timings);

        /// <summary>
        /// Nudges one phase duration by whole steps, keeping it inside the allowed range. Returns the
        /// same setup when the value is already at the edge, so a button press can never build an
        /// invalid setup — the host simply stops moving.
        /// </summary>
        public MatchSetup WithNightSeconds(double seconds) =>
            Retime(Timings.RoleRevealSeconds, seconds, Timings.AnnouncementSeconds,
                Timings.DiscussionSeconds, Timings.VotingSeconds);

        public MatchSetup WithDiscussionSeconds(double seconds) =>
            Retime(Timings.RoleRevealSeconds, Timings.NightSeconds, Timings.AnnouncementSeconds,
                seconds, Timings.VotingSeconds);

        public MatchSetup WithVotingSeconds(double seconds) =>
            Retime(Timings.RoleRevealSeconds, Timings.NightSeconds, Timings.AnnouncementSeconds,
                Timings.DiscussionSeconds, seconds);

        private MatchSetup Retime(
            double roleReveal, double night, double announcement, double discussion, double voting)
        {
            MatchTimingsResult result =
                MatchTimings.Create(roleReveal, night, announcement, discussion, voting);
            return result.IsValid ? WithTimings(result.Timings) : this;
        }

        /// <summary>
        /// Turns the host's choices into an authoritative configuration for
        /// <paramref name="playerCount"/> players, or reports why they are not allowed. The lobby
        /// shows the error instead of silently repairing the setup, so the host sees what is wrong.
        /// </summary>
        public MatchConfigurationResult ToConfiguration(int playerCount) => MatchConfiguration.Create(
            playerCount, MafiaCount, IncludeDoctor, IncludeDetective, RevealRoleOnElimination);

        /// <summary>A one-line, leak-free summary every player may see before the match starts.</summary>
        public string Describe()
        {
            string special;
            if (IncludeDoctor && IncludeDetective)
            {
                special = "Doktor + Detektiv";
            }
            else if (IncludeDoctor)
            {
                special = "Doktor";
            }
            else if (IncludeDetective)
            {
                special = "Detektiv";
            }
            else
            {
                special = "bez specijalnih uloga";
            }

            string reveal = RevealRoleOnElimination ? "uloga se otkriva" : "uloga se ne otkriva";
            return $"Mafija: {MafiaCount}  |  {special}  |  {reveal}\n" +
                   $"Noć {Timings.NightSeconds:0}s  |  Diskusija {Timings.DiscussionSeconds:0}s  |  " +
                   $"Glasanje {Timings.VotingSeconds:0}s";
        }
    }
}
