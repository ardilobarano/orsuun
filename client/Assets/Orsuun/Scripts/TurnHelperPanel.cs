using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The turning helper (owner, 24 Sep 2026, after the classic bonus switcher): pick up to five etchings, each with the
    /// lowest tier you accept, and it turns the piece on the anvil in Bulk Turn batches until all of them are there, the
    /// Turnstones run out, or STOP. The server rolls every batch and stops it on the goal; this screen only chains the
    /// batches, shows the odds, and never turns a piece that already meets its goal.
    /// </summary>
    public sealed class TurnHelperPanel : MonoBehaviour
    {
        private const int Rows = EtchingService.MaxTargets;
        private const float BatchPause = 0.25f;
        private const string GoalKey = "orsuun.turnGoal.";

        private GameRoot _root;
        private GameObject _canvas;
        private Text _item;
        private Text _info;
        private Text _current;
        private Text _odds;
        private Text _status;
        private Text _startLabel;
        private Image _startImage;
        private Button _back;
        private readonly Button[] _entryButtons = new Button[Rows];
        private readonly Text[] _entryLabels = new Text[Rows];
        private readonly Button[] _tierButtons = new Button[Rows];
        private readonly Text[] _tierLabels = new Text[Rows];
        private readonly Button[] _clearButtons = new Button[Rows];

        // One goal for weapons and one for the other slots (they roll from different pools): entry id (-1 empty), tier.
        private readonly int[][] _entry = { Filled(-1), Filled(-1) };
        private readonly int[][] _tier = { Filled(1), Filled(1) };

        private GameObject _picker;
        private Text _pickerTitle;
        private readonly List<Button> _pickButtons = new List<Button>();
        private readonly List<Text> _pickLabels = new List<Text>();
        private int _pickingRow = -1;

        private bool _running;
        private bool _stopAsked;
        private int _turned;

        public bool IsOpen => _canvas.activeSelf;

        private static int[] Filled(int value)
        {
            var row = new int[Rows];
            for (int i = 0; i < Rows; i++) row[i] = value;
            return row;
        }

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("TurnHelperCanvas", 11).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            // Opaque: it sits on the Forge, whose text would show through the usual dim.
            Ui.Panel("Back", canvas, 0f, 0f, 1f, 1f, Palette.Background);
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.975f, "TURNING HELPER", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _item = Ui.Title("Item", canvas, 0.05f, 0.89f, 0.95f, 0.93f, "", 40, TextAnchor.MiddleCenter, Palette.Parchment);
            _info = Ui.Label("Info", canvas, 0.05f, 0.86f, 0.95f, 0.888f, "", 24, TextAnchor.MiddleCenter, Palette.Muted);

            Ui.Framed("CurrentBack", canvas, 0.06f, 0.685f, 0.94f, 0.852f, Palette.PanelDark);
            _current = Ui.Label("Current", canvas, 0.09f, 0.69f, 0.91f, 0.847f, "", 28, TextAnchor.MiddleLeft, Palette.Parchment);

            Ui.Title("GoalTitle", canvas, 0.06f, 0.638f, 0.94f, 0.675f, "STOP WHEN ALL OF THESE ARE ON IT", 26, TextAnchor.MiddleLeft, Palette.Sorn);
            for (int i = 0; i < Rows; i++)
            {
                int row = i;
                float y1 = 0.63f - i * 0.063f;
                float y0 = y1 - 0.055f;
                Ui.Label("Num" + i, canvas, 0.05f, y0, 0.11f, y1, (i + 1).ToString(), 30, TextAnchor.MiddleCenter, Palette.Muted);
                _entryButtons[i] = Ui.Button("Entry" + i, canvas, 0.11f, y0, 0.64f, y1, "", 26, Palette.ButtonIdle, () => OpenPicker(row), out _entryLabels[i]);
                _tierButtons[i] = Ui.Button("Tier" + i, canvas, 0.655f, y0, 0.815f, y1, "", 28, Palette.PanelDark, () => CycleTier(row), out _tierLabels[i]);
                _clearButtons[i] = Ui.Button("Clear" + i, canvas, 0.83f, y0, 0.94f, y1, "X", 28, Palette.PanelDark, () => SetEntry(row, -1), out _);
            }

            _odds = Ui.Label("Odds", canvas, 0.05f, 0.255f, 0.95f, 0.305f, "", 28, TextAnchor.MiddleCenter, Palette.Parchment);
            _status = Ui.Label("Status", canvas, 0.05f, 0.2f, 0.95f, 0.25f, "", 30, TextAnchor.MiddleCenter, Palette.Parchment);
            Button start = Ui.Button("Start", canvas, 0.1f, 0.11f, 0.9f, 0.19f, "", 36, Palette.ButtonForge, StartOrStop, out _startLabel);
            _startImage = start.GetComponent<Image>();
            _back = Ui.Button("Back", canvas, 0.25f, 0.025f, 0.75f, 0.09f, "BACK TO THE FORGE", 30, Palette.ButtonIdle, Close, out _);

            BuildPicker(canvas);
            Load(0);
            Load(1);
            _canvas.SetActive(false);
        }

        /// <summary>Choosing an etching: every entry of the piece's pool with its T1-T5 values; ones picked in other rows are off.</summary>
        private void BuildPicker(Transform canvas)
        {
            _picker = Ui.Panel("Picker", canvas, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.8f)).gameObject;
            Transform p = _picker.transform;
            Ui.Framed("Box", p, 0.04f, 0.1f, 0.96f, 0.9f, Palette.PanelDark);
            _pickerTitle = Ui.Title("PickTitle", p, 0.06f, 0.84f, 0.94f, 0.89f, "", 32, TextAnchor.MiddleCenter, Palette.Sorn);
            int entries = Math.Max(EtchingPool.Weapon().Entries.Count, EtchingPool.Armor().Entries.Count);
            int rows = (entries + 1) / 2;
            for (int i = 0; i < entries; i++)
            {
                int index = i;
                float x0 = i % 2 == 0 ? 0.06f : 0.505f;
                float y1 = 0.83f - (i / 2) * (0.64f / rows);
                Button b = Ui.Button("Pick" + i, p, x0, y1 - 0.64f / rows + 0.006f, x0 + 0.435f, y1, "", 24, Palette.ButtonIdle, () => Pick(index), out Text label);
                _pickButtons.Add(b);
                _pickLabels.Add(label);
            }
            Ui.Button("Cancel", p, 0.3f, 0.12f, 0.7f, 0.175f, "CANCEL", 28, Palette.ButtonIdle, () => _picker.SetActive(false), out _);
            _picker.SetActive(false);
        }

        public void Open()
        {
            _status.text = "";
            _picker.SetActive(false);
            _canvas.SetActive(true);
        }

        private void Close()
        {
            if (_running) return;
            _canvas.SetActive(false);
        }

        private int Kind => _root.Session.OnAnvil.Slot == EquipSlot.Weapon ? 0 : 1;

        private List<TurnTarget> Goal()
        {
            var goal = new List<TurnTarget>(Rows);
            int k = Kind;
            for (int i = 0; i < Rows; i++)
                if (_entry[k][i] >= 0) goal.Add(new TurnTarget(_entry[k][i], _tier[k][i]));
            return goal;
        }

        public void OpenPicker(int row)
        {
            if (_running) return;
            _pickingRow = row;
            _pickerTitle.text = "Etching " + (row + 1);
            _picker.SetActive(true);
        }

        private void Pick(int entry)
        {
            if (_pickingRow >= 0) SetEntry(_pickingRow, entry);
            _picker.SetActive(false);
        }

        private void SetEntry(int row, int entry)
        {
            if (_running) return;
            _entry[Kind][row] = entry;
            Save(Kind);
        }

        private void CycleTier(int row)
        {
            if (_running) return;
            int cap = EtchingRules.MaxTier(_root.Session.OnAnvil.Rarity);
            int k = Kind;
            _tier[k][row] = _tier[k][row] >= cap ? 1 : _tier[k][row] + 1;
            Save(k);
        }

        private void Save(int kind)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Rows; i++) sb.Append(_entry[kind][i]).Append(':').Append(_tier[kind][i]).Append(';');
            try { PlayerPrefs.SetString(GoalKey + kind, sb.ToString()); } catch { }
        }

        private void Load(int kind)
        {
            string saved;
            try { saved = PlayerPrefs.GetString(GoalKey + kind, ""); } catch { return; }
            string[] rows = saved.Split(';');
            for (int i = 0; i < Rows && i < rows.Length; i++)
            {
                string[] parts = rows[i].Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int e) && int.TryParse(parts[1], out int t))
                {
                    _entry[kind][i] = e;
                    _tier[kind][i] = Mathf.Clamp(t, 1, EtchingRules.TierCount);
                }
            }
        }

        private void StartOrStop()
        {
            if (_running) _stopAsked = true;
            else StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _running = true;
            _stopAsked = false;
            _turned = 0;
            PlayerSession s = _root.Session;
            while (true)
            {
                ItemState item = s.OnAnvil;
                List<TurnTarget> goal = Goal();
                string problem = goal.Count == 0 ? "Pick at least one etching first." : s.TurnBlocker() ?? EtchingService.TargetProblem(item, s.Pool, goal);
                if (EtchingService.MatchesAll(item, goal) && goal.Count > 0)
                {
                    // Never turn away a goal that is already met.
                    if (_turned == 0) Show("This piece already has your goal.", Palette.Good);
                    else
                    {
                        Show($"Found after {_turned:N0} turn{(_turned == 1 ? "" : "s")}!", Palette.Good);
                        GameAudio.Instance?.Play("ForgeSuccess", 0.9f, 0.5f, 0f);
                    }
                    break;
                }
                if (problem != null)
                {
                    bool broke = _turned > 0 && s.Inventory.Turnstones < (item.LockedEtchingIndex >= 0 ? 2 : 1);
                    Show(broke ? $"Out of Turnstones after {_turned:N0} turns." : problem, Palette.Warn);
                    break;
                }
                if (_stopAsked)
                {
                    Show($"Stopped after {_turned:N0} turns.", Palette.Muted);
                    break;
                }
                int cost = item.LockedEtchingIndex >= 0 ? 2 : 1;
                int batch = Math.Min(EtchingService.BulkTurnMax, s.Inventory.Turnstones / cost);
                if (batch < 1)
                {
                    Show($"Out of Turnstones after {_turned:N0} turns.", Palette.Warn);
                    break;
                }

                int turns = 0;
                string error = null;
                if (_root.Server.Online)
                {
                    yield return _root.Server.Turn(batch, goal, s.AnvilSlot, _root.Server.IdOf(item), (t, _, e) => { turns = t; error = e; });
                }
                else
                {
                    try { turns = s.TurnBulk(batch, goal, out _); }
                    catch (InvalidOperationException ex) { error = ex.Message; }
                }
                _turned += turns;
                if (error != null)
                {
                    Show(error, Palette.Bad);
                    break;
                }
                if (turns == 0) break;
                GameAudio.Instance?.Play("ForgeClang", 0.3f, 0.2f);
                Show($"Turning...  {_turned:N0} so far", Palette.Parchment);
                yield return new WaitForSecondsRealtime(BatchPause);
            }
            _running = false;
        }

        private void Show(string text, Color color)
        {
            _status.text = text;
            _status.color = color;
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            PlayerSession s = _root.Session;
            ItemState item = s.OnAnvil;
            EtchingPool pool = s.Pool;
            int k = Kind;
            int cap = EtchingRules.MaxTier(item.Rarity);
            int cost = item.LockedEtchingIndex >= 0 ? 2 : 1;
            List<TurnTarget> goal = Goal();

            _item.text = $"{item.DisplayName} +{item.UpgradeLevel}";
            _item.color = ForgePanel.LevelColor(item.UpgradeLevel);
            _info.text = $"{item.Rarity}  ·  {item.Etchings.Count} etching{(item.Etchings.Count == 1 ? "" : "s")}  ·  rolls up to T{cap}  ·  "
                         + $"{cost} Turnstone{(cost == 1 ? "" : "s")} a turn  ·  you have {s.Inventory.Turnstones:N0}";

            // The piece as it stands: goal etchings in green when at their tier, amber when below it.
            var sb = new StringBuilder();
            for (int i = 0; i < item.Etchings.Count; i++)
            {
                Etching e = item.Etchings[i];
                string line = $"T{e.Tier}   {pool.Entries[e.EntryId].Name}  +{e.Value}" + (i == item.LockedEtchingIndex ? "  (pinned)" : "");
                int want = 0;
                foreach (TurnTarget t in goal) if (t.EntryId == e.EntryId) want = t.MinTier;
                sb.Append(want == 0 ? line : ConfirmDialog.Tint(line, e.Tier >= want ? Palette.Good : Palette.Warn)).Append('\n');
            }
            _current.text = item.Etchings.Count == 0 ? "No etchings yet: add them with Etching Needles." : sb.ToString().TrimEnd();

            for (int i = 0; i < Rows; i++)
            {
                int entry = _entry[k][i];
                _entryLabels[i].text = entry < 0 || entry >= pool.Entries.Count ? "Choose an etching" : pool.Entries[entry].Name;
                _entryLabels[i].color = entry < 0 ? Palette.Muted : Palette.Parchment;
                _tierLabels[i].text = "T" + _tier[k][i] + "+";
                _tierLabels[i].color = _tier[k][i] > cap ? Palette.Bad : Palette.Parchment;
                _entryButtons[i].interactable = !_running;
                _tierButtons[i].interactable = !_running && entry >= 0;
                _clearButtons[i].interactable = !_running && entry >= 0;
            }

            if (goal.Count == 0) _odds.text = "Pick up to five etchings and the lowest tier you accept for each.";
            else
            {
                string problem = EtchingService.TargetProblem(item, pool, goal);
                double p = EtchingService.TargetChance(item, pool, goal);
                _odds.text = problem != null ? ConfirmDialog.Tint(problem, Palette.Bad)
                    : p >= 0.05 ? $"{p * 100:0}% a turn  ·  about {Math.Ceiling(cost / p):N0} Turnstones on average"
                    : $"About 1 in {1 / p:N0} turns  ·  {Math.Ceiling(cost / p):N0} Turnstones on average";
            }

            _startLabel.text = _running ? (_stopAsked ? "STOPPING..." : "STOP") : "START TURNING";
            _startImage.color = _running ? Palette.Danger : Palette.ButtonForge;
            _back.interactable = !_running;

            if (_picker.activeSelf)
            {
                for (int i = 0; i < _pickButtons.Count; i++)
                {
                    bool exists = i < pool.Entries.Count;
                    _pickButtons[i].gameObject.SetActive(exists);
                    if (!exists) continue;
                    int[] v = pool.Entries[i].TierValues;
                    _pickLabels[i].text = $"{pool.Entries[i].Name}\n<size=18>T1 +{v[0]}  ·  T5 +{v[4]}</size>";
                    bool taken = false;
                    for (int r = 0; r < Rows; r++) if (r != _pickingRow && _entry[k][r] == i) taken = true;
                    _pickButtons[i].interactable = !taken;
                }
            }
        }
    }
}
