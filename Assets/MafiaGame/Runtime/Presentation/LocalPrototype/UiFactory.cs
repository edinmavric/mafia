using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MafiaGame.Presentation.LocalPrototype
{
    /// <summary>
    /// Minimal helpers to build placeholder TMP UI from code. Deliberately plain: this is a
    /// developer prototype, not final art. All visuals are throwaway.
    /// </summary>
    internal static class UiFactory
    {
        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            return canvas;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            return text;
        }

        public static Button CreateButton(Transform parent, string label)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            go.GetComponent<Image>().color = new Color(0.20f, 0.20f, 0.26f, 1f);
            var button = go.AddComponent<Button>();

            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 48f;
            layout.preferredHeight = 56f;

            TextMeshProUGUI text = CreateText(go.transform, "Label", 24f, TextAlignmentOptions.Center);
            RectTransform rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12f, 4f);
            rt.offsetMax = new Vector2(-12f, -4f);
            text.text = label;

            return button;
        }
    }
}
