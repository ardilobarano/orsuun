using System.Collections;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// Map quests (owner, 30 Sep 2026; Rules.MapQuests): the QUEST chip under the minimap (the step and how far along, or
    /// CLAIM when it is done) and the quest card it opens: the chain's giver and words, the step with its bar and pay, the
    /// three steps, every map's chain, CLAIM and MAP (the full map, where a scroll marks the quest's camp). The chip shows
    /// while the hero hunts a campaign map online and quests are open (level 4). -questshow opens the card once the quest
    /// is known (screenshots).
    /// </summary>
    public sealed class QuestPanel : MonoBehaviour
    {
        public const string IconName = "Quest";
        private GameRoot _root;
        private GameObject _canvas;
        private Button _chip, _claim;
        private Text _chipLabel, _title, _giver, _ask, _task, _count, _pay, _steps, _mapsLeft, _mapsRight, _message;
        private Image _chipImage;
        private RectTransform _fill;
        private bool _busy, _fetching, _showOnce, _tipOffered;
        private Net.ServerLink.QuestsDto _all;
        private float _allAt = -100f;
        private long _chipKey = -1;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _showOnce = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-questshow") >= 0;
            _canvas = Ui.Canvas("QuestCanvas", 33).gameObject;
            Transform canvas = _canvas.transform;
            Image dim = Ui.Panel("Dim", canvas, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            Transform box = Ui.Framed("Box", canvas, 0.05f, 0.13f, 0.95f, 0.87f, Palette.PanelDark).transform;
            Ui.Icon("Icon", Ui.Rect("IconBox", box, 0.03f, 0.865f, 0.19f, 0.985f), 0f, 0f, 1f, 1f, IconName);
            _title = Ui.Title("Title", box, 0.2f, 0.915f, 0.97f, 0.98f, "", 32, TextAnchor.MiddleLeft, Palette.Sorn);
            _giver = Ui.Label("Giver", box, 0.2f, 0.87f, 0.97f, 0.915f, "", 20, TextAnchor.MiddleLeft, Palette.Muted);
            Ui.Trim("Rule", box, 0.04f, 0.858f, 0.96f, 0.861f);
            _ask = Ui.Label("Ask", box, 0.06f, 0.7f, 0.94f, 0.85f, "", 25, TextAnchor.MiddleCenter, Palette.Parchment);
            _ask.fontStyle = FontStyle.Italic;
            _task = Ui.Label("Task", box, 0.05f, 0.645f, 0.95f, 0.695f, "", 26, TextAnchor.MiddleCenter, Palette.Sorn);
            _task.fontStyle = FontStyle.Bold;
            Ui.Bar("Bar", box, 0.1f, 0.6f, 0.9f, 0.64f, Palette.Good, out Image fill);
            _fill = fill.rectTransform;
            _count = Ui.Label("Count", box, 0.1f, 0.6f, 0.9f, 0.64f, "", 19, TextAnchor.MiddleCenter, Palette.Parchment);
            _pay = Ui.Label("Pay", box, 0.05f, 0.55f, 0.95f, 0.595f, "", 22, TextAnchor.MiddleCenter, Palette.Good);
            _pay.supportRichText = true;
            _steps = Ui.Label("Steps", box, 0.06f, 0.4f, 0.94f, 0.545f, "", 23, TextAnchor.MiddleLeft, Palette.Parchment);
            _steps.supportRichText = true;
            Ui.Trim("Rule2", box, 0.04f, 0.392f, 0.96f, 0.395f);
            Ui.Label("Maps", box, 0.05f, 0.355f, 0.95f, 0.387f, "EVERY MAP'S QUEST", 19, TextAnchor.MiddleCenter, Palette.Muted);
            _mapsLeft = Ui.Label("MapsLeft", box, 0.05f, 0.165f, 0.5f, 0.355f, "", 21, TextAnchor.UpperLeft, Palette.Parchment);
            _mapsRight = Ui.Label("MapsRight", box, 0.51f, 0.165f, 0.97f, 0.355f, "", 21, TextAnchor.UpperLeft, Palette.Parchment);
            foreach (Text t in new[] { _mapsLeft, _mapsRight }) t.supportRichText = true;
            _message = Ui.Label("Message", box, 0.05f, 0.118f, 0.95f, 0.163f, "", 20, TextAnchor.MiddleCenter, Palette.Muted);
            _message.supportRichText = true;
            // Filled with pieces translated one by one (Loc.T): the labels themselves translate nothing again.
            foreach (Text t in new[] { _giver, _ask, _task, _count, _pay, _steps, _mapsLeft, _mapsRight, _message }) Ui.Raw(t);
            _claim = Ui.Button("Claim", box, 0.04f, 0.02f, 0.37f, 0.11f, "CLAIM", 26, Palette.Good, Claim, out _);
            Ui.Button("Map", box, 0.39f, 0.02f, 0.63f, 0.11f, "MAP", 24, Palette.Alloy, () => { Close(); _root.MapScreen.Open(); }, out _);
            Ui.Button("Close", box, 0.65f, 0.02f, 0.96f, 0.11f, "CLOSE", 24, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);

            // The chip under the party's: the step and how far along, or CLAIM (it glows) when the step is done.
            _chip = Ui.Button("QuestChip", root.Hud.Canvas, 0.02f, 0.591f, 0.22f, 0.623f, "", 18, Palette.Alloy, Open, out _chipLabel);
            _chipLabel = Ui.Raw(_chipLabel);
            _chipLabel.supportRichText = true;
            _chipImage = _chip.GetComponent<Image>();
            _chip.gameObject.SetActive(false);
        }

        public void Open()
        {
            if (!_root.Server.Online) { _root.Hud.Log("Quests need the server."); return; }
            if (!_root.Unlocked(Feature.Quests)) { _root.Hud.Log(Unlocks.Locked(Feature.Quests)); return; }
            _message.text = "";
            _canvas.SetActive(true);
            FetchAll();
        }

        public void Close() => _canvas.SetActive(false);

        private void FetchAll()
        {
            if (_fetching) return;
            _fetching = true;
            _allAt = Time.realtimeSinceStartup;
            StartCoroutine(_root.Server.FetchQuests((all, error) =>
            {
                _fetching = false;
                if (all != null) _all = all;
                else if (IsOpen) _message.text = ConfirmDialog.Tint(Loc.T(error), Palette.Bad);
            }));
        }

        private void Claim()
        {
            Net.ServerLink.MapQuestDto q = _root.Server.Quest;
            if (_busy || q == null || !q.ready) return;
            _busy = true;
            bool last = q.piece;
            string pay = PayText(q);
            StartCoroutine(_root.Server.ClaimQuest(q.map, error =>
            {
                _busy = false;
                if (error != null) { _message.text = ConfirmDialog.Tint(Loc.T(error), Palette.Bad); return; }
                _message.text = ConfirmDialog.Tint(Loc.T(last ? "The quest is done!" : "Step done!"), Palette.Good) + "  " + pay;
                _root.Hud.Log(last ? $"Quest done: {q.title}." : "Quest step done.");
                GameAudio.Instance?.Play(last ? "LaneLevelUp" : "LaneLoot", 0.8f, 0.2f, 0f);
                FetchAll();
            }));
        }

        /// <summary>What a step pays, in a line.</summary>
        private static string PayText(Net.ServerLink.MapQuestDto q) =>
            Loc.T($"{q.sorn:N0} sorn") + "  ·  " + Loc.T($"{q.xp:N0} XP") + "  ·  " + Loc.T($"{q.materials} materials")
            + (q.piece ? "  ·  " + ConfirmDialog.Tint(Loc.T("an Epic piece"), GearPanel.RarityColor(Rarity.Epic)) : "");

        private void Update()
        {
            if (_root == null) return;
            Net.ServerLink.MapQuestDto q = _root.Server.Online ? _root.Server.Quest : null;
            bool chip = q != null && !_root.Server.WaitingForHero && !_root.Server.AtRiver && !_root.Town.IsOpen && !_root.Replaying
                        && _root.Unlocked(Feature.Quests);
            if (_chip.gameObject.activeSelf != chip) _chip.gameObject.SetActive(chip);
            if (chip)
            {
                bool done = q.step >= q.steps;
                long percent = q.target > 0 ? q.progress * 100 / q.target : 0;
                // Made again only when it changes (not every frame).
                long chipKey = ((((q.map * 8L + q.step) * 8 + q.steps) * 2 + (q.ready ? 1 : 0)) * 101 + percent) * 8 + (int)Loc.Current;
                if (chipKey != _chipKey)
                {
                    _chipKey = chipKey;
                    _chipLabel.text = done ? Loc.T("QUESTS") : q.ready ? "<b>" + Loc.T("QUEST: CLAIM") + "</b>"
                        : Loc.T($"QUEST {q.step + 1}/{q.steps}") + "  " + percent + "%";
                }
                // A step waiting to be claimed glows on the chip.
                _chipImage.color = q.ready ? Color.Lerp(Palette.Good, Palette.Sorn, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f)) : Palette.Alloy;
                // The first time the chip shows, a card explains quests (once the guide and any story card are done).
                if (!_tipOffered && !_root.Tips.Showing && !_root.Tutorial.Running && !_root.Story.Showing)
                {
                    _tipOffered = true;
                    _root.Tips.Offer(TipCard.Tip.Quest);
                }
                if (_showOnce && !_root.Tips.Showing) { _showOnce = false; Open(); }
            }
            if (!_canvas.activeSelf) return;
            if (!_root.Server.Online) { Close(); return; }
            if (Time.realtimeSinceStartup - _allAt > 20f) FetchAll();
            Show(q);
        }

        private void Show(Net.ServerLink.MapQuestDto q)
        {
            MapQuestDef def = q != null ? MapQuests.For(q.map) : null;
            _claim.gameObject.SetActive(q != null && q.ready && !_busy);
            if (q == null || def == null)
            {
                _title.text = "MAP QUESTS";
                _giver.text = "";
                _ask.text = Loc.T("Hunt on one of the campaign's maps to take its folk's quest.");
                _task.text = "";
                _count.text = "";
                _pay.text = "";
                _steps.text = "";
                _fill.anchorMax = new Vector2(0f, 1f);
            }
            else
            {
                bool done = q.step >= q.steps;
                _title.text = q.title;
                _giver.text = Loc.T(q.giver) + "  ·  " + Loc.T(q.mapName);
                _ask.text = "“" + Loc.T(done ? q.ending : q.ask) + "”";
                QuestStepDef step = done ? null : def.Steps[q.step];
                _task.text = done ? Loc.T("Every step is done.") : Loc.T(q.task) + (step.Kind == QuestKind.Commander && !_root.Unlocked(Feature.Commanders)
                    ? "  " + ConfirmDialog.Tint(Loc.T($"(Commanders open at level {Unlocks.Level(Feature.Commanders)})"), Palette.Warn) : "");
                float share = done ? 1f : q.target > 0 ? Mathf.Clamp01((float)q.progress / q.target) : 0f;
                _fill.anchorMax = new Vector2(share, 1f);
                _count.text = done ? "" : step.Kind == QuestKind.Hunt ? $"{q.progress / 60}:{q.progress % 60:00} / {q.target / 60}:00" : $"{q.progress} / {q.target}";
                _pay.text = done ? "" : Loc.T("Pay:") + "  " + PayText(q);
                var lines = new System.Text.StringBuilder();
                for (int i = 0; i < def.Steps.Length; i++)
                {
                    string task = MapQuests.Task(def, def.Steps[i]);
                    string where = Loc.T(def.Steps[i].Camp);
                    lines.Append(i < q.step ? ConfirmDialog.Tint("✓  " + Loc.T(task), Palette.Good)
                        : i == q.step ? "<b>▸  " + Loc.T(task) + "</b>  " + ConfirmDialog.Tint("· " + where, Palette.Muted)
                        : ConfirmDialog.Tint("·  " + Loc.T(task), Palette.Muted));
                    if (i < def.Steps.Length - 1) lines.Append('\n');
                }
                _steps.text = lines.ToString();
            }
            // Every map's chain: done, the step it is on, or the stage that opens it.
            if (_all?.maps == null) { _mapsLeft.text = ""; _mapsRight.text = ""; return; }
            var left = new System.Text.StringBuilder();
            var right = new System.Text.StringBuilder();
            for (int i = 0; i < _all.maps.Length; i++)
            {
                Net.ServerLink.MapQuestDto m = _all.maps[i];
                string name = Loc.T(m.mapName);
                string state = !m.open ? ConfirmDialog.Tint(Loc.T($"stage {m.opensAfter}"), Palette.Muted)
                    : m.step >= m.steps ? ConfirmDialog.Tint("✓", Palette.Good)
                    : m.ready ? ConfirmDialog.Tint(Loc.T("CLAIM"), Palette.Sorn) : $"{m.step}/{m.steps}";
                string line = (m.map == _all.hunting ? "<b>" + name + "</b>" : name) + "   " + state;
                System.Text.StringBuilder column = i < (_all.maps.Length + 1) / 2 ? left : right;
                if (column.Length > 0) column.Append('\n');
                column.Append(line);
            }
            _mapsLeft.text = left.ToString();
            _mapsRight.text = right.ToString();
        }
    }
}
