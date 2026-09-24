using System;
using System.Collections.Generic;
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

        /// <summary>
        /// A heading with a soft drop shadow; carved = the screen-title face (no digits). A carved title at the top of a
        /// screen stands on the crimson ribbon unless ribbon is false.
        /// </summary>
        public static Text Title(string name, Transform parent, float xMin, float yMin, float xMax, float yMax,
            string content, int size, TextAnchor anchor, Color color, bool carved = false, bool? ribbon = null)
        {
            if (ribbon ?? (carved && yMax >= 0.9f))
            {
                float inset = (xMax - xMin) * 0.12f;
                Sliced(name + "Ribbon", parent, xMin + inset, yMin - 0.006f, xMax - inset, yMax + 0.006f, "Ribbon", Color.white).raycastTarget = false;
            }
            Text text = Label(name, parent, xMin, yMin, xMax, yMax, content, size, anchor, color);
            text.font = carved ? CarvedFont : TitleFont;
            text.fontStyle = FontStyle.Bold;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        /// <summary>A content panel (etchings, rows, sockets): a tinted card with a bronze rim and corner diamonds.</summary>
        public static Image Framed(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, Color color)
        {
            Image image = Sliced(name, parent, xMin, yMin, xMax, yMax, "CardFill", color);
            Sliced("Rim", image.transform, 0f, 0f, 1f, 1f, "CardRim", Color.white).raycastTarget = false;
            return image;
        }

        /// <summary>
        /// A full-screen screen background (opaque, so the hunt does not show through) that also fades the screen in
        /// each time its canvas is shown.
        /// </summary>
        public static Image Backdrop(Transform canvas, string scene = null)
        {
            Image back = Sliced("Backdrop", canvas, 0f, 0f, 1f, 1f, "Backdrop", Color.white);
            // A painted scene for the screen (Resources/Scenes, docs/concept/screens): it fills the screen (cropped at
            // the sides on tall phones) under a shade that darkens toward the bottom, where the panels sit.
            Texture2D art = scene == null ? null : Resources.Load<Texture2D>("Scenes/" + scene);
            if (art != null)
            {
                RectTransform box = Rect("Scene", back.transform, 0f, 0f, 1f, 1f);
                RectTransform inner = Rect("Image", box, 0f, 0f, 1f, 1f);
                var raw = inner.gameObject.AddComponent<RawImage>();
                raw.texture = art;
                raw.raycastTarget = false;
                var fit = inner.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = art.width / (float)art.height;
                Panel("Shade", back.transform, 0f, 0f, 1f, 1f, new Color(0.03f, 0.03f, 0.06f, 0.38f)).raycastTarget = false;
                Panel("ShadeLow", back.transform, 0f, 0f, 1f, 0.6f, new Color(0.03f, 0.03f, 0.06f, 0.3f)).raycastTarget = false;
            }
            if (canvas.GetComponent<ScreenFade>() == null) canvas.gameObject.AddComponent<ScreenFade>();
            return back;
        }

        /// <summary>An image from the UI kit (Resources/UI, tools/ui/make_ui_kit.py), nine-sliced where the kit has borders.</summary>
        public static Image Sliced(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, string sprite, Color color)
        {
            var image = Rect(name, parent, xMin, yMin, xMax, yMax).gameObject.AddComponent<Image>();
            Kit.Apply(image, sprite);
            image.color = color;
            return image;
        }

        /// <summary>A bar: bronze-rimmed trough with a glossy fill inside it; move the fill's anchorMax.x from 0 to 1.</summary>
        public static RectTransform Bar(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, Color fill, out Image fillImage)
        {
            Image frame = Sliced(name, parent, xMin, yMin, xMax, yMax, "BarFrame", Color.white);
            RectTransform inner = Rect("Inner", frame.transform, 0.012f, 0.2f, 0.988f, 0.8f);
            fillImage = Sliced("Fill", inner, 0f, 0f, 1f, 1f, "BarFill", fill);
            fillImage.raycastTarget = false;
            return fillImage.rectTransform;
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

        /// <summary>Points an Icon at another Resources/Icons texture (loads only when it changes).</summary>
        public static void SetIcon(RawImage image, string icon)
        {
            if (image.texture != null && image.texture.name == icon) return;
            image.texture = Resources.Load<Texture2D>("Icons/" + icon);
            image.enabled = image.texture != null;
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

        /// <summary>A button with an icon on its left and the label beside it (FORGE, GEAR, SHARDS, PUSH).</summary>
        public static Button IconButton(string name, Transform parent, float xMin, float yMin, float xMax, float yMax,
            string label, int size, Color background, string icon, Action onClick, out Text labelText)
        {
            Button button = Button(name, parent, xMin, yMin, xMax, yMax, label, size, background, onClick, out labelText);
            RectTransform box = Rect("IconBox", button.transform, 0.04f, 0.1f, 0.34f, 0.9f);
            Icon("Icon", box, 0f, 0f, 1f, 1f, icon);
            RectTransform text = labelText.rectTransform;
            text.anchorMin = new Vector2(0.3f, text.anchorMin.y);
            return button;
        }

        /// <summary>
        /// A square-ish action tile from the hunt mockup (FORGE, GEAR, SHARDS, PUSH): the tinted lacquer plate under its
        /// gold frame, a big painted icon on top and the label under it.
        /// </summary>
        public static Button Tile(string name, Transform parent, float xMin, float yMin, float xMax, float yMax,
            string label, int size, Color background, string icon, Action onClick, out Text labelText)
        {
            Button button = Button(name, parent, xMin, yMin, xMax, yMax, label, size, background, onClick, out labelText);
            RectTransform box = Rect("IconBox", button.transform, 0.2f, 0.36f, 0.8f, 0.9f);
            Icon("Icon", box, 0f, 0f, 1f, 1f, icon);
            RectTransform text = labelText.rectTransform;
            text.anchorMin = new Vector2(0.08f, 0.1f);
            text.anchorMax = new Vector2(0.92f, 0.4f);
            return button;
        }

        /// <summary>A dark tile under the gold slot frame: the bottom bar's buttons and other framed cells stand on it.</summary>
        public static Image SlotTile(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, Color fill)
        {
            Image tile = Sliced(name, parent, xMin, yMin, xMax, yMax, "CardFill", fill);
            tile.raycastTarget = false;
            Sliced("Rim", tile.transform, 0f, 0f, 1f, 1f, "SlotRim", Color.white).raycastTarget = false;
            return tile;
        }

        /// <summary>
        /// A round button in its own box (kept round by a fitter): tinted plate, an icon, a dark cooldown sweep (fill it
        /// 1 to 0) and a bronze ring. The label sits in the middle for cooldown seconds.
        /// </summary>
        public static Button RoundButton(string name, Transform parent, float xMin, float yMin, float xMax, float yMax,
            string icon, Color background, Action onClick, out Image sweep, out Text label)
        {
            RectTransform box = Rect(name, parent, xMin, yMin, xMax, yMax);
            RectTransform round = Rect("Round", box, 0f, 0f, 1f, 1f);
            var fit = round.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            // The painted ring is slim: the plate, the art and the sweep sit inside it.
            var hit = round.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            Image plate = Sliced("Plate", round, 0.08f, 0.08f, 0.92f, 0.92f, "RoundFill", background);
            plate.raycastTarget = false;
            var art = Rect("Art", round, 0.12f, 0.12f, 0.88f, 0.88f).gameObject.AddComponent<RawImage>();
            art.texture = Resources.Load<Texture2D>("Icons/" + icon);
            art.raycastTarget = false;
            art.enabled = art.texture != null;
            sweep = Rect("Sweep", round, 0.12f, 0.12f, 0.88f, 0.88f).gameObject.AddComponent<Image>();
            Kit.Apply(sweep, "Disc");
            sweep.color = new Color(0.02f, 0.02f, 0.05f, 0.72f);
            sweep.type = Image.Type.Filled;
            sweep.fillMethod = Image.FillMethod.Radial360;
            sweep.fillOrigin = (int)Image.Origin360.Top;
            sweep.fillClockwise = false;
            sweep.fillAmount = 0f;
            sweep.raycastTarget = false;
            var ringImage = Rect("Ring", round, 0f, 0f, 1f, 1f).gameObject.AddComponent<Image>();
            Kit.Apply(ringImage, "RoundRim");
            ringImage.raycastTarget = false;
            label = Title("Label", round, 0.1f, 0.1f, 0.9f, 0.9f, "", 60, TextAnchor.MiddleCenter, Palette.Parchment);
            round.gameObject.AddComponent<Press>();
            var button = round.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>A section heading on the crimson plate with gold flourishes (from the screen mockups).</summary>
        public static Text Section(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, string text, int size)
        {
            Sliced(name + "Plate", parent, xMin, yMin, xMax, yMax, "Section", Color.white).raycastTarget = false;
            float inset = (xMax - xMin) * 0.24f;
            return Title(name, parent, xMin + inset, yMin, xMax - inset, yMax, text, size, TextAnchor.MiddleCenter, Palette.Sorn);
        }

        /// <summary>A picture from Resources (Thumbs, card art) in its own box, cropped to fill it, under a gold slot frame.</summary>
        public static RawImage Picture(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, string texture, bool frame = true)
        {
            RectTransform box = Rect(name, parent, xMin, yMin, xMax, yMax);
            box.gameObject.AddComponent<RectMask2D>();
            RectTransform inner = Rect("Image", box, 0f, 0f, 1f, 1f);
            var raw = inner.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var fit = inner.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            SetPicture(raw, texture);
            if (frame) Sliced("Frame", box, 0f, 0f, 1f, 1f, "SlotRim", Color.white).raycastTarget = false;
            return raw;
        }

        public static void SetPicture(RawImage raw, string texture)
        {
            Texture2D art = texture == null ? null : Resources.Load<Texture2D>(texture);
            if (raw.texture == art) return;
            raw.texture = art;
            raw.enabled = art != null;
            if (art != null) raw.GetComponent<AspectRatioFitter>().aspectRatio = art.width / (float)art.height;
        }

        /// <summary>A bottom-bar button: a painted icon over a small label, and a red badge (hidden until needed).</summary>
        public static Button NavButton(string name, Transform parent, float xMin, float yMin, float xMax, float yMax,
            string icon, string label, Action onClick, out Text labelText, out Image badge)
        {
            RectTransform box = Rect(name, parent, xMin, yMin, xMax, yMax);
            var hit = box.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            RectTransform iconBox = Rect("IconBox", box, 0.1f, 0.34f, 0.9f, 1f);
            Icon("Icon", iconBox, 0f, 0f, 1f, 1f, "Nav" + icon);
            labelText = Title("Label", box, 0f, 0f, 1f, 0.36f, label, 20, TextAnchor.MiddleCenter, Palette.Parchment);
            // The fitter keeps the badge round inside its own small box (a fitter fits its parent).
            RectTransform badgeBox = Rect("BadgeBox", box, 0.64f, 0.7f, 0.92f, 1f);
            badge = Sliced("Badge", badgeBox, 0f, 0f, 1f, 1f, "Badge", Color.white);
            var badgeFit = badge.gameObject.AddComponent<AspectRatioFitter>();
            badgeFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            badgeFit.aspectRatio = 1f;
            badge.raycastTarget = false;
            badge.gameObject.SetActive(false);
            box.gameObject.AddComponent<Press>();
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            return button;
        }

        public static Button Button(string name, Transform parent, float xMin, float yMin, float xMax, float yMax,
            string label, int size, Color background, Action onClick, out Text labelText)
        {
            // Direction B: a lacquered plate (tinted) under an ornate gold frame, carved capitals on top. The way back to
            // the hunt or the Forge is the long indigo plate with arrow tips instead (from the screen mockups).
            bool back = label.StartsWith("BACK TO");
            Image image = back
                ? Sliced(name, parent, xMin, yMin, xMax, yMax, "Back", Color.white)
                : Sliced(name, parent, xMin, yMin, xMax, yMax, "ButtonFill", background);
            if (!back) Sliced("Rim", image.transform, 0f, 0f, 1f, 1f, "ButtonRim", Color.white).raycastTarget = false;
            image.gameObject.AddComponent<Press>();
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.05f, 1f);
            colors.pressedColor = new Color(0.78f, 0.74f, 0.70f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.9f);
            colors.colorMultiplier = 1.1f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            labelText = Label("Label", image.transform, back ? 0.14f : 0.1f, 0.14f, back ? 0.86f : 0.9f, 0.86f, label, size, TextAnchor.MiddleCenter, Palette.Parchment);
            labelText.font = TitleFont;
            labelText.fontStyle = FontStyle.Bold;
            var shadow = labelText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return button;
        }
    }

    /// <summary>
    /// The UI kit's sprites (Resources/UI, drawn by tools/ui/make_ui_kit.py at 2x): loaded once, clamped, nine-sliced by
    /// the borders below (texture pixels: left, bottom, right, top).
    /// </summary>
    public static class Kit
    {
        private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            ["ButtonFill"] = new Vector4(44, 44, 44, 44),
            ["ButtonRim"] = new Vector4(44, 44, 44, 44),
            ["CardFill"] = new Vector4(28, 28, 28, 28),
            ["CardRim"] = new Vector4(28, 28, 28, 28),
            ["BarFrame"] = new Vector4(30, 26, 30, 26),
            ["BarFill"] = new Vector4(20, 20, 20, 20),
            ["Pill"] = new Vector4(60, 30, 60, 30),
            ["Ribbon"] = new Vector4(190, 40, 190, 40),
            ["NavBar"] = new Vector4(8, 24, 8, 24),
            ["TopBar"] = new Vector4(8, 24, 8, 24),
            ["Rule"] = new Vector4(200, 0, 200, 0),
        };

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static bool _paintedBordersRead;

        /// <summary>
        /// The painted pieces (tools/ui/cut_ai_kit.py) carry their own nine-slice borders in Resources/UI/Borders.json,
        /// {"Name": [left, bottom, right, top]}; they override the procedural kit's.
        /// </summary>
        private static void ReadPaintedBorders()
        {
            if (_paintedBordersRead) return;
            _paintedBordersRead = true;
            var json = Resources.Load<TextAsset>("UI/Borders");
            if (json == null) return;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(json.text,
                         "\"(\\w+)\"\\s*:\\s*\\[\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*\\]"))
                Borders[m.Groups[1].Value] = new Vector4(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value));
        }

        public static Sprite Get(string name)
        {
            ReadPaintedBorders();
            if (Cache.TryGetValue(name, out Sprite sprite)) return sprite;
            var texture = Resources.Load<Texture2D>("UI/" + name);
            if (texture == null) return Cache[name] = null;
            texture.wrapMode = TextureWrapMode.Clamp;
            Borders.TryGetValue(name, out Vector4 border);
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            return Cache[name] = sprite;
        }

        public static void Apply(Image image, string name)
        {
            image.sprite = Get(name);
            bool sliced = Borders.ContainsKey(name) && Borders[name] != Vector4.zero;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            // The kit is drawn at 2x: borders show at half their pixel size in canvas units.
            image.pixelsPerUnitMultiplier = 2f;
        }
    }

    /// <summary>Presses sink a little under the finger and spring back.</summary>
    public sealed class Press : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private float _target = 1f;

        public void OnPointerDown(PointerEventData eventData) => _target = 0.94f;
        public void OnPointerUp(PointerEventData eventData) => _target = 1f;
        public void OnPointerExit(PointerEventData eventData) => _target = 1f;

        private void OnDisable()
        {
            _target = 1f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float s = transform.localScale.x;
            if (Mathf.Abs(s - _target) < 0.001f) return;
            s = Mathf.MoveTowards(s, _target, Time.unscaledDeltaTime * 2.5f);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Fades a screen in each time its canvas is shown.</summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        private const float Seconds = 0.14f;
        private CanvasGroup _group;
        private float _age;

        private void OnEnable()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            _age = 0f;
            _group.alpha = 0f;
        }

        private void Update()
        {
            if (_age >= Seconds) return;
            _age += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(_age / Seconds);
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
