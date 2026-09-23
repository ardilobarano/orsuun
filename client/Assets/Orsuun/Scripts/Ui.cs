using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// Code-built uGUI for the grey-box. Every element is placed by anchor fractions of its parent
    /// (0..1, y from the bottom), so the layout holds on any portrait resolution.
    /// </summary>
    public static class Ui
    {
        private static Font _font;

        public static Font Font => _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Canvas Canvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent, float xMin, float yMin, float xMax, float yMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image Panel(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, Color color)
        {
            var image = Rect(name, parent, xMin, yMin, xMax, yMax).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text Label(string name, Transform parent, float xMin, float yMin, float xMax, float yMax,
            string content, int size, TextAnchor anchor, Color color)
        {
            var text = Rect(name, parent, xMin, yMin, xMax, yMax).gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = size;
            return text;
        }

        /// <summary>An icon from Resources/Icons (docs/concept icon sheets, sliced to 256 px). Null texture if missing.</summary>
        public static RawImage Icon(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, string icon)
        {
            RectTransform rect = Rect(name, parent, xMin, yMin, xMax, yMax);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>("Icons/" + icon);
            image.raycastTarget = false;
            image.enabled = image.texture != null;
            // Icons are square: keep them square inside whatever box they are given.
            var fit = rect.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            return image;
        }

        public static Button Button(string name, Transform parent, float xMin, float yMin, float xMax, float yMax,
            string label, int size, Color background, Action onClick, out Text labelText)
        {
            Image image = Panel(name, parent, xMin, yMin, xMax, yMax, background);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            labelText = Label("Label", image.transform, 0.04f, 0.06f, 0.96f, 0.94f, label, size, TextAnchor.MiddleCenter, Color.white);
            return button;
        }
    }

    public static class Palette
    {
        public static readonly Color Background = new Color(0.07f, 0.08f, 0.10f);
        public static readonly Color PanelDark = new Color(0.11f, 0.12f, 0.15f);
        public static readonly Color ButtonIdle = new Color(0.22f, 0.25f, 0.32f);
        public static readonly Color ButtonForge = new Color(0.62f, 0.30f, 0.12f);
        public static readonly Color Danger = new Color(0.78f, 0.18f, 0.16f);
        public static readonly Color Safe = new Color(0.18f, 0.48f, 0.32f);
        public static readonly Color Alloy = new Color(0.45f, 0.36f, 0.62f);
        public static readonly Color Good = new Color(0.45f, 0.90f, 0.50f);
        public static readonly Color Warn = new Color(1.00f, 0.70f, 0.25f);
        public static readonly Color Bad = new Color(1.00f, 0.35f, 0.30f);
        public static readonly Color Sorn = new Color(1.00f, 0.85f, 0.40f);
        public static readonly Color Muted = new Color(0.70f, 0.73f, 0.78f);
    }
}
