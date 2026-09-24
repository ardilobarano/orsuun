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
        private static Font _titleFont;
        private static Font _carvedFont;
        private static bool _titleLoaded;

        public static Font Font => _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Philosopher Bold (SIL OFL, Resources/Fonts) for buttons and headings: classic, with a clear 1.</summary>
        public static Font TitleFont
        {
            get
            {
                LoadFonts();
                return _titleFont != null ? _titleFont : Font;
            }
        }

        /// <summary>Cinzel (SIL OFL) for screen titles only: carved capitals, but its 1 reads as a Roman I.</summary>
        public static Font CarvedFont
        {
            get
            {
                LoadFonts();
                return _carvedFont != null ? _carvedFont : TitleFont;
            }
        }

        private static void LoadFonts()
        {
            if (_titleLoaded) return;
            _titleLoaded = true;
            _titleFont = Resources.Load<Font>("Fonts/Philosopher-Bold");
            _carvedFont = Resources.Load<Font>("Fonts/Cinzel");
        }

        /// <summary>A heading with a soft drop shadow; carved = the screen-title face (no digits).</summary>
        public static Text Title(string name, Transform parent, float xMin, float yMin, float xMax, float yMax,
            string content, int size, TextAnchor anchor, Color color, bool carved = false)
        {
            Text text = Label(name, parent, xMin, yMin, xMax, yMax, content, size, anchor, color);
            text.font = carved ? CarvedFont : TitleFont;
            text.fontStyle = FontStyle.Bold;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        /// <summary>A content panel with a thin bronze frame (etchings, rows, sockets).</summary>
        public static Image Framed(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, Color color)
        {
            Image image = Panel(name, parent, xMin, yMin, xMax, yMax, color);
            var rim = image.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(Palette.Trim.r, Palette.Trim.g, Palette.Trim.b, 0.55f);
            rim.effectDistance = new Vector2(1.5f, -1.5f);
            return image;
        }

        /// <summary>A thin bronze rule, for panel edges.</summary>
        public static Image Trim(string name, Transform parent, float xMin, float yMin, float xMax, float yMax) =>
            Panel(name, parent, xMin, yMin, xMax, yMax, Palette.Trim);

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

        /// <summary>A one-line text field (the phone's keyboard opens on tap) with a muted placeholder.</summary>
        public static InputField Input(string name, Transform parent, float xMin, float yMin, float xMax, float yMax,
            string placeholder, int size, int limit)
        {
            Image back = Framed(name, parent, xMin, yMin, xMax, yMax, new Color(0.05f, 0.05f, 0.09f));
            var field = back.gameObject.AddComponent<InputField>();
            Text text = Label("Text", back.transform, 0.04f, 0.05f, 0.96f, 0.95f, "", size, TextAnchor.MiddleLeft, Palette.Parchment);
            text.resizeTextForBestFit = false;
            text.supportRichText = false;
            Text hint = Label("Placeholder", back.transform, 0.04f, 0.05f, 0.96f, 0.95f, placeholder, size, TextAnchor.MiddleLeft, Palette.Muted);
            hint.fontStyle = FontStyle.Italic;
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = limit;
            field.targetGraphic = back;
            return field;
        }

        /// <summary>
        /// A vertical scroll list: rows added under content stack from the top and size to their text (a
        /// VerticalLayoutGroup plus ContentSizeFitter); the viewport clips them.
        /// </summary>
        public static ScrollRect Scroll(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, out RectTransform content)
        {
            Image back = Framed(name, parent, xMin, yMin, xMax, yMax, new Color(0.05f, 0.05f, 0.09f, 0.92f));
            var scroll = back.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Rect("Viewport", back.transform, 0.005f, 0.005f, 0.995f, 0.995f);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 8f;
            layout.padding = new RectOffset(14, 14, 10, 10);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        /// <summary>A wrapping rich-text row for a Scroll list, tappable (the text is the button's graphic).</summary>
        public static Text ListRow(string name, Transform content, int size, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(content, false);
            var text = go.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.color = Palette.Parchment;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = text;
            button.onClick.AddListener(() => onClick());
            return text;
        }

        public static Button Button(string name, Transform parent, float xMin, float yMin, float xMax, float yMax,
            string label, int size, Color background, Action onClick, out Text labelText)
        {
            // Direction B: a lacquered plate with a bronze rim and a darker lower lip, carved capitals on top.
            Image image = Panel(name, parent, xMin, yMin, xMax, yMax, background);
            var rim = image.gameObject.AddComponent<Outline>();
            rim.effectColor = Palette.Trim;
            rim.effectDistance = new Vector2(2f, -2f);
            Image lip = Panel("Lip", image.transform, 0f, 0f, 1f, 0.12f, new Color(0f, 0f, 0f, 0.28f));
            lip.raycastTarget = false;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.05f, 1f);
            colors.pressedColor = new Color(0.78f, 0.74f, 0.70f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.9f);
            colors.colorMultiplier = 1.1f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            labelText = Label("Label", image.transform, 0.04f, 0.10f, 0.96f, 0.94f, label, size, TextAnchor.MiddleCenter, Palette.Parchment);
            labelText.font = TitleFont;
            labelText.fontStyle = FontStyle.Bold;
            var shadow = labelText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return button;
        }
    }

    /// <summary>
    /// Direction B (modernized classic, owner's pick 23 Sep 2026): dusk-indigo panels under an amber steppe, bronze
    /// rims, ember for the Forge, crimson lacquer for risk, jade for safety, amethyst for the Khan's Alloy.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Background = new Color(0.06f, 0.06f, 0.10f);
        public static readonly Color PanelDark = new Color(0.10f, 0.10f, 0.16f);
        /// <summary>Full-screen dim behind the Forge, Gear, Zones and Shards screens.</summary>
        public static readonly Color Dim = new Color(0.04f, 0.04f, 0.08f, 0.95f);
        public static readonly Color ButtonIdle = new Color(0.19f, 0.20f, 0.30f);
        public static readonly Color ButtonForge = new Color(0.70f, 0.32f, 0.10f);
        public static readonly Color Danger = new Color(0.62f, 0.14f, 0.13f);
        public static readonly Color Safe = new Color(0.15f, 0.42f, 0.34f);
        public static readonly Color Alloy = new Color(0.40f, 0.29f, 0.56f);
        public static readonly Color DevGrey = new Color(0.17f, 0.17f, 0.20f);
        public static readonly Color Trim = new Color(0.72f, 0.55f, 0.30f);
        public static readonly Color Parchment = new Color(0.96f, 0.91f, 0.80f);
        public static readonly Color Good = new Color(0.52f, 0.88f, 0.52f);
        public static readonly Color Warn = new Color(1.00f, 0.72f, 0.28f);
        public static readonly Color Bad = new Color(1.00f, 0.38f, 0.30f);
        public static readonly Color Sorn = new Color(1.00f, 0.84f, 0.42f);
        public static readonly Color Muted = new Color(0.76f, 0.73f, 0.68f);
    }
}
