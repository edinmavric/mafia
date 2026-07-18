using System;
using MafiaGame.Domain.Roles;

namespace MafiaGame.Domain.Matches
{
    /// <summary>
    /// Evaluates the win condition from the current alive roster.
    /// Town wins when no Mafia remain; Mafia wins when they reach parity with the non-Mafia side.
    /// </summary>
    public sealed class WinConditionEvaluator
    {
        public GameOutcome Evaluate(Match match)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            int aliveMafia = match.AliveCountOf(Faction.Mafia);
            int aliveTown = match.AliveCountOf(Faction.Town);

            if (aliveMafia == 0)
            {
                return GameOutcome.TownWins;
            }

            if (aliveMafia >= aliveTown)
            {
                return GameOutcome.MafiaWins;
            }

            return GameOutcome.None;
        }
    }
}
