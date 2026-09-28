using System.Globalization;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// Old Nergui's river (owner, 28 Sep 2026: "we need a fishing map just like our game but we need to see the char from
    /// behind ... there is no afk farm there. the auto fishing is buyable with real money. make it a very very mini game
    /// that is basic"; Rules.Fishing). The painted river from the jetty, the hero on it seen from behind with a rod
    /// (HeroStage.FromBehind). CAST: the float flies out and bobs; when it goes under, REEL! in time for a fish or a
    /// mussel, else it gets away. CREEL lists the fish (EAT: a hunting boost for a while), the mussels (Old Nergui opens
    /// them) and the pearls. THE TIRELESS ROD (Amber) fishes by itself while the hero stays. The hunt stops here; LEAVE
    /// goes back to it. The screen follows the server: it shows while the hero is at the river, so this component lives
    /// off its canvas and keeps watching while the canvas is hidden.
    /// </summary>
    public sealed class RiverPanel : MonoBehaviour
    {
        private enum Phase { Idle, Waiting, Bite, Reeling, Fight }

        private GameRoot _root;
        private GameObject _canvas;
        private HeroStage _stage;
        private RiverScene _place;
        private bool _landed;
        private Text _status, _message, _castLabel, _mark, _musselLine, _rodLine;
        private readonly Text[] _pearls = new Text[3];
        private Button _cast;
        private Image _castImage;
        private RawImage _catchIcon;
        private GameObject _creel, _rodBox;
        // The fishing contest (Rules.Fishing, WorldEventKind.FishingContest): its board, fetched now and then.
        private GameObject _contestBox;
        private Text _contestLabel, _contestStatus, _contestMine, _contestTop, _contestPrizes;
        private ContestDto _contest;
        private float _contestAt = -100f;
        private bool _contestFetching;
        private readonly Text[] _fishLines = new Text[Fishing.Fish.Length];
        private readonly Button[] _eat = new Button[Fishing.Fish.Length];
        private Button _open1, _openAll;
        private ConfirmDialog _confirm;

        // The catch (owner, 28 Sep 2026: "the fish goes up or down randomly, you need to keep the fish inside bar for a
        // while to catch the fish"): a tall river channel, the fish darting in it, a catch box lifted by holding anywhere.
        private GameObject _fight;
        private RectTransform _play, _box, _fishBox, _progressFill;
        private RawImage _fishIcon;
        private Text _fightTitle, _progressText;
        private int _fightFish = -1;
        private float _boxY, _boxV, _fishY, _fishTarget, _nextTurn, _progress;
        private bool _fightOver, _frozen, _holding;
        private const float BoxHeight = 0.24f, FishSize = 0.13f;

        private Phase _phase;
        private float _phaseAt, _biteAt, _missAt, _catchAt = -10f;
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("RiverCanvas", 5).gameObject;
            Transform canvas = _canvas.transform;
            // The river is a place (RiverScene), drawn by the stage's camera behind the hero; the canvas only lays UI over it.
            RectTransform stageBox = Ui.Rect("Stage", canvas, 0f, 0f, 1f, 1f);
            Vector3 spot = HeroStage.Below + new Vector3(-120f, 0f, 0f);
            _stage = new GameObject("RiverStage").AddComponent<HeroStage>();
            _stage.Init(stageBox, spot, fromBehind: true);
            _stage.gameObject.SetActive(false);
            _place = new GameObject("RiverScene").AddComponent<RiverScene>();
            _place.Init(spot);

            Ui.Title("Title", canvas, 0.1f, 0.93f, 0.9f, 0.98f, "OLD NERGUI'S RIVER", 38, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Framed("StatusBack", canvas, 0.08f, 0.86f, 0.92f, 0.925f, new Color(0.05f, 0.04f, 0.04f, 0.78f)).raycastTarget = false;
            _status = Ui.Label("Status", canvas, 0.1f, 0.862f, 0.9f, 0.923f, "", 24, TextAnchor.MiddleCenter, Palette.Parchment);
            _status.supportRichText = true;

            _mark = Ui.Title("Bite", canvas, 0.38f, 0.46f, 0.62f, 0.6f, "!", 120, TextAnchor.MiddleCenter, Palette.Warn);
            _mark.resizeTextForBestFit = false;
            _mark.gameObject.AddComponent<Outline>().effectColor = new Color(0.2f, 0.05f, 0f, 0.9f);
            _mark.raycastTarget = false;
            RectTransform catchBox = Ui.Rect("CatchBox", canvas, 0.36f, 0.45f, 0.64f, 0.6f);
            _catchIcon = Ui.Icon("Catch", catchBox, 0f, 0f, 1f, 1f, "FishCarp");
            _catchIcon.raycastTarget = false;

            Ui.Framed("MessageBack", canvas, 0.06f, 0.225f, 0.94f, 0.272f, new Color(0.05f, 0.04f, 0.04f, 0.8f)).raycastTarget = false;
            _message = Ui.Label("Message", canvas, 0.08f, 0.227f, 0.92f, 0.27f, "", 24, TextAnchor.MiddleCenter, Palette.Parchment);
            _cast = Ui.Button("Cast", canvas, 0.22f, 0.14f, 0.78f, 0.215f, "CAST", 40, Palette.Alloy, Tap, out _castLabel);
            _castImage = _cast.GetComponent<Image>();
            Ui.Button("Creel", canvas, 0.03f, 0.08f, 0.26f, 0.132f, "CREEL", 22, Palette.ButtonIdle, () => ShowCreel(true), out _);
            Ui.Button("Rod", canvas, 0.27f, 0.08f, 0.5f, 0.132f, "TIRELESS ROD", 17, Palette.ButtonIdle, () => ShowRod(true), out _);
            Ui.Button("Contest", canvas, 0.51f, 0.08f, 0.73f, 0.132f, "CONTEST", 20, Palette.ButtonIdle, () => ShowContest(true), out _contestLabel);
            _contestLabel.supportRichText = true;
            Ui.Button("Bag", canvas, 0.74f, 0.08f, 0.97f, 0.132f, "INVENTORY", 18, Palette.ButtonIdle, () => _root.Gear.Open(), out _);
            Ui.Button("Leave", canvas, 0.2f, 0.015f, 0.8f, 0.07f, "BACK TO THE HUNT", 28, Palette.ButtonIdle, Leave, out _);

            BuildCreel(canvas);
            BuildRod(canvas);
            BuildContest(canvas);
            BuildFight(canvas);
            _confirm = new GameObject("RiverConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
            _canvas.SetActive(false);
        }

        private void BuildCreel(Transform canvas)
        {
            _creel = Ui.Rect("Creel", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform c = _creel.transform;
            Ui.Panel("Dim", c, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f)).gameObject.AddComponent<Button>().onClick.AddListener(() => ShowCreel(false));
            Transform box = Ui.Framed("Box", c, 0.04f, 0.12f, 0.96f, 0.86f, Palette.PanelDark).transform;
            Ui.Title("Title", box, 0.05f, 0.92f, 0.95f, 0.985f, "THE CREEL", 34, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            for (int i = 0; i < Fishing.Fish.Length; i++)
            {
                int id = i;
                float y1 = 0.91f - i * 0.115f;
                RectTransform iconBox = Ui.Rect("FishIcon" + i, box, 0.03f, y1 - 0.105f, 0.2f, y1);
                Ui.Icon("Icon", iconBox, 0f, 0f, 1f, 1f, Fishing.Fish[i].Icon);
                _fishLines[i] = Ui.Label("Fish" + i, box, 0.22f, y1 - 0.105f, 0.73f, y1, "", 25, TextAnchor.MiddleLeft, Palette.Parchment);
                _fishLines[i].supportRichText = true;
                _eat[i] = Ui.Button("Eat" + i, box, 0.74f, y1 - 0.09f, 0.97f, y1 - 0.015f, "EAT", 22, Palette.ButtonForge, () => Eat(id), out _);
            }
            float my = 0.91f - Fishing.Fish.Length * 0.115f;
            RectTransform musselBox = Ui.Rect("MusselIcon", box, 0.03f, my - 0.105f, 0.2f, my);
            Ui.Icon("Icon", musselBox, 0f, 0f, 1f, 1f, "Mussel");
            _musselLine = Ui.Label("Mussels", box, 0.22f, my - 0.105f, 0.5f, my, "", 24, TextAnchor.MiddleLeft, Palette.Parchment);
            _open1 = Ui.Button("Open1", box, 0.51f, my - 0.09f, 0.73f, my - 0.015f, "OPEN 1", 20, Palette.Alloy, () => Open(1), out _);
            _openAll = Ui.Button("OpenAll", box, 0.75f, my - 0.09f, 0.97f, my - 0.015f, "OPEN ALL", 20, Palette.Alloy, () => Open(Fishing.OpenMax), out _);
            float py = my - 0.12f;
            for (int p = 0; p < 3; p++)
            {
                RectTransform pearlBox = Ui.Rect("PearlIcon" + p, box, 0.05f + p * 0.31f, py - 0.075f, 0.14f + p * 0.31f, py);
                Ui.Icon("Icon", pearlBox, 0f, 0f, 1f, 1f, Fishing.PearlIcons[p]);
                _pearls[p] = Ui.Label("Pearl" + p, box, 0.15f + p * 0.31f, py - 0.075f, 0.34f + p * 0.31f, py, "", 24, TextAnchor.MiddleLeft, Palette.Parchment);
                _pearls[p].supportRichText = true;
            }
            Ui.Label("PearlNote", box, 0.05f, 0.075f, 0.95f, py - 0.08f, "A pearl pays the materials of a +7 (Moon), +8 (Tide) or +9 (Heart) attempt at the Forge.", 20,
                TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Close", box, 0.3f, 0.012f, 0.7f, 0.07f, "CLOSE", 24, Palette.ButtonIdle, () => ShowCreel(false), out _);
            _creel.SetActive(false);
        }

        private void BuildRod(Transform canvas)
        {
            _rodBox = Ui.Rect("RodBox", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform c = _rodBox.transform;
            Ui.Panel("Dim", c, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f)).gameObject.AddComponent<Button>().onClick.AddListener(() => ShowRod(false));
            Transform box = Ui.Framed("Box", c, 0.06f, 0.24f, 0.94f, 0.76f, Palette.PanelDark).transform;
            Ui.Title("Title", box, 0.05f, 0.86f, 0.95f, 0.97f, "THE TIRELESS ROD", 32, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            RectTransform iconBox = Ui.Rect("Icon", box, 0.04f, 0.6f, 0.26f, 0.84f);
            Ui.Icon("Rod", iconBox, 0f, 0f, 1f, 1f, "Fishing");
            Ui.Label("Blurb", box, 0.28f, 0.6f, 0.96f, 0.85f,
                $"Fishes by itself while you stay at the river, even with the game closed: one catch every {Fishing.AutoSeconds} seconds, up to {Fishing.AutoCapHours} hours between visits.",
                23, TextAnchor.MiddleLeft, Palette.Parchment);
            _rodLine = Ui.Label("Held", box, 0.05f, 0.5f, 0.95f, 0.59f, "", 20, TextAnchor.MiddleCenter, Palette.Sorn);
            for (int i = 0; i < Fishing.RodDays.Length; i++)
            {
                int index = i;
                float x0 = 0.05f + (i % 2) * 0.46f, y1 = i < 2 ? 0.48f : 0.3f;
                string days = Fishing.RodDays[i] == 1 ? "1 DAY" : Fishing.RodDays[i] + " DAYS";
                Ui.Button("Days" + i, box, x0, y1 - 0.16f, x0 + 0.44f, y1, $"{days}\n{Fishing.RodAmber[i]} Amber", 22, Palette.Alloy, () => AskRod(index), out _);
            }
            Ui.Button("Close", box, 0.3f, 0.02f, 0.7f, 0.12f, "CLOSE", 24, Palette.ButtonIdle, () => ShowRod(false), out _);
            _rodBox.SetActive(false);
        }

        private void BuildContest(Transform canvas)
        {
            _contestBox = Ui.Rect("ContestBox", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform c = _contestBox.transform;
            Ui.Panel("Dim", c, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f)).gameObject.AddComponent<Button>().onClick.AddListener(() => ShowContest(false));
            Transform box = Ui.Framed("Box", c, 0.05f, 0.16f, 0.95f, 0.84f, Palette.PanelDark).transform;
            Ui.Title("Title", box, 0.05f, 0.9f, 0.95f, 0.98f, "FISHING CONTEST", 32, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _contestStatus = Ui.Label("Status", box, 0.05f, 0.83f, 0.95f, 0.89f, "", 21, TextAnchor.MiddleCenter, Palette.Parchment);
            _contestMine = Ui.Label("Mine", box, 0.05f, 0.76f, 0.95f, 0.83f, "", 22, TextAnchor.MiddleCenter, Palette.Sorn);
            _contestMine.supportRichText = true;
            _contestTop = Ui.Label("Top", box, 0.07f, 0.3f, 0.93f, 0.75f, "", 25, TextAnchor.UpperLeft, Palette.Parchment);
            _contestTop.supportRichText = true;
            Ui.Raw(_contestTop);
            _contestPrizes = Ui.Label("Prizes", box, 0.06f, 0.12f, 0.94f, 0.3f, "", 20, TextAnchor.MiddleCenter, Palette.Muted);
            _contestPrizes.supportRichText = true;
            Ui.Button("Close", box, 0.3f, 0.02f, 0.7f, 0.1f, "CLOSE", 24, Palette.ButtonIdle, () => ShowContest(false), out _);
            _contestBox.SetActive(false);
        }

        private void ShowContest(bool on)
        {
            _contestBox.SetActive(on);
            if (on) { _contestAt = -100f; FillContest(); }
        }

        private void FetchContest()
        {
            if (_contestFetching || !_root.Server.Online) return;
            _contestFetching = true;
            _contestAt = Time.time;
            StartCoroutine(_root.Server.FetchContest((board, error) =>
            {
                _contestFetching = false;
                if (board != null) _contest = board;
                if (_contestBox.activeSelf) FillContest();
            }));
        }

        private void FillContest()
        {
            ContestDto b = _contest;
            PlayerSession s = _root.Session;
            _contestStatus.text = b == null ? "..." : b.running ? $"Ends in {Clock(b.endsInSeconds)}"
                : b.nextInSeconds > 0 ? $"The next contest begins in {Clock(b.nextInSeconds)}" : "No contest on the calendar";
            _contestMine.text = b == null || !b.any ? "" : b.myGrams > 0
                ? $"Your heaviest: {Fishing.Kilos(b.myGrams)} {Fishing.Fish[Mathf.Clamp(b.myFish, 0, Fishing.Fish.Length - 1)].Name}  ·  #{b.myRank}"
                : b.running ? "Land a fish by hand to get on the board." : "";
            var sb = new System.Text.StringBuilder();
            if (b != null && !b.running && b.any) sb.Append(ConfirmDialog.Tint(Loc.T("The last contest"), Palette.Muted)).Append('\n');
            if (b?.top != null)
                for (int i = 0; i < b.top.Length; i++)
                {
                    ContestRowDto r = b.top[i];
                    string fish = Loc.T(Fishing.FishById(r.fish)?.Name ?? "");
                    string line = $"#{i + 1}  {r.name}  ·  {fish}  {Fishing.Kilos(r.grams)}";
                    sb.Append(r.mine ? ConfirmDialog.Tint(line, Palette.Safe) : line).Append('\n');
                }
            if (b != null && (b.top == null || b.top.Length == 0)) sb.Append(ConfirmDialog.Tint(Loc.T("Nobody has landed a fish yet."), Palette.Muted));
            _contestTop.text = sb.ToString().TrimEnd();
            // The prizes by rank, priced for this hero (Rules.Fishing.ContestPrize), and the winner's title.
            var prizes = new System.Text.StringBuilder();
            foreach (int rank in new[] { 1, 2, 3, 4 })
            {
                (long sorn, int good, int count) = Fishing.ContestPrize(rank, s.HighestStageCleared);
                string who = rank < 4 ? "#" + rank : $"#4-{Fishing.ContestPaid}";
                prizes.Append($"{who}: {sorn:N0} sorn + {count} {TradeGoods.Name(good)}").Append(rank < 4 ? "  ·  " : "\n");
                if (rank == 2) prizes.Append('\n');
            }
            prizes.Append(ConfirmDialog.Tint($"The winner wears {Fishing.AnglerTitle} for a week.", Palette.Sorn));
            _contestPrizes.text = prizes.ToString();
        }

        private void BuildFight(Transform canvas)
        {
            _fight = Ui.Rect("Fight", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform f = _fight.transform;
            // Holding anywhere lifts the box: the shade takes every touch, so nothing below is pressed by it.
            Ui.Panel("Shade", f, 0f, 0f, 1f, 1f, new Color(0.01f, 0.01f, 0.03f, 0.86f));
            Ui.Framed("TitlePlate", f, 0.12f, 0.885f, 0.88f, 0.94f, Palette.PanelDark).raycastTarget = false;
            _fightTitle = Ui.Title("FishName", f, 0.14f, 0.888f, 0.86f, 0.937f, "", 34, TextAnchor.MiddleCenter, Palette.Sorn);
            Ui.Framed("Bottom", f, 0.06f, 0.075f, 0.94f, 0.212f, Palette.PanelDark).raycastTarget = false;
            RawImage water = Ui.Picture("Channel", f, 0.16f, 0.215f, 0.84f, 0.88f, "Scenes/Channel");
            var channel = (RectTransform)water.transform.parent;
            // The fish and the box move in the channel's water, its middle third.
            _play = Ui.Rect("Play", channel, 0.33f, 0.025f, 0.71f, 0.975f);
            Image box = Ui.Framed("Box", _play, 0f, 0f, 1f, BoxHeight, new Color(1f, 0.84f, 0.42f, 0.3f));
            _box = (RectTransform)box.transform;
            _fishBox = Ui.Rect("FishBox", _play, 0.15f, 0f, 0.85f, FishSize);
            _fishIcon = Ui.Icon("Fish", _fishBox, 0f, 0f, 1f, 1f, "FishCarp");
            // The icons swim to the right and a little up: turned to swim up the channel.
            _fishIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 68f);
            Ui.Label("ProgressHead", f, 0.1f, 0.168f, 0.6f, 0.2f, "Progress", 23, TextAnchor.MiddleLeft, Palette.Parchment);
            _progressText = Ui.Label("ProgressValue", f, 0.6f, 0.168f, 0.9f, 0.2f, "", 23, TextAnchor.MiddleRight, Palette.Good);
            _progressFill = Ui.Bar("Progress", f, 0.1f, 0.13f, 0.9f, 0.162f, new Color(0.3f, 0.78f, 0.3f), out _);
            Ui.Label("Hint", f, 0.1f, 0.085f, 0.9f, 0.125f, "Hold anywhere to lift the box", 24, TextAnchor.MiddleCenter, Palette.Sorn);
            _fight.SetActive(false);
        }

        private void StartFight(int fish)
        {
            _fightFish = fish;
            _fightOver = false;
            _progress = 0.3f;
            _boxY = 0f;
            _boxV = 0f;
            _fishY = 0.35f;
            _fishTarget = 0.5f;
            _nextTurn = Time.time + 0.8f;
            _phase = Phase.Fight;
            _phaseAt = Time.time;
            _fightTitle.text = Fishing.Fish[fish].Name;
            // The first catch explains itself; the fish waits while the card shows (the server allows 90 seconds).
            _root.Tips.Offer(TipCard.Tip.Catch);
            Ui.SetIcon(_fishIcon, Fishing.Fish[fish].Icon);
            _fight.SetActive(true);
        }

        /// <summary>A frame of the catch: the box rises while held and falls when let go, the fish darts toward a new depth
        /// every second or two (the rarer, the faster and oftener), and the bar fills while the fish is in the box.</summary>
        private void UpdateFight()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            bool hold = Input.GetMouseButton(0) || Input.touchCount > 0 || Input.GetKey(KeyCode.Space);
            _holding = hold && !_fightOver;
            if (_frozen)
            {
                // Screenshots (-rivershot fight): the fish held in the box, the bar where it was set.
                _fishTarget = 0.62f;
                hold = _boxY + BoxHeight * 0.5f < _fishY;
                _fightOver = true;
            }
            _boxV = Mathf.Clamp(_boxV + (hold ? 2.6f : -2.1f) * dt, -1.3f, 1.3f);
            _boxY += _boxV * dt;
            if (_boxY < 0f) { _boxY = 0f; _boxV = _boxV < -0.5f ? -_boxV * 0.3f : 0f; }
            if (_boxY > 1f - BoxHeight) { _boxY = 1f - BoxHeight; _boxV = Mathf.Min(0f, _boxV); }

            int wild = Fishing.Fish[_fightFish].Fight;
            if (Time.time >= _nextTurn)
            {
                _fishTarget = Random.Range(0.06f, 0.94f);
                _nextTurn = Time.time + Random.Range(1.1f, 2.2f) / (1f + wild * 0.45f);
            }
            _fishY = Mathf.MoveTowards(_fishY, _fishTarget, (0.3f + wild * 0.26f) * dt);
            float shown = Mathf.Clamp(_fishY + Mathf.Sin(Time.time * (5f + wild * 2f)) * 0.012f, FishSize / 2f, 1f - FishSize / 2f);
            bool inside = shown >= _boxY && shown <= _boxY + BoxHeight;
            if (!_fightOver) _progress = Mathf.Clamp01(_progress + (inside ? 0.26f : -(0.12f + wild * 0.04f)) * dt);

            _box.anchorMin = new Vector2(0f, _boxY);
            _box.anchorMax = new Vector2(1f, _boxY + BoxHeight);
            _box.GetComponent<Image>().color = inside ? new Color(1f, 0.88f, 0.45f, 0.42f) : new Color(1f, 0.84f, 0.42f, 0.22f);
            _fishBox.anchorMin = new Vector2(0.15f, shown - FishSize / 2f);
            _fishBox.anchorMax = new Vector2(0.85f, shown + FishSize / 2f);
            _progressFill.anchorMax = new Vector2(_progress, 1f);
            _progressText.text = Mathf.RoundToInt(_progress * 100f) + "%";

            if (!_fightOver && (_progress >= 1f || _progress <= 0f)) EndFight(_progress >= 1f);
        }

        private void EndFight(bool landed)
        {
            _fightOver = true;
            _busy = true;
            StartCoroutine(_root.Server.Land(landed, (result, error) =>
            {
                _busy = false;
                _fight.SetActive(false);
                _phase = Phase.Reeling;
                _phaseAt = Time.time;
                _stage.PlayOnce("Attack");
                if (error != null) { _message.text = error; return; }
                _message.text = result.message;
                if (result.kind == "fish")
                {
                    _contestAt = -100f;   // the board may have moved
                    Ui.SetIcon(_catchIcon, Fishing.Fish[result.fish].Icon);
                    _catchAt = Time.time;
                    GameAudio.Instance?.Play("LaneLoot", 0.9f);
                    GameAudio.Instance?.Play("RiverSplash", 0.6f);
                }
            }));
        }

        /// <summary>From ZONES: goes to the river (the hunt stops there).</summary>
        public void Go()
        {
            if (!_root.Server.Online) { _root.Hud.Log("Fishing needs the server."); return; }
            if (_busy) return;
            _busy = true;
            StartCoroutine(_root.Server.GoRiver(true, error =>
            {
                _busy = false;
                if (error != null) _root.Hud.Log(error);
            }));
        }

        private void Leave()
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(_root.Server.GoRiver(false, error =>
            {
                _busy = false;
                if (error != null) _message.text = error;
            }));
        }

        private void Show(bool on)
        {
            _canvas.SetActive(on);
            _stage.gameObject.SetActive(on);
            if (on) _place.Show(); else _place.Hide();
            // The river's own sounds (owner, 28 Sep 2026: picked "River and fishing sounds"), the map's theme low under them.
            GameAudio audio = GameAudio.Instance;
            if (audio != null)
            {
                audio.Ambience("RiverWater", on ? 0.55f : 0f);
                audio.Ambience("RiverBirds", on ? 0.3f : 0f);
                audio.Ambience("RiverFire", on ? 0.18f : 0f);
                if (!on) audio.Ambience("RiverReel", 0f, 0.2f);
                audio.MusicUnder(on ? 0.35f : 1f);
            }
            if (!on) { _stage.Show(null, 0, 0, null); return; }
            _phase = Phase.Idle;
            _message.text = "CAST, and REEL when the float goes under.";
            ShowCreel(false);
            ShowRod(false);
            ShowContest(false);
        }

        private void ShowCreel(bool on)
        {
            _creel.SetActive(on);
            if (on) FillCreel();
        }

        private void ShowRod(bool on) => _rodBox.SetActive(on);

        /// <summary>The one big button: CAST, or REEL once the line is out.</summary>
        private void Tap()
        {
            if (_busy || _phase == Phase.Fight) return;
            if (_phase == Phase.Idle || _phase == Phase.Reeling) Cast();
            else Reel();
        }

        private void Cast()
        {
            _busy = true;
            StartCoroutine(_root.Server.Cast((bite, error) =>
            {
                _busy = false;
                if (error != null) { _message.text = error; return; }
                _stage.PlayOnce("Attack");
                GameAudio.Instance?.Play("RiverCast", 0.8f);
                _phase = Phase.Waiting;
                _phaseAt = Time.time;
                _landed = false;
                _biteAt = Time.time + bite.biteMs / 1000f;
                _missAt = _biteAt + (bite.windowMs + Fishing.LateMs) / 1000f;
                _message.text = "Wait for it...";
            }));
        }

        /// <summary>REEL: hooks what bit (a fish then has to be landed in the catch; a mussel comes straight up).</summary>
        private void Reel()
        {
            _busy = true;
            bool bitten = _phase == Phase.Bite;
            _stage.PlayOnce("Attack");
            GameAudio.Instance?.Play("RiverCast", 0.55f, pitchJitter: 0.1f);
            StartCoroutine(_root.Server.Reel((reel, error) =>
            {
                _busy = false;
                if (error != null) { _message.text = error; _phase = Phase.Idle; return; }
                _message.text = reel.message;
                if (reel.kind == "fight" && reel.fish >= 0)
                {
                    StartFight(reel.fish);
                    return;
                }
                _phase = Phase.Reeling;
                _phaseAt = Time.time;
                if (reel.kind == "mussel")
                {
                    Ui.SetIcon(_catchIcon, "Mussel");
                    _catchAt = Time.time;
                    GameAudio.Instance?.Play("LaneLoot", 0.9f);
                }
            }));
            if (!bitten) { _phase = Phase.Reeling; _phaseAt = Time.time; }
        }

        private void Eat(int fish)
        {
            if (_busy) return;
            FishDef def = Fishing.Fish[fish];
            // Each fish boosts on its own clock beside the others; one already running gets its time added.
            _busy = true;
            StartCoroutine(_root.Server.Eat(fish, error =>
            {
                _busy = false;
                _message.text = error ?? $"You ate the {def.Name}: {def.BoostText}.";
                if (error == null) GameAudio.Instance?.Play("LanePotion", 0.8f);
                FillCreel();
            }));
        }

        private void Open(int count)
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(_root.Server.OpenMussels(count, (result, error) =>
            {
                _busy = false;
                _message.text = error ?? result.message;
                if (result != null && result.pearls != null && (result.pearls[0] + result.pearls[1] + result.pearls[2]) > 0)
                    GameAudio.Instance?.Play("ForgeSuccess", 0.8f);
                FillCreel();
            }));
        }

        private void AskRod(int index)
        {
            int days = Fishing.RodDays[index], price = Fishing.RodAmber[index];
            long amber = _root.Server.Wardrobe?.amber ?? 0;
            if (amber < price)
            {
                _confirm.Show("Not enough Amber", $"The rod for {days} day{(days == 1 ? "" : "s")} costs {price} Amber; you have {amber}.", "TO THE CARAVAN", Palette.Alloy,
                    () => _root.Caravan.Open(3));
                return;
            }
            _confirm.Show("The Tireless Rod", $"{days} day{(days == 1 ? "" : "s")} for {price} Amber (you have {amber}). Days add to a rod you hold.", "BUY", Palette.Alloy, () =>
            {
                _busy = true;
                StartCoroutine(_root.Server.BuyRod(days, error =>
                {
                    _busy = false;
                    _message.text = error ?? "The Tireless Rod is yours. It fishes while you stay here.";
                    if (error == null) ShowRod(false);
                }));
            });
        }

        private void FillCreel()
        {
            Inventory inv = _root.Session.Inventory;
            for (int i = 0; i < Fishing.Fish.Length; i++)
            {
                FishDef f = Fishing.Fish[i];
                long left = _root.Server.MealSecondsLeft(i);
                string running = left > 0 ? $"  <color=#8CF08C>{Clock(left)}</color>" : "";
                _fishLines[i].text = $"<b>{f.Name}</b>  ×{inv.Fish[i]}{running}\n<size=19><color=#B8A98A>{f.BoostText}</color></size>";
                _eat[i].interactable = inv.Fish[i] > 0;
            }
            _musselLine.text = $"<b>River mussels</b>  ×{inv.Mussels}";
            _musselLine.supportRichText = true;
            _open1.interactable = _openAll.interactable = inv.Mussels > 0;
            for (int p = 0; p < 3; p++) _pearls[p].text = $"×{inv.Pearls[p]}\n<size=17><color=#B8A98A>{Fishing.PearlNames[p]}</color></size>";
        }

        private static string Clock(long seconds) =>
            seconds >= 86400 ? $"{seconds / 86400}d {seconds % 86400 / 3600}h"
            : seconds >= 3600 ? $"{seconds / 3600}h {seconds % 3600 / 60:00}m"
            : $"{seconds / 60}:{seconds % 60:00}";

        private string _shot = Arg("-rivershot");

        private static string Arg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private void Update()
        {
            if (_root == null) return;
            if (_shot != null && _canvas.activeSelf && Time.time > 3f)
            {
                if (_shot == "nergui")
                {
                    Vector3 n = _place.NerguiAt;
                    _stage.ViewOverride = (n + new Vector3(3.2f, 1.6f, -2.6f), n + new Vector3(0f, 0.6f, 0f));
                }
                if (_shot == "creel") ShowCreel(true);
                else if (_shot == "rod") ShowRod(true);
                else if (_shot == "contest") ShowContest(true);
                else if (_shot == "bite") { _phase = Phase.Bite; _phaseAt = Time.time; _missAt = Time.time + 60f; }
                else if (_shot == "fight") { StartFight(4); _progress = 0.76f; _frozen = true; }
                _shot = null;
            }
            Net.ServerLink server = _root.Server;
            // The screen follows the server: at the river it shows (a new launch lands here), back at the hunt it hides.
            bool here = server.AtRiver && !server.WaitingForHero && !_root.Title.Showing && !_root.Account.Showing && !_root.Oath.Showing && !_root.Characters.IsOpen;
            if (here != _canvas.activeSelf) Show(here);
            if (!here) return;

            PlayerSession s = _root.Session;
            ItemState armor = s.Equipped(EquipSlot.Armor), weapon = s.Equipped(EquipSlot.Weapon);
            string skin = null;
            foreach (WardrobeDef piece in s.Worn) if (piece.Kind == WardrobeKind.Skin) skin = piece.Look;
            _stage.Show(s.Class, armor != null ? ItemLooks.Tier(armor.ItemLevel) : 0, weapon != null ? ItemLooks.Tier(weapon.ItemLevel) : 0, skin,
                armor != null ? UpgradeGlow.ForLevel(armor.UpgradeLevel) : 0f, 0f, s.SecondLook);

            if (_creel.activeSelf && Time.frameCount % 30 == 0) FillCreel();
            // The Tireless Rod's catches since the last state.
            if (server.AutoCatches > 0)
            {
                _message.text = server.AutoCatches == 1 ? "The Tireless Rod brought in a catch." : $"The Tireless Rod brought in {server.AutoCatches} catches.";
                server.AutoCatches = 0;
                if (_creel.activeSelf) FillCreel();
            }

            // The boosts running now, side by side: +XP and +sorn added up, and the soonest to end.
            int xp = 0, sornBoost = 0;
            long soonest = 0;
            for (int i = 0; i < Fishing.Fish.Length; i++)
            {
                long left = server.MealSecondsLeft(i);
                if (left <= 0) continue;
                xp += Fishing.Fish[i].XpPercent;
                sornBoost += Fishing.Fish[i].SornPercent;
                soonest = soonest == 0 ? left : System.Math.Min(soonest, left);
            }
            string meal = xp + sornBoost == 0 ? "No meal: eat a fish for a hunting boost"
                : $"Fish boosts: +{xp}% XP  ·  +{sornBoost}% sorn";
            string rod = server.RodSecondsLeft > 0 ? $"Tireless Rod: {Clock(server.RodSecondsLeft)}" : "No hunting here";
            _status.text = $"{meal}\n<size=20><color=#B8A98A>{rod}</color></size>";
            if (Time.time - _contestAt > 60f) FetchContest();
            _contestLabel.text = _contest != null && _contest.running ? ConfirmDialog.Tint("CONTEST", Palette.Warn) : "CONTEST";
            _rodLine.text = server.RodSecondsLeft > 0 ? $"Held: {Clock(server.RodSecondsLeft)} left" : "";

            // The float: out on the water while the line is out, under when a fish bites, back to the rod on the strike.
            float t = Time.time, h = _stage.Height;
            Vector3 rest = _stage.FloatRest;
            Vector3? at = null;
            if (_phase == Phase.Waiting && t >= _biteAt)
            {
                _phase = Phase.Bite;
                _place.Ripple(rest, second: true);
                GameAudio.Instance?.Play("RiverPlop", 0.9f);
            }
            if (_phase == Phase.Fight && _fight.activeSelf && !_root.Tips.Showing) UpdateFight();
            if (_phase == Phase.Bite && t >= _missAt)
            {
                _phase = Phase.Idle;
                _message.text = "Too slow: it stole the bait and swam off.";
            }
            switch (_phase)
            {
                case Phase.Waiting:
                    float fly = Mathf.Clamp01((t - _phaseAt) / 0.55f);
                    at = fly < 1f ? Vector3.Lerp(_stage.RodTip, rest, fly) + Vector3.up * (Mathf.Sin(fly * Mathf.PI) * 0.5f * h)
                        : rest + Vector3.up * (Mathf.Sin(t * 2.4f) * 0.012f * h);
                    if (fly >= 1f && !_landed)
                    {
                        _landed = true;
                        _place.Ripple(rest);
                        GameAudio.Instance?.Play("RiverSplash", 0.3f, pitchJitter: 0.15f);
                    }
                    break;
                case Phase.Bite:
                case Phase.Fight:
                    at = rest + Vector3.up * (-0.05f * h + Mathf.Sin(t * 28f) * 0.012f * h)
                         + (_phase == Phase.Fight ? new Vector3(Mathf.Sin(t * 3.1f), 0f, Mathf.Cos(t * 2.3f)) * (0.12f * h) : Vector3.zero);
                    break;
                case Phase.Reeling:
                    float back = Mathf.Clamp01((t - _phaseAt) / 0.4f);
                    if (back < 1f) at = Vector3.Lerp(rest, _stage.RodTip, back) + Vector3.up * (Mathf.Sin(back * Mathf.PI) * 0.3f * h);
                    else if (!_busy) _phase = Phase.Idle;
                    break;
            }
            _stage.SetFloat(at);
            // The reel runs while a fish is fought and held, and as the line comes back in.
            bool reeling = (_phase == Phase.Fight && _fight.activeSelf && _holding) || (_phase == Phase.Reeling && t - _phaseAt < 0.45f);
            GameAudio.Instance?.Ambience("RiverReel", reeling ? 0.5f : 0f, 0.12f);
            bool bite = _phase == Phase.Bite;
            _mark.gameObject.SetActive(bite);
            if (bite) _mark.rectTransform.position = _stage.ScreenOf(rest) + new Vector3(0f, Screen.height * 0.06f, 0f);
            _castLabel.text = _phase == Phase.Bite || _phase == Phase.Fight ? "REEL!" : _phase == Phase.Waiting ? "REEL" : "CAST";
            _castImage.color = _phase == Phase.Bite ? Palette.ButtonForge : _phase == Phase.Waiting ? Palette.ButtonIdle : Palette.Alloy;

            // A catch rises from the water and fades.
            float shown = t - _catchAt;
            _catchIcon.gameObject.SetActive(shown < 1.6f);
            if (shown < 1.6f)
            {
                _catchIcon.rectTransform.parent.position = _stage.ScreenOf(rest) + new Vector3(0f, Screen.height * (0.04f + shown * 0.05f), 0f);
                _catchIcon.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1.6f - shown));
            }
        }
    }
}
