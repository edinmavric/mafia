using MafiaGame.Domain.Roles;

namespace MafiaGame.Domain.Players
{
    /// <summary>
    /// Authoritative per-player runtime state within a match: identity, assigned role, and
    /// whether the player is still alive. This is the server-truth model. It must NEVER be sent
    /// wholesale to clients (roles are hidden information); public/private snapshots are built
    /// separately when networking is added.
    ///
    /// Alive/dead is mutated only through the owning <c>Match</c> aggregate via the internal
    /// <see cref="MarkDead"/>, so elimination goes through one controlled path.
    /// </summary>
    public sealed class PlayerState
    {
        public PlayerState(PlayerId id, Role role)
        {
            Id = id;
            Role = role;
            IsAlive = true;
        }

        public PlayerId Id { get; }

        public Role Role { get; }

        public bool IsAlive { get; private set; }

        internal void MarkDead() => IsAlive = false;
    }
}
