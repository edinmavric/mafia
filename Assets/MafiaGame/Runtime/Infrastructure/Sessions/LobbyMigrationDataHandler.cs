using Unity.Services.Multiplayer;

namespace MafiaGame.Infrastructure.Sessions
{
    /// <summary>
    /// Carries nothing across a host handover, because in the lobby there is nothing to carry: no
    /// roles have been dealt, no phase is running, and the new host simply becomes the one who may
    /// press start (owner decision 2026-07-21, "lobby only").
    ///
    /// The Sessions SDK requires a handler to enable host migration at all, and it uploads whatever
    /// this returns to the Lobby service on a timer. Returning nothing is therefore also the safe
    /// choice: a match state would contain **every player's role**, and shipping that through a
    /// service — to whichever player happens to be picked as the next host — is a decision on its
    /// own. Carrying a running match through a handover is deliberately out of scope; see
    /// docs/game-rules.md.
    /// </summary>
    public sealed class LobbyMigrationDataHandler : IMigrationDataHandler
    {
        /// <summary>
        /// Marks what produced the payload, so a later handler that really does carry a match can
        /// tell the two apart instead of guessing at a bare blob.
        /// </summary>
        private const byte LobbyOnlyFormat = 1;

        /// <summary>
        /// One marker byte rather than an empty array. "Nothing to carry" is not the same as "send
        /// nothing": the Lobby service rejects a zero-length upload, and the exception stopped the
        /// SDK from scheduling any further uploads — which left host migration half-finished, with
        /// the lobby handed over but the network never following.
        /// </summary>
        public byte[] Generate() => new[] { LobbyOnlyFormat };

        public void Apply(byte[] migrationData)
        {
            // Nothing to restore. The new host starts from the default lobby setup, which is public
            // information the players can see and change again before they start.
        }
    }
}
