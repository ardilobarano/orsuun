using System.Collections.Generic;
using System.Linq;
using System.Text;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>Equipped pieces on top, the best loose drops below with an EQUIP button each.</summary>
    public sealed class GearPanel : MonoBehaviour
    {
        private const int LooseRows = 8;

        private GameRoot _root;
        private GameObject _canvas;
        private Text _equipped;
        private Text _hero;
        private Text _message;
        private readonly Text[] _rowLabels = new Text[LooseRows];
        private readonly Button[] _rowButtons = new Button[LooseRows];
        private readonly ItemState[] _rowItems = new ItemState[LooseRows];

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("GearCanvas", 9).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Panel("Dim", canvas, 0f, 0f, 1f, 1f, new Color(0.04f, 0.04f, 0.05f, 0.94f));
            Ui.Label("Title", canvas, 0.05f, 0.925f, 0.95f, 0.975f, "GEAR", 40, TextAnchor.MiddleCenter, Palette.Warn);
            _hero = Ui.Label("Hero", canvas, 0.05f, 0.885f, 0.95f, 0.925f, "", 28, TextAnchor.MiddleCenter, Palette.Muted);

            Ui.Panel("EquippedBack", canvas, 0.04f, 0.60f, 0.96f, 0.88f, Palette.PanelDark);
            _equipped = Ui.Label("Equipped", canvas, 0.06f, 0.605f, 0.94f, 0.875f, "", 26, TextAnchor.UpperLeft, Color.white);

            Ui.Label("LooseTitle", canvas, 0.05f, 0.555f, 0.95f, 0.595f, "LOOT  ·  best pieces first", 26, TextAnchor.MiddleLeft, Palette.Muted);
            for (int i = 0; i < LooseRows; i++)
            {
                int row = i;
                float y1 = 0.55f - i * 0.057f;
                float y0 = y1 - 0.052f;
                Ui.Panel("RowBack" + i, canvas, 0.04f, y0, 0.74f, y1, Palette.PanelDark);
                _rowLabels[i] = Ui.Label("Row" + i, canvas, 0.06f, y0, 0.73f, y1, "", 24, TextAnchor.MiddleLeft, Color.white);
                _rowButtons[i] = Ui.Button("Equip" + i, canvas, 0.76f, y0, 0.96f, y1, "EQUIP", 24, Palette.Safe, () => Equip(row), out _);
            }

            _message = Ui.Label("Message", canvas, 0.05f, 0.075f, 0.95f, 0.095f, "", 24, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.07f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _message.text = "";
            _canvas.SetActive(true);
        }

        private void Equip(int row)
        {
            ItemState item = _rowItems[row];
            if (item == null) return;

            if (_root.Server.Online)
            {
                if (!_root.Server.ItemIds.TryGetValue(item, out string id)) return;
                StartCoroutine(_root.Server.Equip(id, error => _message.text = error ?? "Equipped."));
            }
            else
            {
                _root.Session.Equip(item);
                _message.text = "Equipped.";
            }
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            PlayerSession session = _root.Session;

            HeroStats hero = session.Hero;
            _hero.text = $"Attack {hero.Attack}  ·  Defense {hero.Defense}  ·  HP {hero.MaxHp}  ·  Crit {hero.CritChanceBp / 100}%";

            var sb = new StringBuilder();
            for (int s = 0; s < 8; s++)
            {
                ItemState item = session.Equipped((EquipSlot)s);
                sb.Append(((EquipSlot)s).ToString().PadRight(9)).Append("  ");
                sb.Append(item == null ? "—" : Describe(item)).Append('\n');
            }
            _equipped.text = sb.ToString().TrimEnd();

            List<ItemState> loose = session.Inventory.Loot.OrderByDescending(i => (int)i.Rarity).ThenByDescending(i => i.ItemLevel).Take(LooseRows).ToList();
            for (int i = 0; i < LooseRows; i++)
            {
                bool has = i < loose.Count;
                _rowItems[i] = has ? loose[i] : null;
                _rowLabels[i].text = has ? Describe(loose[i]) : "";
                _rowButtons[i].gameObject.SetActive(has);
            }
        }

        private static string Describe(ItemState item)
        {
            EtchingPool pool = EtchingPool.For(item.Slot);
            string etchings = string.Join(", ", item.Etchings.Select(e => pool.Entries[e.EntryId].Name + " +" + e.Value));
            return $"{item.DisplayName} +{item.UpgradeLevel}  Lv{item.ItemLevel}" + (etchings.Length > 0 ? "  ·  " + etchings : "");
        }
    }
}
