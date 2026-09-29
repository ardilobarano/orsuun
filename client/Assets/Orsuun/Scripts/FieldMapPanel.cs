using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// A big map's minimap and full map (owner, 28 Sep 2026: "Yes" to a minimap in the hunt that opens the region's full
    /// map). The minimap stands at the lane's left, north up: about 90 metres of the region's painted map
    /// (Content/FieldMaps/&lt;layout&gt;, tools/art/field_map.py) round the hero, his arrow turning as he walks, camps as red
    /// dots (blue where another player hunts). Tap it for the full map: the whole painted region with its camps and places
    /// named, the hero, the other heroes hunting here by name, and a legend. Both show only while the lane is on a big map.
    /// -mapshow opens the full map once the hero is on one (screenshots).
    /// </summary>
    public sealed class FieldMapPanel : MonoBehaviour
    {
        private const float MiniSpan = 90f;
        private static readonly Color CampRed = new Color(0.86f, 0.22f, 0.16f), HeroBlue = new Color(0.35f, 0.62f, 1f);

        private GameRoot _root;
        private GameObject _mini, _canvas;
        private RectTransform _miniSquare, _miniClip, _miniArrow, _fullSquare, _fullArrow;
        private RawImage _miniMap, _fullMap;
        private Text _title;
        private readonly List<RawImage> _miniCamps = new List<RawImage>();
        private readonly List<RawImage> _fullCamps = new List<RawImage>();
        private readonly List<Text> _fullHunters = new List<Text>();
        private readonly (string id, string name)[] _hunterIds = new (string, string)[4];
        private readonly List<GameObject> _fullLabels = new List<GameObject>();
        private FieldMap.Layout _built;
        private bool _showOnce;
        private static Texture2D _arrow, _dot;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _showOnce = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-mapshow") >= 0;

            // The minimap: on the HUD's canvas, under everything else there (cards and calls cover it).
            RectTransform box = Ui.Rect("MiniMap", root.Hud.Canvas, 0.02f, 0.665f, 0.22f, 0.778f);
            box.SetAsFirstSibling();
            _mini = box.gameObject;
            _miniSquare = Square(box, "Square");
            Ui.Panel("Shade", _miniSquare, -0.03f, -0.03f, 1.03f, 1.03f, new Color(0f, 0f, 0f, 0.55f)).raycastTarget = false;
            _miniClip = Ui.Rect("Clip", _miniSquare, 0f, 0f, 1f, 1f);
            _miniClip.gameObject.AddComponent<RectMask2D>();
            _miniMap = Raw("Map", _miniClip, 0f, 0f, 1f, 1f, null);
            _miniMap.raycastTarget = true;
            _miniMap.gameObject.AddComponent<Button>().onClick.AddListener(Open);
            _miniArrow = Raw("You", _miniClip, 0.39f, 0.39f, 0.61f, 0.61f, Arrow()).rectTransform;
            Ui.Sliced("Frame", _miniSquare, -0.05f, -0.05f, 1.05f, 1.05f, "SlotRim", Color.white).raycastTarget = false;
            _mini.SetActive(false);

            // The full map, a screen of its own.
            _canvas = Ui.Canvas("FieldMapCanvas", 12).gameObject;
            Transform canvas = _canvas.transform;
            Ui.Backdrop(canvas);
            _title = Ui.Title("Title", canvas, 0.05f, 0.9f, 0.95f, 0.96f, "", 46, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            RectTransform mapBox = Ui.Rect("MapBox", canvas, 0.03f, 0.22f, 0.97f, 0.885f);
            _fullSquare = Square(mapBox, "Square");
            _fullMap = Raw("Map", _fullSquare, 0f, 0f, 1f, 1f, null);
            Ui.Sliced("Frame", _fullSquare, -0.015f, -0.015f, 1.015f, 1.015f, "SlotRim", Color.white).raycastTarget = false;
            // The legend: you, a camp, another hero.
            Transform legend = Ui.Framed("Legend", canvas, 0.03f, 0.125f, 0.97f, 0.19f, new Color(0.06f, 0.06f, 0.11f, 0.9f)).transform;
            Raw("YouMark", Square(Ui.Rect("YouBox", legend, 0.03f, 0.2f, 0.1f, 0.8f), "In"), 0f, 0f, 1f, 1f, Arrow()).color = Palette.Sorn;
            Ui.Label("You", legend, 0.11f, 0f, 0.3f, 1f, "You", 26, TextAnchor.MiddleLeft, Palette.Parchment);
            Raw("CampMark", Square(Ui.Rect("CampBox", legend, 0.33f, 0.28f, 0.39f, 0.72f), "In"), 0f, 0f, 1f, 1f, Dot()).color = CampRed;
            Ui.Label("Camp", legend, 0.4f, 0f, 0.6f, 1f, "Monster camp", 26, TextAnchor.MiddleLeft, Palette.Parchment);
            Raw("HeroMark", Square(Ui.Rect("HeroBox", legend, 0.63f, 0.28f, 0.69f, 0.72f), "In"), 0f, 0f, 1f, 1f, Dot()).color = HeroBlue;
            Ui.Label("Heroes", legend, 0.7f, 0f, 0.99f, 1f, "Heroes hunting", 26, TextAnchor.MiddleLeft, Palette.Parchment);
            Ui.Button("Close", canvas, 0.3f, 0.03f, 0.7f, 0.1f, "CLOSE", 30, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);
            _canvas.SetActive(false);
        }

        private void Update()
        {
            if (_root == null) return;
            FieldMap map = _root.Lane != null ? _root.Lane.Map : null;
            bool on = map != null && map.Active && map.Current != null && !_root.Server.WaitingForHero;
            if (!on)
            {
                if (_mini.activeSelf) _mini.SetActive(false);
                if (IsOpen) _canvas.SetActive(false);
                return;
            }
            if (_built != map.Current) Build(map.Current);
            if (!_mini.activeSelf) _mini.SetActive(true);
            if (_showOnce) { _showOnce = false; Open(); }

            FieldMap.Layout layout = map.Current;
            Vector2 hero = map.HeroOnMap;
            var hunted = new HashSet<int>();
            foreach ((string _, string _, int camp) in _root.Field.AtCamps) hunted.Add(camp);

            // The minimap: the painted map round the hero, north up.
            float span = MiniSpan / layout.Size;
            Vector2 at = Uv(layout, hero);
            _miniMap.uvRect = new Rect(at.x - span / 2f, at.y - span / 2f, span, span);
            _miniArrow.localRotation = Quaternion.Euler(0f, 0f, -map.HeadingDegrees);
            for (int i = 0; i < _miniCamps.Count; i++)
            {
                Vector2 c = (layout.Camps[i].At - hero) / MiniSpan + new Vector2(0.5f, 0.5f);
                bool inside = c.x > 0.05f && c.x < 0.95f && c.y > 0.05f && c.y < 0.95f;
                _miniCamps[i].gameObject.SetActive(inside);
                if (!inside) continue;
                Place(_miniCamps[i].rectTransform, c);
                _miniCamps[i].color = hunted.Contains(i) ? HeroBlue : CampRed;
            }

            if (!IsOpen) return;
            Place(_fullArrow, at);
            _fullArrow.localRotation = Quaternion.Euler(0f, 0f, -map.HeadingDegrees);
            for (int i = 0; i < _fullCamps.Count; i++) _fullCamps[i].color = hunted.Contains(i) ? HeroBlue : CampRed;
            // Each other hero's name over his camp's (blue) dot.
            int shown = 0;
            foreach ((string id, string name, int camp) in _root.Field.AtCamps)
            {
                if (shown >= _fullHunters.Count) break;
                _hunterIds[shown] = (id, name);
                Text label = _fullHunters[shown++];
                label.gameObject.SetActive(true);
                Place(label.rectTransform, Uv(layout, layout.Camps[camp].At));
                label.rectTransform.anchoredPosition = new Vector2(0f, 32f);
                label.text = name;
            }
            for (int i = shown; i < _fullHunters.Count; i++) _fullHunters[i].gameObject.SetActive(false);
        }

        public void Open()
        {
            FieldMap map = _root.Lane != null ? _root.Lane.Map : null;
            if (map == null || !map.Active || map.Current == null) return;
            if (_built != map.Current) Build(map.Current);
            _canvas.SetActive(true);
        }

        /// <summary>The map's painting, its camps and its named places, made once for each map.</summary>
        private void Build(FieldMap.Layout layout)
        {
            _built = layout;
            var art = Art.Load<Texture2D>("FieldMaps/" + layout.Key);
            _miniMap.texture = art;
            _fullMap.texture = art;
            _miniMap.color = art != null ? Color.white : new Color(0.55f, 0.45f, 0.25f);
            _fullMap.color = _miniMap.color;
            _title.text = layout.Name;
            foreach (RawImage dot in _miniCamps) Destroy(dot.gameObject);
            _miniCamps.Clear();
            foreach (GameObject go in _fullLabels) Destroy(go);
            _fullLabels.Clear();
            _fullCamps.Clear();
            _fullHunters.Clear();
            FieldMap.Spot[] camps = layout.Camps ?? new FieldMap.Spot[0];
            for (int i = 0; i < camps.Length; i++)
            {
                RawImage mini = Raw("Camp" + i, _miniClip, 0f, 0f, 0f, 0f, Dot());
                mini.rectTransform.sizeDelta = new Vector2(18f, 18f);
                mini.color = CampRed;
                _miniCamps.Add(mini);
                RawImage full = Raw("Camp" + i, _fullSquare, 0f, 0f, 0f, 0f, Dot());
                full.rectTransform.sizeDelta = new Vector2(30f, 30f);
                Place(full.rectTransform, Uv(layout, camps[i].At));
                _fullCamps.Add(full);
                _fullLabels.Add(full.gameObject);
                _fullLabels.Add(Tag(layout, camps[i], 26, Palette.Parchment, -34f).gameObject);
            }
            foreach (FieldMap.Spot place in layout.Places ?? new FieldMap.Spot[0])
                _fullLabels.Add(Tag(layout, place, 24, Palette.Sorn, 0f).gameObject);
            // Other heroes: a name each (as written, never translated), set every frame while the map is open.
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                Text name = Ui.Raw(Ui.Label("Hero" + i, _fullSquare, 0f, 0f, 0f, 0f, "", 24, TextAnchor.MiddleCenter, HeroBlue));
                name.rectTransform.sizeDelta = new Vector2(320f, 44f);
                name.fontStyle = FontStyle.Bold;
                // A name on the map opens the hero card (29 Sep 2026: "Tap heroes on the map").
                name.raycastTarget = true;
                name.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    (string id, string who) = _hunterIds[slot];
                    if (id != null) _root.HeroCard.Show(id, who);
                });
                name.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                name.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.5f);
                name.gameObject.SetActive(false);
                _fullHunters.Add(name);
                _fullLabels.Add(name.gameObject);
            }
            // The hero's arrow over everything on the map.
            if (_fullArrow != null) Destroy(_fullArrow.gameObject);
            _fullArrow = Raw("You", _fullSquare, 0f, 0f, 0f, 0f, Arrow()).rectTransform;
            _fullArrow.sizeDelta = new Vector2(46f, 46f);
            _fullArrow.GetComponent<RawImage>().color = Palette.Sorn;
            _miniArrow.GetComponent<RawImage>().color = Palette.Sorn;
            _miniArrow.SetAsLastSibling();
        }

        /// <summary>A place's name on the full map (translated), <paramref name="below"/> pixels under its point.</summary>
        private Text Tag(FieldMap.Layout layout, FieldMap.Spot spot, int size, Color color, float below)
        {
            Vector2 at = Uv(layout, spot.At);
            Text text = Ui.Label("Tag", _fullSquare, at.x, at.y, at.x, at.y, spot.Name, size, TextAnchor.MiddleCenter, color);
            text.rectTransform.sizeDelta = new Vector2(320f, 40f);
            text.rectTransform.anchoredPosition = new Vector2(0f, below);
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;
            text.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            text.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.5f);
            return text;
        }

        private static Vector2 Uv(FieldMap.Layout layout, Vector2 at) => at / layout.Size + new Vector2(0.5f, 0.5f);

        private static void Place(RectTransform rect, Vector2 anchor)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>A square box filling as much of <paramref name="box"/> as fits.</summary>
        private static RectTransform Square(RectTransform box, string name)
        {
            RectTransform square = Ui.Rect(name, box, 0f, 0f, 1f, 1f);
            var fit = square.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            return square;
        }

        private static RawImage Raw(string name, Transform parent, float xMin, float yMin, float xMax, float yMax, Texture texture)
        {
            var raw = Ui.Rect(name, parent, xMin, yMin, xMax, yMax).gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;
            return raw;
        }

        /// <summary>The hero's arrow: a white arrowhead pointing up with a dark rim (tinted where it is used).</summary>
        private static Texture2D Arrow()
        {
            if (_arrow != null) return _arrow;
            const int n = 64;
            _arrow = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "MapArrow" };
            var tip = new Vector2(32f, 60f);
            var left = new Vector2(8f, 6f);
            var right = new Vector2(56f, 6f);
            var notch = new Vector2(32f, 20f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    // How far inside the arrowhead (two triangles meeting at the notch cut from its base): white within,
                    // a dark rim at the edge, soft outside it.
                    float inside = Mathf.Max(Inside(p, tip, left, notch), Inside(p, tip, notch, right));
                    _arrow.SetPixel(x, y, inside > 2.5f ? Color.white : new Color(0.08f, 0.06f, 0.04f, Mathf.Clamp01(inside + 1.5f)));
                }
            _arrow.Apply();
            return _arrow;
        }

        /// <summary>How far inside triangle abc a point is (pixels; negative outside).</summary>
        private static float Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float e1 = Edge(p, a, b), e2 = Edge(p, b, c), e3 = Edge(p, c, a);
            float sign = Edge(c, a, b) > 0f ? 1f : -1f;
            return Mathf.Min(e1 * sign, Mathf.Min(e2 * sign, e3 * sign));
        }

        private static float Edge(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            return (d.x * (p.y - a.y) - d.y * (p.x - a.x)) / Mathf.Max(0.001f, d.magnitude);
        }

        /// <summary>A round marker: a white disc with a dark rim (tinted where it is used).</summary>
        private static Texture2D Dot()
        {
            if (_dot != null) return _dot;
            const int n = 32;
            _dot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "MapDot" };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    Color c = r < 11f ? Color.white : r < 15f ? new Color(0.08f, 0.06f, 0.04f, 1f) : new Color(0f, 0f, 0f, Mathf.Clamp01(16f - r));
                    _dot.SetPixel(x, y, c);
                }
            _dot.Apply();
            return _dot;
        }
    }
}
