using System.Text;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// DEPOT (owner, 25 Sep 2026: "a common depot of items to trade between each other"): the account's shared chest of
    /// Rules.Characters.DepotSlots pieces. A bag piece put in here can be taken out by any of the account's heroes (it
    /// becomes theirs). Tap a row to move it. Opened from GEAR.
    /// </summary>
    public sealed class DepotPanel : MonoBehaviour
    {
        private GameRoot _root;
        private GameObject _canvas;
        private RectTransform _depotList;
        private RectTransform _bagList;
        private Text _depotHead;
        private Text _message;
        private string _shownKey = "";
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("DepotCanvas", 10).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Gear");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "DEPOT", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Label("Note", canvas, 0.05f, 0.885f, 0.95f, 0.925f, "Shared by your account's heroes: put a piece in, take it out with another.", 21,
                TextAnchor.MiddleCenter, Palette.Muted);
            _depotHead = Ui.Section("DepotHead", canvas, 0.15f, 0.835f, 0.85f, 0.875f, "", 26);
            Ui.Scroll("Depot", canvas, 0.04f, 0.53f, 0.96f, 0.83f, out _depotList);
            Ui.Section("BagHead", canvas, 0.15f, 0.475f, 0.85f, 0.515f, "YOUR BAG", 26);
            Ui.Scroll("Bag", canvas, 0.04f, 0.13f, 0.96f, 0.47f, out _bagList);
            _message = Ui.Label("Message", canvas, 0.05f, 0.075f, 0.95f, 0.12f, "", 22, TextAnchor.MiddleCenter, Palette.Warn);
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.068f, "BACK TO THE HUNT", 28, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _message.text = _root.Server.Online ? "Tap a piece to move it." : "The depot needs the server.";
            _shownKey = "";
            _canvas.SetActive(true);
            if (_root.Server.Online) StartCoroutine(_root.Server.FetchDepot(error => { if (error != null) _message.text = error; }));
        }

        public void Close() => _canvas.SetActive(false);

        private static string Line(Net.ServerLink.ItemDto item) =>
            $"<b>{item.name}{(item.upgradeLevel > 0 ? " +" + item.upgradeLevel : "")}</b>  <color=#B9B3A8>· {item.slot} · item level {item.itemLevel}</color>";

        private void Move(bool put, string itemId)
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(_root.Server.DepotMove(put, itemId, (message, error) =>
            {
                _busy = false;
                _message.text = error ?? message ?? "";
                if (error == null) GameAudio.Instance?.Play("LaneLoot", 0.9f, 0.1f, 0f);
            }));
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            var depot = _root.Server.Depot;
            var items = depot?.state?.items;
            var key = new StringBuilder();
            if (depot?.items != null) foreach (var i in depot.items) key.Append(i.id).Append(',');
            key.Append('|');
            if (items != null) foreach (var i in items) if (!i.equipped) key.Append(i.id).Append(',');
            if (key.ToString() == _shownKey) return;
            _shownKey = key.ToString();

            foreach (Transform child in _depotList) Destroy(child.gameObject);
            foreach (Transform child in _bagList) Destroy(child.gameObject);
            int held = depot?.items?.Length ?? 0;
            _depotHead.text = $"DEPOT · {held}/{depot?.capacity ?? Characters.DepotSlots}";
            if (held == 0) Ui.ListRow("Empty", _depotList, 22, () => { }).text = "<color=#B9B3A8>Empty. Put pieces from your bag here.</color>";
            else
                foreach (var item in depot.items)
                {
                    string id = item.id;
                    Ui.ListRow("Take", _depotList, 24, () => Move(false, id)).text = "<color=#FFD76B>TAKE</color>   " + Line(item);
                }
            int bag = 0;
            if (items != null)
                foreach (var item in items)
                {
                    if (item.equipped) continue;
                    string id = item.id;
                    Ui.ListRow("Put", _bagList, 24, () => Move(true, id)).text = "<color=#8FE08F>PUT</color>   " + Line(item);
                    bag++;
                }
            if (bag == 0) Ui.ListRow("EmptyBag", _bagList, 22, () => { }).text = "<color=#B9B3A8>Nothing in the bag (worn pieces stay on).</color>";
        }
    }
}
