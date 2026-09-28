using System.Collections.Generic;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// The town square (owner, 28 Sep 2026: picked "Town square in 3D"; TownScene is the place). The hero stands at the
    /// square's near end seen from behind; the Banner's starting town names it (Emberhearth, Wardenstep, Scalehouse). Tap
    /// one of the townsfolk (or the plate over his head) and the hero walks over, then his screen opens: Forgemaster Dorun
    /// the Forge, Ilke of the Scales the Caravan, Elder Tamir the skills, Pitmaster Bora the Pits (from their level). The
    /// newest rugs lie along the aisle, the hero's own first; a rug's tag opens it on RUG STALLS. The hunt goes on while
    /// the hero is in town (only the river stops it); BACK TO THE HUNT leaves. ZONES has the way in.
    /// Town life (owner, 28 Sep 2026: picked "Town life"): up to six other heroes who are in town now stand about the
    /// square in their own looks, a name over each; tap one to INSPECT, WHISPER, TRADE or ADD FRIEND. The square has its
    /// own theme and a market's murmur.
    /// </summary>
    public sealed class TownPanel : MonoBehaviour
    {
        private static readonly string[] FolkNames = { "Forgemaster Dorun", "Ilke of the Scales", "Elder Tamir", "Pitmaster Bora" };
        private static readonly string[] FolkRoles = { "The Forge", "The Caravan", "Skills", "The Pits" };
        /// <summary>What each townsman says as he pays an errand (Rules.Errands), and his screen's button after it.</summary>
        private static readonly string[] FolkThanks =
        {
            "Good. The anvil remembers a steady hand.", "Coin that moves keeps the steppe alive.",
            "Patience feeds the wise. Take this.", "Well fought. Come back tomorrow.",
        };
        private static readonly string[] FolkScreens = { "THE FORGE", "THE CARAVAN", "SKILLS", "THE PITS" };
        private const float WalkSpeed = 4.2f, StopShort = 1.35f, RugsEvery = 60f, VisitEvery = 20f;

        private GameRoot _root;
        private GameObject _canvas;
        private RectTransform _canvasRect;
        private HeroStage _stage;
        private TownScene _place;
        private Text _title, _message;
        private readonly RectTransform[] _hits = new RectTransform[4];
        private readonly RectTransform[] _plates = new RectTransform[4];
        private readonly Text[] _badges = new Text[4];
        private GameObject _errandsBox;
        private readonly Text[] _errandRows = new Text[4];
        private Text _errandsPay;
        private ConfirmDialog _confirm;
        private bool _handing;
        private readonly RectTransform[] _tags = new RectTransform[6];
        private readonly Text[] _tagLabels = new Text[6];
        private readonly string[] _tagSellers = new string[6];
        private RugsDto _rugs;
        private float _rugsAt = -100f;
        private bool _fetching;
        private int _walkingTo = -1;
        private Vector3 _walkFrom, _walkTarget;
        private float _walkStart, _walkTime;
        private Banner _banner = (Banner)(-1);
        private int _shotWalk = -1, _shotVisit = -1;
        private bool _shotPick = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-townpick") >= 0;
        private bool _shotErrands = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-errands") >= 0;
        // Town life: who stands on each spot, their name tags, and the actions box for the one tapped.
        private readonly TownHeroDto[] _visitors = new TownHeroDto[TownScene.HeroSpots.Length];
        private readonly RectTransform[] _visitorTags = new RectTransform[TownScene.HeroSpots.Length];
        private readonly RectTransform[] _visitorHits = new RectTransform[TownScene.HeroSpots.Length];
        private readonly Text[] _visitorNames = new Text[TownScene.HeroSpots.Length];
        private float _visitAt = -100f;
        private bool _visiting;
        private GameObject _actions;
        private Text _actionsTitle;
        private TownHeroDto _picked;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("TownCanvas", 6).gameObject;
            Transform canvas = _canvas.transform;
            _canvasRect = (RectTransform)canvas;
            RectTransform stageBox = Ui.Rect("Stage", canvas, 0f, 0f, 1f, 1f);
            Vector3 spot = HeroStage.Below + new Vector3(-240f, 0f, 0f);
            _stage = new GameObject("TownStage").AddComponent<HeroStage>();
            _stage.Init(stageBox, spot, fromBehind: true, rod: false);
            _stage.ViewOverride = (spot + TownScene.CameraFrom, spot + TownScene.CameraTo);
            _stage.gameObject.SetActive(false);
            _place = new GameObject("TownScene").AddComponent<TownScene>();
            _place.Init(spot);

            Ui.Framed("TitleBack", canvas, 0.14f, 0.925f, 0.86f, 0.985f, new Color(0.05f, 0.04f, 0.04f, 0.72f)).raycastTarget = false;
            _title = Ui.Title("Title", canvas, 0.15f, 0.93f, 0.85f, 0.98f, "", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Raw(_title);

            // Visitors' names first, so the townsfolk's plates draw over them where they meet.
            for (int i = 0; i < _visitorTags.Length; i++)
            {
                int index = i;
                // An invisible button over the hero's figure, under the townsfolk's own.
                Image hit = Ui.Panel("VisitorHit" + i, canvas, 0f, 0f, 0.1f, 0.1f, new Color(0f, 0f, 0f, 0f));
                hit.gameObject.AddComponent<Button>().onClick.AddListener(() => PickVisitor(index));
                _visitorHits[i] = hit.rectTransform;
                hit.gameObject.SetActive(false);
                Image tag = Ui.Framed("Visitor" + i, canvas, 0f, 0f, 0.1f, 0.1f, new Color(0.05f, 0.05f, 0.08f, 0.72f));
                tag.gameObject.AddComponent<Button>().onClick.AddListener(() => PickVisitor(index));
                _visitorTags[i] = tag.rectTransform;
                _visitorNames[i] = Ui.Label("Name", tag.transform, 0.04f, 0.04f, 0.96f, 0.96f, "", 15, TextAnchor.MiddleCenter, Palette.Parchment);
                Ui.Raw(_visitorNames[i]);
                _visitorNames[i].supportRichText = true;
                tag.gameObject.SetActive(false);
            }

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                // An invisible button over the townsman's figure, and his plate above him.
                Image hit = Ui.Panel("Folk" + i, canvas, 0f, 0f, 0.1f, 0.1f, new Color(0f, 0f, 0f, 0f));
                hit.gameObject.AddComponent<Button>().onClick.AddListener(() => Visit(index));
                _hits[i] = hit.rectTransform;
                Image plate = Ui.Framed("Plate" + i, canvas, 0f, 0f, 0.1f, 0.1f, new Color(0.07f, 0.05f, 0.04f, 0.86f));
                plate.gameObject.AddComponent<Button>().onClick.AddListener(() => Visit(index));
                _plates[i] = plate.rectTransform;
                Ui.Title("Name", plate.transform, 0.04f, 0.46f, 0.96f, 0.96f, FolkNames[i], 20, TextAnchor.MiddleCenter, Palette.Parchment);
                Ui.Label("Role", plate.transform, 0.04f, 0.04f, 0.96f, 0.5f, FolkRoles[i], 17, TextAnchor.MiddleCenter, Palette.Sorn);
                // His errand's mark: "!" when it is done and waits to be handed in, a tick once paid.
                _badges[i] = Ui.Title("Badge", plate.transform, 0.84f, 0.1f, 1.08f, 0.95f, "", 30, TextAnchor.MiddleCenter, Palette.Warn);
                _badges[i].raycastTarget = false;
                _badges[i].gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            }
            for (int i = 0; i < _tags.Length; i++)
            {
                int index = i;
                Image tag = Ui.Framed("Rug" + i, canvas, 0f, 0f, 0.1f, 0.1f, new Color(0.06f, 0.05f, 0.05f, 0.78f));
                tag.gameObject.AddComponent<Button>().onClick.AddListener(() => OpenRug(index));
                _tags[i] = tag.rectTransform;
                _tagLabels[i] = Ui.Label("Label", tag.transform, 0.04f, 0.04f, 0.96f, 0.96f, "", 16, TextAnchor.MiddleCenter, Palette.Parchment);
                Ui.Raw(_tagLabels[i]);
                _tagLabels[i].supportRichText = true;
                tag.gameObject.SetActive(false);
            }

            Ui.Framed("MessageBack", canvas, 0.06f, 0.09f, 0.94f, 0.135f, new Color(0.05f, 0.04f, 0.04f, 0.78f)).raycastTarget = false;
            _message = Ui.Label("Message", canvas, 0.08f, 0.092f, 0.92f, 0.133f, "", 21, TextAnchor.MiddleCenter, Palette.Parchment);
            Ui.Button("Rugs", canvas, 0.03f, 0.015f, 0.31f, 0.075f, "RUG STALLS", 20, Palette.ButtonForge, () => _root.Rugs.Open(), out _);
            Ui.Button("Errands", canvas, 0.33f, 0.015f, 0.6f, 0.075f, "ERRANDS", 22, Palette.Alloy, () => ShowErrands(true), out _);
            Ui.Button("Leave", canvas, 0.62f, 0.015f, 0.97f, 0.075f, "BACK TO THE HUNT", 22, Palette.ButtonIdle, Close, out _);
            BuildActions(canvas);
            BuildErrands(canvas);
            _confirm = new GameObject("TownConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
            _canvas.SetActive(false);
        }

        private void BuildActions(Transform canvas)
        {
            _actions = Ui.Rect("Actions", canvas, 0f, 0f, 1f, 1f).gameObject;
            Image dim = Ui.Panel("Dim", _actions.transform, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.55f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => _actions.SetActive(false));
            Transform box = Ui.Framed("Box", _actions.transform, 0.12f, 0.36f, 0.88f, 0.64f, Palette.PanelDark).transform;
            _actionsTitle = Ui.Title("Title", box, 0.05f, 0.8f, 0.95f, 0.97f, "", 28, TextAnchor.MiddleCenter, Palette.Sorn);
            Ui.Raw(_actionsTitle);
            Ui.Button("Inspect", box, 0.06f, 0.54f, 0.48f, 0.76f, "INSPECT", 22, Palette.Alloy, () => Act(h => _root.Inspect.Open(h.id)), out _);
            Ui.Button("Whisper", box, 0.52f, 0.54f, 0.94f, 0.76f, "WHISPER", 22, Palette.Safe, () => Act(h => _root.Messages.OpenWith(h.id, h.name)), out _);
            Ui.Button("Trade", box, 0.06f, 0.29f, 0.48f, 0.51f, "TRADE", 22, Palette.Alloy, () => Act(h =>
                StartCoroutine(_root.Server.TradeInvite(null, error =>
                {
                    if (error != null) _message.text = error;
                    else _root.Trade.Open();
                }, h.id))), out _);
            Ui.Button("Friend", box, 0.52f, 0.29f, 0.94f, 0.51f, "ADD FRIEND", 20, Palette.Safe, () => Act(h =>
                StartCoroutine(_root.Server.AddFriend(h.id, null, (message, error) => _message.text = error ?? message))), out _);
            Ui.Button("Close", box, 0.3f, 0.04f, 0.7f, 0.24f, "CLOSE", 22, Palette.ButtonIdle, () => _actions.SetActive(false), out _);
            _actions.SetActive(false);
        }

        /// <summary>TODAY'S ERRANDS: each townsman's, how far along, and what one pays; a row walks the hero to him.</summary>
        private void BuildErrands(Transform canvas)
        {
            _errandsBox = Ui.Rect("Errands", canvas, 0f, 0f, 1f, 1f).gameObject;
            Image dim = Ui.Panel("Dim", _errandsBox.transform, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => ShowErrands(false));
            Transform box = Ui.Framed("Box", _errandsBox.transform, 0.05f, 0.24f, 0.95f, 0.78f, Palette.PanelDark).transform;
            Ui.Title("Title", box, 0.05f, 0.89f, 0.95f, 0.98f, "TODAY'S ERRANDS", 32, TextAnchor.MiddleCenter, Palette.Sorn);
            for (int i = 0; i < 4; i++)
            {
                int giver = i;
                float y1 = 0.87f - i * 0.17f;
                Image row = Ui.Framed("Row" + i, box, 0.04f, y1 - 0.155f, 0.96f, y1, new Color(0.08f, 0.06f, 0.05f, 0.93f));
                row.gameObject.AddComponent<Button>().onClick.AddListener(() => { ShowErrands(false); Visit(giver); });
                _errandRows[i] = Ui.Label("Text", row.transform, 0.04f, 0.05f, 0.96f, 0.95f, "", 24, TextAnchor.MiddleLeft, Palette.Parchment);
                _errandRows[i].supportRichText = true;
            }
            _errandsPay = Ui.Label("Pay", box, 0.05f, 0.13f, 0.95f, 0.2f, "", 19, TextAnchor.MiddleCenter, Palette.Muted);
            _errandsPay.supportRichText = true;
            Ui.Button("Close", box, 0.3f, 0.02f, 0.7f, 0.11f, "CLOSE", 22, Palette.ButtonIdle, () => ShowErrands(false), out _);
            _errandsBox.SetActive(false);
        }

        private void ShowErrands(bool on)
        {
            _errandsBox.SetActive(on);
            if (on) FillErrands();
        }

        private ErrandDto ErrandOf(int giver)
        {
            ErrandDto[] list = _root.Server.Errands?.list;
            if (list == null) return null;
            foreach (ErrandDto e in list) if (e.giver == giver) return e;
            return null;
        }

        private static bool Ready(ErrandDto e) => e != null && !e.paid && e.progress >= e.target;

        /// <summary>An errand's progress as a player reads it (minutes for the hunt's, a count otherwise).</summary>
        private static string Progress(ErrandDto e) => e.target >= 60 ? Loc.T($"{e.progress / 60}/{e.target / 60} min") : $"{e.progress}/{e.target}";

        private void FillErrands()
        {
            ErrandsDto all = _root.Server.Errands;
            for (int i = 0; i < 4; i++)
            {
                ErrandDto e = ErrandOf(i);
                string state = e == null ? "" : e.paid ? ConfirmDialog.Tint("✓ " + Loc.T("paid"), Palette.Safe)
                    : Ready(e) ? ConfirmDialog.Tint("! " + Loc.T("go and hand it in"), Palette.Warn) : ConfirmDialog.Tint(Progress(e), Palette.Sorn);
                _errandRows[i].text = $"<b>{Loc.T(FolkNames[i])}</b>\n{(e == null ? "..." : Loc.T(e.text))}   {state}";
            }
            _errandsPay.text = all == null ? (_root.Server.Online ? "..." : "Errands need the server.")
                : Loc.T($"Each pays {all.sorn:N0} sorn and {all.materials} materials") + "\n"
                  + ConfirmDialog.Tint(Loc.T($"New errands in {Clock(all.secondsToReset)}"), Palette.Muted);
        }

        private static string Clock(long seconds) => seconds >= 3600 ? $"{seconds / 3600}h {seconds % 3600 / 60}m" : $"{Mathf.Max(1, (int)(seconds / 60))}m";

        private void PickVisitor(int slot)
        {
            TownHeroDto hero = _visitors[slot];
            if (hero == null) return;
            _picked = hero;
            _actionsTitle.text = hero.name;
            _actions.SetActive(true);
        }

        private void Act(System.Action<TownHeroDto> act)
        {
            _actions.SetActive(false);
            if (_picked != null) act(_picked);
        }

        public void Open()
        {
            _canvas.SetActive(true);
            _actions.SetActive(false);
            _visitAt = -100f;
            _stage.gameObject.SetActive(true);
            _place.Show();
            StopWalking();
            _message.text = "Tap someone in the square to go and see them.";
            _rugsAt = -100f;
            string[] args = System.Environment.GetCommandLineArgs();
            int walk = System.Array.IndexOf(args, "-townwalk");
            if (walk >= 0 && walk + 1 < args.Length) int.TryParse(args[walk + 1], out _shotWalk);
            int visit = System.Array.IndexOf(args, "-townvisit");
            if (visit >= 0 && visit + 1 < args.Length && int.TryParse(args[visit + 1], out int who)) _shotVisit = who;
        }

        public void Close()
        {
            // Out of town: the others stop seeing this hero in their squares.
            if (_canvas.activeSelf && _root.Server.Online) StartCoroutine(_root.Server.TownVisit(true, null));
            GameAudio.Instance?.Ambience("TownMarket", 0f);
            _canvas.SetActive(false);
            _stage.Show(null, 0, 0, null);
            _stage.gameObject.SetActive(false);
            _place.Hide();
            StopWalking();
        }

        private void StopWalking()
        {
            _walkingTo = -1;
            _stage.Walk = Vector3.zero;
            _place.SetHeroAt(Vector3.zero);
            _stage.Facing = 0f;
            _stage.Loop("Idle");
        }

        /// <summary>The hero walks over to a townsman; his screen opens when he gets there.</summary>
        private void Visit(int index)
        {
            if (_walkingTo >= 0) return;
            // Bora's Pits open by level, but his errand can be handed in before they do.
            if (index == 3 && !_root.Unlocked(Feature.Pits) && !Ready(ErrandOf(3))) { _message.text = Unlocks.Locked(Feature.Pits); return; }
            Vector3 to = TownScene.Folk[index];
            Vector3 from = _stage.Walk;
            Vector3 way = to - from;
            way.y = 0f;
            _walkFrom = from;
            _walkTarget = to - way.normalized * StopShort;
            _walkTime = Mathf.Max(0.2f, (_walkTarget - from).magnitude / WalkSpeed);
            _walkStart = Time.time;
            _walkingTo = index;
            _stage.Facing = Mathf.Atan2(way.x, way.z) * Mathf.Rad2Deg;
            _stage.Loop("Run");
            _message.text = FolkNames[index];
        }

        /// <summary>At a townsman: his errand, if done, is handed in first (his thanks and the pay, then his screen).</summary>
        private void Arrive(int index)
        {
            StopWalking();
            _message.text = "Tap someone in the square to go and see them.";
            if (Ready(ErrandOf(index)) && _root.Server.Online && !_handing)
            {
                _handing = true;
                ErrandsDto pay = _root.Server.Errands;
                StartCoroutine(_root.Server.HandInErrand(index, error =>
                {
                    _handing = false;
                    if (error != null) { _message.text = error; OpenScreen(index); return; }
                    GameAudio.Instance?.Play("LaneLoot", 0.9f);
                    string reward = Loc.T($"+{pay.sorn:N0} sorn") + "  ·  " + Loc.T($"+{pay.materials} materials");
                    _confirm.Show(Loc.T(FolkNames[index]), "“" + Loc.T(FolkThanks[index]) + "”\n\n" + ConfirmDialog.Tint(reward, Palette.Sorn),
                        FolkScreens[index], Palette.Safe, () => OpenScreen(index));
                }));
                return;
            }
            OpenScreen(index);
        }

        private void OpenScreen(int index)
        {
            if (index == 3 && !_root.Unlocked(Feature.Pits)) { _message.text = Unlocks.Locked(Feature.Pits); return; }
            switch (index)
            {
                case 0: _root.Forge.Open(); break;
                case 1: _root.Caravan.Open(); break;
                case 2: _root.Skills.Open(); break;
                default: _root.Pits.Open(); break;
            }
        }

        private void OpenRug(int index)
        {
            string seller = _tagSellers[index];
            if (!string.IsNullOrEmpty(seller)) _root.Rugs.Open(seller);
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            Net.ServerLink server = _root.Server;
            // The river takes the hero out of town (another device may send him there); so does a change of hero.
            if (server.AtRiver || server.WaitingForHero) { Close(); return; }
            // A screen opened from the square covers it: the square's camera rests meanwhile.
            // Still in town under another screen: say so now and then, and learn who else is here.
            if (server.Online && !_visiting && Time.time - _visitAt > VisitEvery)
            {
                _visiting = true;
                _visitAt = Time.time;
                StartCoroutine(server.TownVisit(false, (town, error) =>
                {
                    _visiting = false;
                    if (town != null && _canvas.activeSelf) Receive(town.heroes ?? new TownHeroDto[0]);
                }));
            }
            bool covered = _root.Forge.IsOpen || _root.Caravan.IsOpen || _root.Skills.IsOpen || _root.Pits.IsOpen || _root.Rugs.IsOpen
                           || _root.Gear.IsOpen || _root.Menu.IsOpen || _root.Inspect.IsOpen || _root.Messages.IsOpen || _root.Trade.IsOpen;
            GameAudio.Instance?.Ambience("TownMarket", covered ? 0f : 0.35f);
            if (_stage.gameObject.activeSelf == covered) _stage.gameObject.SetActive(!covered);
            if (covered) return;

            // Each townsman's errand mark, and the list while it is open.
            for (int i = 0; i < 4; i++)
            {
                ErrandDto e = ErrandOf(i);
                _badges[i].text = Ready(e) ? "!" : e != null && e.paid ? "✓" : "";
                _badges[i].color = Ready(e) ? Palette.Warn : Palette.Safe;
            }
            if (_errandsBox.activeSelf && Time.frameCount % 30 == 0) FillErrands();

            Banner banner = server.Banner;
            if (banner != _banner)
            {
                _banner = banner;
                _title.text = Loc.ToUpper(banner == Banner.None ? "The Town Square" : Banners.Def(banner).StartingTown);
                _place.SetBanner(banner);
            }

            PlayerSession s = _root.Session;
            ItemState armor = s.Equipped(EquipSlot.Armor), weapon = s.Equipped(EquipSlot.Weapon);
            string skin = null;
            foreach (WardrobeDef piece in s.Worn) if (piece.Kind == WardrobeKind.Skin) skin = piece.Look;
            _stage.Show(s.Class, armor != null ? ItemLooks.Tier(armor.ItemLevel) : 0, weapon != null ? ItemLooks.Tier(weapon.ItemLevel) : 0, skin,
                armor != null ? UpgradeGlow.ForLevel(armor.UpgradeLevel) : 0f, weapon != null ? UpgradeGlow.ForLevel(weapon.UpgradeLevel) : 0f, s.SecondLook);

            // Screenshots (-errands): TODAY'S ERRANDS.
            if (_shotErrands && Time.time > 6f && server.Errands != null) { _shotErrands = false; ShowErrands(true); }
            // Screenshots (-townpick): the actions of the first hero standing in the square.
            if (_shotPick && Time.time > 6f)
            {
                int first = System.Array.FindIndex(_visitors, v => v != null);
                if (first >= 0) { _shotPick = false; PickVisitor(first); }
            }
            // Screenshots (-townvisit n): a whole visit, the walk and then the screen.
            if (_shotVisit >= 0 && Time.time > 4f)
            {
                Visit(_shotVisit);
                _shotVisit = -1;
            }
            // Screenshots (-townwalk n): the hero caught halfway to a townsman.
            if (_shotWalk >= 0 && Time.time > 4f)
            {
                Visit(_shotWalk);
                _walkTime = 600f;
                _walkStart = Time.time - 300f;
                _shotWalk = -1;
            }
            if (_walkingTo >= 0)
            {
                float k = Mathf.Clamp01((Time.time - _walkStart) / _walkTime);
                _stage.Walk = Vector3.Lerp(_walkFrom, _walkTarget, k);
                _place.SetHeroAt(_stage.Walk);
                if (k >= 1f) Arrive(_walkingTo);
            }

            if (server.Online && !_fetching && Time.time - _rugsAt > RugsEvery)
            {
                _fetching = true;
                _rugsAt = Time.time;
                StartCoroutine(server.FetchRugs((rugs, error) =>
                {
                    _fetching = false;
                    if (rugs != null) { _rugs = rugs; LayRugs(); }
                }));
            }
            PlaceLabels();
        }

        /// <summary>Who is in town now: those already standing keep their spots; newcomers take the free ones.</summary>
        private void Receive(TownHeroDto[] heroes)
        {
            var here = new HashSet<string>();
            foreach (TownHeroDto h in heroes) here.Add(h.id);
            for (int i = 0; i < _visitors.Length; i++)
                if (_visitors[i] != null && !here.Contains(_visitors[i].id)) _visitors[i] = null;
            foreach (TownHeroDto h in heroes)
            {
                int at = System.Array.FindIndex(_visitors, v => v != null && v.id == h.id);
                if (at < 0) at = System.Array.IndexOf(_visitors, null);
                if (at < 0) break;
                _visitors[at] = h;
            }
            for (int i = 0; i < _visitors.Length; i++)
            {
                TownHeroDto h = _visitors[i];
                if (h == null || !System.Enum.TryParse(h.@class, out HeroClass cls)) { _place.SetVisitor(i, null); continue; }
                Figure figure = System.Enum.TryParse(h.figure, out Figure f) ? f : ItemLooks.NativeFigure(cls);
                _place.SetVisitor(i, cls, ItemLooks.Tier(h.armorLevel), ItemLooks.Tier(h.weaponLevel), h.skin, ItemLooks.SecondLook(cls, figure),
                    UpgradeGlow.ForLevel(h.armorPlus), UpgradeGlow.ForLevel(h.weaponPlus));
                string title = string.IsNullOrEmpty(h.title) ? "" : ConfirmDialog.Tint("‹" + Loc.T(h.title) + "›", new Color(1f, 0.84f, 0.42f)) + "\n";
                _visitorNames[i].text = title + h.name + ConfirmDialog.Tint("  " + Loc.T($"Lv {h.level}"), Palette.Muted);
            }
        }

        /// <summary>The newest rugs on the square's spots, the hero's own first.</summary>
        private void LayRugs()
        {
            var shown = new List<RugStallDto>();
            RugStallDto[] stalls = _rugs?.stalls ?? new RugStallDto[0];
            foreach (RugStallDto st in stalls) if (st.mine) shown.Add(st);
            foreach (RugStallDto st in stalls) if (!st.mine && shown.Count < TownScene.RugSpots.Length) shown.Add(st);
            var banners = new List<Banner?>();
            for (int i = 0; i < _tags.Length; i++)
            {
                bool has = i < shown.Count;
                _tagSellers[i] = has ? shown[i].sellerId : null;
                banners.Add(has ? BannerLook.Parse(shown[i].banner) : (Banner?)null);
                if (has)
                    {
                    int n = shown[i].wares;
                    // The seller's name as written; the rest in the player's language (the label is raw).
                    _tagLabels[i].text = (shown[i].mine ? Loc.T("Your rug") : shown[i].name) + "\n"
                                         + ConfirmDialog.Tint(Loc.T(n == 1 ? $"{n} ware" : $"{n} wares"), Palette.Sorn);
                }
            }
            _place.SetRugs(banners);
        }

        /// <summary>Plates over the townsfolk and tags over the rugs, where the square's camera shows them.</summary>
        private void PlaceLabels()
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 feet = _place.World(TownScene.Folk[i]);
                Vector2 size = _place.FolkSize(i);
                Vector3 bottom = _stage.ScreenOf(feet), top = _stage.ScreenOf(feet + Vector3.up * size.y);
                Vector3 side = _stage.ScreenOf(feet + Vector3.right * (size.x * 0.5f));
                float halfWidth = Mathf.Max(Mathf.Abs(side.x - bottom.x), Screen.width * 0.05f);
                SetScreenRect(_hits[i], bottom.x - halfWidth, bottom.y, bottom.x + halfWidth, top.y);
                float plateW = Screen.width * 0.2f, plateH = Screen.height * 0.04f;
                SetScreenRect(_plates[i], top.x - plateW / 2f, top.y + Screen.height * 0.004f, top.x + plateW / 2f, top.y + Screen.height * 0.004f + plateH);
            }
            for (int i = 0; i < _visitorTags.Length; i++)
            {
                bool has = _place.VisitorAt(i) is (Vector3, Vector3);
                if (_visitorTags[i].gameObject.activeSelf != has) _visitorTags[i].gameObject.SetActive(has);
                if (_visitorHits[i].gameObject.activeSelf != has) _visitorHits[i].gameObject.SetActive(has);
                if (!has) continue;
                (Vector3 feet, Vector3 head) = _place.VisitorAt(i).Value;
                Vector3 top = _stage.ScreenOf(head + Vector3.up * 0.12f), bottom = _stage.ScreenOf(feet);
                float half = Mathf.Max(Mathf.Abs(_stage.ScreenOf(feet + Vector3.right * 0.4f).x - bottom.x), Screen.width * 0.03f);
                SetScreenRect(_visitorHits[i], bottom.x - half, bottom.y, bottom.x + half, top.y);
                float w = Screen.width * 0.21f, h = Screen.height * (string.IsNullOrEmpty(_visitors[i]?.title) ? 0.024f : 0.04f);
                SetScreenRect(_visitorTags[i], top.x - w / 2f, top.y, top.x + w / 2f, top.y + h);
            }
            for (int i = 0; i < _tags.Length; i++)
            {
                bool has = _tagSellers[i] != null;
                if (_tags[i].gameObject.activeSelf != has) _tags[i].gameObject.SetActive(has);
                if (!has) continue;
                // Standing on the rug's far edge, so the rug shows under it.
                Vector3 at = _stage.ScreenOf(_place.World(TownScene.RugSpots[i] + new Vector3(0f, 0f, TownScene.RugWidth * 0.45f)));
                float w = Screen.width * 0.2f, h = Screen.height * 0.034f;
                SetScreenRect(_tags[i], at.x - w / 2f, at.y, at.x + w / 2f, at.y + h);
            }
        }

        /// <summary>Places a canvas child over a screen rectangle (pixels).</summary>
        private void SetScreenRect(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            float w = Mathf.Max(1f, Screen.width), h = Mathf.Max(1f, Screen.height);
            rect.anchorMin = new Vector2(x0 / w, y0 / h);
            rect.anchorMax = new Vector2(x1 / w, y1 / h);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
