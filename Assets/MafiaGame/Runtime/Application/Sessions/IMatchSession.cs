using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MafiaGame.Application.Sessions
{
    /// <summary>
    /// Port for a networked match session (host or join-by-code). The concrete adapter lives in
    /// Infrastructure and wraps Unity's Multiplayer Services (Relay + Lobby). This interface is kept
    /// free of any networking-SDK types so application, presentation, and tests depend only on the
    /// abstraction — never on UGS directly.
    /// </summary>
    public interface IMatchSession
    {
        /// <summary>True once a session has been created or joined.</summary>
        bool IsActive { get; }

        /// <summary>True when this peer hosts the session (the authority).</summary>
        bool IsHost { get; }

        /// <summary>The code other players use to join; empty when inactive.</summary>
        string JoinCode { get; }

        /// <summary>The authenticated id of the local player; empty when inactive.</summary>
        string LocalPlayerId { get; }

        /// <summary>Ids of all players currently in the session.</summary>
        IReadOnlyList<string> PlayerIds { get; }

        /// <summary>Raised whenever session membership or state changes.</summary>
        event Action Changed;

        /// <summary>Creates a private session over Relay and returns the join code.</summary>
        Task<string> HostAsync(int maxPlayers);

        /// <summary>Joins an existing session by its code.</summary>
        Task JoinByCodeAsync(string code);

        /// <summary>Leaves (or, for the host, tears down) the session.</summary>
        Task LeaveAsync();
    }
}
