using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// INVENTORY (owner, 26 Sep 2026, mockup D in docs/concept/screens): the hero in the middle as the lane shows him
    /// (HeroStage, turned by a finger) between the eight worn pieces, his stats beside him; below, everything carried in
    /// one grid with ALL / GEAR / BOOKS / MATERIALS tabs, and sorn, Amber and Honor along the bottom. A tile opens its card:
    /// a piece shows its stats, what wearing it would change, etchings and sockets, and goes to EQUIP, the Forge (any piece,
    /// worn or in the bag, forges and turns; owner, 24 Sep 2026) or the Exchange; a Technique Scroll goes to SKILLS or the
    /// Exchange; a material says what it is for.
    /// </summary>
    public sealed class GearPanel : MonoBehaviour
    {
        private const int Columns = 7;
        private const int MaxTiles = 160;

        private enum Tab { All, Gear, Books, Materials }

        private enum CardKind { None, Gear, Book, Good }

        private sealed class Tile
        {
            public RectTransform Rect;
            public Button Button;
            public Outline Selected;
            public Image Glow;
            public RawImage Icon;
            public Text Badge;
            public Text Caption;
            public Entry Entry;
        }

        /// <summary>What a grid tile holds: a piece, a stack of one Technique Scroll, or a material (Goods).</summary>
        private sealed class Entry
        {
            public ItemState Item;
            public int Book = -1;
            public int Good = -1;
            public long Count;
        }

        /// <summary>A material or token the hero carries, with what it is for.</summary>
        private sealed class Good
        {
            public readonly string Icon, Name, Blurb;
            public readonly Func<GameRoot, long> Count;

            public Good(string icon, string name, string blurb, Func<GameRoot, long> count)
            {
                Icon = icon;
                Name = name;
                Blurb = blurb;
                Count = count;
            }
        }

        private static readonly Good[] Goods = BuildGoods();

        private static Good[] BuildGoods()
        {
            var goods = new List<Good>
            {
                new Good("Draught", "Draughts", "Healing draughts: the hero drinks one on the lane when his HP runs low. The Hunt Marks shop sells ten for a mark.",
                    r => r.Session.Inventory.Potions),
                new Good("WolfSinew", "Hunt materials", "What the hunt's beasts leave behind (Wolf Sinew and the like). The Chained Smith takes them for his work.",
                    r => r.Session.Inventory.Materials),
                new Good("ScrollOfMercy", "Scroll of Mercy", "Laid on a Forge attempt: a failure only loses a level instead of the piece.", r => r.Session.Inventory.ScrollsOfMercy),
                new Good("KhansAlloy", "Khan's Alloy", "Laid on a Forge attempt: +10 points of success, and a failure only loses a level.", r => r.Session.Inventory.KhansAlloys),
                new Good("AnvilWard", "Anvil Ward", "Laid on a Forge attempt: a failure keeps the level.", r => r.Session.Inventory.AnvilWards),
                new Good("Turnstone", "Turnstones", "Turn a piece's etchings at the Forge, one turn a stone.", r => r.Session.Inventory.Turnstones),
                new Good("EtchingNeedle", "Etching Needle", "Adds an etching to a piece, the first to the fourth.", r => r.Session.Inventory.EtchingNeedles),
                new Good("MastersNeedle", "Master's Needle", "Adds a piece's fifth etching (the Carvers' Archive's vault, or 90 Laurels in the Pit shop).", r => r.Session.Inventory.MastersNeedles),
                new Good("PinningWax", "Pinning Wax", "Holds one etching of a piece through its turns.", r => r.Session.Inventory.PinningWax),
                new Good("Oathstone", "Oathstone", "A marker fragment that still holds a vow: it pays for Grand skill grades (SKILLS) and a change of Banner.",
                    r => r.Session.Inventory.Oathstones),
                new Good("HuntMark", "Hunt Marks", "Paid by bounties; spent in the Hunt Marks shop (BOUNTIES).", r => r.Session.Inventory.HuntMarks),
                new Good("Laurel", "Laurels", "Won in the Pits; spent in the Pit shop.", r => r.Server.Pits?.laurels ?? 0),
                new Good("GuildTally", "Guild Tallies", "Earned for your guild (donations, the Commander's first place); spent in the guild shop.", r => r.Server.Tallies),
                new Good("SummoningMarker", "Summoning Marker", "A marker an Elder Korstone let fall.", r => r.Session.Inventory.SummoningMarkers),
            };
            for (int rank = 0; rank < Content.KorshardRanks.Length; rank++)
            {
                int r0 = rank;
                goods.Add(new Good(SocketPanel.RankIcons[rank], Content.KorshardRanks[rank] + " Korshard",
                    "Set in a piece's socket at SHARDS: the higher the rank, the stronger the shard.", r => r.Session.Inventory.Korshards[r0]));
            }
            return goods.ToArray();
        }

        private GameRoot _root;
        private GameObject _canvas;
        private ConfirmDialog _confirm;
        private HeroStage _stage;
        private Text _level;
        private Text _attack, _defense, _hp, _crit;
        private Text _classLabel;
        private Text _message;
        private readonly Tile[] _worn = new Tile[8];
        private readonly List<Tile> _grid = new List<Tile>();
        private RectTransform _gridView;
        private GridLayoutGroup _gridLayout;
        private Text _empty;
        private readonly Button[] _tabs = new Button[4];
        private readonly Text[] _tabLabels = new Text[4];
        private Tab _tab;
        private Text _sorn, _amber, _honor;

        // The card of a tapped tile.
        private GameObject _card;
        private CardKind _cardKind;
        private ItemState _selected;
        private string _selectedId;
        private int _cardBook = -1;
        private int _cardGood = -1;
        private RawImage _picture;
        private Image _pictureGlow;
        private Text _pictureLevel;
        private Text _name, _info, _stats, _compare, _body, _sockets;
        private readonly Button[] _actions = new Button[3];
        private readonly Text[] _actionLabels = new Text[3];
        private readonly Action[] _actionDo = new Action[3];

        public bool IsOpen => _canvas.activeSelf;

        public static readonly string[] SlotNames = { "Weapon", "Armour", "Helmet", "Shield", "Bracelet", "Necklace", "Earrings", "Shoes" };

        public static Color RarityColor(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => new Color(0.38f, 0.75f, 0.40f),
            Rarity.Rare => new Color(0.32f, 0.56f, 0.95f),
            Rarity.Epic => new Color(0.66f, 0.38f, 0.92f),
            Rarity.Legendary => new Color(1f, 0.62f, 0.18f),
            _ => new Color(0.55f, 0.55f, 0.58f),
        };

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("GearCanvas", 9).gameObject;
            Transform canvas = _canvas.transform;

            Ui.Backdrop(canvas, "Gear");
            Ui.Title("Title", canvas, 0.15f, 0.943f, 0.85f, 0.982f, "INVENTORY", 42, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _confirm = new GameObject("GearConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();

            // The hero: the worn pieces on either side, the hero himself, his stats.
            Ui.Framed("Top", canvas, 0.02f, 0.56f, 0.98f, 0.937f, new Color(0.03f, 0.03f, 0.07f, 0.55f)).raycastTarget = false;
            for (int i = 0; i < 8; i++)
            {
                bool left = i < 4;
                float x0 = left ? 0.035f : 0.8f, y1 = 0.925f - (i % 4) * 0.091f;
                Tile t = MakeTile(canvas, "Worn" + i, withCaption: true);
                t.Rect.anchorMin = new Vector2(x0, y1 - 0.084f);
                t.Rect.anchorMax = new Vector2(x0 + 0.165f, y1);
                t.Caption.text = SlotNames[i].ToUpperInvariant();
                int slot = i;
                t.Button.onClick.AddListener(() => ShowGear(_worn[slot].Entry?.Item));
                _worn[i] = t;
            }
            // The hero stands on the painted steppe in a gold frame (mockup D).
            Ui.Picture("HeroScene", canvas, 0.21f, 0.618f, 0.585f, 0.925f, "Scenes/Trail");
            RectTransform stageBox = Ui.Rect("Stage", canvas, 0.215f, 0.622f, 0.58f, 0.921f);
            _stage = new GameObject("InventoryStage").AddComponent<HeroStage>();
            _stage.Init(stageBox, HeroStage.Below + new Vector3(60f, 0f, 0f));
            _stage.Zoom = 1.18f;
            _stage.gameObject.SetActive(false);
            Ui.Button("Class", canvas, 0.21f, 0.568f, 0.585f, 0.612f, "", 22, Palette.Alloy, SwitchClass, out _classLabel);

            Ui.Framed("StatsBack", canvas, 0.595f, 0.625f, 0.79f, 0.925f, new Color(0.03f, 0.03f, 0.07f, 0.8f)).raycastTarget = false;
            // The level opens Oath Renewal (GDD section 12): at level 105, back to level 1 for a lasting bonus.
            _level = Ui.Title("Level", canvas, 0.6f, 0.86f, 0.785f, 0.92f, "", 26, TextAnchor.MiddleCenter, Palette.Sorn);
            _level.supportRichText = true;
            _level.raycastTarget = true;
            _level.gameObject.AddComponent<Button>().onClick.AddListener(AskRenew);
            _attack = StatLine("Attack", 0.795f, 0.855f);
            _defense = StatLine("Defense", 0.735f, 0.795f);
            _hp = StatLine("HP", 0.675f, 0.735f);
            _crit = StatLine("Crit", 0.63f, 0.675f);
            Ui.Button("Skills", canvas, 0.595f, 0.568f, 0.79f, 0.618f, "SKILLS", 24, Palette.ButtonForge, () => _root.Skills.Open(), out _);

            // What the hero carries: tabs over one grid.
            Ui.Framed("BagBack", canvas, 0.02f, 0.145f, 0.98f, 0.553f, new Color(0.03f, 0.03f, 0.07f, 0.55f)).raycastTarget = false;
            string[] tabs = { "ALL", "GEAR", "BOOKS", "MATERIALS" };
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = (Tab)i;
                float x0 = 0.035f + i * 0.2325f;
                _tabs[i] = Ui.Button("Tab" + i, canvas, x0, 0.507f, x0 + 0.225f, 0.547f, tabs[i], 22, Palette.ButtonIdle, () => _tab = tab, out _tabLabels[i]);
            }
            _gridView = Ui.Rect("GridView", canvas, 0.03f, 0.152f, 0.97f, 0.502f);
            Image viewBack = _gridView.gameObject.AddComponent<Image>();
            viewBack.color = new Color(0f, 0f, 0f, 0.25f);
            _gridView.gameObject.AddComponent<RectMask2D>();
            var scroll = _gridView.gameObject.AddComponent<ScrollRect>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(_gridView, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            _gridLayout = content.gameObject.AddComponent<GridLayoutGroup>();
            _gridLayout.padding = new RectOffset(8, 8, 8, 8);
            _gridLayout.spacing = new Vector2(8f, 8f);
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayout.constraintCount = Columns;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = _gridView;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            for (int i = 0; i < MaxTiles; i++)
            {
                Tile t = MakeTile(content, "Tile" + i, withCaption: false);
                int index = i;
                t.Button.onClick.AddListener(() => Open(_grid[index].Entry));
                t.Rect.gameObject.SetActive(false);
                _grid.Add(t);
            }
            _empty = Ui.Label("Empty", _gridView, 0.05f, 0.3f, 0.95f, 0.7f, "", 26, TextAnchor.MiddleCenter, Palette.Muted);

            _message = Ui.Label("Message", canvas, 0.04f, 0.127f, 0.96f, 0.148f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);
            _message.supportRichText = true;
            // Sorn, Amber and Honor along the bottom.
            Ui.Framed("Purse", canvas, 0.02f, 0.078f, 0.98f, 0.125f, new Color(0.04f, 0.04f, 0.09f, 0.92f)).raycastTarget = false;
            _sorn = Purse(canvas, 0, "Sorn", "Sorn");
            _amber = Purse(canvas, 1, "Amber", "Amber");
            _honor = Purse(canvas, 2, "Honor", "Honor");
            Ui.Button("Wardrobe", canvas, 0.03f, 0.012f, 0.3f, 0.07f, "WARDROBE", 24, Palette.Alloy, () => { _canvas.SetActive(false); _root.Wardrobe.Open(); }, out _);
            Ui.Button("Depot", canvas, 0.32f, 0.012f, 0.56f, 0.07f, "DEPOT", 24, Palette.Safe, () => { _canvas.SetActive(false); _root.Depot.Open(); }, out _);
            Ui.Button("Close", canvas, 0.58f, 0.012f, 0.97f, 0.07f, "BACK TO THE HUNT", 22, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);

            BuildCard(canvas);
            // Lives on the canvas so it hears the canvas close (the hero's stage stops with it).
            transform.SetParent(canvas, false);
            _canvas.SetActive(false);
        }

        private Text StatLine(string name, float yMin, float yMax)
        {
            Text t = Ui.Label(name, _canvas.transform, 0.605f, yMin, 0.78f, yMax, "", 30, TextAnchor.MiddleCenter, Palette.Parchment);
            t.supportRichText = true;
            t.font = Ui.TitleFont;
            return t;
        }

        private Text Purse(Transform canvas, int index, string icon, string name)
        {
            float x0 = 0.04f + index * 0.315f;
            RectTransform box = Ui.Rect(name + "IconBox", canvas, x0, 0.082f, x0 + 0.06f, 0.121f);
            Ui.Icon(name + "Icon", box, 0f, 0f, 1f, 1f, icon);
            Text t = Ui.Label(name, canvas, x0 + 0.065f, 0.082f, x0 + 0.3f, 0.121f, "", 24, TextAnchor.MiddleLeft, Palette.Parchment);
            t.supportRichText = true;
            return t;
        }

        private void BuildCard(Transform canvas)
        {
            _card = Ui.Rect("Card", canvas, 0f, 0f, 1f, 1f).gameObject;
            Image dim = Ui.Panel("Dim", _card.transform, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.7f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(CloseCard);
            Transform c = Ui.Framed("CardBack", _card.transform, 0.04f, 0.2f, 0.96f, 0.82f, new Color(0.06f, 0.06f, 0.11f, 0.98f)).transform;
            Ui.SlotTile("PictureSlot", c, 0.03f, 0.74f, 0.3f, 0.97f, new Color(0.05f, 0.05f, 0.09f));
            _pictureGlow = Ui.Sliced("PictureGlow", c, 0.04f, 0.755f, 0.29f, 0.955f, "Glow", Color.clear);
            _pictureGlow.raycastTarget = false;
            RectTransform pictureBox = Ui.Rect("PictureBox", c, 0.055f, 0.765f, 0.275f, 0.945f);
            _picture = Ui.Icon("Picture", pictureBox, 0f, 0f, 1f, 1f, "Weapon");
            _pictureLevel = Ui.Title("PictureLevel", c, 0.04f, 0.745f, 0.29f, 0.8f, "", 30, TextAnchor.LowerRight, Palette.Parchment);
            _name = Ui.Title("Name", c, 0.33f, 0.885f, 0.97f, 0.97f, "", 36, TextAnchor.MiddleLeft, Palette.Parchment);
            _info = Ui.Label("Info", c, 0.33f, 0.83f, 0.97f, 0.885f, "", 22, TextAnchor.MiddleLeft, Palette.Muted);
            _stats = Ui.Label("Stats", c, 0.33f, 0.775f, 0.97f, 0.83f, "", 24, TextAnchor.MiddleLeft, Palette.Parchment);
            _compare = Ui.Label("Compare", c, 0.33f, 0.735f, 0.97f, 0.775f, "", 22, TextAnchor.MiddleLeft, Palette.Parchment);
            _body = Ui.Label("Body", c, 0.05f, 0.2f, 0.95f, 0.71f, "", 25, TextAnchor.UpperLeft, Palette.Parchment);
            _body.resizeTextForBestFit = false;
            _sockets = Ui.Label("Sockets", c, 0.05f, 0.125f, 0.95f, 0.2f, "", 21, TextAnchor.MiddleLeft, Palette.Muted);
            foreach (Text t in new[] { _stats, _compare, _body, _sockets }) t.supportRichText = true;
            for (int i = 0; i < _actions.Length; i++)
            {
                int index = i;
                float x0 = 0.025f + i * 0.24f;
                _actions[i] = Ui.Button("Action" + i, c, x0, 0.02f, x0 + 0.23f, 0.11f, "", 22, Palette.Safe, () => _actionDo[index]?.Invoke(), out _actionLabels[i]);
            }
            Ui.Button("CardClose", c, 0.745f, 0.02f, 0.975f, 0.11f, "CLOSE", 22, Palette.ButtonIdle, CloseCard, out _);
            _card.SetActive(false);
        }

        public void Open()
        {
            _message.text = "";
            CloseCard();
            _canvas.SetActive(true);
            // Screenshots: -bagtab <n> opens a tab, -bagcard the first tile's card.
            string[] cmd = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(cmd, "-bagtab");
            if (at >= 0 && at + 1 < cmd.Length && int.TryParse(cmd[at + 1], out int tab)) _tab = (Tab)Mathf.Clamp(tab, 0, 3);
            _cardOnOpen = Array.IndexOf(cmd, "-bagcard") >= 0;
        }

        private bool _cardOnOpen;

        private void OnEnable()
        {
            if (_stage != null) _stage.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (_stage != null) _stage.gameObject.SetActive(false);
        }

        private Tile MakeTile(Transform parent, string name, bool withCaption)
        {
            // A dark slot with the item's rarity glowing behind it, under the painted gold slot frame.
            Image rim = Ui.Sliced(name, parent, 0f, 0f, 1f, 1f, "CardFill", new Color(0.07f, 0.07f, 0.11f));
            var t = new Tile { Rect = rim.rectTransform };
            t.Button = rim.gameObject.AddComponent<Button>();
            t.Button.targetGraphic = rim;
            t.Selected = rim.gameObject.AddComponent<Outline>();
            t.Selected.effectColor = Palette.Sorn;
            t.Selected.effectDistance = new Vector2(4f, -4f);
            t.Selected.enabled = false;
            Image inner = Ui.Panel("Inner", rim.transform, 0.045f, 0.045f, 0.955f, 0.955f, new Color(0f, 0f, 0f, 0f));
            inner.raycastTarget = false;
            t.Glow = Ui.Sliced("Glow", inner.transform, 0.05f, 0.05f, 0.95f, 0.95f, "Glow", Color.clear);
            t.Glow.raycastTarget = false;
            Ui.Sliced("Frame", rim.transform, 0f, 0f, 1f, 1f, "SlotRim", Color.white).raycastTarget = false;
            RectTransform iconBox = Ui.Rect("IconBox", inner.transform, 0.1f, 0.1f, 0.9f, 0.9f);
            t.Icon = Ui.Icon("Icon", iconBox, 0f, 0f, 1f, 1f, "Weapon");
            t.Badge = Ui.Title("Badge", inner.transform, 0.3f, 0.0f, 0.98f, 0.36f, "", 28, TextAnchor.LowerRight, Palette.Parchment);
            t.Caption = withCaption ? Ui.Label("Caption", inner.transform, 0.05f, 0.02f, 0.95f, 0.22f, "", 16, TextAnchor.LowerCenter, Palette.Muted) : null;
            return t;
        }

        private void Open(Entry e)
        {
            if (e == null) return;
            if (e.Item != null) ShowGear(e.Item);
            else if (e.Book >= 0) ShowCard(CardKind.Book, e.Book, -1);
            else if (e.Good >= 0) ShowCard(CardKind.Good, -1, e.Good);
        }

        private void ShowGear(ItemState item)
        {
            if (item == null) return;
            _selected = item;
            _selectedId = _root.Server.IdOf(item);
            ShowCard(CardKind.Gear, -1, -1);
        }

        private void ShowCard(CardKind kind, int book, int good)
        {
            _cardKind = kind;
            _cardBook = book;
            _cardGood = good;
            _message.text = "";
            _card.SetActive(true);
        }

        private void CloseCard()
        {
            _cardKind = CardKind.None;
            _card.SetActive(false);
        }

        private bool Owns(ItemState item) =>
            item != null && (_root.Session.Equipped(item.Slot) == item || _root.Session.Inventory.Loot.Contains(item));

        /// <summary>The card's piece survives the server refreshing every item: it is re-found by its server id (null: gone).</summary>
        private ItemState Selected()
        {
            if (Owns(_selected)) return _selected;
            if (_selectedId != null)
                foreach (KeyValuePair<ItemState, string> pair in _root.Server.ItemIds)
                    if (pair.Value == _selectedId && Owns(pair.Key)) { _selected = pair.Key; return _selected; }
            return null;
        }

        private void Equip(ItemState item)
        {
            if (_root.Session.Equipped(item.Slot) == item) return;
            if (_root.Server.Online)
            {
                string id = _root.Server.IdOf(item);
                if (id == null) return;
                StartCoroutine(_root.Server.Equip(id, error => _message.text = error ?? "Equipped."));
            }
            else
            {
                _root.Session.Equip(item);
                _message.text = "Equipped.";
            }
        }

        private void SendToForge(ItemState item)
        {
            CloseCard();
            _root.Session.PutOnAnvil(item);
            _root.Forge.Open(fromGear: true);
        }

        /// <summary>Playtest: switch between the playable classes at will (server-side on line).</summary>
        private void SwitchClass()
        {
            // Vanguard -> Kestrel -> Wraithsworn -> Drumcaller -> Vanguard.
            HeroClass next = (HeroClass)(((int)_root.Session.Class + 1) % 4);
            if (_root.Server.Online) StartCoroutine(_root.Server.SetClass(next, error => _message.text = error ?? "Now playing " + next + "."));
            else
            {
                _root.Session.SetClass(next);
                _message.text = "Now playing " + next + ".";
            }
        }

        private static string Count(long n) => n >= 100_000 ? $"x{n / 1000}k" : "x" + n;

        private static string BookIcon(int book) => "Book" + Books.ClassOf(book);

        /// <summary>What the tab shows: pieces best first, then scrolls (the class played first), then materials.</summary>
        private List<Entry> Entries(out int gear, out int books)
        {
            PlayerSession session = _root.Session;
            var list = new List<Entry>();
            List<ItemState> pieces = session.Inventory.Loot.Where(x => !x.Destroyed)
                .OrderByDescending(x => x.UpgradeLevel).ThenByDescending(x => (int)x.Rarity).ThenByDescending(x => x.ItemLevel).ToList();
            gear = pieces.Count;
            if (_tab == Tab.All || _tab == Tab.Gear) foreach (ItemState item in pieces) list.Add(new Entry { Item = item, Count = 1 });
            books = 0;
            var scrolls = new List<Entry>();
            for (int b = 0; b < Books.Count; b++)
            {
                int n = session.Inventory.Books[b];
                if (n <= 0) continue;
                books += n;
                scrolls.Add(new Entry { Book = b, Count = n });
            }
            if (_tab == Tab.All || _tab == Tab.Books)
                list.AddRange(scrolls.OrderByDescending(e => Books.ClassOf(e.Book) == session.Class).ThenBy(e => e.Book));
            if (_tab == Tab.All || _tab == Tab.Materials)
                for (int g = 0; g < Goods.Length; g++)
                {
                    long n = Goods[g].Count(_root);
                    if (n > 0) list.Add(new Entry { Good = g, Count = n });
                }
            return list;
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            PlayerSession session = _root.Session;
            HeroStats hero = session.Hero;

            // The hero as the lane shows him: his class, his armour's and weapon's looks, a worn skin's costume.
            ItemState armor = session.Equipped(EquipSlot.Armor);
            string skin = null;
            foreach (WardrobeDef piece in session.Worn) if (piece.Kind == WardrobeKind.Skin) skin = piece.Look;
            _stage.Show(session.Class, armor != null ? ItemLooks.Tier(armor.ItemLevel) : 0, ItemLooks.Tier(session.Weapon.ItemLevel), skin,
                armor != null ? UpgradeGlow.ForLevel(armor.UpgradeLevel) : 0f, UpgradeGlow.ForLevel(session.Weapon.UpgradeLevel));

            bool canRenew = _root.Server.Online && OathRenewal.Problem(session.Level, session.Renewals) == null;
            _level.text = $"LEVEL {session.Level}"
                          + (canRenew ? "\n<size=17>" + ConfirmDialog.Tint("RENEW YOUR OATH ›", Palette.Good) + "</size>"
                              : session.Renewals > 0 ? $"\n<size=17>oath {session.Renewals}/{OathRenewal.MaxRenewals}</size>" : "");
            _attack.text = $"<size=19><color=#C2BAAD>ATTACK</color></size>\n{hero.Attack:N0}";
            _defense.text = $"<size=19><color=#C2BAAD>DEFENSE</color></size>\n{hero.Defense:N0}";
            _hp.text = $"<size=19><color=#C2BAAD>HP</color></size>\n{hero.MaxHp:N0}";
            _crit.text = $"<size=19><color=#C2BAAD>CRIT</color></size>  {hero.CritChanceBp / 100}%";
            _classLabel.text = session.Class.ToString().ToUpperInvariant() + "  ›";

            for (int i = 0; i < 8; i++)
            {
                ItemState worn = session.Equipped((EquipSlot)i);
                FillGear(_worn[i], worn, (EquipSlot)i);
                _worn[i].Caption.enabled = worn == null;
            }

            List<Entry> entries = Entries(out int gear, out int books);
            for (int i = 0; i < _grid.Count; i++)
            {
                bool has = i < entries.Count;
                if (_grid[i].Rect.gameObject.activeSelf != has) _grid[i].Rect.gameObject.SetActive(has);
                if (!has) continue;
                Entry e = entries[i];
                if (e.Item != null) FillGear(_grid[i], e.Item, e.Item.Slot);
                else Fill(_grid[i], e);
            }
            float cell = (_gridView.rect.width - 16f - 8f * (Columns - 1)) / Columns;
            if (cell > 10f && Mathf.Abs(_gridLayout.cellSize.x - cell) > 0.5f) _gridLayout.cellSize = new Vector2(cell, cell);
            _empty.text = entries.Count > 0 ? "" : _tab switch
            {
                Tab.Gear => "No pieces in the bag. Drops from the hunt land here.",
                Tab.Books => "No Technique Scrolls. Warden chests, the Hunt Marks and Pit shops and the Exchange have them.",
                Tab.Materials => "Nothing yet: Korstones, bounties and dungeons pay in these.",
                _ => "The bag is empty. Drops from the hunt land here.",
            };
            _tabLabels[1].text = gear > 0 ? $"GEAR  {gear}" : "GEAR";
            _tabLabels[2].text = books > 0 ? $"BOOKS  {books}" : "BOOKS";
            for (int i = 0; i < _tabs.Length; i++) _tabs[i].targetGraphic.color = (int)_tab == i ? Palette.Danger : Palette.ButtonIdle;

            _sorn.text = $"<size=18><color=#C2BAAD>Sorn</color></size>\n{session.Inventory.Sorn:N0}";
            _amber.text = $"<size=18><color=#C2BAAD>Amber</color></size>\n{_root.Server.Amber:N0}";
            _honor.text = $"<size=18><color=#C2BAAD>Honor</color></size>\n{_root.Server.Honor:N0}";

            if (_cardOnOpen && entries.Count > 0)
            {
                _cardOnOpen = false;
                Open(entries[0]);
            }
            if (_card.activeSelf) ShowDetail(session);
        }

        private void FillGear(Tile t, ItemState item, EquipSlot slot)
        {
            t.Entry = item == null ? null : new Entry { Item = item, Count = 1 };
            Ui.SetIcon(t.Icon, item != null ? Ui.ItemIcon(item) : slot.ToString());
            t.Icon.color = item == null ? new Color(1f, 1f, 1f, 0.18f) : Color.white;
            Color glow = item == null ? Color.clear : RarityColor(item.Rarity);
            glow.a = item == null ? 0f : 0.55f;
            t.Glow.color = glow;
            t.Badge.text = item == null || item.UpgradeLevel == 0 ? "" : "+" + item.UpgradeLevel;
            t.Badge.color = item == null ? Palette.Muted : ForgePanel.LevelColor(item.UpgradeLevel);
            t.Selected.enabled = item != null && _cardKind == CardKind.Gear && item == _selected;
            t.Button.interactable = item != null;
        }

        private void Fill(Tile t, Entry e)
        {
            t.Entry = e;
            bool book = e.Book >= 0;
            Ui.SetIcon(t.Icon, book ? BookIcon(e.Book) : Goods[e.Good].Icon);
            t.Icon.color = Color.white;
            // Scrolls of the class played glow: those are the ones this hero reads.
            t.Glow.color = book && Books.ClassOf(e.Book) == _root.Session.Class ? new Color(1f, 0.84f, 0.42f, 0.35f) : Color.clear;
            t.Badge.text = Count(e.Count);
            t.Badge.color = Palette.Parchment;
            t.Selected.enabled = book ? _cardKind == CardKind.Book && e.Book == _cardBook : _cardKind == CardKind.Good && e.Good == _cardGood;
            t.Button.interactable = true;
        }

        private void SetAction(int index, string label, Color color, bool enabled, Action act)
        {
            bool shown = label != null;
            _actions[index].gameObject.SetActive(shown);
            if (!shown) return;
            _actionLabels[index].text = label;
            _actions[index].targetGraphic.color = color;
            _actions[index].interactable = enabled;
            _actionDo[index] = act;
        }

        private void ShowDetail(PlayerSession session)
        {
            switch (_cardKind)
            {
                case CardKind.Gear:
                    ItemState item = Selected();
                    if (item == null) { CloseCard(); return; }
                    ShowGearDetail(session, item);
                    break;
                case CardKind.Book:
                    ShowBookDetail(session, _cardBook);
                    break;
                case CardKind.Good:
                    ShowGoodDetail(_cardGood);
                    break;
                default:
                    CloseCard();
                    break;
            }
        }

        private void Picture(string icon, Color glow, string corner, Color cornerColor)
        {
            Ui.SetIcon(_picture, icon);
            _pictureGlow.color = glow;
            _pictureLevel.text = corner;
            _pictureLevel.color = cornerColor;
        }

        private void ShowGearDetail(PlayerSession session, ItemState item)
        {
            bool worn = session.Equipped(item.Slot) == item;
            _name.text = $"{item.DisplayName} +{item.UpgradeLevel}";
            _name.color = RarityColor(item.Rarity);
            Color glow = RarityColor(item.Rarity);
            glow.a = item.Rarity == Rarity.Common ? 0.15f : 0.55f;
            Picture(Ui.ItemIcon(item), glow, item.UpgradeLevel == 0 ? "" : "+" + item.UpgradeLevel, ForgePanel.LevelColor(item.UpgradeLevel));
            _info.text = $"{SlotNames[(int)item.Slot]}  ·  Item level {item.ItemLevel}  ·  {item.Rarity}  ·  " + (worn ? "worn" : "in your bag");

            HeroStats bare = HeroFactory.FromEquipment(Array.Empty<ItemState>(), session.Level);
            HeroStats alone = HeroFactory.FromEquipment(new[] { item }, session.Level);
            _stats.text = Stats(alone, bare, signed: false);
            if (worn) _compare.text = ConfirmDialog.Tint("Worn now.", Palette.Muted);
            else
            {
                IEnumerable<ItemState> swapped = session.Equipment.Where(e => e.Slot != item.Slot).Append(item);
                string delta = Stats(HeroFactory.FromEquipment(swapped, session.Level, session.Class, session.Worn, session.Renewals), session.Hero, signed: true);
                _compare.text = "If worn:  " + (delta.Length == 0 ? ConfirmDialog.Tint("no change", Palette.Muted) : delta);
            }

            EtchingPool pool = EtchingPool.For(item.Slot);
            var sb = new StringBuilder(RollLines(item));
            if (item.Etchings.Count == 0) sb.Append(ConfirmDialog.Tint("No etchings yet.", Palette.Muted));
            for (int i = 0; i < item.Etchings.Count; i++)
            {
                Etching e = item.Etchings[i];
                string line = $"T{e.Tier}   {pool.Entries[e.EntryId].Name}  +{e.Value}";
                sb.Append(e.Tier >= 4 ? ConfirmDialog.Tint(line, Palette.Sorn) : line);
                if (i == item.LockedEtchingIndex) sb.Append(ConfirmDialog.Tint("   (pinned)", Palette.Muted));
                sb.Append('\n');
            }
            _body.text = sb.ToString().TrimEnd();
            var sockets = new List<string>();
            foreach (Socket s in item.Sockets)
                sockets.Add(s.Dead ? "Dead Shard" : s.Type == null ? "empty" : SocketRules.Name(s.Type.Value) + " " + Content.KorshardRanks[s.Rank]);
            _sockets.text = sockets.Count == 0 ? "No sockets" : "Sockets: " + string.Join("  ·  ", sockets);

            SetAction(0, worn ? "WORN" : "EQUIP", Palette.Safe, !worn, () => Equip(item));
            SetAction(1, "FORGE / TURN", Palette.ButtonForge, !_root.Forge.Busy, () => SendToForge(item));
            string id = _root.Server.IdOf(item);
            SetAction(2, "SELL", Palette.Alloy, !worn && id != null && _root.Server.Online, () => { CloseCard(); _canvas.SetActive(false); _root.Market.OpenSell(id); });
        }

        private void ShowBookDetail(PlayerSession session, int book)
        {
            int held = session.Inventory.Books[book];
            if (held <= 0) { CloseCard(); return; }
            HeroClass cls = Books.ClassOf(book);
            bool mine = cls == session.Class;
            string skill = Books.SkillName(book);
            _name.text = Books.Name(book);
            _name.color = mine ? Palette.Sorn : Palette.Parchment;
            Picture(BookIcon(book), mine ? new Color(1f, 0.84f, 0.42f, 0.4f) : Color.clear, Count(held), Palette.Parchment);
            _info.text = $"A {cls} skill  ·  you hold {held}";
            int grade = book < session.SkillGradeList.Count ? session.SkillGradeList[book] : 0;
            _stats.text = mine ? $"Your {skill}: {ConfirmDialog.Tint(SkillGrades.Name(grade), Palette.Sorn)}  ·  +{SkillGrades.BonusPercent(grade)}% power" : ConfirmDialog.Tint($"Only a {cls} reads it.", Palette.Muted);
            _compare.text = "";
            var sb = new StringBuilder();
            if (mine)
            {
                if (SkillGrades.NeedsBooks(grade))
                {
                    int progress = book < _root.Server.SkillProgress.Length ? _root.Server.SkillProgress[book] : 0;
                    long rest = _root.Server.SkillRestLeft(book);
                    sb.Append($"{progress} of {SkillGrades.ReadsNeeded(grade)} good reads toward {SkillGrades.Name(grade + 1)}.");
                    if (rest > 0) sb.Append($" The skill rests another {rest / 3600}h {rest % 3600 / 60}m.");
                }
                else sb.Append(ConfirmDialog.Tint($"{skill} is past M10: its next steps burn Oathstones and Honor, not scrolls. Sell the scrolls, or trade them to a friend.", Palette.Muted));
            }
            else sb.Append($"Sell it on the Exchange, or trade it to a {cls}.");
            sb.Append($"\n\nA read spends one scroll: {SkillGrades.ReadChanceBp / 100}% it teaches, and the skill rests {SkillGrades.ReadCooldownHours} hours either way. ")
              .Append("M1 and M2 need one good read each, M3 two, M4 three, and so on to nine for M10.")
              .Append(ConfirmDialog.Tint("\n\nScrolls come from Warden chests, the Hunt Marks and Pit shops, and the Exchange.", Palette.Muted));
            _body.text = sb.ToString();
            _sockets.text = "";

            int slot = Books.SlotOf(book);
            SetAction(0, mine ? "READ" : "NOT YOUR CLASS", Palette.ButtonForge, mine && SkillGrades.NeedsBooks(grade), () => { CloseCard(); _root.Skills.Open(slot); });
            SetAction(1, "SELL", Palette.Alloy, _root.Server.Online, () => { CloseCard(); _canvas.SetActive(false); _root.Market.OpenSellBook(book); });
            SetAction(2, null, Color.white, false, null);
        }

        private void ShowGoodDetail(int index)
        {
            Good good = Goods[index];
            long n = good.Count(_root);
            if (n <= 0) { CloseCard(); return; }
            _name.text = good.Name;
            _name.color = Palette.Parchment;
            Picture(good.Icon, Color.clear, Count(n), Palette.Parchment);
            _info.text = $"You hold {n:N0}";
            _stats.text = "";
            _compare.text = "";
            _body.text = good.Blurb;
            _sockets.text = "";
            SetAction(0, null, Color.white, false, null);
            SetAction(1, null, Color.white, false, null);
            SetAction(2, null, Color.white, false, null);
        }

        /// <summary>
        /// A weapon's average damage and skill damage (item level 30 and up), one line each ending in a newline, or "":
        /// gold near the top of the range, red below zero.
        /// </summary>
        public static string RollLines(ItemState item)
        {
            if (!WeaponRolls.Applies(item)) return "";
            string Line(string name, int value, int high)
            {
                string text = $"{name} {(value > 0 ? "+" : "")}{value}%";
                return (value < 0 ? ConfirmDialog.Tint(text, Palette.Bad) : value >= high ? ConfirmDialog.Tint(text, Palette.Sorn) : text) + "\n";
            }
            return Line("Average damage", item.AverageDamagePercent, 30) + Line("Skill damage", item.SkillDamagePercent, 15);
        }

        /// <summary>Attack, Defense, HP, Crit and the weapon's rolls of a against b: the item's own share, or signed changes in green and red.</summary>
        public static string Stats(HeroStats a, HeroStats b, bool signed)
        {
            var parts = new List<string>();
            void Add(string name, long diff, string unit = "")
            {
                if (diff == 0) return;
                string value = (signed && diff > 0 ? "+" : "") + diff + unit;
                parts.Add(signed ? ConfirmDialog.Tint($"{name} {value}", diff > 0 ? Palette.Good : Palette.Bad) : $"{name} {value}");
            }
            Add("Attack", a.Attack - b.Attack);
            Add("Defense", a.Defense - b.Defense);
            Add("HP", a.MaxHp - b.MaxHp);
            Add("Crit", (a.CritChanceBp - b.CritChanceBp) / 100, "%");
            Add("Average damage", a.AverageDamagePercent - b.AverageDamagePercent, "%");
            Add("Skill damage", a.SkillDamagePercent - b.SkillDamagePercent, "%");
            if (!signed && parts.Count == 0) return ConfirmDialog.Tint("No stats", Palette.Muted);
            return string.Join("   ", parts);
        }

        /// <summary>Oath Renewal: what it does, and at level 105 the renewal itself (asked first).</summary>
        private void AskRenew()
        {
            PlayerSession session = _root.Session;
            string body = $"At level {OathRenewal.RequiredLevel} a hero may renew the oath: back to level 1, with +{OathRenewal.PercentPerRenewal}% attack and HP for good, "
                          + $"up to {OathRenewal.MaxRenewals} times.\n\nYour oath: {session.Renewals}/{OathRenewal.MaxRenewals} renewals (+{OathRenewal.BonusPercent(session.Renewals)}% now).";
            string problem = !_root.Server.Online ? "Offline: renewal needs the server." : OathRenewal.Problem(session.Level, session.Renewals);
            if (problem != null)
            {
                _confirm.Show("Oath Renewal", body + "\n\n" + ConfirmDialog.Tint(problem, Palette.Muted), "OK", Palette.ButtonIdle, null);
                return;
            }
            _confirm.Show("Renew your oath?", body + "\n\n" + ConfirmDialog.Tint("Your level goes back to 1. Your gear, sorn and stages stay.", Palette.Bad),
                "RENEW", Palette.ButtonForge, () => StartCoroutine(_root.Server.Renew(error =>
                {
                    if (error != null) { _root.Hud.Log(error); return; }
                    GameAudio.Instance?.Play("LaneLevelUp", 0.9f, 1f, 0f);
                    _root.Hud.Log($"Your oath is renewed ({_root.Session.Renewals}/{OathRenewal.MaxRenewals}): +{OathRenewal.BonusPercent(_root.Session.Renewals)}% attack and HP.");
                })));
        }
    }
}
