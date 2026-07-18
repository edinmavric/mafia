using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MafiaGame.Application.Sessions;

namespace MafiaGame.Tests.EditMode
{
    /// <summary>In-memory <see cref="IMatchSession"/> for testing the lobby presenter without UGS.</summary>
    internal sealed class FakeMatchSession : IMatchSession
    {
        private readonly List<string> _players = new List<string>();

        public bool IsActive { get; private set; }
        public bool IsHost { get; private set; }
        public string JoinCode { get; private set; } = string.Empty;
        public string LocalPlayerId { get; } = "local-player";
        public IReadOnlyList<string> PlayerIds => _players;

        public event Action Changed;

        /// <summary>When set, host/join throw this to exercise error handling.</summary>
        public Exception FailWith { get; set; }

        public Task<string> HostAsync(int maxPlayers)
        {
            if (FailWith != null)
            {
                throw FailWith;
            }

            IsActive = true;
            IsHost = true;
            JoinCode = "ABC123";
            _players.Clear();
            _players.Add(LocalPlayerId);
            Changed?.Invoke();
            return Task.FromResult(JoinCode);
        }

        public Task JoinByCodeAsync(string code)
        {
            if (FailWith != null)
            {
                throw FailWith;
            }

            IsActive = true;
            IsHost = false;
            JoinCode = code;
            _players.Clear();
            _players.Add("remote-host");
            _players.Add(LocalPlayerId);
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        public Task LeaveAsync()
        {
            IsActive = false;
            IsHost = false;
            JoinCode = string.Empty;
            _players.Clear();
            Changed?.Invoke();
            return Task.CompletedTask;
        }
    }
}
