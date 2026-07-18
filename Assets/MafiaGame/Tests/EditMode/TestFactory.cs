using System.Collections.Generic;
using MafiaGame.Domain.Matches;
using MafiaGame.Domain.Players;
using MafiaGame.Domain.Roles;

namespace MafiaGame.Tests.EditMode
{
    /// <summary>Small builders to keep the domain tests terse and readable.</summary>
    internal static class TestFactory
    {
        public static PlayerId Id(int value) => new PlayerId(value);

        public static Match MatchWith(params (int id, Role role)[] players)
        {
            var states = new List<PlayerState>(players.Length);
            foreach ((int id, Role role) in players)
            {
                states.Add(new PlayerState(new PlayerId(id), role));
            }

            return new Match(states);
        }
    }
}
