using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MafiaGame.Application.Sessions;
using MafiaGame.Infrastructure.Networking;
using Unity.Services.Multiplayer;

namespace MafiaGame.Infrastructure.Sessions
{
    /// <summary>
    /// <see cref="IMatchSession"/> implemented over Unity Multiplayer Services (Sessions API). A host
    /// creates a private Relay-backed session and shares its code; clients join by code. Session
    /// membership changes are surfaced through <see cref="Changed"/> so the lobby UI can refresh.
    ///
    /// This adapter deliberately exposes only ids and the join code — no SDK types leak outward.
    /// </summary>
    public sealed class RelayMatchSession : IMatchSession
    {
        private ISession _session;

        public bool IsActive => _session != null;

        public bool IsHost => _session != null && _session.IsHost;

        public string JoinCode => _session != null ? _session.Code : string.Empty;

        public string LocalPlayerId =>
            _session != null && _session.CurrentPlayer != null ? _session.CurrentPlayer.Id : string.Empty;

        public IReadOnlyList<string> PlayerIds
        {
            get
            {
                var ids = new List<string>();
                if (_session != null)
                {
                    foreach (IReadOnlyPlayer player in _session.Players)
                    {
                        ids.Add(player.Id);
                    }
                }

                return ids;
            }
        }

        public event Action Changed;

        public async Task<string> HostAsync(int maxPlayers)
        {
            TransportTuning.ApplyFastFailure();
            var options = new SessionOptions { MaxPlayers = maxPlayers, IsPrivate = true }.WithRelayNetwork();
            IHostSession session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Bind(session);
            return session.Code;
        }

        public async Task JoinByCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("Join code is required.", nameof(code));
            }

            TransportTuning.ApplyFastFailure();
            ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim());
            Bind(session);
        }

        public async Task LeaveAsync()
        {
            if (_session == null)
            {
                return;
            }

            ISession leaving = _session;
            Unbind(leaving);
            _session = null;
            await leaving.LeaveAsync();
            Changed?.Invoke();
        }

        private void Bind(ISession session)
        {
            _session = session;
            session.PlayerJoined += OnPlayerChanged;
            session.PlayerLeaving += OnPlayerChanged;
            session.Changed += OnChanged;
            session.RemovedFromSession += OnRemoved;
            Changed?.Invoke();
        }

        private void Unbind(ISession session)
        {
            session.PlayerJoined -= OnPlayerChanged;
            session.PlayerLeaving -= OnPlayerChanged;
            session.Changed -= OnChanged;
            session.RemovedFromSession -= OnRemoved;
        }

        private void OnPlayerChanged(string playerId) => Changed?.Invoke();

        private void OnChanged() => Changed?.Invoke();

        private void OnRemoved()
        {
            if (_session != null)
            {
                Unbind(_session);
                _session = null;
            }

            Changed?.Invoke();
        }
    }
}
