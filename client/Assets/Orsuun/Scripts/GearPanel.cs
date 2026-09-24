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
    /// The Gear screen: the eight worn pieces as tiles, a detail card for the selected piece (its own stats, what
    /// wearing it would change, etchings, sockets), and the bag as a scrolling grid. Any piece, worn or in the bag,
    /// can go straight to the Forge to be forged or turned (owner, 24 Sep 2026); bag pieces can be equipped.
    /// </summary>
    public sealed class GearPanel : MonoBehaviour
    {
        private const int BagColumns = 6;
        private const int MaxBagTiles = 72;

        private sealed class Tile
        {
            public RectTransform Rect;
            public Button Button;
            public Image Rim;
            public Outline Selected;
            public Image Glow;
            public RawImage Icon;
            public Text Badge;
            public Text Caption;
            public ItemState Item;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private Text _hero;
        private Text _message;
        private readonly Tile[] _worn = new Tile[8];
        private readonly List<Tile> _bag = new List<Tile>();
        private RectTransform _bagView;
        private GridLayoutGroup _bagGrid;
        private Text _bagTitle;
        private Text _bagEmpty;
        private Text _filterLabel;
        private int _filter = -1;

        private Text _name;
        private Text _info;
        private Text _stats;
        private Text _compare;
        private Text _etchings;
        private Text _sockets;
        private Button _equipButton;
        private Text _equipLabel;
        private Button _forgeButton;

        private Text _classLabel;
        private ItemState _selected;
        private string _selectedId;

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
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Gear");
            // The title sits left of the class switch, so its ribbon stays clear of it.
            Ui.Title("Title", canvas, 0.04f, 0.94f, 0.64f, 0.978f, "GEAR", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _hero = Ui.Label("Hero", canvas, 0.04f, 0.91f, 0.96f, 0.937f, "", 26, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Class", canvas, 0.66f, 0.944f, 0.96f, 0.976f, "", 22, Palette.Alloy, SwitchClass, out _classLabel);

            // Worn: two rows of four.
            for (int i = 0; i < 8; i++)
            {
                int col = i % 4, row = i / 4;
                float x0 = 0.04f + col * 0.232f, y1 = 0.905f - row * 0.082f;
                Tile t = MakeTile(canvas, "Worn" + i, withCaption: true);
                t.Rect.anchorMin = new Vector2(x0, y1 - 0.077f);
                t.Rect.anchorMax = new Vector2(x0 + 0.222f, y1);
                t.Caption.text = SlotNames[i].ToUpperInvariant();
                int slot = i;
                t.Button.onClick.AddListener(() => Select(_worn[slot].Item));
                _worn[i] = t;
            }

            // Detail card of the selected piece.
            Image card = Ui.Framed("Card", canvas, 0.04f, 0.46f, 0.96f, 0.735f, Palette.PanelDark);
            Transform c = card.transform;
            _name = Ui.Title("Name", c, 0.04f, 0.84f, 0.96f, 0.98f, "", 40, TextAnchor.MiddleLeft, Palette.Parchment);
            _info = Ui.Label("Info", c, 0.04f, 0.75f, 0.96f, 0.84f, "", 26, TextAnchor.MiddleLeft, Palette.Muted);
            _stats = Ui.Label("Stats", c, 0.04f, 0.64f, 0.96f, 0.75f, "", 28, TextAnchor.MiddleLeft, Palette.Parchment);
            _compare = Ui.Label("Compare", c, 0.04f, 0.54f, 0.96f, 0.64f, "", 26, TextAnchor.MiddleLeft, Palette.Parchment);
            _etchings = Ui.Label("Etchings", c, 0.04f, 0.13f, 0.96f, 0.53f, "", 24, TextAnchor.UpperLeft, Palette.Parchment);
            _sockets = Ui.Label("Sockets", c, 0.04f, 0.02f, 0.96f, 0.13f, "", 22, TextAnchor.MiddleLeft, Palette.Muted);
            foreach (Text t in new[] { _stats, _compare, _etchings, _sockets }) t.supportRichText = true;

            _equipButton = Ui.Button("Equip", canvas, 0.04f, 0.405f, 0.40f, 0.452f, "EQUIP", 30, Palette.Safe, Equip, out _equipLabel);
            _forgeButton = Ui.Button("ForgeTurn", canvas, 0.42f, 0.405f, 0.96f, 0.452f, "FORGE / TURN", 30, Palette.ButtonForge, SendToForge, out _);

            // The bag: a scrolling grid, best pieces first, with a slot filter.
            _bagTitle = Ui.Title("BagTitle", canvas, 0.04f, 0.365f, 0.50f, 0.397f, "", 30, TextAnchor.MiddleLeft, Palette.Parchment);
            Ui.Button("Filter", canvas, 0.56f, 0.365f, 0.96f, 0.397f, "", 24, Palette.ButtonIdle, CycleFilter, out _filterLabel);
            _bagView = Ui.Rect("BagView", canvas, 0.04f, 0.10f, 0.96f, 0.36f);
            Image viewBack = _bagView.gameObject.AddComponent<Image>();
            viewBack.color = new Color(0f, 0f, 0f, 0.25f);
            _bagView.gameObject.AddComponent<RectMask2D>();
            var scroll = _bagView.gameObject.AddComponent<ScrollRect>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(_bagView, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            _bagGrid = content.gameObject.AddComponent<GridLayoutGroup>();
            _bagGrid.padding = new RectOffset(10, 10, 10, 10);
            _bagGrid.spacing = new Vector2(10f, 10f);
            _bagGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _bagGrid.constraintCount = BagColumns;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = _bagView;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            for (int i = 0; i < MaxBagTiles; i++)
            {
                Tile t = MakeTile(content, "Bag" + i, withCaption: false);
                int index = i;
                t.Button.onClick.AddListener(() => Select(_bag[index].Item));
                t.Rect.gameObject.SetActive(false);
                _bag.Add(t);
            }
            _bagEmpty = Ui.Label("Empty", _bagView, 0.05f, 0.3f, 0.95f, 0.7f, "The bag is empty. Drops from the hunt land here.", 26, TextAnchor.MiddleCenter, Palette.Muted);

            _message = Ui.Label("Message", canvas, 0.05f, 0.072f, 0.95f, 0.097f, "", 24, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.068f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _message.text = "";
            _canvas.SetActive(true);
        }

        private Tile MakeTile(Transform parent, string name, bool withCaption)
        {
            // A dark slot with the item's rarity glowing behind it, under the painted gold slot frame.
            Image rim = Ui.Sliced(name, parent, 0f, 0f, 1f, 1f, "CardFill", new Color(0.07f, 0.07f, 0.11f));
            var t = new Tile { Rect = rim.rectTransform, Rim = rim };
            t.Button = rim.gameObject.AddComponent<Button>();
            t.Button.targetGraphic = rim;
            t.Selected = rim.gameObject.AddComponent<Outline>();
            t.Selected.effectColor = Palette.Sorn;
            t.Selected.effectDistance = new Vector2(5f, -5f);
            t.Selected.enabled = false;
            Image inner = Ui.Panel("Inner", rim.transform, 0.045f, 0.045f, 0.955f, 0.955f, new Color(0f, 0f, 0f, 0f));
            inner.raycastTarget = false;
            t.Glow = Ui.Sliced("Glow", inner.transform, 0.05f, 0.05f, 0.95f, 0.95f, "Glow", Color.clear);
            t.Glow.raycastTarget = false;
            Ui.Sliced("Frame", rim.transform, 0f, 0f, 1f, 1f, "SlotRim", Color.white).raycastTarget = false;
            RectTransform iconBox = Ui.Rect("IconBox", inner.transform, 0.08f, withCaption ? 0.06f : 0.08f, 0.92f, withCaption ? 0.80f : 0.92f);
            t.Icon = Ui.Icon("Icon", iconBox, 0f, 0f, 1f, 1f, "Weapon");
            t.Badge = Ui.Title("Badge", inner.transform, 0.40f, 0.0f, 0.97f, 0.34f, "", 30, TextAnchor.LowerRight, Palette.Parchment);
            t.Caption = withCaption ? Ui.Label("Caption", inner.transform, 0.05f, 0.78f, 0.95f, 0.99f, "", 18, TextAnchor.UpperLeft, Palette.Muted) : null;
            return t;
        }

        private void Select(ItemState item)
        {
            if (item == null) return;
            _selected = item;
            _selectedId = _root.Server.IdOf(item);
            _message.text = "";
        }

        private bool Owns(ItemState item) =>
            item != null && (_root.Session.Equipped(item.Slot) == item || _root.Session.Inventory.Loot.Contains(item));

        /// <summary>The selection survives the server refreshing every item: it is re-found by its server id.</summary>
        private ItemState Selected()
        {
            if (Owns(_selected)) return _selected;
            if (_selectedId != null)
                foreach (KeyValuePair<ItemState, string> pair in _root.Server.ItemIds)
                    if (pair.Value == _selectedId && Owns(pair.Key)) { _selected = pair.Key; return _selected; }
            _selected = _root.Session.Weapon;
            _selectedId = _root.Server.IdOf(_selected);
            return _selected;
        }

        private void Equip()
        {
            ItemState item = Selected();
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

        private void SendToForge()
        {
            ItemState item = Selected();
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

        private void CycleFilter() => _filter = _filter >= 7 ? -1 : _filter + 1;

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            PlayerSession session = _root.Session;
            HeroStats hero = session.Hero;
            _hero.text = $"Level {session.Level}  ·  Attack {hero.Attack}  ·  Defense {hero.Defense}  ·  HP {hero.MaxHp}  ·  Crit {hero.CritChanceBp / 100}%";
            ItemState selected = Selected();
            _classLabel.text = "CLASS: " + session.Class.ToString().ToUpperInvariant() + "  >";

            for (int i = 0; i < 8; i++)
                Fill(_worn[i], session.Equipped((EquipSlot)i), selected, (EquipSlot)i);

            List<ItemState> bag = session.Inventory.Loot
                .Where(x => _filter < 0 || (int)x.Slot == _filter)
                .OrderByDescending(x => x.UpgradeLevel).ThenByDescending(x => (int)x.Rarity).ThenByDescending(x => x.ItemLevel)
                .Take(MaxBagTiles).ToList();
            for (int i = 0; i < _bag.Count; i++)
            {
                bool has = i < bag.Count;
                if (_bag[i].Rect.gameObject.activeSelf != has) _bag[i].Rect.gameObject.SetActive(has);
                if (has) Fill(_bag[i], bag[i], selected, bag[i].Slot);
            }
            float cell = (_bagView.rect.width - 20f - 10f * (BagColumns - 1)) / BagColumns;
            if (cell > 10f && Mathf.Abs(_bagGrid.cellSize.x - cell) > 0.5f) _bagGrid.cellSize = new Vector2(cell, cell);
            _bagEmpty.enabled = bag.Count == 0;
            _bagTitle.text = $"BAG  ·  {session.Inventory.Loot.Count}";
            _filterLabel.text = _filter < 0 ? "SHOW: ALL SLOTS" : "SHOW: " + SlotNames[_filter].ToUpperInvariant();

            ShowDetail(session, selected);
        }

        private static void Fill(Tile t, ItemState item, ItemState selected, EquipSlot slot)
        {
            t.Item = item;
            Texture2D icon = Resources.Load<Texture2D>("Icons/" + slot);
            if (t.Icon.texture != icon) t.Icon.texture = icon;
            t.Icon.enabled = icon != null;
            t.Icon.color = item == null ? new Color(1f, 1f, 1f, 0.18f) : Color.white;
            Color glow = item == null ? Color.clear : RarityColor(item.Rarity);
            glow.a = item == null ? 0f : 0.55f;
            t.Glow.color = glow;
            t.Badge.text = item == null || item.UpgradeLevel == 0 ? "" : "+" + item.UpgradeLevel;
            t.Badge.color = item == null ? Palette.Muted : ForgePanel.LevelColor(item.UpgradeLevel);
            t.Selected.enabled = item != null && item == selected;
            t.Button.interactable = item != null;
        }

        private void ShowDetail(PlayerSession session, ItemState item)
        {
            bool worn = session.Equipped(item.Slot) == item;
            _name.text = $"{item.DisplayName} +{item.UpgradeLevel}";
            _name.color = RarityColor(item.Rarity);
            _info.text = $"{SlotNames[(int)item.Slot]}  ·  Item level {item.ItemLevel}  ·  {item.Rarity}  ·  " + (worn ? "worn" : "in your bag");

            HeroStats bare = HeroFactory.FromEquipment(Array.Empty<ItemState>(), session.Level);
            HeroStats alone = HeroFactory.FromEquipment(new[] { item }, session.Level);
            _stats.text = Stats(alone, bare, signed: false);

            if (worn)
            {
                _compare.text = ConfirmDialog.Tint("Worn now.", Palette.Muted);
            }
            else
            {
                IEnumerable<ItemState> swapped = session.Equipment.Where(e => e.Slot != item.Slot).Append(item);
                string delta = Stats(HeroFactory.FromEquipment(swapped, session.Level), session.Hero, signed: true);
                _compare.text = "If worn:  " + (delta.Length == 0 ? ConfirmDialog.Tint("no change", Palette.Muted) : delta);
            }

            EtchingPool pool = EtchingPool.For(item.Slot);
            var sb = new StringBuilder();
            if (item.Etchings.Count == 0) sb.Append(ConfirmDialog.Tint("No etchings yet.", Palette.Muted));
            for (int i = 0; i < item.Etchings.Count; i++)
            {
                Etching e = item.Etchings[i];
                string line = $"T{e.Tier}   {pool.Entries[e.EntryId].Name}  +{e.Value}";
                sb.Append(e.Tier >= 4 ? ConfirmDialog.Tint(line, Palette.Sorn) : line);
                if (i == item.LockedEtchingIndex) sb.Append(ConfirmDialog.Tint("   (pinned)", Palette.Muted));
                sb.Append('\n');
            }
            _etchings.text = sb.ToString().TrimEnd();

            var sockets = new List<string>();
            foreach (Socket s in item.Sockets)
                sockets.Add(s.Dead ? "Dead Shard" : s.Type == null ? "empty" : SocketRules.Name(s.Type.Value) + " " + Content.KorshardRanks[s.Rank]);
            _sockets.text = sockets.Count == 0 ? "No sockets" : "Sockets: " + string.Join("  ·  ", sockets);

            _equipButton.interactable = !worn;
            _equipLabel.text = worn ? "WORN" : "EQUIP";
            _forgeButton.interactable = !_root.Forge.Busy;
        }

        /// <summary>Attack, Defense, HP and Crit of a against b: the item's own share, or signed changes in green and red.</summary>
        private static string Stats(HeroStats a, HeroStats b, bool signed)
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
            if (!signed && parts.Count == 0) return ConfirmDialog.Tint("No stats", Palette.Muted);
            return string.Join("   ", parts);
        }
    }
}
