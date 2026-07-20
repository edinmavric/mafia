using System;
using MafiaGame.Application.Matches;
using Unity.Netcode;

namespace MafiaGame.Infrastructure.Networking
{
    /// <summary>
    /// The host's lobby setup in a form NGO can replicate. Everything here is public information the
    /// players agree on before the match — how many Mafia, which special roles are in play, whether
    /// an eliminated role is revealed, and the phase durations. It carries no role assignment and no
    /// per-player data, so broadcasting it leaks nothing.
    /// </summary>
    public struct MatchSetupSnapshot : INetworkSerializable, IEquatable<MatchSetupSnapshot>
    {
        public byte MafiaCount;
        public bool IncludeDoctor;
        public bool IncludeDetective;
        public bool RevealRoleOnElimination;
        public ushort NightSeconds;
        public ushort DiscussionSeconds;
        public ushort VotingSeconds;

        public static MatchSetupSnapshot From(MatchSetup setup) => new MatchSetupSnapshot
        {
            MafiaCount = (byte)setup.MafiaCount,
            IncludeDoctor = setup.IncludeDoctor,
            IncludeDetective = setup.IncludeDetective,
            RevealRoleOnElimination = setup.RevealRoleOnElimination,
            NightSeconds = (ushort)setup.Timings.NightSeconds,
            DiscussionSeconds = (ushort)setup.Timings.DiscussionSeconds,
            VotingSeconds = (ushort)setup.Timings.VotingSeconds
        };

        /// <summary>
        /// Rebuilds a setup for display. Durations that fail validation fall back to the defaults
        /// rather than throwing: this is a view-side convenience, never the authority's own state.
        /// </summary>
        public MatchSetup ToSetup()
        {
            MatchSetup setup = MatchSetup.Default
                .WithMafiaCount(MafiaCount)
                .WithDoctor(IncludeDoctor)
                .WithDetective(IncludeDetective)
                .WithRoleReveal(RevealRoleOnElimination);

            return setup
                .WithNightSeconds(NightSeconds)
                .WithDiscussionSeconds(DiscussionSeconds)
                .WithVotingSeconds(VotingSeconds);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MafiaCount);
            serializer.SerializeValue(ref IncludeDoctor);
            serializer.SerializeValue(ref IncludeDetective);
            serializer.SerializeValue(ref RevealRoleOnElimination);
            serializer.SerializeValue(ref NightSeconds);
            serializer.SerializeValue(ref DiscussionSeconds);
            serializer.SerializeValue(ref VotingSeconds);
        }

        public bool Equals(MatchSetupSnapshot other) =>
            MafiaCount == other.MafiaCount &&
            IncludeDoctor == other.IncludeDoctor &&
            IncludeDetective == other.IncludeDetective &&
            RevealRoleOnElimination == other.RevealRoleOnElimination &&
            NightSeconds == other.NightSeconds &&
            DiscussionSeconds == other.DiscussionSeconds &&
            VotingSeconds == other.VotingSeconds;

        public override bool Equals(object obj) => obj is MatchSetupSnapshot other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            MafiaCount, IncludeDoctor, IncludeDetective, RevealRoleOnElimination,
            NightSeconds, DiscussionSeconds, VotingSeconds);
    }
}
