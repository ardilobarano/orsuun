using System.Collections.Generic;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// THE BANNERKIN (owner, 28 Sep 2026: picked "Bannerkin companion"; Rules.Bannerkin): a Drumcaller who walks the lane
    /// behind the hero, blessing and healing. Before it joins (level 25): who it is and CALL THE BANNERKIN. After: it
    /// stands on the painted steppe in its robe and drum, with what its gear makes of its two casts; its six pieces in a
    /// row (tap one for its card and FORGE: its gear forges like any piece, Oathbreak and all); and the Bannerkin pieces
    /// in the bag, each with GIVE. Opened from INVENTORY.
    /// </summary>
    public sealed class BannerkinPanel : MonoBehaviour
    {
        private const int BagRows = 12;

        private sealed class Tile
        {
            public Image Back;
            public RawImage Icon;
            public Text Plus, Name;
        }

        private sealed class Row
        {
            public GameObject Root;
            public RawImage Icon;
            public Text Name;
            public Button Give;
            public ItemState Item;
        }

        private GameRoot _root;
        private GameObject _canvas, _joined, _intro;
        private HeroStage _stage;
        private Text _stats, _detail, _message, _bagEmpty;
        private readonly Tile[] _tiles = new Tile[6];
        private readonly Row[] _rows = new Row[BagRows];
        private Button _forge;
        private EquipSlot? _selected;
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        /// <summary>The Bannerkin is a woman Drumcaller, whichever figure the hero is.</summary>
        public static bool KinSecondLook => ItemLooks.SecondLook(HeroClass.Drumcaller, Figure.Woman);

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("BannerkinCanvas", 10).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);
            Ui.Backdrop(canvas, "Gear");
            Ui.Title("Title", canvas, 0.05f, 0.93f, 0.95f, 0.98f, "THE BANNERKIN", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);

            // Before it joins.
            _intro = Ui.Rect("Intro", canvas, 0f, 0f, 1f, 1f).gameObject;
            Ui.Picture("IntroScene", _intro.transform, 0.1f, 0.5f, 0.9f, 0.9f, "Scenes/Trail");
            Ui.Label("IntroText", _intro.transform, 0.08f, 0.3f, 0.92f, 0.49f,
                "A Drumcaller of the steppe will walk behind you: every 18 seconds its Hunter's Blessing sharpens your strikes, every 12 its Mending Song closes your wounds. It wears six pieces of its own, drum to boots, that forge like yours. Bosses and Commander chests drop them; the Salt Exchange trades them.",
                24, TextAnchor.MiddleCenter, Palette.Parchment);
            Ui.Button("Join", _intro.transform, 0.2f, 0.2f, 0.8f, 0.28f, "CALL THE BANNERKIN", 30, Palette.ButtonForge, Join, out _);

            // After.
            _joined = Ui.Rect("Joined", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform j = _joined.transform;
            Ui.Picture("Scene", j, 0.04f, 0.6f, 0.52f, 0.92f, "Scenes/Trail");
            RectTransform stageBox = Ui.Rect("Stage", j, 0.045f, 0.603f, 0.515f, 0.917f);
            _stage = new GameObject("KinStage").AddComponent<HeroStage>();
            _stage.Init(stageBox, HeroStage.Below + new Vector3(180f, 0f, 0f));
            _stage.gameObject.SetActive(false);
            Ui.Framed("StatsBack", j, 0.54f, 0.6f, 0.96f, 0.92f, new Color(0.03f, 0.03f, 0.07f, 0.85f)).raycastTarget = false;
            _stats = Ui.Label("Stats", j, 0.56f, 0.61f, 0.94f, 0.91f, "", 22, TextAnchor.MiddleLeft, Palette.Parchment);
            _stats.supportRichText = true;

            for (int i = 0; i < 6; i++)
            {
                int index = i;
                float x0 = 0.04f + i * 0.155f;
                var t = new Tile { Back = Ui.Framed("Slot" + i, j, x0, 0.47f, x0 + 0.145f, 0.585f, new Color(0.06f, 0.06f, 0.11f, 0.95f)) };
                RectTransform iconBox = Ui.Rect("IconBox", t.Back.transform, 0.12f, 0.3f, 0.88f, 0.95f);
                t.Icon = Ui.Icon("Icon", iconBox, 0f, 0f, 1f, 1f, Bannerkin.Slots[i].ToString());
                t.Plus = Ui.Title("Plus", t.Back.transform, 0.45f, 0.72f, 0.97f, 0.98f, "", 22, TextAnchor.UpperRight, Palette.Sorn);
                t.Name = Ui.Label("Name", t.Back.transform, 0.03f, 0.02f, 0.97f, 0.3f, Bannerkin.SlotName(Bannerkin.Slots[i]).ToUpperInvariant(), 15,
                    TextAnchor.MiddleCenter, Palette.Muted);
                t.Back.gameObject.AddComponent<Button>().onClick.AddListener(() => Select(Bannerkin.Slots[index]));
                _tiles[i] = t;
            }
            Ui.Framed("DetailBack", j, 0.04f, 0.385f, 0.96f, 0.46f, new Color(0.06f, 0.05f, 0.05f, 0.9f)).raycastTarget = false;
            _detail = Ui.Label("Detail", j, 0.06f, 0.39f, 0.7f, 0.455f, "", 20, TextAnchor.MiddleLeft, Palette.Parchment);
            _detail.supportRichText = true;
            _forge = Ui.Button("Forge", j, 0.72f, 0.395f, 0.95f, 0.45f, "FORGE", 24, Palette.ButtonForge, ForgeSelected, out _);

            Ui.Section("BagHead", j, 0.15f, 0.335f, 0.85f, 0.375f, "BANNERKIN PIECES IN THE BAG", 22);
            Ui.Scroll("Bag", j, 0.04f, 0.12f, 0.96f, 0.33f, out RectTransform content);
            for (int i = 0; i < BagRows; i++)
            {
                var row = new Row();
                RectTransform box = new GameObject("Row" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                box.SetParent(content, false);
                box.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
                row.Root = box.gameObject;
                Ui.Framed("Back", box, 0f, 0f, 1f, 1f, new Color(0.07f, 0.06f, 0.1f, 0.92f)).raycastTarget = false;
                RectTransform iconBox = Ui.Rect("IconBox", box, 0.01f, 0.06f, 0.12f, 0.94f);
                row.Icon = Ui.Icon("Icon", iconBox, 0f, 0f, 1f, 1f, "Weapon");
                row.Name = Ui.Label("Name", box, 0.14f, 0.05f, 0.74f, 0.95f, "", 21, TextAnchor.MiddleLeft, Palette.Parchment);
                row.Name.supportRichText = true;
                Row r = row;
                row.Give = Ui.Button("Give", box, 0.76f, 0.12f, 0.98f, 0.88f, "GIVE", 22, Palette.Safe, () => Give(r.Item), out _);
                _rows[i] = row;
            }
            _bagEmpty = Ui.Label("BagEmpty", j, 0.06f, 0.2f, 0.94f, 0.3f, "No Bannerkin pieces in the bag. Bosses and Commander chests drop them; the Salt Exchange sells them.",
                20, TextAnchor.MiddleCenter, Palette.Muted);

            _message = Ui.Label("Message", canvas, 0.05f, 0.075f, 0.95f, 0.115f, "", 21, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.07f, "BACK", 28, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _root.Tips.Offer(TipCard.Tip.Bannerkin);
            _canvas.SetActive(true);
            _message.text = _root.Server.Online ? "" : "The Bannerkin needs the server.";
            _selected = null;
        }

        public void Close()
        {
            _stage.Show(null, 0, 0, null);
            _stage.gameObject.SetActive(false);
            _canvas.SetActive(false);
        }

        private void Join()
        {
            if (_busy || !_root.Server.Online) return;
            if (!_root.Unlocked(Feature.Bannerkin)) { _message.text = Unlocks.Locked(Feature.Bannerkin); return; }
            _busy = true;
            StartCoroutine(_root.Server.KinJoin(error =>
            {
                _busy = false;
                _message.text = error ?? "The Bannerkin takes up its drum and falls in behind you.";
                if (error == null) GameAudio.Instance?.Play("LaneLevelUp", 0.8f);
            }));
        }

        private void Give(ItemState item)
        {
            if (_busy || item == null) return;
            string id = _root.Server.IdOf(item);
            if (id == null) return;
            _busy = true;
            StartCoroutine(_root.Server.KinWear(id, error =>
            {
                _busy = false;
                _message.text = error ?? "The Bannerkin takes the " + Bannerkin.SlotName(item.Slot).ToLowerInvariant() + ".";
            }));
        }

        private void Select(EquipSlot slot) => _selected = slot;

        private void ForgeSelected()
        {
            ItemState piece = _selected is EquipSlot s ? _root.Session.KinPiece(s) : null;
            if (piece == null) return;
            _root.Session.PutOnAnvil(piece);
            Close();
            _root.Forge.Open();
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            PlayerSession s = _root.Session;
            bool joined = s.KinJoined;
            _intro.SetActive(!joined);
            _joined.SetActive(joined);
            _stage.gameObject.SetActive(joined);
            if (!joined) return;

            ItemState robe = s.KinPiece(EquipSlot.Armor), drum = s.KinPiece(EquipSlot.Weapon);
            _stage.Show(HeroClass.Drumcaller, robe != null ? ItemLooks.Tier(robe.ItemLevel) : 0, drum != null ? ItemLooks.Tier(drum.ItemLevel) : 0, null,
                robe != null ? UpgradeGlow.ForLevel(robe.UpgradeLevel) : 0f, drum != null ? UpgradeGlow.ForLevel(drum.UpgradeLevel) : 0f, KinSecondLook);

            KinStats kin = Bannerkin.Stats(s.KinWorn);
            _stats.text = kin == null ? "" :
                $"<b>Worth {kin.Score}</b>\n\n<color=#FFD66B>Hunter's Blessing</color>\n+{kin.FocusBp / 100}% crit for {Bannerkin.FocusSeconds} s, every {Bannerkin.FocusCooldownSeconds} s"
                + $"\n\n<color=#8CF08C>Mending Song</color>\n{kin.HealPercent}% of your HP back, every {Bannerkin.HealCooldownSeconds} s";

            for (int i = 0; i < 6; i++)
            {
                EquipSlot slot = Bannerkin.Slots[i];
                ItemState piece = s.KinPiece(slot);
                Tile t = _tiles[i];
                Ui.SetIcon(t.Icon, piece != null ? Ui.ItemIcon(piece) : slot.ToString());
                t.Icon.color = piece != null ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                t.Plus.text = piece != null && piece.UpgradeLevel > 0 ? "+" + piece.UpgradeLevel : "";
                t.Plus.color = piece != null && piece.UpgradeLevel >= 7 ? ForgePanel.LevelColor(piece.UpgradeLevel) : Palette.Sorn;
                t.Back.color = _selected == slot ? new Color(0.3f, 0.2f, 0.07f, 0.95f) : new Color(0.06f, 0.06f, 0.11f, 0.95f);
            }
            ItemState chosen = _selected is EquipSlot sel ? s.KinPiece(sel) : null;
            _detail.text = chosen == null ? ConfirmDialog.Tint("Tap a piece for its card; FORGE takes it to the anvil.", Palette.Muted)
                : $"{ConfirmDialog.Tint(chosen.DisplayName + (chosen.UpgradeLevel > 0 ? " +" + chosen.UpgradeLevel : ""), GearPanel.RarityColor(chosen.Rarity))}"
                  + $"\nitem level {chosen.ItemLevel}  ·  worth {Bannerkin.PieceScore(chosen)}";
            _forge.interactable = chosen != null && !_root.Forge.Busy;

            // The bag's Bannerkin pieces, best first.
            var pieces = new List<ItemState>();
            foreach (ItemState item in s.Inventory.Loot) if (item.Kin) pieces.Add(item);
            pieces.Sort((a, b) => Bannerkin.PieceScore(b).CompareTo(Bannerkin.PieceScore(a)));
            _bagEmpty.gameObject.SetActive(pieces.Count == 0);
            for (int i = 0; i < BagRows; i++)
            {
                Row row = _rows[i];
                bool has = i < pieces.Count;
                row.Root.SetActive(has);
                if (!has) { row.Item = null; continue; }
                ItemState item = pieces[i];
                row.Item = item;
                Ui.SetIcon(row.Icon, Ui.ItemIcon(item));
                ItemState worn = s.KinPiece(item.Slot);
                int delta = Bannerkin.PieceScore(item) - (worn != null ? Bannerkin.PieceScore(worn) : 0);
                string change = delta > 0 ? ConfirmDialog.Tint($"  ▲{delta}", Palette.Good) : delta < 0 ? ConfirmDialog.Tint($"  ▼{-delta}", Palette.Bad) : "";
                row.Name.text = $"{ConfirmDialog.Tint(item.DisplayName + (item.UpgradeLevel > 0 ? " +" + item.UpgradeLevel : ""), GearPanel.RarityColor(item.Rarity))}{change}"
                                + $"\n<size=17><color=#B8A98A>item level {item.ItemLevel}  ·  worth {Bannerkin.PieceScore(item)}</color></size>";
                row.Give.interactable = !_busy;
            }
        }

        /// <summary>Screenshots (-kin): opens it once online.</summary>
        public void OpenForShot() => Open();
    }
}
