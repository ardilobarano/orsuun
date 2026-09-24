using System.Text;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// Korshards: pick an equipped item, pick a shard trait and rank for an empty socket, SET it (70%), or
    /// knock a Dead Shard out for sorn.
    /// </summary>
    public sealed class SocketPanel : MonoBehaviour
    {
        private const int MaxSockets = 3;

        private GameRoot _root;
        private GameObject _canvas;
        private Text _itemLabel;
        private Text _shardsLabel;
        private Text _message;
        private readonly Text[] _socketLabels = new Text[MaxSockets];
        private readonly Button[] _typeButtons = new Button[MaxSockets];
        private readonly Text[] _typeLabels = new Text[MaxSockets];
        private readonly Button[] _rankButtons = new Button[MaxSockets];
        private readonly Text[] _rankLabels = new Text[MaxSockets];
        private readonly Button[] _actButtons = new Button[MaxSockets];
        private readonly Text[] _actLabels = new Text[MaxSockets];
        private readonly Image[] _actImages = new Image[MaxSockets];
        private readonly int[] _typeChoice = new int[MaxSockets];
        private readonly int[] _rankChoice = new int[MaxSockets];

        private int _slotIndex;
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("SocketCanvas", 11).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas);
            Ui.Title("Title", canvas, 0.05f, 0.925f, 0.95f, 0.975f, "KORSHARDS", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Label("Hint", canvas, 0.05f, 0.885f, 0.95f, 0.925f, "A set shard takes 70% of the time. A failed one dies in the socket and costs sorn to remove.", 20, TextAnchor.MiddleCenter, Palette.Muted);

            Ui.Button("Prev", canvas, 0.04f, 0.80f, 0.14f, 0.87f, "<", 36, Palette.ButtonIdle, () => Step(-1), out _);
            Ui.Framed("ItemBack", canvas, 0.15f, 0.80f, 0.85f, 0.87f, Palette.PanelDark);
            _itemLabel = Ui.Label("Item", canvas, 0.16f, 0.80f, 0.84f, 0.87f, "", 24, TextAnchor.MiddleCenter, Palette.Parchment);
            Ui.Button("Next", canvas, 0.86f, 0.80f, 0.96f, 0.87f, ">", 36, Palette.ButtonIdle, () => Step(1), out _);

            _shardsLabel = Ui.Label("Shards", canvas, 0.05f, 0.745f, 0.95f, 0.795f, "", 22, TextAnchor.MiddleCenter, Palette.Sorn);

            for (int i = 0; i < MaxSockets; i++)
            {
                int index = i;
                float y1 = 0.72f - i * 0.16f;
                Ui.Framed("SocketBack" + i, canvas, 0.04f, y1 - 0.15f, 0.96f, y1, Palette.PanelDark);
                _socketLabels[i] = Ui.Label("Socket" + i, canvas, 0.06f, y1 - 0.06f, 0.94f, y1, "", 24, TextAnchor.MiddleLeft, Palette.Parchment);
                _typeButtons[i] = Ui.Button("Type" + i, canvas, 0.06f, y1 - 0.14f, 0.42f, y1 - 0.07f, "", 22, Palette.ButtonIdle, () => { _typeChoice[index] = (_typeChoice[index] + 1) % 8; }, out _typeLabels[i]);
                _rankButtons[i] = Ui.Button("Rank" + i, canvas, 0.44f, y1 - 0.14f, 0.68f, y1 - 0.07f, "", 22, Palette.ButtonIdle, () => { _rankChoice[index] = (_rankChoice[index] + 1) % SocketRules.RankCount; }, out _rankLabels[i]);
                _actButtons[i] = Ui.Button("Act" + i, canvas, 0.70f, y1 - 0.14f, 0.94f, y1 - 0.07f, "SET", 24, Palette.Safe, () => Act(index), out _actLabels[i]);
                _actImages[i] = _actButtons[i].GetComponent<Image>();
            }

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
                if (_root.Session.Equipped((EquipSlot)_slotIndex) != null) return;
            }
        }

        private ItemState Current => _root.Session.Equipped((EquipSlot)_slotIndex);

        private void Act(int socketIndex)
        {
            ItemState item = Current;
            if (item == null || _busy || socketIndex >= item.Sockets.Length) return;
            Socket socket = item.Sockets[socketIndex];

            if (socket.Dead)
            {
                string blocker = _root.Session.ClearSocketBlocker(item, socketIndex);
                if (blocker != null) { _message.text = blocker; return; }
                if (_root.Server.Online)
                {
                    _busy = true;
                    StartCoroutine(_root.Server.SocketClear(_root.Server.ItemIds[item], socketIndex, text => { _message.text = text; _busy = false; }));
                }
                else
                {
                    _root.Session.ClearSocket(item, socketIndex);
                    _message.text = "Dead Shard removed.";
                }
                return;
            }

            if (!socket.IsEmpty) return;
            ShardType type = SocketRules.ShardsFor(item.Slot)[_typeChoice[socketIndex] % SocketRules.ShardsFor(item.Slot).Length];
            int rank = _rankChoice[socketIndex];
            string insertBlocker = _root.Session.SocketBlocker(item, socketIndex, type, rank);
            if (insertBlocker != null) { _message.text = insertBlocker; return; }

            if (_root.Server.Online)
            {
                _busy = true;
                StartCoroutine(_root.Server.SocketInsert(_root.Server.ItemIds[item], socketIndex, type, rank, (ok, text) =>
                {
                    _message.text = text;
                    _message.color = ok ? Palette.Good : Palette.Bad;
                    _busy = false;
                }));
            }
            else
            {
                bool ok = _root.Session.SetShard(item, socketIndex, type, rank);
                _message.text = ok ? "Shard set: " + SocketRules.Describe(type, rank) : "The shard shattered. A Dead Shard blocks the socket.";
                _message.color = ok ? Palette.Good : Palette.Bad;
            }
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            PlayerSession session = _root.Session;
            ItemState item = Current;
            if (item == null) { Step(1); item = Current; }
            if (item == null) return;

            HeroStats hero = session.Hero;
            _itemLabel.text = $"{item.DisplayName} +{item.UpgradeLevel}  ·  {item.Sockets.Length} socket{(item.Sockets.Length == 1 ? "" : "s")}";
            var sb = new StringBuilder("Shards held:  ");
            for (int r = 0; r < SocketRules.RankCount; r++) sb.Append(Content.KorshardRanks[r]).Append(' ').Append(session.Inventory.Korshards[r]).Append(r < 4 ? "   " : "");
            _shardsLabel.text = sb.ToString();

            ShardType[] options = SocketRules.ShardsFor(item.Slot);
            for (int i = 0; i < MaxSockets; i++)
            {
                bool has = i < item.Sockets.Length;
                _socketLabels[i].gameObject.SetActive(has);
                _typeButtons[i].gameObject.SetActive(has);
                _rankButtons[i].gameObject.SetActive(has);
                _actButtons[i].gameObject.SetActive(has);
                if (!has) continue;

                Socket s = item.Sockets[i];
                ShardType type = options[_typeChoice[i] % options.Length];
                int rank = _rankChoice[i];
                _socketLabels[i].text = s.Dead ? $"Socket {i + 1}:  DEAD SHARD  ·  clear for {SocketRules.ClearCost(item):N0} sorn"
                    : s.Type == null ? $"Socket {i + 1}:  empty"
                    : $"Socket {i + 1}:  {SocketRules.Name(s.Type.Value)} {Content.KorshardRanks[s.Rank]}  ·  {SocketRules.Describe(s.Type.Value, s.Rank)}";
                _socketLabels[i].color = s.Dead ? Palette.Bad : s.Type == null ? Palette.Muted : Palette.Good;

                bool empty = s.IsEmpty;
                _typeButtons[i].gameObject.SetActive(empty);
                _rankButtons[i].gameObject.SetActive(empty);
                _typeLabels[i].text = SocketRules.Name(type) + "\n" + SocketRules.Describe(type, rank);
                _rankLabels[i].text = Content.KorshardRanks[rank] + $"\n(have {session.Inventory.Korshards[rank]})";
                _actLabels[i].text = s.Dead ? "CLEAR" : empty ? "SET  70%" : "SET";
                _actImages[i].color = s.Dead ? Palette.Danger : Palette.Safe;
                _actButtons[i].gameObject.SetActive(empty || s.Dead);
                _actButtons[i].interactable = !_busy;
            }
        }
    }
}
