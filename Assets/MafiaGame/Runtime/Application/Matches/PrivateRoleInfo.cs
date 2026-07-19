using System.Collections.Generic;
using MafiaGame.Domain.Roles;

namespace MafiaGame.Application.Matches
{
    /// <summary>
    /// The private role payload for a single seat. This is what the authority will send to exactly
    /// one client over a targeted channel — never broadcast. Only a Mafia member receives a
    /// non-empty <see cref="MafiaTeammateSeats"/>; every other role receives an empty list.
    /// </summary>
    public sealed class PrivateRoleInfo
    {
        public PrivateRoleInfo(int seat, Role role, IReadOnlyList<int> mafiaTeammateSeats)
        {
            Seat = seat;
            Role = role;
            MafiaTeammateSeats = mafiaTeammateSeats;
        }

        /// <summary>The seat (0..N-1) this payload belongs to.</summary>
        public int Seat { get; }

        /// <summary>The role privately revealed to this seat.</summary>
        public Role Role { get; }

        /// <summary>Seats of the Mafia teammates; empty unless <see cref="Role"/> is Mafia.</summary>
        public IReadOnlyList<int> MafiaTeammateSeats { get; }
    }
}
