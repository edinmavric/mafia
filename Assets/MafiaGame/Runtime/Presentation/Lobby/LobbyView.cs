using System;
using System.Collections.Generic;
using System.Text;
using MafiaGame.Presentation.LocalPrototype;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaGame.Presentation.Lobby
{
    /// <summary>
    /// Thin MonoBehaviour lobby view. Builds a placeholder TMP UI with a host button, a join-code
    /// input + join button, and a connected panel showing the code and player list. Holds no rules
    /// and no networking; it raises intents and renders what the presenter tells it.
    /// </summary>
    public sealed class LobbyView : MonoBehaviour, ILobbyView
    {
        private TextMeshProUGUI _status;
        private GameObject _disconnectedPanel;
        private GameObject _connectedPanel;
        private TMP_InputField _codeInput;
        private TextMeshProUGUI _codeDisplay;
        private TextMeshProUGUI _playersText;

        public string JoinCodeInput => _codeInput != null ? _codeInput.text : string.Empty;

        public event Action HostClicked;
        public event Action JoinClicked;
        public event Action LeaveClicked;

        private void Awake() => BuildUi();

        public void ShowStatus(string message)
        {
            if (_status != null)
            {
                _status.text = message;
            }
        }

        public void ShowDisconnected()
        {
            _disconnectedPanel.SetActive(true);
            _connectedPanel.SetActive(false);
        }

        public void ShowConnected(string joinCode, bool isHost, IReadOnlyList<string> playerIds)
        {
            _disconnectedPanel.SetActive(false);
            _connectedPanel.SetActive(true);
            _codeDisplay.text = (isHost ? "Kod (podeli sa igračima): " : "Povezan, kod: ") + joinCode;

            var builder = new StringBuilder();
            builder.AppendLine($"Igrači ({playerIds.Count}):");
            foreach (string id in playerIds)
            {
                builder.AppendLine("• " + Shorten(id));
            }

            _playersText.text = builder.ToString();
        }

        private static string Shorten(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return "?";
            }

            return id.Length <= 8 ? id : id.Substring(0, 8);
        }

        private void BuildUi()
        {
            Canvas canvas = UiFactory.CreateCanvas("LobbyCanvas");
            canvas.transform.SetParent(transform, false);

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(canvas.transform, false);
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.09f, 0.09f, 0.12f, 1f);

            _status = UiFactory.CreateText(canvas.transform, "Status", 30f, TextAlignmentOptions.Center);
            Anchor(_status.rectTransform, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.98f));
            _status.text = "MafiaGame — lobi (DEV)";

            _disconnectedPanel = BuildDisconnectedPanel(canvas.transform);
            _connectedPanel = BuildConnectedPanel(canvas.transform);
            _connectedPanel.SetActive(false);
        }

        private GameObject BuildDisconnectedPanel(Transform parent)
        {
            GameObject panel = CreatePanel(parent, new Vector2(0.2f, 0.30f), new Vector2(0.8f, 0.82f));

            Button hostButton = UiFactory.CreateButton(panel.transform, "Napravi igru (Host)");
            hostButton.onClick.AddListener(() => HostClicked?.Invoke());

            _codeInput = UiFactory.CreateInputField(panel.transform, "Unesi kod…");
            AddHeight(_codeInput.gameObject, 56f);

            Button joinButton = UiFactory.CreateButton(panel.transform, "Pridruži se kodom");
            joinButton.onClick.AddListener(() => JoinClicked?.Invoke());

            return panel;
        }

        private GameObject BuildConnectedPanel(Transform parent)
        {
            GameObject panel = CreatePanel(parent, new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.82f));

            _codeDisplay = UiFactory.CreateText(panel.transform, "Code", 26f, TextAlignmentOptions.Center);
            AddHeight(_codeDisplay.gameObject, 50f);

            _playersText = UiFactory.CreateText(panel.transform, "Players", 24f, TextAlignmentOptions.TopLeft);
            var flexible = _playersText.gameObject.AddComponent<LayoutElement>();
            flexible.flexibleHeight = 1f;

            Button leaveButton = UiFactory.CreateButton(panel.transform, "Napusti");
            leaveButton.onClick.AddListener(() => LeaveClicked?.Invoke());

            return panel;
        }

        private static GameObject CreatePanel(Transform parent, Vector2 min, Vector2 max)
        {
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            Anchor(panel.GetComponent<RectTransform>(), min, max);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperCenter;
            return panel;
        }

        private static void AddHeight(GameObject go, float height)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = go.AddComponent<LayoutElement>();
            }

            element.minHeight = height;
            element.preferredHeight = height;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
