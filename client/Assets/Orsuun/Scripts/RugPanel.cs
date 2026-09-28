using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// RUG STALLS (owner, 28 Sep 2026: picked "Rug Stalls"; GDD: "the player unrolls a rug in the town square with up to 12
    /// items, and it sells while the player is away. This is the most nostalgic idle mechanic in the design"). The rugs laid
    /// out now, newest first, each with its seller and how many wares; a rug's wares with their prices, BUY on each (the
    /// Exchange's buy: the seller is paid by letter, the Exchange keeps its tax). MY RUG opens the hero's own (a ware taps to
    /// take it back); LAY WARES opens the Exchange's SELL, where ON MY RUG lays a piece or stack on it (Rules.Market.RugWares).
    /// </summary>
    public sealed class RugPanel : MonoBehaviour
    {
        private const int Rows = 12;

        private sealed class Row
        {
            public GameObject Root;
            public Text Name, Detail, Right;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private ConfirmDialog _confirm;
        private Text _title, _message, _mineLabel, _empty;
        private readonly Row[] _rows = new Row[Rows];
        private RugsDto _rugs;
        private RugDto _rug;
        private string _open;   // the seller whose rug is shown, or null for the list
        private bool _straight; // opened on a rug (from the town square): BACK closes
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("RugCanvas", 9).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);
            Ui.Backdrop(canvas, "Exchange");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "RUG STALLS", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _title = Ui.Label("Sub", canvas, 0.05f, 0.895f, 0.95f, 0.932f, "", 24, TextAnchor.MiddleCenter, Palette.Parchment);
            Ui.Raw(_title);
            Ui.Button("Mine", canvas, 0.04f, 0.84f, 0.49f, 0.888f, "", 22, Palette.Alloy, () => Show(_root.Server.AccountId), out _mineLabel);
            Ui.Button("Lay", canvas, 0.51f, 0.84f, 0.96f, 0.888f, "LAY WARES", 22, Palette.ButtonForge, Lay, out _);
            Ui.Scroll("List", canvas, 0.03f, 0.15f, 0.97f, 0.83f, out RectTransform content);
            for (int i = 0; i < Rows; i++)
            {
                int index = i;
                var row = new Row();
                RectTransform box = new GameObject("Row" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                box.SetParent(content, false);
                box.gameObject.AddComponent<LayoutElement>().preferredHeight = 76f;
                row.Root = box.gameObject;
                Image back = Ui.Framed("Back", box, 0f, 0f, 1f, 1f, new Color(0.08f, 0.06f, 0.05f, 0.93f));
                back.gameObject.AddComponent<Button>().onClick.AddListener(() => Tap(index));
                row.Name = Ui.Title("Name", box, 0.04f, 0.5f, 0.68f, 0.95f, "", 24, TextAnchor.MiddleLeft, Palette.Parchment);
                Ui.Raw(row.Name);
                row.Detail = Ui.Label("Detail", box, 0.04f, 0.06f, 0.7f, 0.5f, "", 18, TextAnchor.MiddleLeft, Palette.Muted);
                row.Detail.supportRichText = true;
                row.Right = Ui.Title("Right", box, 0.68f, 0.1f, 0.97f, 0.9f, "", 22, TextAnchor.MiddleRight, Palette.Sorn);
                row.Right.supportRichText = true;
                _rows[i] = row;
            }
            _empty = Ui.Label("Empty", canvas, 0.06f, 0.5f, 0.94f, 0.62f, "", 24, TextAnchor.MiddleCenter, Palette.Muted);
            _message = Ui.Label("Message", canvas, 0.05f, 0.085f, 0.95f, 0.14f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Back", canvas, 0.25f, 0.015f, 0.75f, 0.075f, "BACK", 28, Palette.ButtonIdle, Back, out _);
            _confirm = new GameObject("RugConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
            _canvas.SetActive(false);
        }

        /// <summary>Opens the list of rugs, or one seller's rug (the town square's rug tags).</summary>
        public void Open(string seller = null)
        {
            _canvas.SetActive(true);
            _message.text = _root.Server.Online ? "" : "Rug Stalls need the server.";
            _straight = seller != null;
            Show(seller);
        }

        public void Close() => _canvas.SetActive(false);

        private void Back()
        {
            if (_open != null && !_straight) Show(null);
            else Close();
        }

        /// <summary>The list of rugs (null) or one seller's rug.</summary>
        private void Show(string seller)
        {
            _open = seller;
            _rug = null;
            Fetch();
        }

        private void Fetch()
        {
            if (!_root.Server.Online || _busy) return;
            _busy = true;
            if (_open == null)
                StartCoroutine(_root.Server.FetchRugs((rugs, error) =>
                {
                    _busy = false;
                    if (error != null) _message.text = error;
                    _rugs = rugs ?? _rugs;
                }));
            else
                StartCoroutine(_root.Server.FetchRug(_open, (rug, error) =>
                {
                    _busy = false;
                    if (error != null) _message.text = error;
                    _rug = rug;
                }));
        }

        private void Lay()
        {
            Close();
            _root.Market.Open();
            _root.Market.OpenSellTab();
            _root.Hud.Log("Pick a piece or a stack and choose ON MY RUG.");
        }

        private void Tap(int index)
        {
            if (_open == null)
            {
                RugStallDto[] stalls = _rugs?.stalls;
                if (stalls != null && index < stalls.Length) Show(stalls[index].sellerId);
                return;
            }
            ListingDto[] wares = _rug?.wares;
            if (wares == null || index >= wares.Length) return;
            ListingDto l = wares[index];
            string title = Title(l), body = Body(l);
            if (_rug.mine)
            {
                _confirm.Show("Roll it up?", title + "\n\nIt comes back to you.", "TAKE BACK", Palette.ButtonIdle,
                    () => Call("cancel", l.id, "Taken back."));
                return;
            }
            bool afford = _root.Session.Inventory.Sorn >= l.price;
            _confirm.Show(title, body + $"\n\nOn {_rug.name}'s rug  ·  {MarketPanel.Left(l.minutesLeft)}"
                                 + (afford ? "" : "\n" + ConfirmDialog.Tint("You do not have enough sorn.", Palette.Bad)),
                $"BUY  ·  {l.price:N0}", Palette.Safe, () => Call("buy", l.id, "Bought."));
        }

        private void Call(string path, long listing, string done)
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(_root.Server.MarketCall(path, new MarketBuyRequest { requestId = NewRequestId(), listingId = listing }, (message, error) =>
            {
                _busy = false;
                _message.text = error ?? message ?? done;
                Fetch();
            }));
        }

        private static string Title(ListingDto l) =>
            l.goodId >= 0 ? MarketPanel.GoodTitle(l.goodId, l.goodCount)
            : l.bookId >= 0 ? MarketPanel.BookTitle(l.bookId, l.bookCount)
            : MarketPanel.Title(ToState(l.item));

        private static string Body(ListingDto l) =>
            l.goodId >= 0 ? GearPanel.GoodBlurb(l.goodId)
            : l.bookId >= 0 ? Books.Name(l.bookId)
            : MarketPanel.Summary(ToState(l.item));

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            _mineLabel.text = $"MY RUG  {_rugs?.myWares ?? 0}/{_rugs?.maxWares ?? Market.RugWares}";
            if (_open == null)
            {
                RugStallDto[] stalls = _rugs?.stalls ?? new RugStallDto[0];
                _title.text = "The square's rugs, the newest first";
                _empty.text = _rugs != null && stalls.Length == 0 ? "No rugs are laid out. Be the first: LAY WARES." : "";
                for (int i = 0; i < Rows; i++)
                {
                    Row row = _rows[i];
                    bool has = i < stalls.Length;
                    row.Root.SetActive(has);
                    if (!has) continue;
                    RugStallDto s = stalls[i];
                    row.Name.text = s.name + (s.mine ? "  (you)" : "");
                    row.Detail.text = ConfirmDialog.Tint(BannerLook.Name(BannerLook.Parse(s.banner)), BannerLook.Color(BannerLook.Parse(s.banner)));
                    row.Right.text = $"{s.wares} ware{(s.wares == 1 ? "" : "s")}";
                }
                return;
            }
            ListingDto[] wares = _rug?.wares ?? new ListingDto[0];
            _title.text = _rug == null ? "..." : _rug.mine ? "Your rug: tap a ware to take it back" : _rug.name + "'s rug";
            _empty.text = _rug != null && wares.Length == 0 ? (_rug.mine ? "Your rug is bare: LAY WARES." : "Nothing left on this rug.") : "";
            for (int i = 0; i < Rows; i++)
            {
                Row row = _rows[i];
                bool has = i < wares.Length;
                row.Root.SetActive(has);
                if (!has) continue;
                ListingDto l = wares[i];
                row.Name.text = Title(l);
                row.Detail.text = MarketPanel.Left(l.minutesLeft);
                row.Right.text = $"{l.price:N0}\n<size=15>sorn</size>";
            }
        }
    }
}
