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
        private GameObject _root;
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

        /// <summary>
        /// Shows or hides the whole lobby UI. The match view hides it once a match starts so the two
        /// placeholder canvases do not overlap; the presenter keeps working either way.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.SetActive(visible);
            }
        }

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
            _root = canvas.gameObject;

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
            Transform content = CreatePanel(parent, new Vector2(0.15f, 0.14f), new Vector2(0.85f, 0.82f));

            Button hostButton = UiFactory.CreateButton(content, "Napravi igru (Host)");
            hostButton.onClick.AddListener(() => HostClicked?.Invoke());

            _codeInput = UiFactory.CreateInputField(content, "Unesi kod…");
            AddHeight(_codeInput.gameObject, 56f);

            Button joinButton = UiFactory.CreateButton(content, "Pridruži se kodom");
            joinButton.onClick.AddListener(() => JoinClicked?.Invoke());

            // Toggle the whole scroll view, not just its content.
            return content.parent.gameObject;
        }

        private GameObject BuildConnectedPanel(Transform parent)
        {
            Transform content = CreatePanel(parent, new Vector2(0.1f, 0.14f), new Vector2(0.9f, 0.82f));

            _codeDisplay = UiFactory.CreateText(content, "Code", 26f, TextAlignmentOptions.Center);
            AddHeight(_codeDisplay.gameObject, 50f);

            _playersText = UiFactory.CreateText(content, "Players", 24f, TextAlignmentOptions.TopLeft);
            AddHeight(_playersText.gameObject, 140f);

            Button leaveButton = UiFactory.CreateButton(content, "Napusti");
            leaveButton.onClick.AddListener(() => LeaveClicked?.Invoke());

            return content.parent.gameObject;
        }

        /// <summary>
        /// A scrolling column of controls. Scrolling matters even here: on a short window the fixed
        /// panel used to push the join input and the buttons past the bottom edge, where they could
        /// not be clicked or typed into.
        /// </summary>
        private static Transform CreatePanel(Transform parent, Vector2 min, Vector2 max) =>
            UiFactory.CreateScrollColumn(parent, "Panel", min, max);

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
