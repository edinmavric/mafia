namespace MafiaGame.Domain.Matches
{
    /// <summary>
    /// Immutable, host-chosen configuration for a single match. Instances can only be created
    /// through <see cref="Create"/>, which enforces every configuration rule, so a
    /// <see cref="MatchConfiguration"/> is always valid once it exists.
    ///
    /// Rules (see docs/game-rules.md):
    /// - at least <see cref="MinPlayers"/> players;
    /// - 1..<see cref="MaxMafiaFor"/> mafia;
    /// - a special role needs <see cref="MinPlayersForSpecialRole"/> players, both need
    ///   <see cref="MinPlayersForBothSpecialRoles"/>;
    /// - mafia must start strictly fewer than the non-mafia side.
    /// </summary>
    public sealed class MatchConfiguration
    {
        public const int MinPlayers = 4;
        public const int MinPlayersForSpecialRole = 5;
        public const int MinPlayersForBothSpecialRoles = 7;

        private MatchConfiguration(
            int playerCount,
            int mafiaCount,
            bool includeDoctor,
            bool includeDetective,
            bool revealRoleOnElimination)
        {
            PlayerCount = playerCount;
            MafiaCount = mafiaCount;
            IncludeDoctor = includeDoctor;
            IncludeDetective = includeDetective;
            RevealRoleOnElimination = revealRoleOnElimination;
        }

        public int PlayerCount { get; }

        public int MafiaCount { get; }

        public bool IncludeDoctor { get; }

        public bool IncludeDetective { get; }

        /// <summary>Host setting chosen before the game: reveal an eliminated player's role publicly.</summary>
        public bool RevealRoleOnElimination { get; }

        public int SpecialRoleCount => (IncludeDoctor ? 1 : 0) + (IncludeDetective ? 1 : 0);

        public int CitizenCount => PlayerCount - MafiaCount - SpecialRoleCount;

        /// <summary>Maximum mafia allowed for a given lobby size.</summary>
        public static int MaxMafiaFor(int playerCount)
        {
            if (playerCount <= 6)
            {
                return 1;
            }

            if (playerCount <= 9)
            {
                return 2;
            }

            return 3;
        }

        public static MatchConfigurationResult Create(
            int playerCount,
            int mafiaCount,
            bool includeDoctor,
            bool includeDetective,
            bool revealRoleOnElimination)
        {
            if (playerCount < MinPlayers)
            {
                return MatchConfigurationResult.Invalid($"Player count must be at least {MinPlayers}.");
            }

            if (mafiaCount < 1)
            {
                return MatchConfigurationResult.Invalid("Mafia count must be at least 1.");
            }

            int maxMafia = MaxMafiaFor(playerCount);
            if (mafiaCount > maxMafia)
            {
                return MatchConfigurationResult.Invalid(
                    $"Mafia count {mafiaCount} exceeds the maximum {maxMafia} for {playerCount} players.");
            }

            int specialRoles = (includeDoctor ? 1 : 0) + (includeDetective ? 1 : 0);
            if (specialRoles >= 1 && playerCount < MinPlayersForSpecialRole)
            {
                return MatchConfigurationResult.Invalid(
                    $"A special role requires at least {MinPlayersForSpecialRole} players.");
            }

            if (specialRoles == 2 && playerCount < MinPlayersForBothSpecialRoles)
            {
                return MatchConfigurationResult.Invalid(
                    $"Both special roles require at least {MinPlayersForBothSpecialRoles} players.");
            }

            int citizens = playerCount - mafiaCount - specialRoles;
            if (citizens < 0)
            {
                return MatchConfigurationResult.Invalid("Configured roles exceed the player count.");
            }

            int nonMafia = playerCount - mafiaCount;
            if (mafiaCount >= nonMafia)
            {
                return MatchConfigurationResult.Invalid("Mafia must start fewer than the non-Mafia players.");
            }

            return MatchConfigurationResult.Valid(
                new MatchConfiguration(playerCount, mafiaCount, includeDoctor, includeDetective, revealRoleOnElimination));
        }
    }
}
