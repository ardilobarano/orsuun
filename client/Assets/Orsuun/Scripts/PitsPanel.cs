using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// THE PITS (Rules.Pits, server GameService.Pits): the league, rating, record, Laurels and tickets; three challengers with
    /// their weapon and the odds, FIGHT and LOOK AGAIN; the Pit shop's Korshards; the board of the best with their weapons
    /// (the GDD's gear inspection, in brief). A fight closes the screen for its replay and brings it back.
    /// </summary>
    public sealed class PitsPanel : MonoBehaviour
    {
        private const float RefreshSeconds = 20f;

        private sealed class Card
        {
            public Image Back;
            public Text Name;
            public Text Line;
            public Text Odds;
            public Button Fight;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private Text _league;
        private Text _record;
        private Text _message;
        private readonly Card[] _cards = new Card[Pits.Challengers];
        private readonly Button[] _shop = new Button[3];
        private readonly Text[] _board = new Text[8];
        private float _nextFetch;
        private bool _fetching;
        private bool _busy;
        private string _note = "";

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("PitsCanvas", 11).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "War");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "THE PITS", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);

            Ui.Framed("RecordBack", canvas, 0.04f, 0.8f, 0.96f, 0.915f, new Color(0.06f, 0.06f, 0.12f, 0.93f));
            _league = Ui.Title("League", canvas, 0.07f, 0.855f, 0.93f, 0.905f, "", 38, TextAnchor.MiddleCenter, Palette.Sorn);
            _league.supportRichText = true;
            _record = Ui.Label("Record", canvas, 0.07f, 0.828f, 0.93f, 0.858f, "", 24, TextAnchor.MiddleCenter, Palette.Parchment);
            _message = Ui.Label("Message", canvas, 0.07f, 0.804f, 0.93f, 0.83f, "", 20, TextAnchor.MiddleCenter, Palette.Warn);

            Ui.Section("ChallengersHead", canvas, 0.04f, 0.752f, 0.7f, 0.79f, "CHALLENGERS", 26);
            Ui.Button("Refresh", canvas, 0.72f, 0.752f, 0.96f, 0.79f, "LOOK AGAIN", 20, Palette.ButtonIdle, Refresh, out _);
            for (int i = 0; i < _cards.Length; i++)
            {
                int index = i;
                float y1 = 0.745f - i * 0.08f;
                var c = new Card();
                c.Back = Ui.Framed("Challenger" + i, canvas, 0.04f, y1 - 0.075f, 0.96f, y1, new Color(0.07f, 0.07f, 0.13f, 0.95f));
                Transform t = c.Back.transform;
                c.Name = Ui.Title("Name", t, 0.04f, 0.52f, 0.66f, 0.92f, "", 26, TextAnchor.MiddleLeft, Palette.Parchment);
                c.Name.supportRichText = true;
                c.Line = Ui.Label("Line", t, 0.04f, 0.1f, 0.66f, 0.5f, "", 20, TextAnchor.MiddleLeft, Palette.Muted);
                c.Odds = Ui.Title("Odds", t, 0.62f, 0.1f, 0.76f, 0.92f, "", 24, TextAnchor.MiddleCenter, Palette.Parchment);
                c.Fight = Ui.Button("Fight", t, 0.77f, 0.14f, 0.97f, 0.86f, "FIGHT", 24, Palette.Danger, () => Fight(index), out _);
                _cards[i] = c;
            }

            Ui.Section("ShopHead", canvas, 0.2f, 0.465f, 0.8f, 0.5f, "PIT SHOP", 26);
            for (int i = 0; i < _shop.Length; i++)
            {
                PitShopItem item = Pits.Shop[i];
                float x0 = 0.04f + i * 0.31f;
                _shop[i] = Ui.IconButton("Shop" + i, canvas, x0, 0.395f, x0 + 0.3f, 0.458f, $"{item.Name.Replace(" Korshard", "")}\n<size=16>{item.Laurels} Laurels</size>",
                    20, Palette.Alloy, SocketPanel.RankIcons[item.KorshardRank], () => Buy(item.Id), out _);
            }

            Ui.Section("BoardHead", canvas, 0.2f, 0.345f, 0.8f, 0.38f, "THE BOARD", 26);
            Ui.Framed("BoardBack", canvas, 0.04f, 0.085f, 0.96f, 0.34f, new Color(0.06f, 0.06f, 0.12f, 0.9f));
            for (int i = 0; i < _board.Length; i++)
            {
                float y1 = 0.332f - i * 0.03f;
                _board[i] = Ui.Label("Board" + i, canvas, 0.07f, y1 - 0.029f, 0.93f, y1, "", 20, TextAnchor.MiddleLeft, Palette.Parchment);
                _board[i].supportRichText = true;
            }
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO WAR", 28, Palette.ButtonIdle, () => { Close(); _root.War.Open(); }, out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _note = _root.Server.Online ? "" : "The Pits need the server.";
            _nextFetch = 0f;
            _canvas.SetActive(true);
        }

        public void Close() => _canvas.SetActive(false);

        /// <summary>Shown when a fight's call failed (the replay never started).</summary>
        public void Say(string text) => _note = text;

        private static Color LeagueColor(string league) => league switch
        {
            "Iron" => new Color(0.72f, 0.74f, 0.78f),
            "Silver" => new Color(0.86f, 0.88f, 0.94f),
            "Gold" => new Color(1f, 0.82f, 0.36f),
            "Jade" => new Color(0.42f, 0.86f, 0.62f),
            "Khagan" => new Color(1f, 0.45f, 0.3f),
            _ => new Color(0.8f, 0.55f, 0.35f),
        };

        private void Refresh()
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(_root.Server.PitCall("refresh", null, (message, error) => { _busy = false; _note = error ?? message ?? ""; }));
        }

        private void Buy(int itemId)
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(_root.Server.PitCall("shop", new Net.ServerLink.PitShopRequest { requestId = Net.ServerLink.NewRequestId(), itemId = itemId },
                (message, error) => { _busy = false; _note = error ?? message ?? ""; }));
        }

        private void Fight(int index)
        {
            Net.ServerLink.PitsDto pits = _root.Server.Pits;
            if (_busy || pits?.challengers == null || index >= pits.challengers.Length || _root.Replaying || _root.PushBusy) return;
            _note = "";
            _root.FightPit(pits.challengers[index].id);
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            if (_root.Server.Online && !_fetching && Time.realtimeSinceStartup >= _nextFetch)
            {
                _fetching = true;
                StartCoroutine(_root.Server.FetchPits(error =>
                {
                    _fetching = false;
                    _nextFetch = Time.realtimeSinceStartup + RefreshSeconds;
                    if (error != null) _note = error;
                }));
            }

            Net.ServerLink.PitsDto pits = _root.Server.Pits;
            _message.text = _note;
            if (pits?.challengers == null)
            {
                _league.text = _root.Server.Online ? "Sanding the pit floor..." : "";
                _record.text = "";
                foreach (Card c in _cards) c.Back.gameObject.SetActive(false);
                return;
            }

            _league.text = ConfirmDialog.Tint(pits.league.ToUpperInvariant(), LeagueColor(pits.league)) + $"  ·  {pits.rating:N0}";
            _record.text = $"{pits.wins} won  ·  {pits.losses} lost  ·  {pits.laurels} Laurels  ·  tickets today {pits.ticketsLeft}/{Pits.TicketsPerDay}";
            for (int i = 0; i < _cards.Length; i++)
            {
                Card c = _cards[i];
                bool has = i < pits.challengers.Length;
                c.Back.gameObject.SetActive(has);
                if (!has) continue;
                Net.ServerLink.PitChallengerDto d = pits.challengers[i];
                c.Name.text = (string.IsNullOrEmpty(d.tag) ? "" : $"[{d.tag}] ") + d.name;
                c.Name.color = d.shade ? Palette.Muted : Palette.Parchment;
                c.Line.text = $"{d.league} {d.rating:N0}  ·  {d.@class}  ·  {d.weapon}" + (d.shade ? "  ·  a Pit shade" : "");
                c.Odds.text = d.winChancePercent + "%";
                c.Odds.color = d.winChancePercent >= 60 ? Palette.Good : d.winChancePercent >= 40 ? Palette.Sorn : Palette.Bad;
                c.Fight.interactable = !_busy && pits.ticketsLeft > 0 && !_root.Replaying && !_root.PushBusy;
            }
            for (int i = 0; i < _shop.Length; i++) _shop[i].interactable = !_busy && pits.laurels >= Pits.Shop[i].Laurels;

            for (int i = 0; i < _board.Length; i++)
            {
                bool has = pits.board != null && i < pits.board.Length;
                bool empty = i == 0 && (pits.board == null || pits.board.Length == 0);
                _board[i].gameObject.SetActive(has || empty);
                if (empty) _board[i].text = ConfirmDialog.Tint("No one has fought in the pits yet. Be the first.", Palette.Muted);
                if (!has) continue;
                Net.ServerLink.PitBoardDto r = pits.board[i];
                string line = $"{r.rank}.  {(string.IsNullOrEmpty(r.tag) ? "" : "[" + r.tag + "] ")}{r.name}   ·   "
                              + ConfirmDialog.Tint($"{r.league} {r.rating:N0}", LeagueColor(r.league)) + $"   ·   {r.wins}-{r.losses}   ·   {r.weapon}";
                _board[i].text = r.me ? ConfirmDialog.Tint(line, Palette.Sorn) : line;
            }
        }
    }
}
