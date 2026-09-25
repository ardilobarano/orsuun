using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// DIRECT TRADE (GDD section 8, the trade window; Rules.DirectTrade): ask a hero by name, and when they accept, both put
    /// bag pieces, Technique Scrolls and sorn on the table. LOCK the offer, then CONFIRM once both are locked; any change
    /// drops both back and holds the button five seconds. The window lives on the server: leaving the screen keeps it (the HUD offers the
    /// way back), CANCEL TRADE ends it. Polls the window while open.
    /// </summary>
    public sealed class TradePanel : MonoBehaviour
    {
        private const float PollSeconds = 1.5f;

        private GameRoot _root;
        private GameObject _canvas;
        private GameObject _askView, _answerView, _windowView;
        private InputField _name;
        private Text _status;
        private Text _answer;
        private Text _theirHead, _theirSorn, _myHead;
        private RectTransform _theirList, _myList, _bagList;
        private InputField _sorn;
        private Button _press;
        private Text _pressLabel;
        private Button _acceptButton;
        private Text _message;
        // The stat card of a tapped piece (owner, 25 Sep 2026: "at trade we need to see stats of items, maybe with clicking").
        private GameObject _card;
        private Text _cardName, _cardInfo, _cardStats, _cardCompare, _cardEtchings;
        private GameObject _cardCountRow;
        private InputField _cardCount;
        private Button _cardAction;
        private Text _cardActionLabel;
        private System.Action _cardDo;
        private string _shownKey = "";
        private float _pollAt;
        private float _fetchedAt;
        private string _lastState = "";
        private bool _busy;
        /// <summary>Dev switch -tradecard: the card of their first piece opens as soon as the window shows (screenshots).</summary>
        private bool _cardOnOpen = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-tradecard") >= 0;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("TradeCanvas", 10).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Exchange");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "TRADE", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _status = Ui.Label("Status", canvas, 0.04f, 0.885f, 0.96f, 0.93f, "", 23, TextAnchor.MiddleCenter, Palette.Parchment);
            _status.supportRichText = true;

            // No trade: ask a hero by name.
            _askView = Ui.Rect("Ask", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform ask = _askView.transform;
            Ui.Framed("AskBack", ask, 0.05f, 0.52f, 0.95f, 0.86f, new Color(0.05f, 0.06f, 0.1f, 0.9f));
            Ui.Title("AskHead", ask, 0.08f, 0.79f, 0.92f, 0.845f, "TRADE WITH A HERO", 30, TextAnchor.MiddleCenter, Palette.Sorn);
            Ui.Label("AskNote", ask, 0.08f, 0.72f, 0.92f, 0.79f, "Both of you put pieces, scrolls and sorn on the table, lock, then confirm.", 21,
                TextAnchor.MiddleCenter, Palette.Parchment);
            _name = Ui.Input("Name", ask, 0.1f, 0.645f, 0.9f, 0.705f, "The hero's name", 28, 16);
            Ui.Button("AskButton", ask, 0.25f, 0.56f, 0.75f, 0.625f, "ASK TO TRADE", 28, Palette.ButtonForge, Ask, out _);
            Ui.Label("Rules", ask, 0.06f, 0.42f, 0.94f, 0.51f,
                $"Both need level {Rules.DirectTrade.MinLevel} and an account {Rules.DirectTrade.MinAccountAgeHours} hours old. {Rules.DirectTrade.TaxPercent}% of the sorn " +
                "goes to the tax. Technique Scrolls trade too. Your own heroes share the depot instead.", 20, TextAnchor.MiddleCenter, Palette.Muted);

            // Asked (either way): answer it, or wait.
            _answerView = Ui.Rect("Answer", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform answer = _answerView.transform;
            Ui.Framed("AnswerBack", answer, 0.05f, 0.56f, 0.95f, 0.82f, new Color(0.05f, 0.06f, 0.1f, 0.9f));
            _answer = Ui.Title("AnswerText", answer, 0.08f, 0.7f, 0.92f, 0.8f, "", 30, TextAnchor.MiddleCenter, Palette.Parchment);
            _acceptButton = Ui.Button("Accept", answer, 0.1f, 0.59f, 0.48f, 0.66f, "ACCEPT", 28, Palette.Safe, () => Act("accept"), out _);
            Ui.Button("Decline", answer, 0.52f, 0.59f, 0.9f, 0.66f, "CANCEL", 28, Palette.ButtonIdle, () => Act("cancel"), out _);

            // The window.
            _windowView = Ui.Rect("Window", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform window = _windowView.transform;
            _theirHead = Ui.Title("TheirHead", window, 0.04f, 0.845f, 0.96f, 0.88f, "", 25, TextAnchor.MiddleLeft, Palette.Sorn);
            _theirHead.supportRichText = true;
            Ui.Scroll("Theirs", window, 0.03f, 0.7f, 0.97f, 0.842f, out _theirList);
            _theirSorn = Ui.Label("TheirSorn", window, 0.05f, 0.665f, 0.95f, 0.698f, "", 23, TextAnchor.MiddleLeft, Palette.Parchment);
            _myHead = Ui.Title("MyHead", window, 0.04f, 0.625f, 0.96f, 0.66f, "", 25, TextAnchor.MiddleLeft, Palette.Sorn);
            _myHead.supportRichText = true;
            Ui.Scroll("Mine", window, 0.03f, 0.49f, 0.97f, 0.622f, out _myList);
            _sorn = Ui.Input("Sorn", window, 0.04f, 0.43f, 0.6f, 0.482f, "Sorn to give", 26, 10);
            _sorn.contentType = InputField.ContentType.IntegerNumber;
            Ui.Button("SetSorn", window, 0.62f, 0.43f, 0.96f, 0.482f, "PUT SORN", 22, Palette.ButtonIdle, PutSorn, out _);
            Ui.Section("BagHead", window, 0.15f, 0.385f, 0.85f, 0.422f, "YOUR BAG (TAP TO OFFER)", 22);
            Ui.Scroll("Bag", window, 0.03f, 0.14f, 0.97f, 0.38f, out _bagList);
            _press = Ui.Button("Press", window, 0.03f, 0.015f, 0.4f, 0.075f, "", 24, Palette.Safe, Press, out _pressLabel);
            Ui.Button("CancelTrade", window, 0.42f, 0.015f, 0.7f, 0.075f, "CANCEL TRADE", 20, Palette.Danger, () => Act("cancel"), out _);

            _message = Ui.Label("Message", canvas, 0.04f, 0.082f, 0.96f, 0.135f, "", 22, TextAnchor.MiddleCenter, Palette.Warn);
            Ui.Button("Close", canvas, 0.72f, 0.015f, 0.97f, 0.075f, "BACK", 24, Palette.ButtonIdle, Close, out _);

            _card = Ui.Rect("Card", canvas, 0f, 0f, 1f, 1f).gameObject;
            Ui.Panel("Dim", _card.transform, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, 0.6f));
            Transform card = Ui.Framed("CardBack", _card.transform, 0.05f, 0.24f, 0.95f, 0.78f, new Color(0.06f, 0.06f, 0.11f, 0.98f)).transform;
            _cardName = Ui.Title("Name", card, 0.05f, 0.87f, 0.95f, 0.97f, "", 32, TextAnchor.MiddleCenter, Palette.Parchment);
            _cardInfo = Ui.Label("Info", card, 0.05f, 0.8f, 0.95f, 0.87f, "", 21, TextAnchor.MiddleCenter, Palette.Muted);
            _cardStats = Ui.Title("Stats", card, 0.05f, 0.71f, 0.95f, 0.8f, "", 25, TextAnchor.MiddleCenter, Palette.Parchment);
            _cardCompare = Ui.Label("Compare", card, 0.05f, 0.62f, 0.95f, 0.71f, "", 21, TextAnchor.MiddleCenter, Palette.Parchment);
            _cardEtchings = Ui.Label("Etchings", card, 0.08f, 0.17f, 0.92f, 0.61f, "", 22, TextAnchor.UpperLeft, Palette.Parchment);
            foreach (Text t in new[] { _cardStats, _cardCompare, _cardEtchings }) t.supportRichText = true;
            _cardEtchings.resizeTextForBestFit = false;
            // A scroll stack offered: how many.
            _cardCountRow = Ui.Rect("CountRow", card, 0f, 0f, 1f, 1f).gameObject;
            Ui.Label("CountLabel", _cardCountRow.transform, 0.08f, 0.19f, 0.4f, 0.28f, "How many", 24, TextAnchor.MiddleLeft, Palette.Muted);
            _cardCount = Ui.Input("Count", _cardCountRow.transform, 0.42f, 0.185f, 0.92f, 0.285f, "1", 30, 3);
            _cardCount.contentType = InputField.ContentType.IntegerNumber;
            _cardAction = Ui.Button("Action", card, 0.06f, 0.03f, 0.48f, 0.14f, "", 24, Palette.Safe, () => { _card.SetActive(false); _cardDo?.Invoke(); }, out _cardActionLabel);
            Ui.Button("CardClose", card, 0.52f, 0.03f, 0.94f, 0.14f, "CLOSE", 24, Palette.ButtonIdle, () => _card.SetActive(false), out _);
            _card.SetActive(false);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _message.text = _root.Server.Online ? "" : "Trading needs the server.";
            _shownKey = "";
            _lastState = "";
            _canvas.SetActive(true);
            _pollAt = 0f;
        }

        public void Close() => _canvas.SetActive(false);

        private void Run(System.Collections.IEnumerator call) => StartCoroutine(call);

        private void Done(string error)
        {
            _busy = false;
            _fetchedAt = Time.realtimeSinceStartup;
            var t = _root.Server.Trade;
            _message.text = error ?? t?.message ?? "";
        }

        private void Ask()
        {
            string name = _name.text.Trim();
            if (_busy || name.Length == 0) return;
            _busy = true;
            Run(_root.Server.TradeInvite(name, Done));
        }

        private void Act(string action)
        {
            if (_busy) return;
            _busy = true;
            Run(_root.Server.TradeAct(action, Done));
        }

        private void Press()
        {
            if (_busy) return;
            _busy = true;
            Run(_root.Server.TradeAct("press", Done));
        }

        private List<string> OfferedIds()
        {
            var ids = new List<string>();
            var mine = _root.Server.Trade?.myItems;
            if (mine != null) foreach (var i in mine) ids.Add(i.id);
            return ids;
        }

        /// <summary>The scrolls this side has on the table, by book id.</summary>
        private Dictionary<int, int> OfferedBooks()
        {
            var books = new Dictionary<int, int>();
            var mine = _root.Server.Trade?.myBooks;
            if (mine != null) foreach (var b in mine) books[b.bookId] = b.count;
            return books;
        }

        /// <summary>Puts the whole offer on the table: these pieces and sorn, and the scrolls (this side's current ones if null).</summary>
        private void Offer(List<string> ids, long sorn, Dictionary<int, int> books = null)
        {
            if (_busy) return;
            _busy = true;
            var list = new List<Net.ServerLink.BookOfferDto>();
            foreach (var b in books ?? OfferedBooks()) if (b.Value > 0) list.Add(new Net.ServerLink.BookOfferDto { bookId = b.Key, count = b.Value });
            Run(_root.Server.TradeOffer(ids.ToArray(), sorn, Done, list.ToArray()));
        }

        private static string BookLine(int book, int count) =>
            $"<b><color=#EDCC8C>{count} × {Rules.Books.Name(book)}</color></b>  <color=#B9B3A8>· {Rules.Books.ClassOf(book)} skill</color>";

        private void PutSorn()
        {
            long.TryParse(_sorn.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long sorn);
            Offer(OfferedIds(), System.Math.Max(0, sorn));
        }

        /// <summary>A piece's row: its name in its rarity's colour, +level, slot and item level.</summary>
        private static string Line(Net.ServerLink.ItemDto item)
        {
            Color color = System.Enum.TryParse(item.rarity, out Rules.Rarity rarity) ? GearPanel.RarityColor(rarity) : Palette.Parchment;
            return $"<b><color=#{ColorUtility.ToHtmlStringRGB(color)}>{item.name}</color>{(item.upgradeLevel > 0 ? " +" + item.upgradeLevel : "")}</b>"
                   + $"  <color=#B9B3A8>· {item.slot} · item level {item.itemLevel}</color>";
        }

        private static string StepText(string step) => step switch
        {
            "Locked" => "<color=#FFD76B>LOCKED</color>",
            "Confirmed" => "<color=#8FE08F>CONFIRMED</color>",
            _ => "<color=#B9B3A8>offering</color>",
        };

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            var server = _root.Server;
            if (server.Online && !_busy && Time.realtimeSinceStartup >= _pollAt)
            {
                _pollAt = Time.realtimeSinceStartup + PollSeconds;
                _busy = true;
                Run(server.FetchTrade(error => { _busy = false; _fetchedAt = Time.realtimeSinceStartup; if (error != null) _message.text = error; }));
            }

            var t = server.Trade;
            string state = t == null || t.id == 0 ? "None" : t.state;
            if (state != _lastState)
            {
                // The other side finished the trade, or it ended: say so once, and bring the bag up to date.
                if (_lastState == "Open" && (state == "None" || state == "Cancelled" || state == "Done"))
                {
                    _message.text = t != null && t.message == "Traded." ? "Traded." : "The trade is over.";
                    Run(server.RefreshState());
                }
                _lastState = state;
            }
            bool live = state == "Invited" || state == "Open";
            _askView.SetActive(!live);
            _answerView.SetActive(state == "Invited");
            _windowView.SetActive(state == "Open");

            if (!live)
            {
                _status.text = "Trade pieces and sorn with another hero.";
                return;
            }
            if (state == "Invited")
            {
                _status.text = t.incoming ? "An invitation" : "Waiting for an answer";
                _answer.text = t.incoming ? $"{t.otherName} asks to trade." : $"Waiting for {t.otherName}...";
                _acceptButton.gameObject.SetActive(t.incoming);
                return;
            }

            int lockLeft = Mathf.Max(0, t.lockLeft - (int)(Time.realtimeSinceStartup - _fetchedAt));
            _status.text = $"Trading with <b>{t.otherName}</b>" + (t.rulesRelaxed ? "  <color=#B9B3A8>(playtest: level and age rules off)</color>" : "");
            _theirHead.text = $"{t.otherName.ToUpperInvariant()} OFFERS  ·  {StepText(t.theirStep)}";
            _theirSorn.text = $"Sorn: {t.theirSorn:N0}" + (t.theirSorn > 0 ? $"  <color=#B9B3A8>(you get {Rules.DirectTrade.Received(t.theirSorn):N0} after the {t.taxPercent}% tax)</color>" : "");
            _theirSorn.supportRichText = true;
            _myHead.text = $"YOU OFFER  ·  {StepText(t.myStep)}  <color=#B9B3A8>· {t.mySorn:N0} sorn</color>";

            string mine = t.myStep, theirs = t.theirStep;
            _pressLabel.text = lockLeft > 0 ? $"WAIT {lockLeft}"
                : mine == "Offering" ? "LOCK OFFER"
                : mine == "Locked" ? (theirs == "Offering" ? "LOCKED" : "CONFIRM")
                : "CONFIRMED";
            _press.interactable = !_busy && lockLeft == 0 && (mine == "Offering" || (mine == "Locked" && theirs != "Offering"));
            _press.targetGraphic.color = mine == "Locked" && theirs != "Offering" ? Palette.ButtonForge : Palette.Safe;

            // Lists: rebuilt when the offers or the bag change.
            var key = new StringBuilder();
            if (t.theirItems != null) foreach (var i in t.theirItems) key.Append(i.id).Append(',');
            key.Append('|');
            if (t.myItems != null) foreach (var i in t.myItems) key.Append(i.id).Append(',');
            key.Append('|');
            foreach (var i in BagItems()) key.Append(i.id).Append(',');
            key.Append('|');
            if (t.theirBooks != null) foreach (var b in t.theirBooks) key.Append(b.bookId).Append(':').Append(b.count).Append(',');
            key.Append('|');
            if (t.myBooks != null) foreach (var b in t.myBooks) key.Append(b.bookId).Append(':').Append(b.count).Append(',');
            key.Append('|');
            foreach (int n in _root.Session.Inventory.Books) key.Append(n).Append(',');
            if (_cardOnOpen && t.theirItems != null && t.theirItems.Length > 0)
            {
                _cardOnOpen = false;
                ShowCard(t.theirItems[0], null, null);
            }
            if (key.ToString() == _shownKey) return;
            _shownKey = key.ToString();

            foreach (Transform child in _theirList) Destroy(child.gameObject);
            foreach (Transform child in _myList) Destroy(child.gameObject);
            foreach (Transform child in _bagList) Destroy(child.gameObject);
            bool theirBooks = t.theirBooks != null && t.theirBooks.Length > 0;
            if ((t.theirItems == null || t.theirItems.Length == 0) && !theirBooks) Ui.ListRow("None", _theirList, 21, () => { }).text = "<color=#B9B3A8>Nothing yet.</color>";
            if (t.theirItems != null)
                foreach (var item in t.theirItems)
                {
                    var piece = item;
                    Ui.ListRow("Theirs", _theirList, 22, () => ShowCard(piece, null, null)).text = "<color=#9FC7FF>STATS</color>   " + Line(item);
                }
            if (theirBooks)
                foreach (var b in t.theirBooks)
                {
                    var stack = b;
                    Ui.ListRow("TheirBook", _theirList, 22, () => ShowBookCard(stack.bookId, stack.count, null, null)).text = "<color=#9FC7FF>BOOK</color>   " + BookLine(b.bookId, b.count);
                }
            var offered = OfferedIds();
            var offeredBooks = OfferedBooks();
            if (offered.Count == 0 && offeredBooks.Count == 0)
                Ui.ListRow("None", _myList, 21, () => { }).text = "<color=#B9B3A8>Tap a piece or a scroll below to see it and offer it.</color>";
            if (t.myItems != null)
                foreach (var item in t.myItems)
                {
                    string id = item.id;
                    var piece = item;
                    Ui.ListRow("Mine", _myList, 22, () => ShowCard(piece, "TAKE BACK", () => { var ids = OfferedIds(); ids.Remove(id); Offer(ids, _root.Server.Trade.mySorn); })).text =
                        "<color=#9FC7FF>STATS</color>   " + Line(item);
                }
            foreach (var b in offeredBooks)
            {
                int book = b.Key;
                Ui.ListRow("MyBook", _myList, 22, () => ShowBookCard(book, b.Value, "TAKE BACK", _ =>
                {
                    var books = OfferedBooks();
                    books.Remove(book);
                    Offer(OfferedIds(), _root.Server.Trade.mySorn, books);
                })).text = "<color=#9FC7FF>BOOK</color>   " + BookLine(book, b.Value);
            }
            int shown = 0;
            // Scrolls first (held beyond those already on the table), then the bag's pieces.
            for (int b = 0; b < Rules.Books.Count; b++)
            {
                int spare = _root.Session.Inventory.Books[b] - (offeredBooks.TryGetValue(b, out int on) ? on : 0);
                if (spare <= 0) continue;
                int book = b;
                Ui.ListRow("BagBook", _bagList, 22, () => ShowBookCard(book, spare, "OFFER THEM", count =>
                {
                    var books = OfferedBooks();
                    books[book] = (books.TryGetValue(book, out int already) ? already : 0) + count;
                    Offer(OfferedIds(), _root.Server.Trade.mySorn, books);
                })).text = "<color=#9FC7FF>BOOK</color>   " + BookLine(book, spare);
                shown++;
            }
            foreach (var item in BagItems())
            {
                if (offered.Contains(item.id)) continue;
                string id = item.id;
                var piece = item;
                Ui.ListRow("Bag", _bagList, 22, () => ShowCard(piece, "OFFER IT", () =>
                {
                    var ids = OfferedIds();
                    if (ids.Count >= Rules.DirectTrade.MaxPieces) { _message.text = $"At most {Rules.DirectTrade.MaxPieces} pieces a trade."; return; }
                    ids.Add(id);
                    Offer(ids, _root.Server.Trade.mySorn);
                })).text = "<color=#9FC7FF>STATS</color>   " + Line(item);
                shown++;
            }
            if (shown == 0) Ui.ListRow("Empty", _bagList, 21, () => { }).text = "<color=#B9B3A8>Nothing in the bag to offer (worn pieces stay on).</color>";
        }

        /// <summary>
        /// The stat card: the piece's own stats, what wearing it would change for this hero, its etchings and sockets; with
        /// an action (offer it, take it back) for this side's pieces.
        /// </summary>
        private void ShowCard(Net.ServerLink.ItemDto dto, string action, System.Action act)
        {
            Rules.ItemState item = Net.ServerLink.ToState(dto);
            Rules.PlayerSession session = _root.Session;
            _cardName.text = $"{item.DisplayName} +{item.UpgradeLevel}";
            _cardName.color = GearPanel.RarityColor(item.Rarity);
            _cardInfo.text = $"{GearPanel.SlotNames[(int)item.Slot]}  ·  Item level {item.ItemLevel}  ·  {item.Rarity}";
            Rules.Combat.HeroStats bare = Rules.HeroFactory.FromEquipment(System.Array.Empty<Rules.ItemState>(), session.Level);
            _cardStats.text = GearPanel.Stats(Rules.HeroFactory.FromEquipment(new[] { item }, session.Level), bare, signed: false);
            var swapped = new List<Rules.ItemState>();
            foreach (Rules.ItemState e in session.Equipment) if (e.Slot != item.Slot) swapped.Add(e);
            swapped.Add(item);
            string delta = GearPanel.Stats(Rules.HeroFactory.FromEquipment(swapped, session.Level, session.Class, session.Worn), session.Hero, signed: true);
            _cardCompare.text = "If you wore it:  " + (delta.Length == 0 ? ConfirmDialog.Tint("no change", Palette.Muted) : delta);

            var sb = new StringBuilder();
            Rules.EtchingPool pool = Rules.EtchingPool.For(item.Slot);
            if (item.Etchings.Count == 0) sb.Append(ConfirmDialog.Tint("No etchings.", Palette.Muted)).Append('\n');
            foreach (Rules.Etching e in item.Etchings)
            {
                string line = $"T{e.Tier}   {pool.Entries[e.EntryId].Name}  +{e.Value}";
                sb.Append(e.Tier >= 4 ? ConfirmDialog.Tint(line, Palette.Sorn) : line).Append('\n');
            }
            var sockets = new List<string>();
            foreach (Rules.Socket s in item.Sockets)
                sockets.Add(s.Dead ? "Dead Shard" : s.Type == null ? "empty" : Rules.SocketRules.Name(s.Type.Value) + " " + Rules.Content.KorshardRanks[s.Rank]);
            sb.Append('\n').Append(ConfirmDialog.Tint(sockets.Count == 0 ? "No sockets" : "Sockets: " + string.Join("  ·  ", sockets), Palette.Muted));
            _cardEtchings.text = sb.ToString();

            _cardDo = act;
            _cardAction.gameObject.SetActive(action != null);
            _cardActionLabel.text = action ?? "";
            _cardCountRow.SetActive(false);
            _card.SetActive(true);
        }

        /// <summary>A scroll stack's card: what it is; offering asks how many (up to <paramref name="count"/>).</summary>
        private void ShowBookCard(int book, int count, string action, System.Action<int> act)
        {
            Rules.Combat.HeroClass cls = Rules.Books.ClassOf(book);
            _cardName.text = Rules.Books.Name(book);
            _cardName.color = new Color(0.93f, 0.8f, 0.55f);
            _cardInfo.text = $"A {cls} skill's Technique Scroll  ·  {count} here";
            _cardStats.text = cls == _root.Session.Class ? ConfirmDialog.Tint("Your class reads it.", Palette.Good) : ConfirmDialog.Tint($"Only a {cls} reads it.", Palette.Muted);
            _cardCompare.text = "";
            _cardEtchings.text = $"Read at SKILLS to climb {Rules.Books.SkillName(book)}'s Mastered grades: {Rules.SkillGrades.ReadChanceBp / 100}% a read, "
                                 + $"then {Rules.SkillGrades.ReadCooldownHours} hours of rest.";
            bool asking = action == "OFFER THEM";
            _cardCountRow.SetActive(asking);
            _cardCount.text = count.ToString();
            _cardDo = act == null ? null : () =>
            {
                int n = asking ? (int.TryParse(_cardCount.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int typed) ? typed : 0) : count;
                if (n < 1 || n > count) { _message.text = $"Between 1 and {count}."; return; }
                act(n);
            };
            _cardAction.gameObject.SetActive(action != null);
            _cardActionLabel.text = action ?? "";
            _card.SetActive(true);
        }

        /// <summary>The pieces in the bag (not worn; listed and depot pieces are not in the state's items).</summary>
        private List<Net.ServerLink.ItemDto> BagItems()
        {
            var list = new List<Net.ServerLink.ItemDto>();
            var items = _root.Server.LastItems;
            if (items != null) foreach (var i in items) if (!i.equipped) list.Add(i);
            return list;
        }
    }
}
