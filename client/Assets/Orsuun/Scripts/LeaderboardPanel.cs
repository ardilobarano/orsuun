using System.Globalization;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// LEADERBOARDS (owner, 28 Sep 2026: "Leaderboards"): heroes by level, furthest stage and Pit rating, guilds by level and
    /// by this week's raid; THIS WEEK ranks the gains since the week began (Monday 20:00), ALL TIME where each stands. The
    /// top fifty and, under them, the player's own place. Opened from MENU.
    /// </summary>
    public sealed class LeaderboardPanel : MonoBehaviour
    {
        private static readonly string[] Boards = { "level", "stage", "pits", "guilds" };
        private static readonly string[] BoardLabels = { "LEVEL", "STAGE", "PITS", "GUILDS" };
        private const int Rows = 50;

        private sealed class Row
        {
            public GameObject Root;
            public Image Back;
            public Text Rank, Name, Value;
            public string Id;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private readonly Button[] _boardTabs = new Button[4];
        private Button _week, _all;
        private Text _note, _mine, _message;
        private readonly Row[] _rows = new Row[Rows];
        private string _board = "level";
        private bool _weekly;
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("LeaderboardCanvas", 17).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);
            Ui.Backdrop(canvas, "War");
            Ui.Title("Title", canvas, 0.05f, 0.925f, 0.95f, 0.975f, "LEADERBOARDS", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            for (int i = 0; i < Boards.Length; i++)
            {
                string board = Boards[i];
                float x0 = 0.03f + i * 0.2375f;
                _boardTabs[i] = Ui.Button("Board" + i, canvas, x0, 0.855f, x0 + 0.225f, 0.905f, BoardLabels[i], 22, Palette.ButtonIdle, () => Show(board, _weekly), out _);
            }
            _week = Ui.Button("Week", canvas, 0.15f, 0.8f, 0.49f, 0.843f, "THIS WEEK", 22, Palette.ButtonIdle, () => Show(_board, true), out _);
            _all = Ui.Button("All", canvas, 0.51f, 0.8f, 0.85f, 0.843f, "ALL TIME", 22, Palette.ButtonIdle, () => Show(_board, false), out _);
            _note = Ui.Label("Note", canvas, 0.05f, 0.768f, 0.95f, 0.797f, "", 19, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Scroll("List", canvas, 0.03f, 0.2f, 0.97f, 0.765f, out RectTransform content);
            for (int i = 0; i < Rows; i++)
            {
                var row = new Row();
                RectTransform box = new GameObject("Row" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                box.SetParent(content, false);
                box.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;
                row.Root = box.gameObject;
                row.Back = Ui.Framed("Back", box, 0f, 0f, 1f, 1f, new Color(0.07f, 0.06f, 0.1f, 0.92f));
                row.Rank = Ui.Title("Rank", box, 0.02f, 0.1f, 0.12f, 0.9f, "", 26, TextAnchor.MiddleCenter, Palette.Sorn);
                row.Name = Ui.Label("Name", box, 0.13f, 0.08f, 0.7f, 0.92f, "", 22, TextAnchor.MiddleLeft, Palette.Parchment);
                row.Name.supportRichText = true;
                Ui.Raw(row.Name);   // heroes' and guilds' names
                row.Value = Ui.Label("Value", box, 0.7f, 0.1f, 0.97f, 0.9f, "", 22, TextAnchor.MiddleRight, Palette.Parchment);
                Row tapped = row;
                row.Back.gameObject.AddComponent<Button>().onClick.AddListener(() => Tapped(tapped));
                _rows[i] = row;
            }
            Ui.Framed("MineBack", canvas, 0.03f, 0.13f, 0.97f, 0.19f, new Color(0.14f, 0.1f, 0.05f, 0.95f)).raycastTarget = false;
            _mine = Ui.Label("Mine", canvas, 0.06f, 0.13f, 0.94f, 0.19f, "", 22, TextAnchor.MiddleCenter, Palette.Parchment);
            _mine.supportRichText = true;
            _message = Ui.Label("Message", canvas, 0.05f, 0.085f, 0.95f, 0.125f, "", 20, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE HUNT", 28, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _canvas.SetActive(true);
            Show(_board, _weekly);
        }

        public void Close() => _canvas.SetActive(false);

        public void OpenForShot(string board, bool weekly)
        {
            _canvas.SetActive(true);
            Show(System.Array.IndexOf(Boards, board) >= 0 ? board : "level", weekly);
        }

        /// <summary>A hero's row opens their gear (Inspect); a guild's does nothing yet.</summary>
        private void Tapped(Row row)
        {
            if (string.IsNullOrEmpty(row.Id) || _board == "guilds") return;
            _root.Inspect?.Open(row.Id);
        }

        private void Show(string board, bool weekly)
        {
            _board = board;
            _weekly = weekly && board != "pits";
            for (int i = 0; i < Boards.Length; i++) _boardTabs[i].GetComponent<Image>().color = Boards[i] == board ? Palette.Danger : Palette.ButtonIdle;
            _week.gameObject.SetActive(board != "pits");
            _all.gameObject.SetActive(board != "pits");
            _week.GetComponent<Image>().color = _weekly ? Palette.Alloy : Palette.ButtonIdle;
            _all.GetComponent<Image>().color = _weekly ? Palette.ButtonIdle : Palette.Alloy;
            if (!_root.Server.Online) { _message.text = "Offline: the leaderboards need the server."; Fill(null); return; }
            if (_busy) return;
            _busy = true;
            _message.text = "...";
            StartCoroutine(_root.Server.FetchLeaderboard(board, _weekly ? "week" : "all", (dto, error) =>
            {
                _busy = false;
                _message.text = error ?? "";
                if (dto != null && dto.board == _board) Fill(dto);
            }));
        }

        private void Fill(LeaderboardDto dto)
        {
            LeaderRowDto[] rows = dto?.rows ?? new LeaderRowDto[0];
            _note.text = dto?.note ?? "";
            for (int i = 0; i < Rows; i++)
            {
                Row row = _rows[i];
                bool has = i < rows.Length;
                row.Root.SetActive(has);
                if (!has) continue;
                LeaderRowDto r = rows[i];
                row.Id = r.id;
                row.Rank.text = r.rank.ToString(CultureInfo.InvariantCulture);
                row.Name.text = NameText(r);
                row.Value.text = ValueText(r);
                bool me = r.id == _root.Server.AccountId || (_board == "guilds" && _root.Server.Guild != null && r.tag == _root.Server.Guild.tag);
                row.Back.color = me ? new Color(0.3f, 0.2f, 0.07f, 0.95f) : new Color(0.07f, 0.06f, 0.1f, 0.92f);
            }
            LeaderRowDto mine = dto?.mine;
            _mine.text = mine == null || string.IsNullOrEmpty(mine.id)
                ? ConfirmDialog.Tint(dto == null ? "" : _board == "guilds" ? "Your guild is not on this board yet." : "You are not on this board yet.", Palette.Muted)
                : $"{ConfirmDialog.Tint("YOU", Palette.Sorn)}  #{mine.rank}  ·  {ValueText(mine)}";
            if (rows.Length == 0 && dto != null) _message.text = "Nobody is on this board yet.";
        }

        private string NameText(LeaderRowDto r)
        {
            if (_board == "guilds") return $"<b>{r.name}</b>  <color=#B8A98A>[{r.tag}]</color>";
            string tag = string.IsNullOrEmpty(r.tag) ? "" : $"  <color=#B8A98A>[{r.tag}]</color>";
            string title = string.IsNullOrEmpty(r.title) ? "" : $"\n<size=17><color=#FFE9A8>‹{r.title}›</color>  {r.@class}  ·  Lv {r.level}</size>";
            if (title.Length == 0) title = $"\n<size=17><color=#9A927F>{r.@class}  ·  Lv {r.level}</color></size>";
            return $"<b>{r.name}</b>{tag}{title}";
        }

        private string ValueText(LeaderRowDto r)
        {
            string n = r.value.ToString("N0", CultureInfo.InvariantCulture);
            return (_board, _weekly) switch
            {
                ("level", false) => "Lv " + n,
                ("level", true) => "+" + n + " XP",
                ("stage", false) => Content.StageName((int)r.value),
                ("stage", true) => "+" + n + " stages",
                ("pits", _) => n,
                ("guilds", false) => "Lv " + n,
                _ => n + " damage",
            };
        }
    }
}
