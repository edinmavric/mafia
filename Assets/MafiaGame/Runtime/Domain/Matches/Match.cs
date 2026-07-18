using System;
using System.Collections.Generic;
using MafiaGame.Domain.Players;
using MafiaGame.Domain.Roles;

namespace MafiaGame.Domain.Matches
{
    /// <summary>
    /// Aggregate that owns the authoritative roster and phase for a single match. Elimination is
    /// the one mutation exposed on the roster; role assignment happens once up front and player
    /// roles never change afterwards. The phase machine is owned here so nothing else can mutate
    /// the current phase directly.
    /// </summary>
    public sealed class Match
    {
        private readonly List<PlayerState> _players;

        public Match(IReadOnlyList<PlayerState> players)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            _players = new List<PlayerState>(players);
            Phases = new MatchPhaseMachine();
        }

        public MatchPhaseMachine Phases { get; }

        public IReadOnlyList<PlayerState> Players => _players;

        public IEnumerable<PlayerState> AlivePlayers()
        {
            foreach (PlayerState player in _players)
            {
                if (player.IsAlive)
                {
                    yield return player;
                }
            }
        }

        public PlayerState Find(PlayerId id)
        {
            foreach (PlayerState player in _players)
            {
                if (player.Id == id)
                {
                    return player;
                }
            }

            return null;
        }

        /// <summary>
        /// Eliminates a living player. Returns false when the id is unknown or already dead,
        /// so callers can detect a no-op without exceptions.
        /// </summary>
        public bool Eliminate(PlayerId id)
        {
            PlayerState player = Find(id);
            if (player == null || !player.IsAlive)
            {
                return false;
            }

            player.MarkDead();
            return true;
        }

        public int AliveCountOf(Faction faction)
        {
            int count = 0;
            foreach (PlayerState player in _players)
            {
                if (player.IsAlive && RoleFactions.FactionOf(player.Role) == faction)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
