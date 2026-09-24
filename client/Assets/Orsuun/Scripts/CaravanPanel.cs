using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// THE CARAVAN (owner, 25 Sep 2026; docs/concept/screens/mockup-caravan-*.jpg): skins, mounts and companions for
    /// 1, 3, 5, 7 or 14 days, bought with Amber (Rules.Wardrobe), and the Amber packs (Rules.Amber, real money only; free on
    /// the playtest server until the stores are connected). A piece is picked in the grid, shown large, its duration
    /// chosen, and bought after a confirm. The Commanders' trophies are shown too, as drops only.
    /// </summary>
    public sealed class CaravanPanel : MonoBehaviour
    {
        private static readonly string[] TabNames = { "SKINS", "MOUNTS", "COMPANIONS", "AMBER" };
        private static readonly string[] TabIcons = { "WardrobeSkin", "WardrobeMount", "WardrobeCompanion", "Amber" };
        private const int AmberTab = 3;
        private const int GridCells = 8;

        private sealed class Cell
        {
            public Image Back;
            public RawImage Picture;
            public Text Name;
            public Text Price;
            public WardrobeDef Def;
        }

        private sealed class PackCell
        {
            public RawImage Picture;
            public Text Amount;
            public Text Bonus;
            public Button Buy;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private GameObject _pieces;
        private GameObject _packs;
        private ConfirmDialog _confirm;
        private Text _balance;
        private Text _message;
        private readonly Button[] _tabs = new Button[4];
        private RawImage _featurePicture;
        private Text _featureName;
        private Text _featureKind;
        private Text _featurePerk;
        private Text _featureBlurb;
        private Text _featureHeld;
        private readonly Button[] _dayButtons = new Button[5];
        private readonly Text[] _dayLabels = new Text[5];
        private Button _buy;
        private Text _buyLabel;
        private readonly Cell[] _cells = new Cell[GridCells];
        private readonly PackCell[] _packCells = new PackCell[6];
        private GameObject _firstBanner;
        private int _tab = 1;
        private int _dayIndex = 3;
        private WardrobeDef _selected;
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("CaravanCanvas", 10).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Caravan");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "THE CARAVAN", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);

            // The Amber balance, with a plus that opens the packs.
            Ui.Sliced("BalancePill", canvas, 0.6f, 0.886f, 0.97f, 0.926f, "Pill", Color.white).raycastTarget = false;
            RectTransform amberBox = Ui.Rect("AmberIconBox", canvas, 0.61f, 0.888f, 0.66f, 0.924f);
            Ui.Icon("AmberIcon", amberBox, 0f, 0f, 1f, 1f, "Amber");
            _balance = Ui.Title("Balance", canvas, 0.665f, 0.886f, 0.88f, 0.926f, "", 28, TextAnchor.MiddleLeft, Palette.Sorn);
            Ui.Button("More", canvas, 0.885f, 0.888f, 0.965f, 0.924f, "+", 30, Palette.ButtonForge, () => SetTab(AmberTab), out _);
            _message = Ui.Label("Message", canvas, 0.03f, 0.886f, 0.59f, 0.926f, "", 20, TextAnchor.MiddleLeft, Palette.Warn);

            for (int i = 0; i < TabNames.Length; i++)
            {
                int tab = i;
                float x0 = 0.03f + i * 0.2375f;
                _tabs[i] = Ui.Tile("Tab" + i, canvas, x0, 0.816f, x0 + 0.225f, 0.88f, TabNames[i], 18, Palette.ButtonIdle, TabIcons[i], () => SetTab(tab), out _);
            }

            _pieces = Ui.Rect("Pieces", canvas, 0f, 0f, 1f, 1f).gameObject;
            BuildPieces(_pieces.transform);
            _packs = Ui.Rect("Packs", canvas, 0f, 0f, 1f, 1f).gameObject;
            BuildPacks(_packs.transform);

            Ui.Button("Wardrobe", canvas, 0.03f, 0.015f, 0.47f, 0.068f, "MY WARDROBE", 26, Palette.Alloy, () => { Close(); _root.Wardrobe.Open(); }, out _);
            Ui.Button("Close", canvas, 0.5f, 0.015f, 0.97f, 0.068f, "BACK TO THE HUNT", 24, Palette.ButtonIdle, Close, out _);

            _confirm = new GameObject("CaravanConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
            _canvas.SetActive(false);
        }

        private void BuildPieces(Transform parent)
        {
            Ui.Framed("FeatureBack", parent, 0.03f, 0.52f, 0.97f, 0.81f, new Color(0.06f, 0.06f, 0.12f, 0.94f));
            _featurePicture = Ui.Picture("Feature", parent, 0.05f, 0.532f, 0.52f, 0.798f, null);
            _featureName = Ui.Title("Name", parent, 0.55f, 0.745f, 0.95f, 0.795f, "", 32, TextAnchor.MiddleLeft, Palette.Sorn);
            _featureKind = Ui.Label("Kind", parent, 0.55f, 0.712f, 0.95f, 0.745f, "", 22, TextAnchor.MiddleLeft, Palette.Muted);
            _featurePerk = Ui.Title("Perk", parent, 0.55f, 0.672f, 0.95f, 0.712f, "", 28, TextAnchor.MiddleLeft, Palette.Good);
            _featureBlurb = Ui.Label("Blurb", parent, 0.55f, 0.578f, 0.95f, 0.672f, "", 21, TextAnchor.UpperLeft, Palette.Parchment);
            _featureBlurb.resizeTextForBestFit = false;
            _featureHeld = Ui.Label("Held", parent, 0.55f, 0.538f, 0.95f, 0.578f, "", 22, TextAnchor.MiddleLeft, Palette.Sorn);
            _featureHeld.supportRichText = true;

            for (int i = 0; i < Wardrobe.Days.Length; i++)
            {
                int index = i;
                float x0 = 0.03f + i * 0.19f;
                _dayButtons[i] = Ui.Button("Days" + i, parent, x0, 0.452f, x0 + 0.18f, 0.51f, "", 20, Palette.ButtonIdle, () => _dayIndex = index, out _dayLabels[i]);
                _dayLabels[i].supportRichText = true;
            }
            _buy = Ui.Button("Buy", parent, 0.15f, 0.388f, 0.85f, 0.444f, "", 28, Palette.Danger, AskBuy, out _buyLabel);
            _buyLabel.supportRichText = true;

            for (int i = 0; i < GridCells; i++)
            {
                int index = i;
                int row = i / 4, col = i % 4;
                float x0 = 0.03f + col * 0.2375f, y1 = 0.378f - row * 0.137f;
                var c = new Cell();
                c.Back = Ui.Framed("Cell" + i, parent, x0, y1 - 0.13f, x0 + 0.225f, y1, new Color(0.07f, 0.07f, 0.13f, 0.95f));
                c.Back.gameObject.AddComponent<Button>().onClick.AddListener(() => Select(_cells[index].Def));
                c.Back.gameObject.AddComponent<Press>();
                c.Picture = Ui.Picture("Picture", c.Back.transform, 0.07f, 0.3f, 0.93f, 0.95f, null);
                c.Name = Ui.Label("Name", c.Back.transform, 0.03f, 0.14f, 0.97f, 0.3f, "", 17, TextAnchor.MiddleCenter, Palette.Parchment);
                c.Price = Ui.Title("Price", c.Back.transform, 0.03f, 0.02f, 0.97f, 0.15f, "", 17, TextAnchor.MiddleCenter, AmberColor);
                _cells[i] = c;
            }
            Ui.Label("Note", parent, 0.04f, 0.074f, 0.96f, 0.104f, "Bosses from Gorak Pass on drop 1-3 day pieces. Getting a piece again adds its days.",
                19, TextAnchor.MiddleCenter, Palette.Muted);
        }

        private void BuildPacks(Transform parent)
        {
            _firstBanner = Ui.Rect("FirstBox", parent, 0f, 0f, 1f, 1f).gameObject;
            Ui.Section("First", _firstBanner.transform, 0.05f, 0.755f, 0.95f, 0.805f, "FIRST PURCHASE: DOUBLE AMBER", 26);
            for (int i = 0; i < Amber.Packs.Length; i++)
            {
                AmberPack pack = Amber.Packs[i];
                int row = i / 2, col = i % 2;
                float x0 = 0.04f + col * 0.47f, y1 = 0.745f - row * 0.212f;
                Image back = Ui.Framed("Pack" + i, parent, x0, y1 - 0.2f, x0 + 0.45f, y1, new Color(0.06f, 0.06f, 0.12f, 0.95f));
                var p = new PackCell();
                p.Picture = Ui.Picture("Picture", back.transform, 0.18f, 0.4f, 0.82f, 0.96f, "Thumbs/Caravan/pack-" + pack.Id, frame: false);
                p.Amount = Ui.Title("Amount", back.transform, 0.04f, 0.27f, 0.96f, 0.42f, pack.Amber.ToString("N0") + " Amber", 26, TextAnchor.MiddleCenter, Palette.Parchment);
                p.Bonus = Ui.Label("Bonus", back.transform, 0.04f, 0.19f, 0.96f, 0.29f, pack.Bonus > 0 ? "+" + pack.Bonus.ToString("N0") + " bonus" : "", 20, TextAnchor.MiddleCenter, Palette.Sorn);
                p.Buy = Ui.Button("Buy", back.transform, 0.2f, 0.03f, 0.8f, 0.19f, pack.PriceText, 24, Palette.Danger, () => AskPack(pack), out _);
                _packCells[i] = p;
            }
            Ui.Label("Note", parent, 0.04f, 0.074f, 0.96f, 0.118f,
                "Amber is bought with real money only and buys skins, mounts and companions.\nPlaytest: packs are free until the App Store and Google Play open.",
                19, TextAnchor.MiddleCenter, Palette.Muted);
        }

        public static readonly Color AmberColor = new Color(1f, 0.66f, 0.22f);

        public void Open(int tab = -1)
        {
            if (tab >= 0) _tab = tab;
            _message.text = _root.Server.Online ? "" : "The Caravan needs the server.";
            SetTab(_tab);
            _canvas.SetActive(true);
        }

        public void Close() => _canvas.SetActive(false);

        private void SetTab(int tab)
        {
            _tab = tab;
            _pieces.SetActive(tab != AmberTab);
            _packs.SetActive(tab == AmberTab);
            if (tab == AmberTab) return;
            var kind = (WardrobeKind)tab;
            int n = 0;
            foreach (WardrobeDef def in Wardrobe.All)
            {
                if (def.Kind != kind || n >= GridCells) continue;
                Cell c = _cells[n++];
                c.Def = def;
                Ui.SetPicture(c.Picture, "Thumbs/Caravan/" + def.Id);
                c.Name.text = def.Name;
                c.Price.text = def.Sold ? Wardrobe.Price(def, 1) + "+ Amber" : "drops only";
            }
            for (int i = 0; i < GridCells; i++) _cells[i].Back.gameObject.SetActive(i < n);
            if (_selected == null || _selected.Kind != kind) Select(_cells[0].Def);
        }

        private void Select(WardrobeDef def)
        {
            if (def == null) return;
            _selected = def;
            Ui.SetPicture(_featurePicture, "Thumbs/Caravan/" + def.Id);
            _featureName.text = def.Name;
            _featureKind.text = Wardrobe.KindName(def.Kind).ToUpperInvariant() + "  ·  " + TierName(def.Tier);
            _featurePerk.text = def.PerkText;
            _featureBlurb.text = def.Blurb;
        }

        public static string TierName(int tier) => tier switch { 1 => "Plain", 2 => "Fine", 3 => "Rare", _ => "Legendary" };

        public static Color TierColor(int tier) => tier switch
        {
            1 => new Color(0.75f, 0.72f, 0.66f),
            2 => new Color(0.45f, 0.7f, 1f),
            3 => new Color(0.75f, 0.5f, 1f),
            _ => new Color(1f, 0.6f, 0.2f),
        };

        private void AskBuy()
        {
            if (_busy || _selected == null || !_selected.Sold) return;
            int days = Wardrobe.Days[_dayIndex];
            int price = Wardrobe.Price(_selected, days);
            if (_root.Server.Amber < price)
            {
                _message.text = "Not enough Amber.";
                SetTab(AmberTab);
                return;
            }
            WardrobeDef def = _selected;
            long held = _root.Server.SecondsLeft(def.Id);
            string body = $"{def.Name} ({def.PerkText}) for {days} {(days == 1 ? "day" : "days")}, for {price} Amber."
                          + (held > 0 ? $"\nYou hold it for {WardrobePanel.Left(held)} more: the days are added." : "");
            _confirm.Show("BUY FROM THE CARAVAN", body, "BUY  " + price, Palette.Danger, () => Buy(def, days));
        }

        private void Buy(WardrobeDef def, int days)
        {
            _busy = true;
            StartCoroutine(_root.Server.CaravanBuy(def.Id, days, error =>
            {
                _busy = false;
                _message.text = error ?? $"{def.Name}: yours for {WardrobePanel.Left(_root.Server.SecondsLeft(def.Id))}.";
                if (error == null) GameAudio.Instance?.Play("LaneLoot", 0.9f, 0.1f, 0f);
            }));
        }

        private void AskPack(AmberPack pack)
        {
            if (_busy) return;
            bool first = _root.Server.Wardrobe?.firstPurchase ?? false;
            int paid = Amber.Paid(pack, first);
            _confirm.Show("AMBER", $"{paid:N0} Amber for {pack.PriceText}{(first ? " (first purchase: double)" : "")}.\nPlaytest: free until the stores open.",
                "BUY  " + pack.PriceText, Palette.Danger, () =>
                {
                    _busy = true;
                    StartCoroutine(_root.Server.AmberPack(pack.Id, error =>
                    {
                        _busy = false;
                        _message.text = error ?? $"+{paid:N0} Amber.";
                        if (error == null) GameAudio.Instance?.Play("LaneKorstoneBreak", 0.9f, 0.3f, 0f);
                    }));
                });
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            _balance.text = _root.Server.Amber.ToString("N0");
            for (int i = 0; i < _tabs.Length; i++) _tabs[i].targetGraphic.color = i == _tab ? Palette.Danger : Palette.ButtonIdle;

            if (_tab == AmberTab)
            {
                _firstBanner.SetActive(_root.Server.Wardrobe?.firstPurchase ?? false);
                foreach (PackCell p in _packCells) p.Buy.interactable = !_busy && _root.Server.Online;
                return;
            }

            for (int i = 0; i < GridCells; i++)
            {
                Cell c = _cells[i];
                if (c.Def == null) continue;
                c.Back.color = c.Def == _selected ? new Color(0.32f, 0.2f, 0.08f, 0.97f) : new Color(0.07f, 0.07f, 0.13f, 0.95f);
                c.Name.color = TierColor(c.Def.Tier);
            }
            if (_selected == null) return;
            _featureName.color = TierColor(_selected.Tier);
            long held = _root.Server.SecondsLeft(_selected.Id);
            bool worn = _root.Server.WornId(_selected.Kind) == _selected.Id;
            _featureHeld.text = held > 0 ? (worn ? "Worn" : "Held") + $"  ·  {WardrobePanel.Left(held)} left" : _selected.Sold ? "" : "Only its Commander drops it.";
            for (int i = 0; i < _dayButtons.Length; i++)
            {
                int days = Wardrobe.Days[i];
                int price = Wardrobe.Price(_selected, days);
                _dayButtons[i].gameObject.SetActive(_selected.Sold);
                _dayButtons[i].targetGraphic.color = i == _dayIndex ? Palette.ButtonForge : Palette.ButtonIdle;
                _dayLabels[i].text = $"{days} {(days == 1 ? "DAY" : "DAYS")}\n" + ConfirmDialog.Tint(price.ToString(), AmberColor);
            }
            int cost = Wardrobe.Price(_selected, Wardrobe.Days[_dayIndex]);
            _buy.gameObject.SetActive(_selected.Sold);
            _buy.interactable = !_busy && _root.Server.Online;
            _buyLabel.text = (held > 0 ? "ADD " : "BUY ") + Wardrobe.Days[_dayIndex] + (Wardrobe.Days[_dayIndex] == 1 ? " DAY" : " DAYS") + "  ·  " + ConfirmDialog.Tint(cost + " AMBER", AmberColor);
        }
    }
}
