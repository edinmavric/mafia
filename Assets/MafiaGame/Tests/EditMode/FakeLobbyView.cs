using System;
using System.Collections.Generic;
using MafiaGame.Presentation.Lobby;

namespace MafiaGame.Tests.EditMode
{
    /// <summary>Fake <see cref="ILobbyView"/> that records renders and lets tests raise UI intents.</summary>
    internal sealed class FakeLobbyView : ILobbyView
    {
        public string JoinCodeInput { get; set; } = string.Empty;

        public event Action HostClicked;
        public event Action JoinClicked;
        public event Action LeaveClicked;

        public string LastStatus { get; private set; }
        public bool IsConnectedShown { get; private set; }
        public string ShownJoinCode { get; private set; }
        public bool ShownIsHost { get; private set; }
        public IReadOnlyList<string> ShownPlayers { get; private set; }

        public void ShowStatus(string message) => LastStatus = message;

        public void ShowDisconnected() => IsConnectedShown = false;

        public void ShowConnected(string joinCode, bool isHost, IReadOnlyList<string> playerIds)
        {
            IsConnectedShown = true;
            ShownJoinCode = joinCode;
            ShownIsHost = isHost;
            ShownPlayers = playerIds;
        }

        public void ClickHost() => HostClicked?.Invoke();
        public void ClickJoin() => JoinClicked?.Invoke();
        public void ClickLeave() => LeaveClicked?.Invoke();
    }
}
