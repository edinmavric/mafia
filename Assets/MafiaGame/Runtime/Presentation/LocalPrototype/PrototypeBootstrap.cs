using MafiaGame.Application.Matches;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace MafiaGame.Presentation.LocalPrototype
{
    /// <summary>
    /// Composition root for the local prototype scene. Wires the driver, view, and presenter and
    /// starts the flow. The only thing that needs to exist in the scene is one GameObject with this
    /// component; everything else (canvas, event system, UI) is built at runtime.
    /// </summary>
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        public LocalMatchDriver Driver { get; private set; }

        public LocalMatchView View { get; private set; }

        public MatchFlowPresenter Presenter { get; private set; }

        private void Start()
        {
            EnsureEventSystem();

            var viewObject = new GameObject("MatchView");
            viewObject.transform.SetParent(transform, false);
            View = viewObject.AddComponent<LocalMatchView>();

            Driver = new LocalMatchDriver();
            Presenter = new MatchFlowPresenter(Driver, View);
            Presenter.Begin();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));
            InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();

            // The project uses the Input System package; assign its default UI actions so buttons
            // respond. Guarded because this is optional wiring and must not break headless runs.
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
