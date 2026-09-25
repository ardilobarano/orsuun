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
    /// The server decides everything; the seller gets the price less the Exchange's 5%.
    /// </summary>
    public sealed class MarketPanel : MonoBehaviour
    {
        private const int Rows = 8;
        private const float RefreshSeconds = 20f;
        private static readonly string[] SlotChips = { "ALL", "WPN", "ARM", "HELM", "SHLD", "BRAC", "NECK", "EAR", "SHOE" };
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

        // SELL: the price box for one bag piece.
        private GameObject _sellBox;
        private Text _sellTitle;
        private Text _sellInfo;
        private InputField _price;
        private Text _payout;
        private string _sellItemId;

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
                float x0 = 0.04f + i * 0.1022f;
                _chips[i] = Ui.Button("Chip" + i, _filters.transform, x0, 0.795f, x0 + 0.097f, 0.835f, SlotChips[i], 18, Palette.ButtonIdle,
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
            Ui.Button("Trade", canvas, 0.03f, 0.015f, 0.47f, 0.075f, "DIRECT TRADE", 26, Palette.Alloy, () => { Close(); _root.Trade.Open(); }, out _);
            Ui.Button("Close", canvas, 0.5f, 0.015f, 0.97f, 0.075f, "BACK TO THE HUNT", 26, Palette.ButtonIdle, Close, out _);

            _sellBox = Ui.Rect("SellBox", canvas, 0f, 0f, 1f, 1f).gameObject;
            Image dim = Ui.Panel("Dim", _sellBox.transform, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.65f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => _sellBox.SetActive(false));
            Transform box = Ui.Framed("Box", _sellBox.transform, 0.06f, 0.3f, 0.94f, 0.72f, Palette.PanelDark).transform;
            _sellTitle = Ui.Title("Title", box, 0.05f, 0.84f, 0.95f, 0.97f, "", 32, TextAnchor.MiddleCenter, Palette.Parchment);
            _sellInfo = Ui.Label("Info", box, 0.06f, 0.5f, 0.94f, 0.84f, "", 22, TextAnchor.UpperLeft, Palette.Parchment);
            _sellInfo.supportRichText = true;
            Ui.Label("PriceLabel", box, 0.06f, 0.37f, 0.4f, 0.48f, "Price in sorn", 24, TextAnchor.MiddleLeft, Palette.Muted);
            _price = Ui.Input("Price", box, 0.42f, 0.36f, 0.94f, 0.49f, "e.g. 50000", 30, 10);
            _price.contentType = InputField.ContentType.IntegerNumber;
            _payout = Ui.Label("Payout", box, 0.06f, 0.24f, 0.94f, 0.35f, "", 22, TextAnchor.MiddleLeft, Palette.Muted);
            Ui.Button("List", box, 0.06f, 0.04f, 0.6f, 0.2f, "LIST IT", 30, Palette.ButtonForge, AskList, out _);
            Ui.Button("Cancel", box, 0.64f, 0.04f, 0.94f, 0.2f, "CANCEL", 26, Palette.ButtonIdle, () => _sellBox.SetActive(false), out _);
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
        private static string Title(ItemState item) => $"+{item.UpgradeLevel}  {item.DisplayName}";

        private static string Summary(ItemState item)
        {
            int sockets = 0;
            foreach (Socket s in item.Sockets) if (!s.Dead && s.Type != null) sockets++;
            return $"{GearPanel.SlotNames[(int)item.Slot]}  ·  item level {item.ItemLevel}  ·  {item.Etchings.Count} etching{(item.Etchings.Count == 1 ? "" : "s")}"
                   + (sockets > 0 ? $"  ·  {sockets} shard{(sockets == 1 ? "" : "s")}" : "");
        }

        private static string Etchings(ItemState item)
        {
            EtchingPool pool = EtchingPool.For(item.Slot);
            var sb = new StringBuilder();
            foreach (Etching e in item.Etchings)
            {
                string line = $"T{e.Tier}  {pool.Entries[e.EntryId].Name}  +{e.Value}";
                sb.Append(e.Tier >= 4 ? ConfirmDialog.Tint(line, Palette.Sorn) : line).Append('\n');
            }
            foreach (Socket s in item.Sockets)
                if (!s.Dead && s.Type != null) sb.Append(ConfirmDialog.Tint(SocketRules.Name(s.Type.Value) + " " + Content.KorshardRanks[s.Rank], Palette.Muted)).Append('\n');
            return sb.Length == 0 ? ConfirmDialog.Tint("No etchings.", Palette.Muted) : sb.ToString().TrimEnd();
        }

        private static string Left(int minutes) => minutes >= 60 ? $"{minutes / 60}h left" : $"{minutes}m left";

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
                ItemState item = ToState(l.item);
                if (l.mine) { _message.text = "That is your own listing: take it back under MY LISTINGS."; return; }
                bool afford = _root.Session.Inventory.Sorn >= l.price;
                _confirm.Show(Title(item), Summary(item) + "\n\n" + Etchings(item) + $"\n\nSold by {l.sellerName}  ·  {Left(l.minutesLeft)}"
                              + (afford ? "" : "\n" + ConfirmDialog.Tint("You do not have enough sorn.", Palette.Bad)),
                    $"BUY  ·  {l.price:N0}", Palette.Safe, () => Call("buy", new MarketBuyRequest { requestId = NewRequestId(), listingId = l.id }));
            }
            else if (_tab == Tab.Sell)
            {
                var bag = Sellable();
                int index = _sellPage * Rows + row;
                if (index >= bag.Count) return;
                (ItemState item, string id) = bag[index];
                _sellItemId = id;
                _sellTitle.text = Title(item);
                _sellTitle.color = GearPanel.RarityColor(item.Rarity);
                _sellInfo.text = Summary(item) + "\n" + Etchings(item);
                _price.text = "";
                _sellBox.SetActive(true);
            }
            else
            {
                if (v?.mine == null || row >= v.mine.Length) return;
                ListingDto l = v.mine[row];
                if (l.status != nameof(ListingStatus.Active)) return;
                ItemState item = ToState(l.item);
                _confirm.Show("Take it back?", $"{Title(item)} comes off the Exchange and back into your bag.", "TAKE BACK", Palette.ButtonIdle,
                    () => Call("cancel", new MarketBuyRequest { requestId = NewRequestId(), listingId = l.id }));
            }
        }

        private long PriceEntered() => long.TryParse(_price.text, out long p) ? p : 0;

        private void AskList()
        {
            long price = PriceEntered();
            if (Market.PriceProblem(price) is string problem) { _payout.text = ConfirmDialog.Tint(problem, Palette.Bad); return; }
            string id = _sellItemId;
            string title = _sellTitle.text;
            _sellBox.SetActive(false);
            _confirm.Show("List " + title + "?",
                $"Price {price:N0} sorn. When it sells you receive {Market.Payout(price):N0} (the Exchange keeps {Market.TaxPercent}%).\n\nIt leaves your bag now and comes back if nobody buys it within {Market.ListingHours} hours.",
                "LIST IT", Palette.ButtonForge, () => Call("list", new MarketListRequest { requestId = NewRequestId(), itemId = id, price = price }));
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            if (_root.Server.Online && !_fetching && !_busy && Time.realtimeSinceStartup >= _nextFetch)
            {
                _fetching = true;
                string slot = _slot < 0 ? "" : ((EquipSlot)_slot).ToString();
                StartCoroutine(_root.Server.FetchMarket(slot, Sorts[_sort], _page, error =>
                {
                    _fetching = false;
                    _nextFetch = Time.realtimeSinceStartup + RefreshSeconds;
                    if (error != null) _message.text = ConfirmDialog.Tint(error, Palette.Bad);
                }));
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
                    ItemState item = ToState(l.item);
                    Row r = _rows[shown];
                    r.Name.text = Title(item);
                    r.Name.color = GearPanel.RarityColor(item.Rarity);
                    r.Detail.text = Summary(item) + $"\n{l.sellerName}  ·  {Left(l.minutesLeft)}";
                    r.Price.text = $"{l.price:N0}\n<size=16>{(l.mine ? "YOURS" : "sorn")}</size>";
                    r.Price.color = _root.Session.Inventory.Sorn >= l.price || l.mine ? Palette.Sorn : Palette.Bad;
                }
                empty = v == null ? "Opening the Exchange..." : "Nothing listed here yet.";
            }
            else if (_tab == Tab.Sell)
            {
                var bag = Sellable();
                int pages = Mathf.Max(1, (bag.Count + Rows - 1) / Rows);
                _sellPage = Mathf.Clamp(_sellPage, 0, pages - 1);
                _pageText.text = $"{_sellPage + 1} / {pages}";
                for (; shown < Rows && _sellPage * Rows + shown < bag.Count; shown++)
                {
                    ItemState item = bag[_sellPage * Rows + shown].Item;
                    Row r = _rows[shown];
                    r.Name.text = Title(item);
                    r.Name.color = GearPanel.RarityColor(item.Rarity);
                    r.Detail.text = Summary(item) + "\nTap to set a price";
                    r.Price.text = "SELL";
                    r.Price.color = Palette.Sorn;
                }
                empty = "Your bag is empty. Worn pieces must be taken off (GEAR) before they can be sold.";
            }
            else
            {
                ListingDto[] mine = v?.mine ?? new ListingDto[0];
                _pageText.text = "";
                for (; shown < mine.Length && shown < Rows; shown++)
                {
                    ListingDto l = mine[shown];
                    ItemState item = ToState(l.item);
                    Row r = _rows[shown];
                    r.Name.text = Title(item);
                    r.Name.color = GearPanel.RarityColor(item.Rarity);
                    bool active = l.status == nameof(ListingStatus.Active);
                    r.Detail.text = Summary(item) + "\n" + (l.status switch
                    {
                        nameof(ListingStatus.Active) => $"On the Exchange  ·  {Left(l.minutesLeft)}  ·  tap to take it back",
                        nameof(ListingStatus.Sold) => ConfirmDialog.Tint($"Sold: you received {Market.Payout(l.price):N0} sorn", Palette.Good),
                        nameof(ListingStatus.Expired) => "Nobody bought it: back in your bag",
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
