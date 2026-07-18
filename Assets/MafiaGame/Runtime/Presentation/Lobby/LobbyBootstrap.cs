using MafiaGame.Infrastructure.Services;
using MafiaGame.Infrastructure.Sessions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace MafiaGame.Presentation.Lobby
{
    /// <summary>
    /// Composition root for the lobby scene. Wires the Relay-backed session adapter to the lobby
    /// view/presenter. The only scene object needed is one GameObject with this component (plus an
    /// NGO NetworkManager for actual networking — see OWNER_ACTION.md).
    /// </summary>
    public sealed class LobbyBootstrap : MonoBehaviour
    {
        public LobbyView View { get; private set; }

        public LobbyPresenter Presenter { get; private set; }

        private void Start()
        {
            EnsureEventSystem();

            var viewObject = new GameObject("LobbyView");
            viewObject.transform.SetParent(transform, false);
            View = viewObject.AddComponent<LobbyView>();

            var session = new RelayMatchSession();
            Presenter = new LobbyPresenter(View, session, GameServices.InitializeAndSignInAsync);
            Presenter.Begin();
        }

        private void OnDestroy()
        {
            Presenter?.Dispose();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));
            InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
            try
            {
                module.AssignDefaultActions();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Could not assign default UI input actions: {exception.Message}");
            }
        }
    }
}
