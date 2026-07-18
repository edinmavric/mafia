namespace MafiaGame.Domain.Roles
{
    /// <summary>
    /// Single source of truth mapping a <see cref="Role"/> to its <see cref="Faction"/>.
    /// The Detective's public investigation result is derived from this mapping.
    /// </summary>
    public static class RoleFactions
    {
        public static Faction FactionOf(Role role) =>
            role == Role.Mafia ? Faction.Mafia : Faction.Town;
    }
}
