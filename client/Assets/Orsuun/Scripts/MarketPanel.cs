using System.Collections.Generic;
using System.Text;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// SALT EXCHANGE (owner, 24 Sep 2026: "a global trading screen"): BUY pages through every player's listings by
    /// slot and order; SELL lists a bag piece for a price; MY LISTINGS shows what is up, sold, expired or taken back.
    /// The server decides everything; the seller gets the price less the Exchange's 5%. Technique Scrolls (owner, 26 Sep
    /// 2026: books trade) list as stacks, a count for one price, under their own BOOK filter; goods (owner, 26 Sep 2026:
    /// "Exchange: materials + prices"; Rules.TradeGoods) the same way under GOODS. Buying or pricing something shows what
    /// its kind sold for over the last two weeks (the server's price history).
    /// </summary>
    public sealed class MarketPanel : MonoBehaviour
    {
        private const int Rows = 8;
        private const float RefreshSeconds = 20f;
        private static readonly string[] SlotChips = { "ALL", "WPN", "ARM", "HELM", "SHLD", "BRAC", "NECK", "EAR", "SHOE", "BOOK", "GOODS" };
        /// <summary>The slot filter's value for the BOOK chip.</summary>
        private const int BookFilter = 8;
        /// <summary>The slot filter's value for the GOODS chip.</summary>
        private const int GoodsFilter = 9;
        private static readonly string[] Sorts = { "cheapest", "newest", "level" };
        private static readonly string[] SortLabels = { "CHEAPEST", "NEWEST", "HIGHEST +" };

        private enum Tab { Buy, Sell, Mine }

        private sealed class Row
        {
            public Button Button;
            public Text Name;
            public Text Detail;
            public Text Price;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private ConfirmDialog _confirm;
        private Tab _tab;
        private Text _purse;
        private Text _message;
        private readonly Button[] _tabs = new Button[3];
        private GameObject _filters;
        private readonly Button[] _chips = new Button[SlotChips.Length];
        private readonly Button[] _sorts = new Button[Sorts.Length];
        private Text _pageText;
        private readonly Row[] _rows = new Row[Rows];
        private Text _empty;
        private int _slot = -1;
        private int _sort;
        private int _page;
        private int _sellPage;
        private bool _busy;
        private bool _fetching;
        private float _nextFetch;
        private bool _offlineShown;

        // SELL: the price box for one bag piece, or a count of one Technique Scroll.
        private GameObject _sellBox;
        private Text _sellTitle;
        private Text _sellInfo;
        private InputField _price;
        private Text _payout;
        private string _sellItemId;
        private int _sellBook = -1;
        private int _sellGood = -1;
        /// <summary>A price history being fetched (a tap waits for it before its dialog opens).</summary>
        private bool _looking;
        private GameObject _countRow;
        private InputField _count;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("MarketCanvas", 9).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Exchange");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "SALT EXCHANGE", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _purse = Ui.Label("Purse", canvas, 0.05f, 0.9f, 0.95f, 0.935f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);
            string[] tabs = { "BUY", "SELL", "MY LISTINGS" };
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = (Tab)i;
                float x0 = 0.04f + i * 0.31f;
                _tabs[i] = Ui.Button("Tab" + i, canvas, x0, 0.845f, x0 + 0.3f, 0.893f, tabs[i], 26, Palette.ButtonIdle, () => SetTab(tab), out _);
            }

            _filters = Ui.Rect("Filters", canvas, 0f, 0f, 1f, 1f).gameObject;
            for (int i = 0; i < SlotChips.Length; i++)
            {
                int slot = i - 1;
                float x0 = 0.035f + i * 0.084f;
                _chips[i] = Ui.Button("Chip" + i, _filters.transform, x0, 0.795f, x0 + 0.08f, 0.835f, SlotChips[i], 15, Palette.ButtonIdle,
                    () => { _slot = slot; _page = 0; Refresh(); }, out _);
            }
            for (int i = 0; i < Sorts.Length; i++)
            {
                int sort = i;
                float x0 = 0.04f + i * 0.17f;
                _sorts[i] = Ui.Button("Sort" + i, _filters.transform, x0, 0.748f, x0 + 0.16f, 0.787f, SortLabels[i], 18, Palette.ButtonIdle,
                    () => { _sort = sort; _page = 0; Refresh(); }, out _);
            }
            Ui.Button("Prev", canvas, 0.56f, 0.748f, 0.68f, 0.787f, "PREV", 18, Palette.ButtonIdle, () => Turn(-1), out _);
            _pageText = Ui.Label("Page", canvas, 0.68f, 0.748f, 0.84f, 0.787f, "", 20, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Next", canvas, 0.84f, 0.748f, 0.96f, 0.787f, "NEXT", 18, Palette.ButtonIdle, () => Turn(1), out _);

            for (int i = 0; i < Rows; i++)
            {
                int index = i;
                float y1 = 0.74f - i * 0.068f;
                var r = new Row();
                r.Button = Ui.Button("Row" + i, canvas, 0.04f, y1 - 0.063f, 0.96f, y1, "", 20, Palette.PanelDark, () => Pick(index), out Text unused);
                unused.text = "";
                Transform row = r.Button.transform;
                r.Name = Ui.Title("Name", row, 0.03f, 0.5f, 0.7f, 0.95f, "", 24, TextAnchor.MiddleLeft, Palette.Parchment);
                r.Detail = Ui.Label("Detail", row, 0.03f, 0.08f, 0.72f, 0.5f, "", 18, TextAnchor.MiddleLeft, Palette.Muted);
                r.Detail.supportRichText = true;
                r.Price = Ui.Title("Price", row, 0.7f, 0.1f, 0.97f, 0.9f, "", 22, TextAnchor.MiddleRight, Palette.Sorn);
                r.Price.supportRichText = true;
                _rows[i] = r;
            }
            _empty = Ui.Label("Empty", canvas, 0.05f, 0.5f, 0.95f, 0.6f, "", 26, TextAnchor.MiddleCenter, Palette.Muted);

            _message = Ui.Label("Message", canvas, 0.05f, 0.085f, 0.95f, 0.14f, "", 24, TextAnchor.MiddleCenter, Palette.Muted);
            _message.supportRichText = true;
            // Direct trade (GDD section 8): face to face with another hero, beside the Exchange.
            Ui.Button("Trade", canvas, 0.03f, 0.015f, 0.34f, 0.075f, "DIRECT TRADE", 22, Palette.Alloy, () => { Close(); _root.Trade.Open(); }, out _);
            // Rug Stalls (Rules.Market.RugWares): the rugs laid out in the square, browsed stall by stall.
            Ui.Button("Rugs", canvas, 0.35f, 0.015f, 0.65f, 0.075f, "RUG STALLS", 22, Palette.Safe, () => { Close(); _root.Rugs.Open(); }, out _);
            Ui.Button("Close", canvas, 0.66f, 0.015f, 0.97f, 0.075f, "BACK", 24, Palette.ButtonIdle, Close, out _);

            _sellBox = Ui.Rect("SellBox", canvas, 0f, 0f, 1f, 1f).gameObject;
            Image dim = Ui.Panel("Dim", _sellBox.transform, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.65f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => _sellBox.SetActive(false));
            Transform box = Ui.Framed("Box", _sellBox.transform, 0.06f, 0.3f, 0.94f, 0.72f, Palette.PanelDark).transform;
            _sellTitle = Ui.Title("Title", box, 0.05f, 0.84f, 0.95f, 0.97f, "", 32, TextAnchor.MiddleCenter, Palette.Parchment);
            _sellInfo = Ui.Label("Info", box, 0.06f, 0.63f, 0.94f, 0.84f, "", 22, TextAnchor.UpperLeft, Palette.Parchment);
            _sellInfo.supportRichText = true;
            // A scroll stack: how many of the held ones go up, for the one price.
            _countRow = Ui.Rect("CountRow", box, 0f, 0f, 1f, 1f).gameObject;
            Ui.Label("CountLabel", _countRow.transform, 0.06f, 0.5f, 0.4f, 0.61f, "How many", 24, TextAnchor.MiddleLeft, Palette.Muted);
            _count = Ui.Input("Count", _countRow.transform, 0.42f, 0.49f, 0.94f, 0.62f, "1", 30, 7);
            _count.contentType = InputField.ContentType.IntegerNumber;
            Ui.Label("PriceLabel", box, 0.06f, 0.37f, 0.4f, 0.48f, "Price in sorn", 24, TextAnchor.MiddleLeft, Palette.Muted);
            _price = Ui.Input("Price", box, 0.42f, 0.36f, 0.94f, 0.49f, "e.g. 50000", 30, 10);
            _price.contentType = InputField.ContentType.IntegerNumber;
            _payout = Ui.Label("Payout", box, 0.06f, 0.24f, 0.94f, 0.35f, "", 22, TextAnchor.MiddleLeft, Palette.Muted);
            Ui.Button("List", box, 0.04f, 0.04f, 0.36f, 0.2f, "LIST IT", 28, Palette.ButtonForge, AskList, out _);
            Ui.Button("Rug", box, 0.38f, 0.04f, 0.66f, 0.2f, "ON MY RUG", 22, Palette.Alloy, () => AskList(rug: true), out _);
            Ui.Button("Cancel", box, 0.68f, 0.04f, 0.96f, 0.2f, "CANCEL", 24, Palette.ButtonIdle, () => _sellBox.SetActive(false), out _);
            _sellBox.SetActive(false);

            _canvas.SetActive(false);
            _confirm = new GameObject("MarketConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
        }

        public void Open()
        {
            _message.text = "";
            _sellBox.SetActive(false);
            _canvas.SetActive(true);
            SetTab(Tab.Buy);
        }

        /// <summary>
        /// Screenshots: -sell, -mylistings, -marketbooks (the BOOK filter), -sellbook (the first scroll's price box),
        /// -marketgoods (the GOODS filter), -sellgood (the first good's price box).
        /// </summary>
        public void ShotView(string[] cmd)
        {
            if (System.Array.IndexOf(cmd, "-marketgoods") >= 0) { _slot = GoodsFilter; Refresh(); }
            if (System.Array.IndexOf(cmd, "-sellgood") >= 0 && SellableGoods().Count > 0) OpenSellGood(SellableGoods()[0]);
            if (System.Array.IndexOf(cmd, "-sell") >= 0) SetTab(Tab.Sell);
            if (System.Array.IndexOf(cmd, "-mylistings") >= 0) SetTab(Tab.Mine);
            if (System.Array.IndexOf(cmd, "-marketbooks") >= 0) { _slot = BookFilter; Refresh(); }
            if (System.Array.IndexOf(cmd, "-sellbook") >= 0 && SellableBooks().Count > 0) OpenSellBook(SellableBooks()[0]);
        }

        /// <summary>Opens SELL with a bag piece's price box (the inventory's SELL).</summary>
        /// <summary>SELL, from RUG STALLS' LAY WARES.</summary>
        public void OpenSellTab() => SetTab(Tab.Sell);

        public void OpenSell(string itemId)
        {
            Open();
            SetTab(Tab.Sell);
            foreach ((ItemState item, string id) in Sellable())
                if (id == itemId) { ShowSellPiece(item, id); return; }
        }

        /// <summary>Opens SELL with a Technique Scroll's price box (the inventory's SELL on a scroll).</summary>
        public void OpenSellBook(int book)
        {
            Open();
            SetTab(Tab.Sell);
            if (Books.Valid(book) && _root.Session.Inventory.Books[book] > 0) ShowSellBook(book);
        }

        /// <summary>Opens SELL with a good's price box (the inventory's SELL on a material tile).</summary>
        public void OpenSellGood(int good)
        {
            Open();
            SetTab(Tab.Sell);
            if (TradeGoods.Valid(good) && TradeGoods.Held(_root.Session.Inventory, good) > 0) ShowSellGood(good);
        }

        public void Close()
        {
            _sellBox.SetActive(false);
            _canvas.SetActive(false);
        }

        private void SetTab(Tab tab)
        {
            _tab = tab;
            _page = 0;
            _sellPage = 0;
            Refresh();
        }

        private void Refresh() => _nextFetch = 0f;

        private void Turn(int step)
        {
            if (_tab == Tab.Buy) { _page = Mathf.Max(0, _page + step); Refresh(); }
            else if (_tab == Tab.Sell) _sellPage = Mathf.Max(0, _sellPage + step);
        }

        private void Call(string path, object request)
        {
            if (_busy || !_root.Server.Online) return;
            _busy = true;
            StartCoroutine(_root.Server.MarketCall(path, request, (message, error) =>
            {
                _busy = false;
                _message.text = error != null ? ConfirmDialog.Tint(error, Palette.Bad) : message ?? "";
                if (error == null) GameAudio.Instance?.Play("LaneLoot", 0.9f, 0.1f, 0f);
            }));
        }

        /// <summary>Name line for a piece: +level, rarity and name, tinted by rarity.</summary>
        internal static string Title(ItemState item) => $"+{item.UpgradeLevel}  {item.DisplayName}";

        internal static string BookTitle(int book, int count) => $"{count} × {Books.Name(book)}";

        private static string BookSummary(int book) => $"A {Books.ClassOf(book)} skill's Technique Scroll";

        private static readonly Color BookColor = new Color(0.93f, 0.8f, 0.55f);

        internal static string GoodTitle(int good, int count) => $"{count} × {TradeGoods.Name(good)}";

        private static string GoodSummary(int good)
        {
            string blurb = GearPanel.GoodBlurb(good);
            int stop = blurb.IndexOf(". ", System.StringComparison.Ordinal);
            return stop > 0 ? blurb.Substring(0, stop + 1) : blurb;
        }

        private static readonly Color GoodColor = new Color(0.78f, 0.88f, 0.95f);

        /// <summary>The price history query for what a listing or a sell box holds.</summary>
        private static string HistoryQuery(ItemState item) =>
            $"kind=piece&slot={item.Slot}&band={ItemLooks.Tier(item.ItemLevel)}&plus={item.UpgradeLevel}&rarity={item.Rarity}";

        private static string HistoryQuery(ListingDto l) =>
            l.goodId >= 0 ? "kind=good&id=" + l.goodId : l.bookId >= 0 ? "kind=book&id=" + l.bookId : HistoryQuery(ToState(l.item));

        private static string Ago(int minutes) =>
            minutes < 60 ? $"{Mathf.Max(1, minutes)}m ago" : minutes < 1440 ? $"{minutes / 60}h ago" : $"{minutes / 1440}d ago";

        /// <summary>One line on what such things sold for (stacks priced for one).</summary>
        private static string HistoryLine(PriceHistoryDto h, bool stack)
        {
            if (h == null) return "";
            if (h.sales == 0) return ConfirmDialog.Tint($"No sales of its kind in the last {h.days} days.", Palette.Muted);
            string each = stack ? " each" : "";
            string range = h.low == h.high ? $"{h.low:N0}{each}" : $"{h.low:N0} to {h.high:N0}{each}";
            return ConfirmDialog.Tint($"Sold lately ({h.days} days): {h.sales} sale{(h.sales == 1 ? "" : "s")}, {range}, about {h.average:N0}; "
                                      + $"the last {h.last:N0}, {Ago(h.lastMinutesAgo)}.", Palette.Sorn);
        }

        /// <summary>Fetches a price history, then hands its line on (empty offline or on a failure).</summary>
        private void WithHistory(string query, bool stack, System.Action<string> show)
        {
            if (!_root.Server.Online) { show(""); return; }
            if (_looking) return;
            _looking = true;
            StartCoroutine(_root.Server.FetchPriceHistory(query, h => { _looking = false; show(HistoryLine(h, stack)); }));
        }

        internal static string Summary(ItemState item)
        {
            int sockets = 0;
            foreach (Socket s in item.Sockets) if (!s.Dead && s.Type != null) sockets++;
            return $"{GearPanel.SlotNames[(int)item.Slot]}  ·  item level {item.ItemLevel}  ·  {item.Etchings.Count} etching{(item.Etchings.Count == 1 ? "" : "s")}"
                   + (sockets > 0 ? $"  ·  {sockets} shard{(sockets == 1 ? "" : "s")}" : "");
        }

        private static string Etchings(ItemState item)
        {
            EtchingPool pool = EtchingPool.For(item.Slot);
            var sb = new StringBuilder(GearPanel.RollLines(item));
            foreach (Etching e in item.Etchings)
            {
                string line = $"T{e.Tier}  {pool.Entries[e.EntryId].Name}  +{e.Value}";
                sb.Append(e.Tier >= 4 ? ConfirmDialog.Tint(line, Palette.Sorn) : line).Append('\n');
            }
            foreach (Socket s in item.Sockets)
                if (!s.Dead && s.Type != null) sb.Append(ConfirmDialog.Tint(SocketRules.Name(s.Type.Value) + " " + Content.KorshardRanks[s.Rank], Palette.Muted)).Append('\n');
            return sb.Length == 0 ? ConfirmDialog.Tint("No etchings.", Palette.Muted) : sb.ToString().TrimEnd();
        }

        internal static string Left(int minutes) => minutes >= 60 ? $"{minutes / 60}h left" : $"{minutes}m left";

        /// <summary>The scrolls held, by book id, that can be listed.</summary>
        private List<int> SellableBooks()
        {
            var list = new List<int>();
            for (int b = 0; b < Books.Count; b++) if (_root.Session.Inventory.Books[b] > 0) list.Add(b);
            return list;
        }

        /// <summary>The goods held that the Exchange takes, by Rules.TradeGoods id.</summary>
        private List<int> SellableGoods()
        {
            var list = new List<int>();
            for (int g = 0; g < TradeGoods.Count; g++) if (TradeGoods.Held(_root.Session.Inventory, g) > 0) list.Add(g);
            return list;
        }

        /// <summary>The bag pieces that can be listed (the session's loot, with server ids).</summary>
        private List<(ItemState Item, string Id)> Sellable()
        {
            var list = new List<(ItemState, string)>();
            foreach (ItemState item in _root.Session.Inventory.Loot)
            {
                string id = _root.Server.IdOf(item);
                if (id != null && !item.Destroyed) list.Add((item, id));
            }
            return list;
        }

        private void Pick(int row)
        {
            MarketDto v = _root.Server.MarketView;
            if (_tab == Tab.Buy)
            {
                if (v?.listings == null || row >= v.listings.Length) return;
                ListingDto l = v.listings[row];
                if (l.mine) { _message.text = "That is your own listing: take it back under MY LISTINGS."; return; }
                bool afford = _root.Session.Inventory.Sorn >= l.price;
                bool stack = l.bookId >= 0 || l.goodId >= 0;
                int count = l.goodId >= 0 ? l.goodCount : l.bookCount;
                string title, body;
                if (l.goodId >= 0) { title = GoodTitle(l.goodId, l.goodCount); body = GoodSummary(l.goodId); }
                else if (l.bookId >= 0) { title = BookTitle(l.bookId, l.bookCount); body = BookSummary(l.bookId) + "."; }
                else { ItemState item = ToState(l.item); title = Title(item); body = Summary(item) + "\n\n" + Etchings(item); }
                string price = stack && count > 1 ? $"  ·  {Market.UnitPrice(l.price, count):N0} each" : "";
                WithHistory(HistoryQuery(l), stack, history =>
                    _confirm.Show(title, body + $"\n\nSold by {l.sellerName}  ·  {Left(l.minutesLeft)}{price}"
                                         + (history.Length > 0 ? "\n" + history : "")
                                         + (afford ? "" : "\n" + ConfirmDialog.Tint("You do not have enough sorn.", Palette.Bad)),
                        $"BUY  ·  {l.price:N0}", Palette.Safe, () => Call("buy", new MarketBuyRequest { requestId = NewRequestId(), listingId = l.id })));
            }
            else if (_tab == Tab.Sell)
            {
                var books = SellableBooks();
                var goods = SellableGoods();
                var bag = Sellable();
                int index = _sellPage * Rows + row;
                if (index < books.Count) { ShowSellBook(books[index]); return; }
                index -= books.Count;
                if (index < goods.Count) { ShowSellGood(goods[index]); return; }
                index -= goods.Count;
                if (index >= bag.Count) return;
                (ItemState item, string id) = bag[index];
                ShowSellPiece(item, id);
            }
            else
            {
                if (v?.mine == null || row >= v.mine.Length) return;
                ListingDto l = v.mine[row];
                if (l.status != nameof(ListingStatus.Active)) return;
                string what = l.goodId >= 0 ? GoodTitle(l.goodId, l.goodCount) : l.bookId >= 0 ? BookTitle(l.bookId, l.bookCount) : Title(ToState(l.item));
                _confirm.Show("Take it back?", $"{what} comes off the Exchange and back to you.", "TAKE BACK", Palette.ButtonIdle,
                    () => Call("cancel", new MarketBuyRequest { requestId = NewRequestId(), listingId = l.id }));
            }
        }

        private void ShowSellPiece(ItemState item, string id)
        {
            _sellItemId = id;
            _sellBook = -1;
            _sellGood = -1;
            _sellTitle.text = Title(item);
            _sellTitle.color = GearPanel.RarityColor(item.Rarity);
            _sellInfo.text = Summary(item) + "\n" + Etchings(item);
            _countRow.SetActive(false);
            _sellInfo.rectTransform.anchorMin = new Vector2(0.06f, 0.5f);
            _price.text = "";
            _sellBox.SetActive(true);
            AddHistory(HistoryQuery(item), false);
        }

        /// <summary>Adds what such things sold for to the open sell box, once the server answers.</summary>
        private void AddHistory(string query, bool stack)
        {
            string forText = _sellTitle.text;
            WithHistory(query, stack, history =>
            {
                if (history.Length > 0 && _sellBox.activeSelf && Ui.Src(_sellTitle) == forText) _sellInfo.text = history + "\n" + Ui.Src(_sellInfo);
            });
        }

        private void ShowSellGood(int good)
        {
            int held = TradeGoods.Held(_root.Session.Inventory, good);
            _sellItemId = null;
            _sellBook = -1;
            _sellGood = good;
            _sellTitle.text = TradeGoods.Name(good);
            _sellTitle.color = GoodColor;
            _sellInfo.text = $"{GoodSummary(good)}  ·  you hold {held:N0}\nThe price is for the whole count listed.";
            _countRow.SetActive(true);
            _sellInfo.rectTransform.anchorMin = new Vector2(0.06f, 0.63f);
            _count.text = held.ToString();
            _price.text = "";
            _sellBox.SetActive(true);
            AddHistory("kind=good&id=" + good, true);
        }

        private void ShowSellBook(int book)
        {
            int held = _root.Session.Inventory.Books[book];
            _sellItemId = null;
            _sellBook = book;
            _sellGood = -1;
            _sellTitle.text = Books.Name(book);
            _sellTitle.color = BookColor;
            _sellInfo.text = $"{BookSummary(book)}  ·  you hold {held}\nThe price is for the whole count listed.";
            _countRow.SetActive(true);
            _sellInfo.rectTransform.anchorMin = new Vector2(0.06f, 0.63f);
            _count.text = held.ToString();
            _price.text = "";
            _sellBox.SetActive(true);
            AddHistory("kind=book&id=" + book, true);
        }

        private long PriceEntered() => long.TryParse(_price.text, out long p) ? p : 0;

        private int CountEntered() => int.TryParse(_count.text, out int n) ? n : 0;

        /// <summary>Rug Stalls (Rules.Market.RugWares): ON MY RUG lays the piece or stack on the hero's own rug instead.</summary>
        private void AskList() => AskList(rug: false);

        private void AskList(bool rug)
        {
            long price = PriceEntered();
            if (Market.PriceProblem(price) is string problem) { _payout.text = ConfirmDialog.Tint(problem, Palette.Bad); return; }
            string terms = $"Price {price:N0} sorn. When it sells you receive {Market.Payout(price):N0} (the Exchange keeps {Market.TaxPercent}%).\n\n";
            int hours = rug ? Market.RugHours : Market.ListingHours;
            string where = rug ? "on your rug" : "on the Exchange";
            if (_sellGood >= 0)
            {
                int good = _sellGood, count = CountEntered(), held = TradeGoods.Held(_root.Session.Inventory, good);
                if (count < 1 || count > held) { _payout.text = ConfirmDialog.Tint($"You hold {held:N0} of these.", Palette.Bad); return; }
                _sellBox.SetActive(false);
                _confirm.Show("List " + GoodTitle(good, count) + "?",
                    terms + $"They leave you now and come back if nobody buys them within {hours} hours.",
                    "LIST THEM", Palette.ButtonForge, () => Call("list", new MarketListRequest { requestId = NewRequestId(), itemId = System.Guid.Empty.ToString(), goodId = good, goodCount = count, price = price, rug = rug }));
                return;
            }
            if (_sellBook >= 0)
            {
                int book = _sellBook, count = CountEntered(), held = _root.Session.Inventory.Books[book];
                if (count < 1 || count > held) { _payout.text = ConfirmDialog.Tint($"You hold {held} of this scroll.", Palette.Bad); return; }
                _sellBox.SetActive(false);
                _confirm.Show("List " + BookTitle(book, count) + "?",
                    terms + $"They leave you now and come back if nobody buys them within {hours} hours.",
                    "LIST THEM", Palette.ButtonForge, () => Call("list", new MarketListRequest { requestId = NewRequestId(), itemId = System.Guid.Empty.ToString(), bookId = book, bookCount = count, price = price, rug = rug }));
                return;
            }
            string id = _sellItemId;
            string title = _sellTitle.text;
            _sellBox.SetActive(false);
            _confirm.Show((rug ? "Lay " : "List ") + title + (rug ? " on your rug?" : "?"),
                terms + $"It leaves your bag now and comes back if nobody buys it {where} within {hours} hours.",
                rug ? "ON MY RUG" : "LIST IT", Palette.ButtonForge, () => Call("list", new MarketListRequest { requestId = NewRequestId(), itemId = id, price = price, rug = rug }));
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            if (_root.Server.Online && !_fetching && !_busy && Time.realtimeSinceStartup >= _nextFetch)
            {
                _fetching = true;
                string slot = _slot < 0 || _slot == BookFilter || _slot == GoodsFilter ? "" : ((EquipSlot)_slot).ToString();
                StartCoroutine(_root.Server.FetchMarket(slot, Sorts[_sort], _page, error =>
                {
                    _fetching = false;
                    _nextFetch = Time.realtimeSinceStartup + RefreshSeconds;
                    if (error != null) _message.text = ConfirmDialog.Tint(error, Palette.Bad);
                }, books: _slot == BookFilter, goods: _slot == GoodsFilter));
            }

            _purse.text = $"You hold {_root.Session.Inventory.Sorn:N0} sorn  ·  the Exchange keeps {Market.TaxPercent}% of each sale";
            for (int i = 0; i < _tabs.Length; i++) _tabs[i].GetComponent<Image>().color = (int)_tab == i ? Palette.Safe : Palette.ButtonIdle;
            _filters.SetActive(_tab == Tab.Buy);
            for (int i = 0; i < _chips.Length; i++) _chips[i].GetComponent<Image>().color = i - 1 == _slot ? Palette.Alloy : Palette.ButtonIdle;
            for (int i = 0; i < _sorts.Length; i++) _sorts[i].GetComponent<Image>().color = i == _sort ? Palette.Alloy : Palette.ButtonIdle;
            if (!_root.Server.Online && !_offlineShown) { _message.text = "Offline: the Exchange needs the server."; _offlineShown = true; }
            else if (_root.Server.Online && _offlineShown) { _message.text = ""; _offlineShown = false; }
            if (_sellBox.activeSelf)
            {
                long price = PriceEntered();
                _payout.text = price > 0 ? $"You receive {Market.Payout(price):N0} sorn when it sells." : $"At least {Market.MinPrice:N0} sorn.";
            }

            MarketDto v = _root.Server.MarketView;
            int shown = 0;
            string empty = "";
            if (_tab == Tab.Buy)
            {
                ListingDto[] list = v?.listings ?? new ListingDto[0];
                _pageText.text = v == null ? "" : $"{v.page + 1} / {v.pages}";
                for (; shown < list.Length && shown < Rows; shown++)
                {
                    ListingDto l = list[shown];
                    Row r = _rows[shown];
                    if (l.goodId >= 0)
                    {
                        r.Name.text = GoodTitle(l.goodId, l.goodCount);
                        r.Name.color = GoodColor;
                        r.Detail.text = (l.goodCount > 1 ? $"{Market.UnitPrice(l.price, l.goodCount):N0} each  ·  " : "") + $"{l.sellerName}  ·  {Left(l.minutesLeft)}";
                    }
                    else if (l.bookId >= 0)
                    {
                        r.Name.text = BookTitle(l.bookId, l.bookCount);
                        r.Name.color = BookColor;
                        r.Detail.text = BookSummary(l.bookId) + $"\n{l.sellerName}  ·  {Left(l.minutesLeft)}";
                    }
                    else
                    {
                        ItemState item = ToState(l.item);
                        r.Name.text = Title(item);
                        r.Name.color = GearPanel.RarityColor(item.Rarity);
                        r.Detail.text = Summary(item) + $"\n{l.sellerName}  ·  {Left(l.minutesLeft)}";
                    }
                    r.Price.text = $"{l.price:N0}\n<size=16>{(l.mine ? "YOURS" : "sorn")}</size>";
                    r.Price.color = _root.Session.Inventory.Sorn >= l.price || l.mine ? Palette.Sorn : Palette.Bad;
                }
                empty = v == null ? "Opening the Exchange..." : "Nothing listed here yet.";
            }
            else if (_tab == Tab.Sell)
            {
                // Scrolls first, then goods, then the bag's pieces.
                var books = SellableBooks();
                var goods = SellableGoods();
                var bag = Sellable();
                int total = books.Count + goods.Count + bag.Count;
                int pages = Mathf.Max(1, (total + Rows - 1) / Rows);
                _sellPage = Mathf.Clamp(_sellPage, 0, pages - 1);
                _pageText.text = $"{_sellPage + 1} / {pages}";
                for (; shown < Rows && _sellPage * Rows + shown < total; shown++)
                {
                    int index = _sellPage * Rows + shown;
                    Row r = _rows[shown];
                    if (index < books.Count)
                    {
                        int book = books[index];
                        r.Name.text = BookTitle(book, _root.Session.Inventory.Books[book]);
                        r.Name.color = BookColor;
                        r.Detail.text = BookSummary(book) + "\nTap to set a count and a price";
                    }
                    else if (index < books.Count + goods.Count)
                    {
                        int good = goods[index - books.Count];
                        r.Name.text = GoodTitle(good, TradeGoods.Held(_root.Session.Inventory, good));
                        r.Name.color = GoodColor;
                        r.Detail.text = GoodSummary(good) + "\nTap to set a count and a price";
                    }
                    else
                    {
                        ItemState item = bag[index - books.Count - goods.Count].Item;
                        r.Name.text = Title(item);
                        r.Name.color = GearPanel.RarityColor(item.Rarity);
                        r.Detail.text = Summary(item) + "\nTap to set a price";
                    }
                    r.Price.text = "SELL";
                    r.Price.color = Palette.Sorn;
                }
                empty = "Nothing to sell. Worn pieces must be taken off (INVENTORY) before they can be sold.";
            }
            else
            {
                ListingDto[] mine = v?.mine ?? new ListingDto[0];
                _pageText.text = "";
                for (; shown < mine.Length && shown < Rows; shown++)
                {
                    ListingDto l = mine[shown];
                    Row r = _rows[shown];
                    bool book = l.bookId >= 0, good = l.goodId >= 0;
                    ItemState item = book || good ? null : ToState(l.item);
                    r.Name.text = good ? GoodTitle(l.goodId, l.goodCount) : book ? BookTitle(l.bookId, l.bookCount) : Title(item);
                    r.Name.color = good ? GoodColor : book ? BookColor : GearPanel.RarityColor(item.Rarity);
                    bool active = l.status == nameof(ListingStatus.Active);
                    r.Detail.text = (good ? GoodSummary(l.goodId) : book ? BookSummary(l.bookId) : Summary(item)) + "\n" + (l.status switch
                    {
                        nameof(ListingStatus.Active) => $"On the Exchange  ·  {Left(l.minutesLeft)}  ·  tap to take it back",
                        nameof(ListingStatus.Sold) => ConfirmDialog.Tint($"Sold: you received {Market.Payout(l.price):N0} sorn", Palette.Good),
                        nameof(ListingStatus.Expired) => book || good ? "Nobody bought them: back with you" : "Nobody bought it: back in your bag",
                        _ => "Taken back",
                    });
                    r.Price.text = $"{l.price:N0}\n<size=16>{(active ? "ASKING" : l.status.ToUpperInvariant())}</size>";
                    r.Price.color = active ? Palette.Sorn : Palette.Muted;
                }
                empty = v == null ? "" : $"Nothing listed. Up to {Market.MaxListings} pieces at a time, {Market.ListingHours} hours each.";
            }
            for (int i = 0; i < Rows; i++)
            {
                _rows[i].Button.gameObject.SetActive(i < shown);
                _rows[i].Button.interactable = !_busy;
            }
            _empty.text = shown == 0 ? empty : "";
        }
    }
}
