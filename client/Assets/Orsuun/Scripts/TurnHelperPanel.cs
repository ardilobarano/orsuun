using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The turning helper (owner, 24 Sep 2026, after the classic bonus switcher): up to eight pieces, worn or in the
    /// bag, each with its own goal of up to five etchings and the lowest tier accepted for each, and its own ON/OFF.
    /// START turns the pieces that are on in turn, one Bulk Turn batch each, until every one reaches its goal, the
    /// Turnstones run out, or STOP. The server rolls every batch and stops it on the goal; this screen only chains the
    /// batches, shows the odds, and never turns a piece that already meets its goal.
    /// </summary>
    public sealed class TurnHelperPanel : MonoBehaviour
    {
        private const int Rows = EtchingService.MaxTargets;
        private const int MaxPieces = 8;
        private const int PieceColumns = 4;
        private const int AddColumns = 4;
        private const int MaxAddTiles = 60;
        private const float BatchPause = 0.25f;
        private const string GoalKey = "orsuun.turnGoal.";
        private const string QueueKey = "orsuun.turnQueue";

        /// <summary>A piece in the helper: found again by its server id after each refresh (the instance offline).</summary>
        private sealed class Piece
        {
            public string Id;
            public ItemState Item;
            public bool On = true;
            public bool Done;
            public string Note = "";
            public readonly int[] Entry = Filled(-1);
            public readonly int[] Tier = Filled(1);
        }

        private sealed class Tile
        {
            public RectTransform Rect;
            public Button Button;
            public Image Rim;
            public Outline Selected;
            public RawImage Icon;
            public Text Badge;
            public Text Note;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private readonly List<Piece> _pieces = new List<Piece>();
        private int _selected = -1;
        private bool _queueLoaded;

        private readonly Tile[] _pieceTiles = new Tile[MaxPieces];
        private Text _name;
        private Text _info;
        private Text _current;
        private Button _onOff;
        private Text _onOffLabel;
        private Button _remove;
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

        // The last goal set for weapons and for the other slots: a newly added piece starts from it.
        private readonly int[][] _defaultEntry = { Filled(-1), Filled(-1) };
        private readonly int[][] _defaultTier = { Filled(1), Filled(1) };

        private GameObject _picker;
        private Text _pickerTitle;
        private readonly List<Button> _pickButtons = new List<Button>();
        private readonly List<Text> _pickLabels = new List<Text>();
        private int _pickingRow = -1;

        private GameObject _adder;
        private RectTransform _addView;
        private GridLayoutGroup _addGrid;
        private Text _addEmpty;
        private readonly List<Tile> _addTiles = new List<Tile>();
        private readonly List<ItemState> _addItems = new List<ItemState>();

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

        private static int KindOf(ItemState item) => item.Slot == EquipSlot.Weapon ? 0 : 1;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("TurnHelperCanvas", 11).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            // Opaque: it sits on the Forge, whose text would show through the usual dim.
            Ui.Backdrop(canvas, "Turning");
            Ui.Title("Title", canvas, 0.05f, 0.94f, 0.95f, 0.978f, "TURNING HELPER", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);

            // The pieces: two rows of four, the next free tile adds one.
            for (int i = 0; i < MaxPieces; i++)
            {
                int index = i;
                float x0 = 0.04f + (i % PieceColumns) * 0.233f;
                float y1 = i < PieceColumns ? 0.93f : 0.862f;
                RectTransform box = Ui.Rect("PieceBox" + i, canvas, x0, y1 - 0.062f, x0 + 0.22f, y1);
                _pieceTiles[i] = MakeTile(box, "Piece" + i);
                _pieceTiles[i].Button.onClick.AddListener(() => TapPiece(index));
            }

            _name = Ui.Title("Name", canvas, 0.04f, 0.745f, 0.60f, 0.788f, "", 34, TextAnchor.MiddleLeft, Palette.Parchment);
            _onOff = Ui.Button("OnOff", canvas, 0.61f, 0.747f, 0.78f, 0.787f, "", 24, Palette.Safe, ToggleOn, out _onOffLabel);
            _remove = Ui.Button("Remove", canvas, 0.79f, 0.747f, 0.96f, 0.787f, "REMOVE", 24, Palette.PanelDark, RemoveSelected, out _);
            _info = Ui.Label("Info", canvas, 0.04f, 0.72f, 0.96f, 0.744f, "", 22, TextAnchor.MiddleLeft, Palette.Muted);

            Ui.Framed("CurrentBack", canvas, 0.04f, 0.595f, 0.96f, 0.716f, Palette.PanelDark);
            _current = Ui.Label("Current", canvas, 0.07f, 0.598f, 0.93f, 0.713f, "", 26, TextAnchor.MiddleLeft, Palette.Parchment);

            Ui.Title("GoalTitle", canvas, 0.04f, 0.556f, 0.96f, 0.59f, "STOP WHEN ALL OF THESE ARE ON IT", 24, TextAnchor.MiddleLeft, Palette.Sorn);
            for (int i = 0; i < Rows; i++)
            {
                int row = i;
                float y1 = 0.552f - i * 0.057f;
                float y0 = y1 - 0.05f;
                Ui.Label("Num" + i, canvas, 0.03f, y0, 0.09f, y1, (i + 1).ToString(), 28, TextAnchor.MiddleCenter, Palette.Muted);
                _entryButtons[i] = Ui.Button("Entry" + i, canvas, 0.09f, y0, 0.64f, y1, "", 26, Palette.ButtonIdle, () => OpenPicker(row), out _entryLabels[i]);
                _tierButtons[i] = Ui.Button("Tier" + i, canvas, 0.655f, y0, 0.815f, y1, "", 28, Palette.PanelDark, () => CycleTier(row), out _tierLabels[i]);
                _clearButtons[i] = Ui.Button("Clear" + i, canvas, 0.83f, y0, 0.96f, y1, "X", 28, Palette.PanelDark, () => SetEntry(row, -1), out _);
            }

            _odds = Ui.Label("Odds", canvas, 0.04f, 0.225f, 0.96f, 0.265f, "", 26, TextAnchor.MiddleCenter, Palette.Parchment);
            _status = Ui.Label("Status", canvas, 0.04f, 0.18f, 0.96f, 0.222f, "", 28, TextAnchor.MiddleCenter, Palette.Parchment);
            Button start = Ui.Button("Start", canvas, 0.1f, 0.1f, 0.9f, 0.172f, "", 36, Palette.ButtonForge, StartOrStop, out _startLabel);
            _startImage = start.GetComponent<Image>();
            _back = Ui.Button("Back", canvas, 0.25f, 0.02f, 0.75f, 0.085f, "BACK TO THE FORGE", 30, Palette.ButtonIdle, Close, out _);

            BuildPicker(canvas);
            BuildAdder(canvas);
            LoadDefault(0);
            LoadDefault(1);
            _canvas.SetActive(false);
        }

        private static Tile MakeTile(Transform parent, string name)
        {
            Image rim = Ui.Panel(name, parent, 0f, 0f, 1f, 1f, Palette.Trim);
            var t = new Tile { Rect = rim.rectTransform, Rim = rim };
            t.Button = rim.gameObject.AddComponent<Button>();
            t.Button.targetGraphic = rim;
            t.Selected = rim.gameObject.AddComponent<Outline>();
            t.Selected.effectColor = Palette.Sorn;
            t.Selected.effectDistance = new Vector2(5f, -5f);
            t.Selected.enabled = false;
            Image inner = Ui.Panel("Inner", rim.transform, 0.03f, 0.05f, 0.97f, 0.95f, new Color(0.07f, 0.07f, 0.11f));
            inner.raycastTarget = false;
            RectTransform iconBox = Ui.Rect("IconBox", inner.transform, 0.03f, 0.06f, 0.42f, 0.94f);
            t.Icon = Ui.Icon("Icon", iconBox, 0f, 0f, 1f, 1f, "Weapon");
            t.Badge = Ui.Title("Badge", inner.transform, 0.40f, 0.5f, 0.97f, 0.98f, "", 28, TextAnchor.UpperRight, Palette.Parchment);
            t.Note = Ui.Label("Note", inner.transform, 0.40f, 0.04f, 0.97f, 0.5f, "", 22, TextAnchor.LowerRight, Palette.Muted);
            return t;
        }

        private static void FillTile(Tile t, ItemState item, bool selected)
        {
            Texture2D icon = item == null ? null : Resources.Load<Texture2D>("Icons/" + item.Slot);
            if (t.Icon.texture != icon) t.Icon.texture = icon;
            t.Icon.enabled = icon != null;
            t.Rim.color = item == null ? new Color(0.25f, 0.25f, 0.3f) : GearPanel.RarityColor(item.Rarity);
            t.Badge.text = item == null || item.UpgradeLevel == 0 ? "" : "+" + item.UpgradeLevel;
            t.Badge.color = item == null ? Palette.Muted : ForgePanel.LevelColor(item.UpgradeLevel);
            t.Selected.enabled = selected;
        }

        /// <summary>Choosing an etching: every entry of the piece's pool with its T1 and T5 values; ones in other rows are off.</summary>
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

        /// <summary>Adding a piece: every owned piece with etchings that is not in the helper yet, worn first.</summary>
        private void BuildAdder(Transform canvas)
        {
            _adder = Ui.Panel("Adder", canvas, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.8f)).gameObject;
            Transform p = _adder.transform;
            Ui.Framed("Box", p, 0.04f, 0.1f, 0.96f, 0.9f, Palette.PanelDark);
            Ui.Title("AddTitle", p, 0.06f, 0.84f, 0.94f, 0.89f, "ADD A PIECE", 32, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _addView = Ui.Rect("AddView", p, 0.06f, 0.2f, 0.94f, 0.83f);
            _addView.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
            _addView.gameObject.AddComponent<RectMask2D>();
            var scroll = _addView.gameObject.AddComponent<ScrollRect>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(_addView, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            _addGrid = content.gameObject.AddComponent<GridLayoutGroup>();
            _addGrid.padding = new RectOffset(10, 10, 10, 10);
            _addGrid.spacing = new Vector2(10f, 10f);
            _addGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _addGrid.constraintCount = AddColumns;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = _addView;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            for (int i = 0; i < MaxAddTiles; i++)
            {
                RectTransform cell = new GameObject("Cell" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                cell.SetParent(content, false);
                Tile t = MakeTile(cell, "Add" + i);
                int index = i;
                t.Button.onClick.AddListener(() => AddPiece(index));
                cell.gameObject.SetActive(false);
                _addTiles.Add(t);
            }
            _addEmpty = Ui.Label("Empty", _addView, 0.05f, 0.3f, 0.95f, 0.7f, "No other pieces with etchings. Add etchings with Etching Needles first.",
                26, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("AddCancel", p, 0.3f, 0.12f, 0.7f, 0.175f, "CANCEL", 28, Palette.ButtonIdle, () => _adder.SetActive(false), out _);
            _adder.SetActive(false);
        }

        /// <summary>Opens the helper with the piece on the anvil in it and selected.</summary>
        public void Open()
        {
            if (!_queueLoaded && _root.Server.Online)
            {
                LoadQueue();
                _queueLoaded = true;
            }
            _status.text = "";
            _picker.SetActive(false);
            _adder.SetActive(false);
            ItemState anvil = _root.Session.OnAnvil;
            int at = _pieces.FindIndex(p => Resolve(p) == anvil);
            if (at < 0 && anvil.Etchings.Count > 0 && _pieces.Count < MaxPieces) at = Add(anvil);
            if (at >= 0) _selected = at;
            else if (_selected < 0 && _pieces.Count > 0) _selected = 0;
            _canvas.SetActive(true);
        }

        private void Close()
        {
            if (_running) return;
            _canvas.SetActive(false);
        }

        /// <summary>The piece's current instance: by server id online (each refresh rebuilds every item).</summary>
        private ItemState Resolve(Piece p)
        {
            PlayerSession s = _root.Session;
            if (p.Id != null && _root.Server.Online)
            {
                ItemState found = _root.Server.ItemById(p.Id);
                if (found == null || !s.Owns(found)) return null;
                p.Item = found;
            }
            return p.Item != null && s.Owns(p.Item) ? p.Item : null;
        }

        private Piece Selected => _selected >= 0 && _selected < _pieces.Count ? _pieces[_selected] : null;

        private int Add(ItemState item)
        {
            var p = new Piece { Id = _root.Server.IdOf(item), Item = item };
            int k = KindOf(item);
            Array.Copy(_defaultEntry[k], p.Entry, Rows);
            Array.Copy(_defaultTier[k], p.Tier, Rows);
            _pieces.Add(p);
            SaveQueue();
            return _pieces.Count - 1;
        }

        private void TapPiece(int index)
        {
            if (index < _pieces.Count) _selected = index;
            else if (!_running && _pieces.Count < MaxPieces) OpenAdder();
        }

        /// <summary>Opens the list of pieces that can be added (dev switch -turnhelper add for screenshots).</summary>
        public void OpenAdder()
        {
            PlayerSession s = _root.Session;
            var inHelper = new HashSet<ItemState>(_pieces.Select(Resolve).Where(x => x != null));
            var worn = new List<ItemState>();
            for (int i = 0; i < 8; i++)
            {
                ItemState w = s.Equipped((EquipSlot)i);
                if (w != null) worn.Add(w);
            }
            _addItems.Clear();
            _addItems.AddRange(worn.Concat(s.Inventory.Loot
                    .OrderByDescending(x => x.UpgradeLevel).ThenByDescending(x => (int)x.Rarity).ThenByDescending(x => x.ItemLevel))
                .Where(x => x.Etchings.Count > 0 && !inHelper.Contains(x))
                .Take(MaxAddTiles));
            _adder.SetActive(true);
        }

        /// <summary>Screenshots: adds the first few pieces the add list offers (dev switch -turnhelper demo).</summary>
        public void AddForShot(int count)
        {
            for (int n = 0; n < count; n++)
            {
                OpenAdder();
                if (_addItems.Count == 0) break;
                AddPiece(0);
            }
            _adder.SetActive(false);
        }

        private void AddPiece(int index)
        {
            if (index >= _addItems.Count || _pieces.Count >= MaxPieces) return;
            _selected = Add(_addItems[index]);
            _adder.SetActive(false);
        }

        private void RemoveSelected()
        {
            if (_running || Selected == null) return;
            _pieces.RemoveAt(_selected);
            _selected = Math.Min(_selected, _pieces.Count - 1);
            SaveQueue();
        }

        private void ToggleOn()
        {
            Piece p = Selected;
            if (_running || p == null) return;
            p.On = !p.On;
            SaveQueue();
        }

        private static List<TurnTarget> Goal(Piece p)
        {
            var goal = new List<TurnTarget>(Rows);
            for (int i = 0; i < Rows; i++)
                if (p.Entry[i] >= 0) goal.Add(new TurnTarget(p.Entry[i], p.Tier[i]));
            return goal;
        }

        public void OpenPicker(int row)
        {
            if (_running || Selected == null || Resolve(Selected) == null) return;
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
            Piece p = Selected;
            if (_running || p == null) return;
            p.Entry[row] = entry;
            GoalEdited(p);
        }

        private void CycleTier(int row)
        {
            Piece p = Selected;
            ItemState item = p == null ? null : Resolve(p);
            if (_running || item == null) return;
            int cap = EtchingRules.MaxTier(item.Rarity);
            p.Tier[row] = p.Tier[row] >= cap ? 1 : p.Tier[row] + 1;
            GoalEdited(p);
        }

        /// <summary>An edited goal is checked afresh, and becomes the starting goal for the next piece of its kind.</summary>
        private void GoalEdited(Piece p)
        {
            p.Done = false;
            p.Note = "";
            ItemState item = Resolve(p);
            if (item != null)
            {
                int k = KindOf(item);
                Array.Copy(p.Entry, _defaultEntry[k], Rows);
                Array.Copy(p.Tier, _defaultTier[k], Rows);
                SaveDefault(k);
            }
            SaveQueue();
        }

        private static string GoalText(int[] entry, int[] tier)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Rows; i++) sb.Append(entry[i]).Append(':').Append(tier[i]).Append(';');
            return sb.ToString();
        }

        private static void ParseGoal(string text, int[] entry, int[] tier)
        {
            string[] rows = text.Split(';');
            for (int i = 0; i < Rows && i < rows.Length; i++)
            {
                string[] parts = rows[i].Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int e) && int.TryParse(parts[1], out int t))
                {
                    entry[i] = e;
                    tier[i] = Mathf.Clamp(t, 1, EtchingRules.TierCount);
                }
            }
        }

        private void SaveDefault(int kind)
        {
            try { PlayerPrefs.SetString(GoalKey + kind, GoalText(_defaultEntry[kind], _defaultTier[kind])); } catch { }
        }

        private void LoadDefault(int kind)
        {
            try { ParseGoal(PlayerPrefs.GetString(GoalKey + kind, ""), _defaultEntry[kind], _defaultTier[kind]); } catch { }
        }

        /// <summary>The helper's pieces survive a restart online: server id, ON/OFF and goal per piece.</summary>
        private void SaveQueue()
        {
            var sb = new StringBuilder();
            foreach (Piece p in _pieces)
                if (p.Id != null) sb.Append(p.Id).Append('|').Append(p.On ? 1 : 0).Append('|').Append(GoalText(p.Entry, p.Tier)).Append('#');
            try { PlayerPrefs.SetString(QueueKey, sb.ToString()); } catch { }
        }

        private void LoadQueue()
        {
            string saved;
            try { saved = PlayerPrefs.GetString(QueueKey, ""); } catch { return; }
            foreach (string record in saved.Split('#'))
            {
                string[] parts = record.Split('|');
                if (parts.Length != 3 || _pieces.Count >= MaxPieces || _pieces.Any(x => x.Id == parts[0])) continue;
                ItemState item = _root.Server.ItemById(parts[0]);
                if (item == null || !_root.Session.Owns(item)) continue;
                var p = new Piece { Id = parts[0], Item = item, On = parts[1] == "1" };
                ParseGoal(parts[2], p.Entry, p.Tier);
                _pieces.Add(p);
            }
        }

        /// <summary>Screenshots: starts turning (dev switch -turnhelper run).</summary>
        public void StartForShot()
        {
            if (!_running) StartCoroutine(Run());
        }

        private void StartOrStop()
        {
            if (_running) _stopAsked = true;
            else StartCoroutine(Run());
        }

        /// <summary>
        /// One batch per piece that is on, in turn, until each is done or cannot go on. A piece already at its goal is
        /// marked done without a turn.
        /// </summary>
        private IEnumerator Run()
        {
            _running = true;
            _stopAsked = false;
            _turned = 0;
            PlayerSession s = _root.Session;
            foreach (Piece p in _pieces)
            {
                p.Done = false;
                p.Note = "";
            }
            string end = null;
            while (end == null)
            {
                bool any = false;
                foreach (Piece p in _pieces.ToArray())
                {
                    if (!p.On || p.Done || p.Note.Length > 0) continue;
                    if (_stopAsked)
                    {
                        end = $"Stopped after {_turned:N0} turns.";
                        break;
                    }
                    ItemState item = Resolve(p);
                    if (item == null)
                    {
                        p.Note = "GONE";
                        continue;
                    }
                    List<TurnTarget> goal = Goal(p);
                    EtchingPool pool = EtchingPool.For(item.Slot);
                    if (goal.Count > 0 && EtchingService.MatchesAll(item, goal))
                    {
                        p.Done = true;
                        continue;
                    }
                    int cost = item.LockedEtchingIndex >= 0 ? 2 : 1;
                    if (s.Inventory.Turnstones < cost)
                    {
                        end = $"Out of Turnstones after {_turned:N0} turns.";
                        break;
                    }
                    string problem = goal.Count == 0 ? "no goal" : s.TurnBlocker(item) ?? EtchingService.TargetProblem(item, pool, goal);
                    if (problem != null)
                    {
                        p.Note = "CHECK";
                        continue;
                    }

                    any = true;
                    int batch = Math.Min(EtchingService.BulkTurnMax, s.Inventory.Turnstones / cost);
                    int turns = 0;
                    string error = null;
                    if (_root.Server.Online)
                        yield return _root.Server.Turn(batch, goal, item.Slot, _root.Server.IdOf(item), (t, _, e) => { turns = t; error = e; });
                    else
                    {
                        try { turns = s.TurnBulk(item, batch, goal, out _); }
                        catch (InvalidOperationException ex) { error = ex.Message; }
                    }
                    _turned += turns;
                    if (error != null)
                    {
                        p.Note = "CHECK";
                        Show(error, Palette.Bad);
                        continue;
                    }
                    ItemState after = Resolve(p);
                    if (after != null && EtchingService.MatchesAll(after, goal))
                    {
                        p.Done = true;
                        GameAudio.Instance?.Play("ForgeSuccess", 0.9f, 0.5f, 0f);
                    }
                    else GameAudio.Instance?.Play("ForgeClang", 0.3f, 0.2f);
                    Show($"Turning...  {_turned:N0} turns so far", Palette.Parchment);
                    yield return new WaitForSecondsRealtime(BatchPause);
                }
                if (end == null && !any)
                {
                    int done = _pieces.Count(p => p.On && p.Done);
                    int stuck = _pieces.Count(p => p.On && !p.Done);
                    end = done == 0 && stuck == 0 ? "Turn a piece ON and give it a goal first."
                        : stuck == 0 ? $"All done after {_turned:N0} turns!"
                        : $"{done} done, {stuck} need a look (CHECK).";
                }
            }
            Show(end, end.StartsWith("All done") ? Palette.Good : Palette.Warn);
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

            for (int i = 0; i < MaxPieces; i++)
            {
                Tile t = _pieceTiles[i];
                bool has = i < _pieces.Count;
                bool isAdd = i == _pieces.Count;
                GameObject box = t.Rect.parent.gameObject;
                if (box.activeSelf != (has || isAdd)) box.SetActive(has || isAdd);
                if (!has && !isAdd) continue;
                if (isAdd)
                {
                    FillTile(t, null, false);
                    t.Badge.text = "+";
                    t.Badge.color = Palette.Sorn;
                    t.Note.text = "ADD";
                    t.Note.color = Palette.Muted;
                    t.Button.interactable = !_running;
                    continue;
                }
                Piece p = _pieces[i];
                ItemState item = Resolve(p);
                FillTile(t, item, i == _selected);
                t.Button.interactable = true;
                (string note, Color color) = item == null ? ("GONE", Palette.Bad)
                    : p.Done ? ("DONE", Palette.Good)
                    : p.Note.Length > 0 ? (p.Note, Palette.Warn)
                    : !p.On ? ("OFF", Palette.Muted)
                    : _running ? ("TURNING", Palette.Sorn) : ("ON", Palette.Good);
                t.Note.text = note;
                t.Note.color = color;
            }

            Piece sel = Selected;
            ItemState piece = sel == null ? null : Resolve(sel);
            bool editable = !_running && piece != null;
            _onOff.gameObject.SetActive(sel != null);
            _remove.gameObject.SetActive(sel != null);
            if (sel != null)
            {
                _onOffLabel.text = sel.On ? "ON" : "OFF";
                _onOff.GetComponent<Image>().color = sel.On ? Palette.Safe : Palette.PanelDark;
                _onOff.interactable = !_running;
                _remove.interactable = !_running;
            }

            if (piece == null)
            {
                _name.text = sel == null ? "Add a piece" : "That piece is gone";
                _name.color = Palette.Muted;
                _info.text = sel == null ? "Tap + to add pieces, worn or in the bag, and give each one its goal." : "It was destroyed or sold: REMOVE it.";
                _current.text = "";
                _odds.text = "";
                for (int i = 0; i < Rows; i++)
                {
                    _entryLabels[i].text = "";
                    _tierLabels[i].text = "";
                }
            }
            else
            {
                EtchingPool pool = EtchingPool.For(piece.Slot);
                int cap = EtchingRules.MaxTier(piece.Rarity);
                int cost = piece.LockedEtchingIndex >= 0 ? 2 : 1;
                List<TurnTarget> goal = Goal(sel);
                bool worn = s.Equipped(piece.Slot) == piece;
                _name.text = $"{piece.DisplayName} +{piece.UpgradeLevel}";
                _name.color = GearPanel.RarityColor(piece.Rarity);
                _info.text = $"{piece.Rarity}  ·  {(worn ? "worn" : "in your bag")}  ·  up to T{cap}  ·  "
                             + $"{cost} Turnstone{(cost == 1 ? "" : "s")} a turn  ·  you have {s.Inventory.Turnstones:N0}";

                // Goal etchings in green when at their tier, amber when below it.
                var sb = new StringBuilder();
                for (int i = 0; i < piece.Etchings.Count; i++)
                {
                    Etching e = piece.Etchings[i];
                    string line = $"T{e.Tier}   {pool.Entries[e.EntryId].Name}  +{e.Value}" + (i == piece.LockedEtchingIndex ? "  (pinned)" : "");
                    int want = 0;
                    foreach (TurnTarget t in goal) if (t.EntryId == e.EntryId) want = t.MinTier;
                    sb.Append(want == 0 ? line : ConfirmDialog.Tint(line, e.Tier >= want ? Palette.Good : Palette.Warn)).Append('\n');
                }
                _current.text = sb.ToString().TrimEnd();

                for (int i = 0; i < Rows; i++)
                {
                    int entry = sel.Entry[i];
                    _entryLabels[i].text = entry < 0 || entry >= pool.Entries.Count ? "Choose an etching" : pool.Entries[entry].Name;
                    _entryLabels[i].color = entry < 0 ? Palette.Muted : Palette.Parchment;
                    _tierLabels[i].text = "T" + sel.Tier[i] + "+";
                    _tierLabels[i].color = sel.Tier[i] > cap ? Palette.Bad : Palette.Parchment;
                }

                if (goal.Count == 0) _odds.text = "Pick up to five etchings and the lowest tier you accept for each.";
                else
                {
                    string problem = EtchingService.TargetProblem(piece, pool, goal);
                    double chance = EtchingService.TargetChance(piece, pool, goal);
                    _odds.text = problem != null ? ConfirmDialog.Tint(problem, Palette.Bad)
                        : EtchingService.MatchesAll(piece, goal) ? ConfirmDialog.Tint("This piece has its goal.", Palette.Good)
                        : chance >= 0.05 ? $"{chance * 100:0}% a turn  ·  about {Math.Ceiling(cost / chance):N0} Turnstones on average"
                        : $"About 1 in {1 / chance:N0} turns  ·  {Math.Ceiling(cost / chance):N0} Turnstones on average";
                }
            }
            for (int i = 0; i < Rows; i++)
            {
                _entryButtons[i].interactable = editable;
                _tierButtons[i].interactable = editable && sel.Entry[i] >= 0;
                _clearButtons[i].interactable = editable && sel.Entry[i] >= 0;
            }

            int on = _pieces.Count(p => p.On);
            _startLabel.text = _running ? (_stopAsked ? "STOPPING..." : "STOP")
                : on <= 1 ? "START TURNING" : $"START TURNING ({on} PIECES)";
            _startImage.color = _running ? Palette.Danger : Palette.ButtonForge;
            _back.interactable = !_running;

            if (_picker.activeSelf && piece != null)
            {
                EtchingPool pool = EtchingPool.For(piece.Slot);
                for (int i = 0; i < _pickButtons.Count; i++)
                {
                    bool exists = i < pool.Entries.Count;
                    _pickButtons[i].gameObject.SetActive(exists);
                    if (!exists) continue;
                    int[] v = pool.Entries[i].TierValues;
                    _pickLabels[i].text = $"{pool.Entries[i].Name}\n<size=18>T1 +{v[0]}  ·  T5 +{v[4]}</size>";
                    bool taken = false;
                    for (int r = 0; r < Rows; r++) if (r != _pickingRow && sel.Entry[r] == i) taken = true;
                    _pickButtons[i].interactable = !taken;
                }
            }

            if (_adder.activeSelf)
            {
                for (int i = 0; i < _addTiles.Count; i++)
                {
                    bool has = i < _addItems.Count;
                    GameObject cell = _addTiles[i].Rect.parent.gameObject;
                    if (cell.activeSelf != has) cell.SetActive(has);
                    if (!has) continue;
                    ItemState item = _addItems[i];
                    FillTile(_addTiles[i], item, false);
                    bool worn = s.Equipped(item.Slot) == item;
                    _addTiles[i].Note.text = worn ? "WORN" : "BAG";
                    _addTiles[i].Note.color = worn ? Palette.Sorn : Palette.Muted;
                }
                _addEmpty.enabled = _addItems.Count == 0;
                float cellW = (_addView.rect.width - 20f - 10f * (AddColumns - 1)) / AddColumns;
                if (cellW > 10f && Mathf.Abs(_addGrid.cellSize.x - cellW) > 0.5f) _addGrid.cellSize = new Vector2(cellW, cellW * 0.6f);
            }
        }
    }
}
