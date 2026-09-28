using System.Globalization;
using System.Text;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// INSPECT (owner, 28 Sep 2026: "Inspect a hero"): another hero as anyone may see them, from a name in chat, a row on the
    /// leaderboards or the Pits' board. The hero on the painted stage in their gear (its looks and glow), their title, class,
    /// level, Banner and guild, how far they have come, and the eight worn pieces; tapping a piece shows its etchings.
    /// </summary>
    public sealed class InspectPanel : MonoBehaviour
    {
        private static readonly EquipSlot[] Slots =
        {
            EquipSlot.Weapon, EquipSlot.Armor, EquipSlot.Helmet, EquipSlot.Shield,
            EquipSlot.Necklace, EquipSlot.Earrings, EquipSlot.Bracelet, EquipSlot.Shoes,
        };

        private sealed class Tile
        {
            public Image Back;
            public RawImage Icon;
            public Text Plus, Name;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private HeroStage _stage;
        private Text _name, _line, _guild, _stats, _detail, _message;
        private readonly Tile[] _tiles = new Tile[8];
        private InspectDto _hero;
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("InspectCanvas", 36).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);
            Ui.Backdrop(canvas, "Trail");
            _name = Ui.Title("Name", canvas, 0.05f, 0.925f, 0.95f, 0.975f, "", 40, TextAnchor.MiddleCenter, Palette.Sorn);
            Ui.Raw(_name);
            _line = Ui.Label("Line", canvas, 0.05f, 0.893f, 0.95f, 0.925f, "", 22, TextAnchor.MiddleCenter, Palette.Parchment);
            _line.supportRichText = true;
            _guild = Ui.Label("Guild", canvas, 0.05f, 0.863f, 0.95f, 0.893f, "", 21, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Raw(_guild);
            Ui.Picture("Scene", canvas, 0.2f, 0.5f, 0.8f, 0.855f, "Scenes/Trail");
            RectTransform stageBox = Ui.Rect("Stage", canvas, 0.205f, 0.504f, 0.795f, 0.851f);
            _stage = new GameObject("InspectStage").AddComponent<HeroStage>();
            _stage.Init(stageBox, HeroStage.Below + new Vector3(120f, 0f, 0f));
            _stage.gameObject.SetActive(false);
            _stats = Ui.Label("Stats", canvas, 0.05f, 0.455f, 0.95f, 0.495f, "", 22, TextAnchor.MiddleCenter, Palette.Parchment);
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                float x0 = 0.03f + (i % 4) * 0.2375f, y1 = i < 4 ? 0.445f : 0.325f;
                var t = new Tile { Back = Ui.Framed("Slot" + i, canvas, x0, y1 - 0.11f, x0 + 0.225f, y1, new Color(0.06f, 0.06f, 0.11f, 0.95f)) };
                RectTransform iconBox = Ui.Rect("IconBox", t.Back.transform, 0.2f, 0.32f, 0.8f, 0.95f);
                t.Icon = Ui.Icon("Icon", iconBox, 0f, 0f, 1f, 1f, Slots[i].ToString());
                t.Plus = Ui.Title("Plus", t.Back.transform, 0.55f, 0.72f, 0.97f, 0.97f, "", 24, TextAnchor.UpperRight, Palette.Sorn);
                t.Name = Ui.Label("Name", t.Back.transform, 0.04f, 0.03f, 0.96f, 0.32f, "", 15, TextAnchor.MiddleCenter, Palette.Parchment);
                t.Back.gameObject.AddComponent<Button>().onClick.AddListener(() => ShowPiece(index));
                _tiles[i] = t;
            }
            Ui.Framed("DetailBack", canvas, 0.03f, 0.09f, 0.97f, 0.205f, new Color(0.06f, 0.05f, 0.05f, 0.92f)).raycastTarget = false;
            _detail = Ui.Label("Detail", canvas, 0.06f, 0.095f, 0.94f, 0.2f, "", 19, TextAnchor.MiddleCenter, Palette.Parchment);
            _detail.supportRichText = true;
            _message = Ui.Label("Message", canvas, 0.05f, 0.075f, 0.95f, 0.09f, "", 18, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.07f, "BACK", 28, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);
        }

        public void Open(string heroId)
        {
            if (string.IsNullOrEmpty(heroId) || _busy) return;
            if (!_root.Server.Online) { _root.Hud.Log("Inspecting a hero needs the server."); return; }
            _canvas.SetActive(true);
            _stage.gameObject.SetActive(true);
            _hero = null;
            Fill();
            _message.text = "...";
            _busy = true;
            StartCoroutine(_root.Server.Inspect(heroId, (dto, error) =>
            {
                _busy = false;
                _message.text = error ?? "";
                _hero = dto;
                Fill();
            }));
        }

        public void Close()
        {
            _stage.Show(null, 0, 0, null);
            _stage.gameObject.SetActive(false);
            _canvas.SetActive(false);
        }

        private static HeroClass ClassOf(InspectDto h) => System.Enum.TryParse(h.@class, out HeroClass c) ? c : HeroClass.Vanguard;

        private ItemState Piece(EquipSlot slot)
        {
            if (_hero?.worn == null) return null;
            foreach (ItemDto dto in _hero.worn)
                if (dto.slot == slot.ToString()) return ToState(dto);
            return null;
        }

        private void Fill()
        {
            InspectDto h = _hero;
            _detail.text = h == null ? "" : ConfirmDialog.Tint("Tap a piece to see its etchings.", Palette.Muted);
            if (h == null)
            {
                _name.text = _line.text = _guild.text = _stats.text = "";
                _stage.Show(null, 0, 0, null);
                foreach (Tile t in _tiles) { t.Icon.enabled = false; t.Plus.text = t.Name.text = ""; }
                return;
            }
            HeroClass cls = ClassOf(h);
            Figure figure = System.Enum.TryParse(h.figure, out Figure f) ? f : ItemLooks.NativeFigure(cls);
            _name.text = h.name;
            string title = string.IsNullOrEmpty(h.title) ? "" : ConfirmDialog.Tint("‹" + h.title + "›", Palette.Sorn) + "  ·  ";
            _line.text = $"{title}{cls}  ·  Lv {h.level}  ·  {BannerLook.Name(BannerLook.Parse(h.banner))}" + (h.banned ? "  ·  " + ConfirmDialog.Tint("BANNED", Palette.Bad) : "");
            _guild.text = string.IsNullOrEmpty(h.guildTag) ? "" : $"[{h.guildTag}] {h.guildName}";
            _stats.text = $"Furthest: {Content.StageName(System.Math.Max(1, h.highestStage))}  ·  Pit rating {h.pitRating} ({h.pitWins} wins)";
            ItemState armor = Piece(EquipSlot.Armor), weapon = Piece(EquipSlot.Weapon);
            _stage.Show(cls, armor != null ? ItemLooks.Tier(armor.ItemLevel) : 0, weapon != null ? ItemLooks.Tier(weapon.ItemLevel) : 0,
                string.IsNullOrEmpty(h.skin) ? null : h.skin, armor != null ? UpgradeGlow.ForLevel(armor.UpgradeLevel) : 0f,
                weapon != null ? UpgradeGlow.ForLevel(weapon.UpgradeLevel) : 0f, ItemLooks.SecondLook(cls, figure));
            for (int i = 0; i < Slots.Length; i++)
            {
                Tile t = _tiles[i];
                ItemState piece = Piece(Slots[i]);
                Ui.SetIcon(t.Icon, piece != null ? Ui.ItemIcon(piece.Slot, piece.ItemLevel, cls) : Slots[i].ToString());
                t.Icon.color = piece != null ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                t.Plus.text = piece != null && piece.UpgradeLevel > 0 ? "+" + piece.UpgradeLevel : "";
                t.Plus.color = piece != null && piece.UpgradeLevel >= 7 ? ForgePanel.LevelColor(piece.UpgradeLevel) : Palette.Sorn;
                t.Name.text = piece != null ? Content.ItemName(piece, cls) : "empty";
                t.Name.color = piece != null ? GearPanel.RarityColor(piece.Rarity) : Palette.Muted;
            }
        }

        private void ShowPiece(int index)
        {
            if (_hero == null) return;
            ItemState piece = Piece(Slots[index]);
            if (piece == null) { _detail.text = ConfirmDialog.Tint("Nothing worn there.", Palette.Muted); return; }
            var sb = new StringBuilder();
            sb.Append(ConfirmDialog.Tint(Content.ItemName(piece, ClassOf(_hero)) + (piece.UpgradeLevel > 0 ? " +" + piece.UpgradeLevel : ""), GearPanel.RarityColor(piece.Rarity)))
              .Append("   ").Append(ConfirmDialog.Tint("item level " + piece.ItemLevel.ToString(CultureInfo.InvariantCulture), Palette.Muted)).Append('\n');
            EtchingPool pool = EtchingPool.For(piece.Slot);
            if (piece.Etchings.Count == 0) sb.Append(ConfirmDialog.Tint("No etchings.", Palette.Muted));
            for (int i = 0; i < piece.Etchings.Count; i++)
            {
                Etching e = piece.Etchings[i];
                string line = $"T{e.Tier} {pool.Entries[e.EntryId].Name} +{e.Value}";
                sb.Append(e.Tier >= 4 ? ConfirmDialog.Tint(line, Palette.Sorn) : line).Append(i < piece.Etchings.Count - 1 ? "   ·   " : "");
            }
            _detail.text = sb.ToString();
        }
    }
}
