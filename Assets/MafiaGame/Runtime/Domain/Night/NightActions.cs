using MafiaGame.Domain.Players;

namespace MafiaGame.Domain.Night
{
    /// <summary>
    /// The night's submitted intents. Each is optional (null when not submitted). The Mafia
    /// team submits a single shared <see cref="MafiaTarget"/>. These are inputs only; the
    /// authority validates them during resolution.
    /// </summary>
    public sealed class NightActions
    {
        public NightActions(PlayerId? mafiaTarget, PlayerId? doctorProtect, PlayerId? detectiveInvestigate)
        {
            MafiaTarget = mafiaTarget;
            DoctorProtect = doctorProtect;
            DetectiveInvestigate = detectiveInvestigate;
        }

        public PlayerId? MafiaTarget { get; }

        public PlayerId? DoctorProtect { get; }

        public PlayerId? DetectiveInvestigate { get; }
    }
}
