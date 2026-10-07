using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The Endless Tower's screen (owner, 7 Oct 2026; Rules.Tower), opened from ZONES: the hero's best floor this week and
    /// place on the ladder, the climbs left today and CLIMB (the lane replays the climb's last floors, then this screen comes
    /// back), the next milestone chest, and the week's ten best climbers. -towershow opens it once the hero is in, -towerclimb
    /// also climbs (screenshots).
    /// </summary>
    public sealed class TowerPanel : MonoBehaviour
    {
        private const int Rows = Tower.PaidRanks;
        private GameRoot _root;
        private GameObject _canvas;
        private Text _week, _best, _climbs, _chest, _message, _climbLabel, _empty;
        private Button _climb;
        private Image _climbImage;
        private readonly Text[] _rows = new Text[Rows];
        private readonly Image[] _rowBacks = new Image[Rows];
        private bool _fetching, _showOnce, _climbOnce;
        private float _fetchedAt = -100f;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _showOnce = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-towershow") >= 0;
            // -towerclimb climbs once the tower is known (screenshots of the replay).
            _climbOnce = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-towerclimb") >= 0;
            _showOnce |= _climbOnce;
            _canvas = Ui.Canvas("TowerCanvas", 10).gameObject;
            Transform canvas = _canvas.transform;
            Ui.Backdrop(canvas, "Zones");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "THE ENDLESS TOWER", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            // A dark plate under the week's lines, the climb and the next chest, so they read over the painted scene.
            Ui.Framed("Plate", canvas, 0.03f, 0.585f, 0.97f, 0.928f, new Color(0.05f, 0.05f, 0.1f, 0.88f));
            Ui.Picture("Picture", canvas, 0.06f, 0.765f, 0.4f, 0.92f, "Thumbs/Tower");
            _best = Ui.Title("Best", canvas, 0.43f, 0.855f, 0.96f, 0.915f, "", 32, TextAnchor.MiddleLeft, Palette.Parchment);
            _week = Ui.Label("Week", canvas, 0.43f, 0.805f, 0.96f, 0.855f, "", 21, TextAnchor.MiddleLeft, Palette.Parchment);
            _climbs = Ui.Label("Climbs", canvas, 0.43f, 0.765f, 0.96f, 0.805f, "", 21, TextAnchor.MiddleLeft, Palette.Parchment);
            Ui.Label("How", canvas, 0.05f, 0.72f, 0.95f, 0.76f,
                "Every climb starts at floor 1 and goes on until you fall. Every tenth floor a guardian, and a chest the first time each week.",
                19, TextAnchor.MiddleCenter, Palette.Parchment);
            _climb = Ui.Button("Climb", canvas, 0.2f, 0.645f, 0.8f, 0.71f, "CLIMB", 32, Palette.Danger, Climb, out _climbLabel);
            _climbImage = _climb.GetComponent<Image>();
            _chest = Ui.Label("Chest", canvas, 0.05f, 0.59f, 0.95f, 0.64f, "", 20, TextAnchor.MiddleCenter, Palette.Good);
            Ui.Section("LadderTitle", canvas, 0.1f, 0.545f, 0.9f, 0.582f, "THIS WEEK'S CLIMBERS", 24);
            for (int i = 0; i < Rows; i++)
            {
                float y1 = 0.535f - i * 0.039f;
                _rowBacks[i] = Ui.Framed("Row" + i, canvas, 0.04f, y1 - 0.035f, 0.96f, y1, Palette.PanelDark);
                _rows[i] = Ui.Raw(Ui.Label("RowText" + i, canvas, 0.07f, y1 - 0.035f, 0.94f, y1, "", 21, TextAnchor.MiddleLeft, Palette.Parchment));
                _rows[i].supportRichText = true;
            }
            _empty = Ui.Label("Empty", canvas, 0.05f, 0.47f, 0.95f, 0.53f, "Nobody has climbed yet this week: be the first.", 21, TextAnchor.MiddleCenter, Palette.Muted);
            _message = Ui.Raw(Ui.Label("Message", canvas, 0.05f, 0.085f, 0.95f, 0.13f, "", 20, TextAnchor.MiddleCenter, Palette.Muted));
            _message.supportRichText = true;
            foreach (Text t in new[] { _best, _week, _climbs, _chest }) Ui.Raw(t);
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);
        }

        /// <summary>Opens the screen; <paramref name="chests"/> (from a climb just replayed) is shown under the ladder.</summary>
        public void Open(string chests = null)
        {
            if (!_root.Server.Online) { _root.Hud.Log("The tower needs the server."); return; }
            if (!_root.Unlocked(Feature.Tower)) { _root.Hud.Log(Unlocks.Locked(Feature.Tower)); return; }
            _message.text = string.IsNullOrEmpty(chests) ? "" : ConfirmDialog.Tint(Loc.T("Chests opened") + ": ", Palette.Sorn) + Loc.T(chests);
            _canvas.SetActive(true);
            Fetch();
        }

        public void Close() => _canvas.SetActive(false);

        private void Fetch()
        {
            if (_fetching) return;
            _fetching = true;
            _fetchedAt = Time.realtimeSinceStartup;
            StartCoroutine(_root.Server.FetchTower((tower, error) =>
            {
                _fetching = false;
                if (tower == null && IsOpen) _message.text = ConfirmDialog.Tint(Loc.T(error), Palette.Bad);
            }));
        }

        private void Climb()
        {
            Net.ServerLink.TowerDto tower = _root.Server.Tower;
            if (_root.Replaying || _root.PushBusy || tower == null || tower.climbsLeft <= 0) return;
            Close();
            _root.ClimbTower();
        }

        private static string Span(long seconds)
        {
            seconds = System.Math.Max(0, seconds);
            long hours = seconds / 3600;
            return hours >= 24 ? $"{hours / 24}d {hours % 24}h" : $"{hours}h {seconds % 3600 / 60:00}m";
        }

        private void Update()
        {
            if (_root == null) return;
            if (_showOnce && _root.Server.Online && !_root.Server.WaitingForHero && _root.Unlocked(Feature.Tower))
            {
                _showOnce = false;
                Open();
            }
            if (!_canvas.activeSelf) return;
            if (!_root.Server.Online) { Close(); return; }
            if (Time.realtimeSinceStartup - _fetchedAt > 30f) Fetch();
            Net.ServerLink.TowerDto t = _root.Server.Tower;
            bool busy = _root.Replaying || _root.PushBusy;
            if (_climbOnce && t != null && !busy) { _climbOnce = false; Climb(); return; }
            _climb.interactable = t != null && t.climbsLeft > 0 && !busy;
            _climbImage.color = t != null && t.climbsLeft > 0 ? Palette.Danger : Palette.ButtonIdle;
            _climbLabel.text = t == null ? "..." : t.climbsLeft > 0 ? "CLIMB" : "NO CLIMBS LEFT";
            if (t == null)
            {
                _best.text = "";
                _week.text = "";
                _climbs.text = "";
                _chest.text = "";
                for (int i = 0; i < Rows; i++) { _rows[i].text = ""; _rowBacks[i].gameObject.SetActive(false); }
                _empty.gameObject.SetActive(false);
                return;
            }
            long weekLeft = t.weekEndsIn - (long)(Time.realtimeSinceStartup - _root.Server.TowerAt);
            _best.text = t.best > 0 ? Loc.T($"Best this week: floor {t.best}") : Loc.T("No climb yet this week");
            _week.text = (t.rank > 0 ? Loc.T($"Rank {t.rank}") + "  ·  " : "") + Loc.T($"the week ends in {Span(weekLeft)}")
                         + (t.bestEver > 0 ? "  ·  " + Loc.T($"highest ever: floor {t.bestEver}") : "")
                         + (string.IsNullOrEmpty(t.title) ? "" : "  ·  " + ConfirmDialog.Tint(Loc.T(t.title), Palette.Sorn));
            _climbs.text = Loc.T($"Climbs today: {t.climbsLeft}/{t.climbsPerDay}  ·  new climbs at 20:00");
            _chest.text = Loc.T($"Next chest at floor {t.nextChest}") + ": " + Loc.T(t.nextChestHolds);
            bool nobody = t.ladder == null || t.ladder.Length == 0;
            if (_empty.gameObject.activeSelf != nobody) _empty.gameObject.SetActive(nobody);
            for (int i = 0; i < Rows; i++)
            {
                Net.ServerLink.TowerRowDto row = t.ladder != null && i < t.ladder.Length ? t.ladder[i] : null;
                _rowBacks[i].gameObject.SetActive(row != null);
                if (row == null) { _rows[i].text = ""; continue; }
                bool me = row.id == _root.Server.AccountId;
                string title = string.IsNullOrEmpty(row.title) ? "" : ConfirmDialog.Tint("‹" + Loc.T(row.title) + "› ", Palette.Sorn);
                string place = row.rank <= 3 ? ConfirmDialog.Tint(row.rank + ".", Palette.Sorn) : row.rank + ".";
                string line = $"{place}  {title}<b>{row.name}</b>   <color=#C2BAAD>{Loc.T("Lv " + row.level)} {row.@class}</color>   "
                              + ConfirmDialog.Tint(Loc.T($"floor {row.best}"), Palette.Good);
                _rows[i].text = me ? ConfirmDialog.Tint("▸ ", Palette.Good) + line : line;
            }
        }
    }
}
