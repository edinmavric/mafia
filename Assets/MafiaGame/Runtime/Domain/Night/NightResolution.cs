using System.Collections.Generic;
using MafiaGame.Domain.Players;

namespace MafiaGame.Domain.Night
{
    /// <summary>
    /// Computed outcome of a night. This is a pure computation: it reports who would die but
    /// does NOT mutate the match — the caller applies <see cref="KilledPlayer"/> via the match
    /// aggregate. <see cref="ProtectedTarget"/> is the doctor's applied protection (used to feed
    /// next night's consecutive-protection check). <see cref="Rejections"/> lists ignored or
    /// illegal intents with safe reasons.
    /// </summary>
    public sealed class NightResolution
    {
        public NightResolution(
            PlayerId? killedPlayer,
            PlayerId? protectedTarget,
            DetectiveResult detectiveResult,
            IReadOnlyList<string> rejections)
        {
            KilledPlayer = killedPlayer;
            ProtectedTarget = protectedTarget;
            DetectiveResult = detectiveResult;
            Rejections = rejections;
        }

        public PlayerId? KilledPlayer { get; }

        public PlayerId? ProtectedTarget { get; }

        public DetectiveResult DetectiveResult { get; }

        public IReadOnlyList<string> Rejections { get; }
    }
}
