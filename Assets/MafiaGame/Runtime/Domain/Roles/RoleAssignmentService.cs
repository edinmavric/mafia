using System;
using System.Collections.Generic;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Players;
using MafiaGame.Domain.Randomness;

namespace MafiaGame.Domain.Roles
{
    /// <summary>
    /// Authority-owned role assignment. Builds the exact role multiset described by the
    /// configuration, shuffles it with the injected <see cref="IRandomSource"/>, and pairs it
    /// with the roster. Deterministic for a given seed, which is what the reproducibility tests
    /// rely on. Clients never run this and never see the seed.
    /// </summary>
    public sealed class RoleAssignmentService
    {
        public RoleAssignmentResult Assign(
            IReadOnlyList<PlayerId> players,
            MatchConfiguration configuration,
            IRandomSource random)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (players.Count != configuration.PlayerCount)
            {
                return RoleAssignmentResult.Failure(
                    $"Expected {configuration.PlayerCount} players but received {players.Count}.");
            }

            var seen = new HashSet<PlayerId>();
            foreach (PlayerId id in players)
            {
                if (!seen.Add(id))
                {
                    return RoleAssignmentResult.Failure($"Duplicate player id {id} in roster.");
                }
            }

            var roles = new List<Role>(players.Count);
            for (int i = 0; i < configuration.MafiaCount; i++)
            {
                roles.Add(Role.Mafia);
            }

            if (configuration.IncludeDoctor)
            {
                roles.Add(Role.Doctor);
            }

            if (configuration.IncludeDetective)
            {
                roles.Add(Role.Detective);
            }

            while (roles.Count < players.Count)
            {
                roles.Add(Role.Citizen);
            }

            random.Shuffle(roles);

            var assigned = new List<PlayerState>(players.Count);
            for (int i = 0; i < players.Count; i++)
            {
                assigned.Add(new PlayerState(players[i], roles[i]));
            }

            return RoleAssignmentResult.Success(assigned);
        }
    }
}
