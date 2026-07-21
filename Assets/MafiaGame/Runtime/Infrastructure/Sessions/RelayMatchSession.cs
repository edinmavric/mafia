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

            // The normal path first. A fresh join must not be routed through a reconnect: a player
            // still carrying a stale membership from an earlier game would otherwise be dragged into
            // reconnecting to a dead Relay allocation ("Failed to join allocation") instead of simply
            // joining the game they typed the code for.
            try
            {
                ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim());
                Bind(session);
            }
            catch (Exception exception) when (IsAlreadyMember(exception))
            {
                // Only a genuine returning player lands here: they dropped and are still listed in
                // this session, so a fresh join is refused. Reconnecting puts them back into the same
                // Relay allocation and the same running match.
                await ReconnectToExistingSessionAsync();
            }
        }

        /// <summary>
        /// True when a join failed only because the player is still listed as a member of the
        /// session — the whole exception chain is checked because the detail can arrive wrapped.
        /// </summary>
        private static bool IsAlreadyMember(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (current.Message != null && current.Message.Contains("already a member"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reconnects to the live session this player is still a member of. A profile that has played
        /// before may also be listed in older, dead sessions; those are skipped (their allocation is
        /// gone) until the running one reconnects.
        /// </summary>
        private async Task ReconnectToExistingSessionAsync()
        {
            List<string> joined = await MultiplayerService.Instance.GetJoinedSessionIdsAsync();
            if (joined != null)
            {
                foreach (string id in joined)
                {
                    try
                    {
                        ISession session = await MultiplayerService.Instance.ReconnectToSessionAsync(id);
                        Bind(session);
                        return;
                    }
                    catch (SessionException)
                    {
                        // A stale membership points at a dead allocation; try the next joined session.
                    }
                }
            }

            throw new InvalidOperationException(
                "Već si član partije, ali ponovno povezivanje nije uspelo. Sačekaj koji sekund pa probaj ponovo.");
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
