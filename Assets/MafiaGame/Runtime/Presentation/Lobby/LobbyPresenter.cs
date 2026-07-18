using System;
using System.Threading.Tasks;
using MafiaGame.Application.Sessions;

namespace MafiaGame.Presentation.Lobby
{
    /// <summary>
    /// Mediates between the lobby view and the networked <see cref="IMatchSession"/>. It ensures the
    /// player is signed in, then hosts/joins/leaves, and refreshes the view whenever the session
    /// changes. All game rules stay out of here; this only orchestrates connection use-cases.
    /// Testable: it depends on the <see cref="ILobbyView"/> and <see cref="IMatchSession"/> ports plus
    /// an injected sign-in step.
    /// </summary>
    public sealed class LobbyPresenter : IDisposable
    {
        private readonly ILobbyView _view;
        private readonly IMatchSession _session;
        private readonly Func<Task> _ensureSignedIn;
        private bool _busy;

        public LobbyPresenter(ILobbyView view, IMatchSession session, Func<Task> ensureSignedIn)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _ensureSignedIn = ensureSignedIn ?? throw new ArgumentNullException(nameof(ensureSignedIn));

            _view.HostClicked += OnHostClicked;
            _view.JoinClicked += OnJoinClicked;
            _view.LeaveClicked += OnLeaveClicked;
            _session.Changed += OnSessionChanged;
        }

        public void Begin()
        {
            Refresh();
        }

        private async void OnHostClicked()
        {
            await RunGuarded("Kreiranje igre…", () => _session.HostAsync(10));
        }

        private async void OnJoinClicked()
        {
            string code = _view.JoinCodeInput;
            if (string.IsNullOrWhiteSpace(code))
            {
                _view.ShowStatus("Unesi kod za pridruživanje.");
                return;
            }

            await RunGuarded("Pridruživanje…", () => _session.JoinByCodeAsync(code));
        }

        private async void OnLeaveClicked()
        {
            await RunGuarded("Napuštanje…", () => _session.LeaveAsync());
        }

        private async Task RunGuarded(string status, Func<Task> action)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            _view.ShowStatus(status);
            try
            {
                await _ensureSignedIn();
                await action();
                _view.ShowStatus(string.Empty);
                Refresh();
            }
            catch (Exception exception)
            {
                _view.ShowStatus("Greška: " + exception.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        private void OnSessionChanged() => Refresh();

        private void Refresh()
        {
            if (_session.IsActive)
            {
                _view.ShowConnected(_session.JoinCode, _session.IsHost, _session.PlayerIds);
            }
            else
            {
                _view.ShowDisconnected();
            }
        }

        public void Dispose()
        {
            _view.HostClicked -= OnHostClicked;
            _view.JoinClicked -= OnJoinClicked;
            _view.LeaveClicked -= OnLeaveClicked;
            _session.Changed -= OnSessionChanged;
        }
    }
}
