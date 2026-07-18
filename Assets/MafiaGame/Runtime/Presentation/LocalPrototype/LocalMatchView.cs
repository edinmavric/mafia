using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaGame.Presentation.LocalPrototype
{
    /// <summary>
    /// Thin MonoBehaviour view: builds a placeholder TMP canvas once, then renders whatever
    /// <see cref="ScreenModel"/> the presenter hands it. It holds no game rules. The last rendered
    /// screen is exposed so Play Mode tests can drive the flow.
    /// </summary>
    public sealed class LocalMatchView : MonoBehaviour, IMatchView
    {
        private readonly List<GameObject> _spawnedButtons = new List<GameObject>();
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private RectTransform _buttonsContainer;

        public ScreenModel CurrentScreen { get; private set; }

        private void Awake()
        {
            BuildUi();
        }

        public void Show(ScreenModel screen)
        {
            CurrentScreen = screen;
            _title.text = screen.Title;
            _body.text = screen.Body;

            for (int i = 0; i < _spawnedButtons.Count; i++)
            {
                Destroy(_spawnedButtons[i]);
            }

            _spawnedButtons.Clear();

            foreach (ButtonSpec spec in screen.Buttons)
            {
                Button button = UiFactory.CreateButton(_buttonsContainer, spec.Label);
                ButtonSpec captured = spec;
                button.onClick.AddListener(() => captured.OnClick?.Invoke());
                _spawnedButtons.Add(button.gameObject);
            }
        }

        private void BuildUi()
        {
            Canvas canvas = UiFactory.CreateCanvas("MatchCanvas");
            canvas.transform.SetParent(transform, false);

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(canvas.transform, false);
            Stretch(background.GetComponent<RectTransform>());
            background.GetComponent<Image>().color = new Color(0.09f, 0.09f, 0.12f, 1f);

            _title = UiFactory.CreateText(canvas.transform, "Title", 40f, TextAlignmentOptions.Center);
            Anchor(_title.rectTransform, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.98f));

            _body = UiFactory.CreateText(canvas.transform, "Body", 26f, TextAlignmentOptions.Top);
            Anchor(_body.rectTransform, new Vector2(0.06f, 0.55f), new Vector2(0.94f, 0.80f));

            var buttonsPanel = new GameObject("Buttons", typeof(RectTransform));
            buttonsPanel.transform.SetParent(canvas.transform, false);
            _buttonsContainer = buttonsPanel.GetComponent<RectTransform>();
            Anchor(_buttonsContainer, new Vector2(0.15f, 0.05f), new Vector2(0.85f, 0.53f));

            var layout = buttonsPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperCenter;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
