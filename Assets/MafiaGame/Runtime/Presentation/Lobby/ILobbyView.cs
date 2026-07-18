using System;
using System.Collections.Generic;

namespace MafiaGame.Presentation.Lobby
{
    /// <summary>Abstraction the lobby presenter renders through. The real view is a MonoBehaviour; tests use a fake.</summary>
    public interface ILobbyView
    {
        /// <summary>Current text of the join-code input field.</summary>
        string JoinCodeInput { get; }

        event Action HostClicked;
        event Action JoinClicked;
        event Action LeaveClicked;

        void ShowStatus(string message);

        void ShowDisconnected();

        void ShowConnected(string joinCode, bool isHost, IReadOnlyList<string> playerIds);
    }
}
