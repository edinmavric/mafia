using MafiaGame.Domain.Roles;

namespace MafiaGame.Application.Matches
{
    /// <summary>
    /// The public outcome of a night, safe to broadcast to every client. It reports whether someone
    /// died and, only when the host enabled role reveal on elimination, the dead player's role. When
    /// nobody dies, <see cref="KilledSeat"/> is <c>-1</c> and <see cref="RevealedRole"/> is null.
    /// </summary>
    public sealed class NightPublicResult
    {
        public NightPublicResult(int killedSeat, Role? revealedRole)
        {
            KilledSeat = killedSeat;
            RevealedRole = revealedRole;
        }

        /// <summary>Seat that died, or <c>-1</c> if the night produced no death.</summary>
        public int KilledSeat { get; }

        /// <summary>True when a player died this night.</summary>
        public bool SomeoneDied => KilledSeat >= 0;

        /// <summary>The dead player's role, only when host-enabled reveal applies; otherwise null.</summary>
        public Role? RevealedRole { get; }
    }
}
