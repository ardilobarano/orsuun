using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// Korshards (laid out after docs/concept/screens/mockup-korshards.jpg): the worn piece in a framed card with its
    /// sockets as gem slots, the shards held by rank, a trait for the chosen rank, and SET (70%) for the chosen socket, or
    /// CLEAR to knock a Dead Shard out for sorn.
    /// </summary>
    public sealed class SocketPanel : MonoBehaviour
    {
        private const int MaxSockets = 3;
        /// <summary>Painted gems per rank (docs/concept/icons/korshards-ranks.png, cut by tools/ui/cut_icons.py).</summary>
        public static readonly string[] RankIcons = { "ShardTrooper", "ShardRider", "ShardCaptain", "ShardCommander", "ShardGuard" };

        private GameRoot _root;
        private GameObject _canvas;
        private RawImage _itemIcon;
        private Text _itemLabel;
        private readonly Image[] _socketTiles = new Image[MaxSockets];
        private readonly RawImage[] _socketGems = new RawImage[MaxSockets];
        private readonly Text[] _socketLabels = new Text[MaxSockets];
        private readonly Image[] _rankRows = new Image[SocketRules.RankCount];
        private readonly Text[] _rankNames = new Text[SocketRules.RankCount];
        private readonly Text[] _rankCounts = new Text[SocketRules.RankCount];
        private Text _trait;
        private Button _act;
        private Image _actImage;
        private Text _actLabel;
        private Text _note;
        private Text _message;

        private int _slotIndex;
        private int _socket;
        private int _rank;
        private int _traitIndex;
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("SocketCanvas", 11).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Shards");
            Ui.Title("Title", canvas, 0.05f, 0.925f, 0.95f, 0.975f, "KORSHARDS", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);

            // The piece: its picture in a gold slot, its name, and its sockets as gem slots (tap one to choose it).
            Ui.Framed("ItemCard", canvas, 0.04f, 0.70f, 0.96f, 0.905f, new Color(0.06f, 0.06f, 0.12f, 0.92f));
            Ui.Button("Prev", canvas, 0.055f, 0.765f, 0.135f, 0.84f, "<", 36, Palette.ButtonIdle, () => Step(-1), out _);
            Ui.SlotTile("ItemSlot", canvas, 0.15f, 0.72f, 0.40f, 0.885f, new Color(0.1f, 0.1f, 0.18f));
            RectTransform iconBox = Ui.Rect("ItemIconBox", canvas, 0.165f, 0.73f, 0.385f, 0.875f);
            _itemIcon = Ui.Icon("ItemIcon", iconBox, 0f, 0f, 1f, 1f, "Weapon");
            _itemLabel = Ui.Title("Item", canvas, 0.42f, 0.83f, 0.84f, 0.89f, "", 28, TextAnchor.MiddleLeft, Palette.Parchment);
            for (int i = 0; i < MaxSockets; i++)
            {
                int index = i;
                float x0 = 0.42f + i * 0.14f;
                _socketTiles[i] = Ui.SlotTile("SocketTile" + i, canvas, x0, 0.735f, x0 + 0.125f, 0.815f, new Color(0.04f, 0.04f, 0.08f));
                var tap = _socketTiles[i].gameObject.AddComponent<Button>();
                _socketTiles[i].raycastTarget = true;
                tap.onClick.AddListener(() => _socket = index);
                RectTransform gemBox = Ui.Rect("GemBox", _socketTiles[i].transform, 0.15f, 0.15f, 0.85f, 0.85f);
                _socketGems[i] = Ui.Icon("Gem", gemBox, 0f, 0f, 1f, 1f, RankIcons[0]);
                _socketLabels[i] = Ui.Title("Label", _socketTiles[i].transform, 0f, 0f, 1f, 1f, "", 20, TextAnchor.MiddleCenter, Palette.Muted);
            }
            Ui.Button("Next", canvas, 0.865f, 0.765f, 0.945f, 0.84f, ">", 36, Palette.ButtonIdle, () => Step(1), out _);

            // The shards held, one row per rank (tap one to choose it).
            Ui.Section("ShardsHead", canvas, 0.1f, 0.652f, 0.9f, 0.69f, "SHARDS", 30);
            for (int r = 0; r < SocketRules.RankCount; r++)
            {
                int rank = r;
                float y1 = 0.645f - r * 0.058f;
                _rankRows[r] = Ui.Framed("Rank" + r, canvas, 0.06f, y1 - 0.054f, 0.94f, y1, new Color(0.07f, 0.07f, 0.13f, 0.95f));
                _rankRows[r].gameObject.AddComponent<Button>().onClick.AddListener(() => _rank = rank);
                _rankRows[r].gameObject.AddComponent<Press>();
                Transform row = _rankRows[r].transform;
                Ui.SlotTile("GemSlot", row, 0.015f, 0.08f, 0.13f, 0.92f, new Color(0.04f, 0.04f, 0.08f));
                RectTransform gemBox = Ui.Rect("GemBox", row, 0.025f, 0.14f, 0.12f, 0.86f);
                Ui.Icon("Gem", gemBox, 0f, 0f, 1f, 1f, RankIcons[r]);
                _rankNames[r] = Ui.Title("Name", row, 0.16f, 0.1f, 0.78f, 0.9f, "", 26, TextAnchor.MiddleLeft, Palette.Parchment);
                _rankNames[r].supportRichText = true;
                _rankCounts[r] = Ui.Title("Count", row, 0.78f, 0.1f, 0.97f, 0.9f, "", 32, TextAnchor.MiddleRight, Palette.Sorn);
            }

            // The trait the shard carries (weapons and armour have their own), then SET or CLEAR.
            Ui.Button("TraitPrev", canvas, 0.06f, 0.294f, 0.16f, 0.35f, "<", 32, Palette.ButtonIdle, () => _traitIndex--, out _);
            Ui.Framed("TraitBack", canvas, 0.17f, 0.294f, 0.83f, 0.35f, new Color(0.07f, 0.07f, 0.13f, 0.95f));
            _trait = Ui.Title("Trait", canvas, 0.19f, 0.297f, 0.81f, 0.347f, "", 26, TextAnchor.MiddleCenter, Palette.Parchment);
            Ui.Button("TraitNext", canvas, 0.84f, 0.294f, 0.94f, 0.35f, ">", 32, Palette.ButtonIdle, () => _traitIndex++, out _);

            _act = Ui.Button("Act", canvas, 0.08f, 0.205f, 0.92f, 0.283f, "", 32, Palette.Alloy, Act, out _actLabel);
            _actImage = _act.GetComponent<Image>();
            _note = Ui.Label("Note", canvas, 0.08f, 0.168f, 0.92f, 0.2f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);
            _message = Ui.Label("Message", canvas, 0.05f, 0.09f, 0.95f, 0.16f, "", 26, TextAnchor.MiddleCenter, Palette.Sorn);
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _message.text = "";
            _canvas.SetActive(true);
        }

        private void Step(int delta)
        {
            for (int i = 0; i < 8; i++)
            {
                _slotIndex = (_slotIndex + delta + 8) % 8;
                if (_root.Session.Equipped((EquipSlot)_slotIndex) != null) break;
            }
            _socket = 0;
        }

        private ItemState Current => _root.Session.Equipped((EquipSlot)_slotIndex);

        private ShardType ChosenTrait(ItemState item)
        {
            ShardType[] options = SocketRules.ShardsFor(item.Slot);
            return options[((_traitIndex % options.Length) + options.Length) % options.Length];
        }

        private void Act()
        {
            ItemState item = Current;
            if (item == null || _busy || _socket >= item.Sockets.Length) return;
            int socketIndex = _socket;
            Socket socket = item.Sockets[socketIndex];

            if (socket.Dead)
            {
                string blocker = _root.Session.ClearSocketBlocker(item, socketIndex);
                if (blocker != null) { Say(blocker, Palette.Muted); return; }
                if (_root.Server.Online)
                {
                    _busy = true;
                    StartCoroutine(_root.Server.SocketClear(_root.Server.ItemIds[item], socketIndex, text => { Say(text, Palette.Sorn); _busy = false; }));
                }
                else
                {
                    _root.Session.ClearSocket(item, socketIndex);
                    Say("Dead Shard removed.", Palette.Sorn);
                }
                return;
            }

            if (!socket.IsEmpty) return;
            ShardType type = ChosenTrait(item);
            int rank = _rank;
            string insertBlocker = _root.Session.SocketBlocker(item, socketIndex, type, rank);
            if (insertBlocker != null) { Say(insertBlocker, Palette.Muted); return; }

            if (_root.Server.Online)
            {
                _busy = true;
                StartCoroutine(_root.Server.SocketInsert(_root.Server.ItemIds[item], socketIndex, type, rank, (ok, text) =>
                {
                    Say(text, ok ? Palette.Good : Palette.Bad);
                    _busy = false;
                }));
            }
            else
            {
                bool ok = _root.Session.SetShard(item, socketIndex, type, rank);
                Say(ok ? "Shard set: " + SocketRules.Describe(type, rank) : "The shard shattered. A Dead Shard blocks the socket.", ok ? Palette.Good : Palette.Bad);
            }
        }

        private void Say(string text, Color color)
        {
            _message.text = text;
            _message.color = color;
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            PlayerSession session = _root.Session;
            ItemState item = Current;
            if (item == null) { Step(1); item = Current; }
            if (item == null) return;

            Ui.SetIcon(_itemIcon, item.Slot.ToString());
            _itemLabel.text = $"{item.DisplayName} +{item.UpgradeLevel}";
            _itemLabel.color = ForgePanel.LevelColor(item.UpgradeLevel);
            if (_socket >= item.Sockets.Length) _socket = 0;

            for (int i = 0; i < MaxSockets; i++)
            {
                bool has = i < item.Sockets.Length;
                _socketTiles[i].gameObject.SetActive(has);
                if (!has) continue;
                Socket s = item.Sockets[i];
                string gem = s.Dead ? "DeadShard" : s.Type == null ? null : RankIcons[s.Rank];
                _socketGems[i].enabled = gem != null;
                if (gem != null) Ui.SetIcon(_socketGems[i], gem);
                _socketLabels[i].text = s.Dead || s.Type != null ? "" : "EMPTY";
                _socketTiles[i].color = i == _socket ? new Color(0.45f, 0.34f, 0.1f) : new Color(0.04f, 0.04f, 0.08f);
            }

            Socket chosen = item.Sockets.Length > 0 ? item.Sockets[_socket] : Socket.DeadShard;
            ShardType type = ChosenTrait(item);
            for (int r = 0; r < SocketRules.RankCount; r++)
            {
                _rankNames[r].text = Content.KorshardRanks[r] + "\n<size=19><color=#C2BAAD>" + SocketRules.Describe(type, r) + "</color></size>";
                _rankCounts[r].text = "×" + session.Inventory.Korshards[r];
                _rankRows[r].color = r == _rank ? new Color(0.36f, 0.27f, 0.08f, 0.95f) : new Color(0.07f, 0.07f, 0.13f, 0.95f);
            }
            _trait.text = SocketRules.Name(type) + "  ·  " + SocketRules.Describe(type, _rank);

            if (item.Sockets.Length == 0)
            {
                _actLabel.text = "NO SOCKETS ON THIS PIECE";
                _actImage.color = Palette.ButtonIdle;
                _act.interactable = false;
                _note.text = "Rarer pieces carry more sockets.";
                return;
            }
            if (chosen.Dead)
            {
                _actLabel.text = $"CLEAR DEAD SHARD  ·  {SocketRules.ClearCost(item):N0} sorn";
                _actImage.color = Palette.Danger;
                _note.text = "A Dead Shard blocks the socket until it is knocked out.";
            }
            else if (chosen.IsEmpty)
            {
                _actLabel.text = $"SET {Content.KorshardRanks[_rank].ToUpperInvariant()}  ·  {SocketRules.InsertSuccessBp / 100}% chance";
                _actImage.color = Palette.Alloy;
                _note.text = "A failed set leaves a Dead Shard in the socket.";
            }
            else
            {
                _actLabel.text = $"SOCKET {_socket + 1} HOLDS {SocketRules.Name(chosen.Type.Value).ToUpperInvariant()} {Content.KorshardRanks[chosen.Rank].ToUpperInvariant()}";
                _actImage.color = Palette.ButtonIdle;
                _note.text = SocketRules.Describe(chosen.Type.Value, chosen.Rank);
            }
            _act.interactable = !_busy && (chosen.Dead || chosen.IsEmpty);
        }
    }
}
