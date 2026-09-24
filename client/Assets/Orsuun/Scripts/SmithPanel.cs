using System;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The Chained Smith on the Hollow Spire's sixth floor (Rules.Dungeons, ForgeMethod.ChainedSmith): one forge of a worn
    /// piece at +10 points of success, paid like the Forge and failing by the Forge's rule, or WALK ON. The answer goes
    /// back to GameRoot, which asks the server and replays the rest of the run.
    /// </summary>
    public sealed class SmithPanel : MonoBehaviour
    {
        private GameRoot _root;
        private GameObject _canvas;
        private readonly Image[] _tiles = new Image[8];
        private readonly Button[] _slots = new Button[8];
        private readonly RawImage[] _icons = new RawImage[8];
        private readonly Text[] _levels = new Text[8];
        private Text _piece;
        private Text _odds;
        private Text _risk;
        private Button _forge;
        private int _slot;
        private Action<string> _answer;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("SmithCanvas", 16).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Panel("Dim", canvas, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.78f));
            Ui.Framed("Box", canvas, 0.04f, 0.2f, 0.96f, 0.8f, new Color(0.07f, 0.06f, 0.11f, 0.98f));
            Ui.Title("Title", canvas, 0.08f, 0.72f, 0.92f, 0.77f, "THE CHAINED SMITH", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true, ribbon: false);
            Ui.Label("Flavour", canvas, 0.08f, 0.68f, 0.92f, 0.72f, "\"Chained to this anvil since the Spire rose. One piece from each climber. Choose.\"",
                22, TextAnchor.MiddleCenter, Palette.Muted).fontStyle = FontStyle.Italic;

            for (int i = 0; i < _tiles.Length; i++)
            {
                int slot = i;
                float x0 = 0.07f + i * 0.108f;
                _tiles[i] = Ui.SlotTile("Tile" + i, canvas, x0, 0.585f, x0 + 0.1f, 0.655f, Palette.PanelDark);
                Image hit = Ui.Panel("Slot" + i, canvas, x0, 0.585f, x0 + 0.1f, 0.655f, Color.clear);
                _slots[i] = hit.gameObject.AddComponent<Button>();
                _slots[i].onClick.AddListener(() => _slot = slot);
                hit.gameObject.AddComponent<Press>();
                _icons[i] = Ui.Icon("Icon", hit.transform, 0.14f, 0.26f, 0.86f, 0.92f, ((EquipSlot)i).ToString());
                _levels[i] = Ui.Title("Level", hit.transform, 0.1f, 0.02f, 0.92f, 0.34f, "", 18, TextAnchor.MiddleRight, Palette.Parchment);
            }

            _piece = Ui.Title("Piece", canvas, 0.08f, 0.51f, 0.92f, 0.57f, "", 34, TextAnchor.MiddleCenter, Palette.Parchment);
            _odds = Ui.Label("Odds", canvas, 0.08f, 0.43f, 0.92f, 0.51f, "", 26, TextAnchor.MiddleCenter, Palette.Parchment);
            _odds.supportRichText = true;
            _risk = Ui.Label("Risk", canvas, 0.08f, 0.35f, 0.92f, 0.43f, "", 24, TextAnchor.MiddleCenter, Palette.Warn);
            _forge = Ui.Button("Forge", canvas, 0.08f, 0.24f, 0.49f, 0.32f, "FORGE AT THE SMITH", 26, Palette.Danger, Forge, out _);
            Ui.Button("WalkOn", canvas, 0.51f, 0.24f, 0.92f, 0.32f, "WALK ON", 28, Palette.ButtonIdle, () => Answer(""), out _);
            _canvas.SetActive(false);
        }

        /// <summary>Opens the smith; <paramref name="answer"/> gets the server id of the piece to forge, or "" to walk on.</summary>
        public void Open(Action<string> answer)
        {
            _answer = answer;
            _slot = (int)EquipSlot.Weapon;
            _canvas.SetActive(true);
        }

        private void Forge()
        {
            ItemState piece = _root.Session.Equipped((EquipSlot)_slot);
            if (piece == null || piece.UpgradeLevel >= ItemState.MaxUpgradeLevel) return;
            Answer(_root.Server.IdOf(piece) ?? "");
        }

        private void Answer(string itemId)
        {
            _canvas.SetActive(false);
            Action<string> answer = _answer;
            _answer = null;
            answer?.Invoke(itemId);
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            PlayerSession session = _root.Session;
            for (int i = 0; i < _slots.Length; i++)
            {
                ItemState item = session.Equipped((EquipSlot)i);
                _levels[i].text = item == null ? "" : "+" + item.UpgradeLevel;
                _levels[i].color = item == null ? Palette.Muted : ForgePanel.LevelColor(item.UpgradeLevel);
                _icons[i].color = item == null ? new Color(1f, 1f, 1f, 0.22f) : Color.white;
                _slots[i].interactable = item != null;
                _tiles[i].color = i == _slot ? new Color(0.85f, 0.62f, 0.2f) : item == null ? new Color(0.06f, 0.06f, 0.09f) : Palette.PanelDark;
            }

            ItemState piece = session.Equipped((EquipSlot)_slot);
            if (piece == null)
            {
                _piece.text = "Choose a worn piece";
                _odds.text = _risk.text = "";
                _forge.interactable = false;
                return;
            }
            _piece.text = $"{piece.DisplayName} +{piece.UpgradeLevel}";
            _piece.color = ForgePanel.LevelColor(piece.UpgradeLevel);
            if (piece.UpgradeLevel >= ItemState.MaxUpgradeLevel)
            {
                _odds.text = "It has sworn all nine oaths already.";
                _risk.text = "";
                _forge.interactable = false;
                return;
            }
            int chance = new ForgeService().ChanceBp(piece, ForgeMethod.ChainedSmith) / 100;
            long cost = ForgeRules.Cost(piece.ItemLevel, piece.UpgradeLevel);
            int materials = ForgeRules.MaterialsNeeded(piece.UpgradeLevel + 1);
            bool canPay = session.Inventory.Sorn >= cost && session.Inventory.Materials >= materials;
            _odds.text = $"To +{piece.UpgradeLevel + 1}:  {ConfirmDialog.Tint(chance + "%", Palette.Good)} with the smith's hand\nCost {cost:N0} sorn"
                         + (materials > 0 ? $"  ·  {materials} materials" : "") + (canPay ? "" : "  ·  " + ConfirmDialog.Tint("not enough", Palette.Bad));
            bool breaks = piece.UpgradeLevel + 1 >= ForgeRules.FirstOathbreakTarget;
            _risk.text = breaks ? $"If it fails, your +{piece.UpgradeLevel} {piece.DisplayName} is destroyed." : $"If it fails, it drops to +{Mathf.Max(0, piece.UpgradeLevel - 1)}.";
            _risk.color = breaks ? Palette.Bad : Palette.Warn;
            _forge.interactable = canPay;
        }
    }
}
