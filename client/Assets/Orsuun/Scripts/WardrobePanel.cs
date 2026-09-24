using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// WARDROBE (owner, 25 Sep 2026; docs/concept/screens/mockup-wardrobe.jpg): the skin, mount and companion worn, each
    /// with its stat and the time it has left, and the pieces held, to wear in their place. Opened from GEAR and from
    /// the Caravan. Pieces run out on their own; the server stops counting them at the same moment.
    /// </summary>
    public sealed class WardrobePanel : MonoBehaviour
    {
        private const int Rows = 7;

        private sealed class Slot
        {
            public RawImage Picture;
            public Text Name;
            public Text Perk;
            public Text Left;
            public Button Off;
        }

        private sealed class Row
        {
            public GameObject Root;
            public RawImage Picture;
            public Text Name;
            public Text Left;
            public Button Wear;
            public Text WearLabel;
            public string Id;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private readonly Slot[] _slots = new Slot[3];
        private readonly Row[] _rows = new Row[Rows];
        private Text _empty;
        private Text _message;
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("WardrobeCanvas", 10).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Gear");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "WARDROBE", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);

            Ui.Section("WornHead", canvas, 0.2f, 0.872f, 0.8f, 0.91f, "WORN", 26);
            string[] kinds = { "SKIN", "MOUNT", "COMPANION" };
            for (int i = 0; i < 3; i++)
            {
                var kind = (WardrobeKind)i;
                float x0 = 0.03f + i * 0.32f;
                Image back = Ui.Framed("Slot" + i, canvas, x0, 0.53f, x0 + 0.3f, 0.865f, new Color(0.06f, 0.06f, 0.12f, 0.94f));
                Transform t = back.transform;
                var s = new Slot();
                Ui.Title("Kind", t, 0.05f, 0.9f, 0.95f, 0.98f, kinds[i], 20, TextAnchor.MiddleCenter, Palette.Sorn);
                s.Picture = Ui.Picture("Picture", t, 0.07f, 0.44f, 0.93f, 0.89f, null);
                s.Name = Ui.Title("Name", t, 0.03f, 0.34f, 0.97f, 0.44f, "", 20, TextAnchor.MiddleCenter, Palette.Parchment);
                s.Perk = Ui.Label("Perk", t, 0.03f, 0.26f, 0.97f, 0.34f, "", 18, TextAnchor.MiddleCenter, Palette.Good);
                s.Left = Ui.Label("Left", t, 0.03f, 0.18f, 0.97f, 0.26f, "", 18, TextAnchor.MiddleCenter, Palette.Sorn);
                s.Off = Ui.Button("Off", t, 0.12f, 0.03f, 0.88f, 0.16f, "TAKE OFF", 18, Palette.ButtonIdle, () => Wear("", kind), out _);
                _slots[i] = s;
            }

            Ui.Section("HeldHead", canvas, 0.2f, 0.48f, 0.8f, 0.518f, "HELD", 26);
            Ui.Framed("HeldBack", canvas, 0.03f, 0.105f, 0.97f, 0.475f, new Color(0.06f, 0.06f, 0.12f, 0.9f));
            for (int i = 0; i < Rows; i++)
            {
                int index = i;
                float y1 = 0.465f - i * 0.051f;
                var r = new Row();
                r.Root = Ui.Rect("Row" + i, canvas, 0.05f, y1 - 0.048f, 0.95f, y1).gameObject;
                Transform t = r.Root.transform;
                r.Picture = Ui.Picture("Picture", t, 0f, 0.04f, 0.1f, 0.96f, null);
                r.Name = Ui.Title("Name", t, 0.13f, 0.45f, 0.66f, 1f, "", 22, TextAnchor.MiddleLeft, Palette.Parchment);
                r.Left = Ui.Label("Left", t, 0.13f, 0f, 0.66f, 0.5f, "", 18, TextAnchor.MiddleLeft, Palette.Muted);
                r.Left.supportRichText = true;
                r.Wear = Ui.Button("Wear", t, 0.7f, 0.08f, 1f, 0.92f, "WEAR", 20, Palette.ButtonForge, () => Wear(_rows[index].Id, WardrobeKind.Skin), out r.WearLabel);
                _rows[i] = r;
            }
            _empty = Ui.Label("Empty", canvas, 0.08f, 0.2f, 0.92f, 0.4f,
                "Nothing held yet. The Caravan sells skins, mounts and companions, and bosses from Gorak Pass on drop them.", 24, TextAnchor.MiddleCenter, Palette.Muted);

            _message = Ui.Label("Message", canvas, 0.05f, 0.072f, 0.95f, 0.1f, "", 22, TextAnchor.MiddleCenter, Palette.Warn);
            Ui.Button("Caravan", canvas, 0.03f, 0.015f, 0.47f, 0.068f, "TO THE CARAVAN", 26, Palette.Danger, () => { Close(); _root.Caravan.Open(); }, out _);
            Ui.Button("Close", canvas, 0.5f, 0.015f, 0.97f, 0.068f, "BACK TO THE HUNT", 24, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _message.text = _root.Server.Online ? "" : "The wardrobe needs the server.";
            _canvas.SetActive(true);
        }

        public void Close() => _canvas.SetActive(false);

        /// <summary>Time left as the screens show it: "5d 14h", "23h 10m", "9m".</summary>
        public static string Left(long seconds)
        {
            if (seconds <= 0) return "0m";
            long d = seconds / 86400, h = seconds % 86400 / 3600, m = seconds % 3600 / 60;
            return d > 0 ? $"{d}d {h}h" : h > 0 ? $"{h}h {m}m" : $"{Mathf.Max(1, (int)m)}m";
        }

        private void Wear(string pieceId, WardrobeKind kind)
        {
            if (_busy || !_root.Server.Online) return;
            _busy = true;
            StartCoroutine(_root.Server.Wear(pieceId, kind, error =>
            {
                _busy = false;
                _message.text = error ?? "";
            }));
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            var server = _root.Server;
            for (int i = 0; i < 3; i++)
            {
                var kind = (WardrobeKind)i;
                Slot s = _slots[i];
                WardrobeDef def = Wardrobe.Find(server.WornId(kind));
                long left = def == null ? 0 : server.SecondsLeft(def.Id);
                if (left <= 0) def = null;
                Ui.SetPicture(s.Picture, def == null ? null : "Thumbs/Caravan/" + def.Id);
                s.Name.text = def?.Name ?? "none";
                s.Name.color = def == null ? Palette.Muted : CaravanPanel.TierColor(def.Tier);
                s.Perk.text = def?.PerkText ?? "";
                s.Left.text = def == null ? "" : Left(left);
                s.Left.color = left > 0 && left < 86400 ? Palette.Warn : Palette.Sorn;
                s.Off.gameObject.SetActive(def != null);
                s.Off.interactable = !_busy;
            }

            int n = 0;
            if (server.Wardrobe?.pieces != null)
                foreach (Net.ServerLink.WardrobePieceDto p in server.Wardrobe.pieces)
                {
                    WardrobeDef def = Wardrobe.Find(p.id);
                    long left = server.SecondsLeft(p.id);
                    if (def == null || left <= 0 || n >= Rows) continue;
                    Row r = _rows[n++];
                    r.Id = def.Id;
                    Ui.SetPicture(r.Picture, "Thumbs/Caravan/" + def.Id);
                    r.Name.text = def.Name;
                    r.Name.color = CaravanPanel.TierColor(def.Tier);
                    r.Left.text = Wardrobe.KindName(def.Kind) + "  ·  " + ConfirmDialog.Tint(def.PerkText, Palette.Good) + "  ·  " + Left(left);
                    bool worn = server.WornId(def.Kind) == def.Id;
                    r.WearLabel.text = worn ? "WORN" : "WEAR";
                    r.Wear.interactable = !worn && !_busy;
                }
            for (int i = 0; i < Rows; i++) _rows[i].Root.SetActive(i < n);
            _empty.gameObject.SetActive(n == 0);
        }
    }
}
