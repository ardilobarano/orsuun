using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>Resources on top, hero HP above the panel, skills and actions in thumb reach at the bottom.</summary>
    public sealed class Hud : MonoBehaviour
    {
        private GameRoot _root;
        private Text _level;
        /// <summary>Currency slots on the top bar: icon (Resources/Icons) and count.</summary>
        private static readonly string[] CurrencyIcons = { "Sorn", "Draught", "WolfSinew", "ScrollOfMercy", "KhansAlloy", "Turnstone", "Korshard" };
        private Text[] _currencies;
        private Text _stage;
        private Text _link;
        private Text _banner;
        private Text _weapon;
        private RawImage _weaponIcon;
        private Text _bagFull;
        // Owner, 26 Sep 2026: on a mount the hero only makes plain attacks; this dismounts, or mounts up again.
        private Button _mountButton;
        private Text _mountLabel;
        private bool _mountBusy;
        private const string LastMountKey = "orsuun.lastMount";
        private Text _log;
        private Text _guildLabel;
        private Text _ticker;
        private Text _guildTag;
        private Text _amber;
        private Text _trailTier;
        private Button _tradeCall;
        private Button _socialCall;
        private Text _socialCallLabel;
        private int _seenGuildInvites;
        private Text _tradeCallLabel;
        private Text _bountyLabel;
        private RawImage _flag;
        private int _lastLevel;
        private Text _stageLabel;
        private Text _pushLabel;
        private Button _pushButton;
        private RectTransform _hpFill;
        private Text _hpText;
        private Button[] _skillButtons;
        private Text[] _skillLabels;
        private Image[] _autoImages;
        private Text[] _skillNames;
        private Image[] _skillSweeps;
        private RawImage[] _skillArt;
        private string[] _skillShown;
        private Image[] _navBadges;
        private Text[] _autoLabels;
        private Image[] _autoLamps;
        private float _logAge;
        private Net.ServerLink.SettlementDto _shownSettlement;
        private Image _goalPlate;
        private RawImage _goalIcon;
        private Text _goalText;
        private Text _goalCount;
        private Image _pushGlow;
        private float _pushNudge;
        /// <summary>The account the goal chain belongs to (player name online, "local" offline); null until known.</summary>
        private string _goalKey;
        private float _goalKeySince;
        private int _goalReached;
        private Goal _goal;
        private Goal _chainGoal;
        private float _goalCheckIn;
        private string _goalMetText;
        private float _goalMetAt = -10f;

        private Transform _canvas;

        public void Init(GameRoot root)
        {
            _root = root;
            Transform canvas = Ui.Canvas("HudCanvas", 0).transform;
            _canvas = canvas;
            transform.SetParent(canvas, false);

            // Top bar: the level on a crimson medallion, currencies in bronze-rimmed pills.
            Ui.Sliced("TopBar", canvas, 0f, 0.94f, 1f, 1f, "TopBar", Color.white);
            // The level on the painted crimson medallion (tap for GEAR).
            RectTransform levelBox = Ui.Rect("Level", canvas, 0.004f, 0.941f, 0.112f, 1f);
            Image medal = Ui.Sliced("Medallion", levelBox, 0f, 0f, 1f, 1f, "Medallion", Color.white);
            var medalFit = medal.gameObject.AddComponent<AspectRatioFitter>();
            medalFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            medalFit.aspectRatio = 1f;
            medal.gameObject.AddComponent<Button>().onClick.AddListener(() => root.Gear.Open());
            medal.gameObject.AddComponent<Press>();
            _level = Ui.Title("LevelText", medal.transform, 0.2f, 0.2f, 0.8f, 0.8f, "", 34, TextAnchor.MiddleCenter, Palette.Parchment);
            _currencies = new Text[CurrencyIcons.Length];
            for (int i = 0; i < CurrencyIcons.Length; i++)
            {
                // Sorn gets a wider pill: it runs to seven figures.
                float x0 = i == 0 ? 0.112f : 0.29f + (i - 1) * 0.118f;
                float x1 = i == 0 ? 0.285f : x0 + 0.112f;
                Ui.Sliced("Pill" + CurrencyIcons[i], canvas, x0, 0.951f, x1, 0.991f, "Pill", Color.white).raycastTarget = false;
                // Icon fits its parent, so it gets its own box.
                RectTransform box = Ui.Rect("IconBox" + CurrencyIcons[i], canvas, x0 + 0.004f, 0.953f, x0 + 0.044f, 0.989f);
                Ui.Icon("Icon", box, 0f, 0f, 1f, 1f, CurrencyIcons[i]);
                _currencies[i] = Ui.Label("Count" + CurrencyIcons[i], canvas, x0 + 0.046f, 0.951f, x1 - 0.008f, 0.991f, "", 26, TextAnchor.MiddleLeft, Palette.Sorn);
            }

            // Where the hunt is, over a soft shade so it reads against any sky.
            Ui.Panel("TopShade", canvas, 0f, 0.87f, 1f, 0.94f, new Color(0f, 0f, 0f, 0.32f)).raycastTarget = false;
            _stage = Ui.Title("Stage", canvas, 0.03f, 0.905f, 0.97f, 0.94f, "", 28, TextAnchor.MiddleLeft, Palette.Parchment);
            _link = Ui.Label("Link", canvas, 0.03f, 0.875f, 0.97f, 0.905f, "", 22, TextAnchor.MiddleLeft, Palette.Warn);
            _link.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            _banner = Ui.Title("Banner", canvas, 0.05f, 0.80f, 0.95f, 0.87f, "", 56, TextAnchor.MiddleCenter, Palette.Warn);

            // The next goal (Rules.Goals) at the top of the lane; tap it to go where it is done.
            _goalPlate = Ui.Framed("Goal", canvas, 0.02f, 0.822f, 0.80f, 0.868f, new Color(0.06f, 0.06f, 0.11f, 0.9f));
            _goalPlate.gameObject.AddComponent<Button>().onClick.AddListener(OnGoalTap);
            _goalPlate.gameObject.AddComponent<Press>();
            RectTransform goalIconBox = Ui.Rect("IconBox", _goalPlate.transform, 0.015f, 0.12f, 0.1f, 0.88f);
            _goalIcon = Ui.Icon("Icon", goalIconBox, 0f, 0f, 1f, 1f, "NavForge");
            _goalText = Ui.Label("Text", _goalPlate.transform, 0.11f, 0.08f, 0.86f, 0.92f, "", 24, TextAnchor.MiddleLeft, Palette.Parchment);
            _goalText.supportRichText = true;
            _goalCount = Ui.Title("Count", _goalPlate.transform, 0.86f, 0.08f, 0.98f, 0.92f, "", 24, TextAnchor.MiddleCenter, Palette.Sorn);
            _goalPlate.gameObject.SetActive(false);

            // The hero's HP in a bronze trough, and the newest world chat line above it (tap it for CHAT).
            _hpFill = Ui.Bar("Hp", canvas, 0.03f, 0.452f, 0.97f, 0.486f, new Color(0.82f, 0.17f, 0.14f), out _);
            _hpText = Ui.Title("HpText", canvas, 0.03f, 0.452f, 0.97f, 0.486f, "", 22, TextAnchor.MiddleCenter, Palette.Parchment);
            Image strip = Ui.Panel("ChatStrip", canvas, 0.03f, 0.492f, 0.97f, 0.524f, new Color(0f, 0f, 0f, 0.5f));
            strip.gameObject.AddComponent<Button>().onClick.AddListener(() => root.Chat.Open());
            RectTransform chatIcon = Ui.Rect("ChatIconBox", strip.transform, 0.005f, 0.05f, 0.07f, 0.95f);
            Ui.Icon("ChatIcon", chatIcon, 0f, 0f, 1f, 1f, "NavChat");
            _ticker = Ui.Label("ChatLine", strip.transform, 0.08f, 0f, 0.99f, 1f, "", 20, TextAnchor.MiddleLeft, Palette.Parchment);
            _ticker.supportRichText = true;
            _ticker.horizontalOverflow = HorizontalWrapMode.Overflow;
            _ticker.resizeTextForBestFit = false;
            strip.gameObject.AddComponent<RectMask2D>();

            // The command deck under the lane.
            Ui.Sliced("BottomPanel", canvas, 0f, 0f, 1f, GameRoot.LaneViewportBottom, "Backdrop", Color.white).raycastTarget = false;
            Ui.Sliced("BottomTrim", canvas, 0f, GameRoot.LaneViewportBottom - 0.012f, 1f, GameRoot.LaneViewportBottom + 0.002f, "TopBar", Color.white).raycastTarget = false;
            // The hero plate (hunt mockup): the weapon in a gold slot on the left, its name and the hero's numbers beside it.
            Image plate = Ui.Framed("HeroPlate", canvas, 0.03f, 0.386f, 0.97f, 0.444f, new Color(0.13f, 0.12f, 0.2f));
            Ui.SlotTile("WeaponSlot", plate.transform, 0.012f, 0.06f, 0.16f, 0.94f, new Color(0.08f, 0.08f, 0.14f));
            RectTransform weaponBox = Ui.Rect("WeaponIconBox", plate.transform, 0.025f, 0.12f, 0.147f, 0.88f);
            _weaponIcon = Ui.Icon("WeaponIcon", weaponBox, 0f, 0f, 1f, 1f, "Weapon");
            _weapon = Ui.Title("Weapon", plate.transform, 0.18f, 0.05f, 0.77f, 0.95f, "", 30, TextAnchor.MiddleLeft, Palette.Parchment);
            _mountButton = Ui.Button("Mount", plate.transform, 0.78f, 0.1f, 0.99f, 0.9f, "", 20, Palette.Alloy, ToggleMount, out _mountLabel);
            _mountButton.gameObject.SetActive(false);
            // Loot and news float over the ground of the lane, above the chat line.
            _log = Ui.Title("Log", canvas, 0.04f, 0.528f, 0.96f, 0.562f, "", 26, TextAnchor.MiddleCenter, Palette.Sorn);

            // Skills stand in a framed panel: round, with a painted icon, a cooldown sweep and the seconds left; their name
            // and AUTO switch (a lamp that glows green while on) below. Five a class since 26 Sep 2026: the fourth and
            // fifth show their level, darkened, until the hero reaches it.
            Ui.Framed("SkillPanel", canvas, 0.02f, 0.169f, 0.98f, 0.381f, new Color(0.07f, 0.07f, 0.12f, 0.85f)).raycastTarget = false;
            int count = root.Session.Lane.Skills.Length;
            _skillButtons = new Button[count];
            _skillLabels = new Text[count];
            _skillNames = new Text[count];
            _skillSweeps = new Image[count];
            _skillArt = new RawImage[count];
            _skillShown = new string[count];
            _autoImages = new Image[count];
            _autoLabels = new Text[count];
            _autoLamps = new Image[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                float step = 0.96f / count, cx = 0.02f + step * (i + 0.5f), half = Mathf.Min(0.12f, step * 0.47f);
                _skillButtons[i] = Ui.RoundButton("Skill" + i, canvas, cx - half, 0.24f, cx + half, 0.374f, "", new Color(0.2f, 0.2f, 0.3f),
                    () => { if (!_root.Replaying) _root.Session.Cast(index); }, out _skillSweeps[i], out _skillLabels[i]);
                _skillArt[i] = _skillButtons[i].transform.Find("Art").GetComponent<RawImage>();
                _skillLabels[i].resizeTextForBestFit = true;
                _skillLabels[i].resizeTextMaxSize = 60;
                _skillNames[i] = Ui.Title("SkillName" + i, canvas, cx - step * 0.5f, 0.212f, cx + step * 0.5f, 0.24f, "", 20, TextAnchor.MiddleCenter, Palette.Parchment);
                // The name opens the skill's grades (SKILLS).
                _skillNames[i].supportRichText = true;
                _skillNames[i].raycastTarget = true;
                _skillNames[i].gameObject.AddComponent<Button>().onClick.AddListener(() => _root.Skills.Open(index));
                Button auto = Ui.Button("Auto" + i, canvas, cx - step * 0.46f, 0.177f, cx + step * 0.46f, 0.21f, "", 16,
                    Palette.ButtonIdle, () => _root.Session.ToggleAutoCast(index), out _autoLabels[i]);
                _autoImages[i] = auto.GetComponent<Image>();
                RectTransform lampBox = Ui.Rect("LampBox", auto.transform, 0.1f, 0.22f, 0.24f, 0.78f);
                _autoLamps[i] = Ui.Sliced("Lamp", lampBox, 0f, 0f, 1f, 1f, "Badge", Color.white);
                _autoLamps[i].raycastTarget = false;
                var lampFit = _autoLamps[i].gameObject.AddComponent<AspectRatioFitter>();
                lampFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                lampFit.aspectRatio = 1f;
                RectTransform autoText = _autoLabels[i].rectTransform;
                autoText.anchorMin = new Vector2(0.26f, autoText.anchorMin.y);
            }

            // The four actions as square lacquer tiles with big painted icons (hunt mockup).
            Ui.Tile("Forge", canvas, 0.025f, 0.077f, 0.25f, 0.164f, "FORGE", 26, Palette.ButtonForge, "NavForge", () => root.Forge.Open(), out _);
            Button gearTile = Ui.Tile("Gear", canvas, 0.265f, 0.077f, 0.49f, 0.164f, "INVENTORY", 24, new Color(0.2f, 0.3f, 0.55f), "NavGear", () => root.Gear.Open(), out _);
            // A full bag leaves new drops behind: the tile says so.
            _bagFull = Ui.Title("BagFull", gearTile.transform, 0.04f, 0.78f, 0.96f, 0.97f, "BAG FULL", 17, TextAnchor.MiddleCenter, Palette.Bad);
            _bagFull.gameObject.SetActive(false);
            Ui.Tile("Shards", canvas, 0.505f, 0.077f, 0.73f, 0.164f, "SHARDS", 26, Palette.Alloy, "NavShards", () => root.Sockets.Open(), out _);
            // A push goal tapped on the goal line lights the PUSH tile for a moment.
            _pushGlow = Ui.Sliced("PushGlow", canvas, 0.71f, 0.05f, 1f, 0.19f, "Glow", Palette.Sorn);
            _pushGlow.raycastTarget = false;
            _pushGlow.color = Color.clear;
            _pushButton = Ui.Tile("Push", canvas, 0.745f, 0.077f, 0.97f, 0.164f, "", 22, Palette.Danger, "NavPush", root.Push, out _pushLabel);

            // Bottom bar (24 Sep 2026): the War of Banners, the bounty board, the guild and the Salt Exchange joined; SOUND
            // and SPEED moved into the MENU. Each is a framed tile with a painted icon over its label; a red badge marks
            // something waiting.
            Ui.Sliced("NavBar", canvas, 0f, 0f, 1f, 0.072f, "NavBar", Color.white);
            string[] icons = { "Zones", "War", "Bounties", "Guild", "Trade", "Menu" };
            string[] labels = { "ZONES", "WAR", "BOUNTIES", "GUILD", "TRADE", "MENU" };
            System.Action[] actions = { () => root.Zones.Open(), () => root.War.Open(), () => root.Bounties.Open(), () => root.Guild.Open(),
                () => root.Market.Open(), () => root.Menu.Open() };
            _navBadges = new Image[icons.Length];
            for (int i = 0; i < icons.Length; i++)
            {
                float x0 = 0.005f + i * (0.99f / icons.Length);
                float x1 = x0 + 0.99f / icons.Length;
                Ui.SlotTile("NavTile" + icons[i], canvas, x0 + 0.003f, 0.004f, x1 - 0.003f, 0.07f, new Color(0.09f, 0.09f, 0.16f));
                Ui.NavButton(icons[i], canvas, x0 + 0.012f, 0.008f, x1 - 0.012f, 0.066f, icons[i], labels[i], actions[i],
                    out Text label, out _navBadges[i]);
                if (i == 0) _stageLabel = label;
                if (i == 2) _bountyLabel = label;
                if (i == 3) _guildLabel = label;
            }

            // The Banner's flag in the corner of the lane; tap it for the War of Banners.
            _flag = BannerLook.FlagImage("BannerFlag", canvas, 0.905f, 0.76f, 0.985f, 0.865f);
            var flagButton = _flag.gameObject.AddComponent<Button>();
            _flag.raycastTarget = true;
            flagButton.onClick.AddListener(() => root.War.Open());
            // The guild tag under the flag, in the guild's colour.
            _guildTag = Ui.Title("GuildTag", canvas, 0.88f, 0.735f, 1f, 0.76f, "", 22, TextAnchor.MiddleCenter, Palette.Parchment);
            // The Caravan under the flag: a round camel button with the Amber held beneath it.
            Ui.RoundButton("Caravan", canvas, 0.9f, 0.655f, 0.99f, 0.73f, "Caravan", new Color(0.55f, 0.3f, 0.08f), () => root.Caravan.Open(), out _, out _);
            _amber = Ui.Title("Amber", canvas, 0.86f, 0.632f, 1f, 0.656f, "", 20, TextAnchor.MiddleCenter, CaravanPanel.AmberColor);
            // A trade waiting (someone asks, or a window is open): a call under the next goal; tap it for the window.
            _tradeCall = Ui.Button("TradeCall", canvas, 0.02f, 0.778f, 0.8f, 0.818f, "", 20, Palette.Alloy, () => root.Trade.Open(), out _tradeCallLabel);
            _tradeCallLabel.supportRichText = true;
            _tradeCall.gameObject.SetActive(false);
            // Friend requests or a guild invite waiting (and not seen yet): a call in the same place, when no trade calls.
            _socialCall = Ui.Button("SocialCall", canvas, 0.02f, 0.778f, 0.8f, 0.818f, "", 20, Palette.Safe, OpenSocial, out _socialCallLabel);
            _socialCallLabel.supportRichText = true;
            _socialCall.gameObject.SetActive(false);
            // The Campaign Trail under it: a round waystone button with the tier beneath (CLAIM when a reward waits).
            Ui.RoundButton("Trail", canvas, 0.9f, 0.553f, 0.99f, 0.628f, "Trail", new Color(0.1f, 0.35f, 0.36f), () => root.Trail.Open(), out _, out _);
            _trailTier = Ui.Title("TrailTier", canvas, 0.86f, 0.53f, 1f, 0.554f, "", 20, TextAnchor.MiddleCenter, Palette.Parchment);
        }

        private void OpenSocial()
        {
            if (_root.Server.GuildInvites > _seenGuildInvites) _root.Guild.Open();
            else if (_root.Server.FriendAsks > _root.Friends.SeenAsks) _root.Friends.Open();
            else if (_root.Server.WhisperUnread > 0) _root.Messages.Open();
            else _root.Mail.Open();
        }

        /// <summary>Resources/Icons/Skills name for a skill: its letters ("Kestrel's Dive" is KestrelsDive).</summary>
        private static string SkillIcon(string name) => "Icons/Skills/" + SkillLetters(name);

        /// <summary>Dismounts (the skills come back), or mounts up again on the mount to ride.</summary>
        private void ToggleMount()
        {
            if (_mountBusy || !_root.Server.Online) return;
            bool mounted = _root.Session.Lane.Mounted;
            string target;
            if (mounted)
            {
                PlayerPrefs.SetString(LastMountKey, _root.Server.WornId(WardrobeKind.Mount));
                PlayerPrefs.Save();
                target = "";
            }
            else target = MountToRide();
            if (target == null) return;
            _mountBusy = true;
            StartCoroutine(_root.Server.Wear(target, WardrobeKind.Mount, error =>
            {
                _mountBusy = false;
                Log(error ?? (mounted ? "Dismounted: your skills are ready again." : "Mounted: plain attacks only until you dismount."));
            }));
        }

        /// <summary>The mount to ride: the last one ridden while it has time left, else the held mount with the most time.</summary>
        private string MountToRide()
        {
            Net.ServerLink.WardrobeDto wardrobe = _root.Server.Wardrobe;
            if (wardrobe?.pieces == null) return null;
            string last = PlayerPrefs.GetString(LastMountKey, "");
            if (last.Length > 0 && Rules.Wardrobe.Find(last)?.Kind == WardrobeKind.Mount && _root.Server.SecondsLeft(last) > 0) return last;
            string best = null;
            long bestLeft = 0;
            foreach (Net.ServerLink.WardrobePieceDto piece in wardrobe.pieces)
            {
                WardrobeDef def = Rules.Wardrobe.Find(piece.id);
                long left = def != null && def.Kind == WardrobeKind.Mount ? _root.Server.SecondsLeft(piece.id) : 0;
                if (left > bestLeft) { best = piece.id; bestLeft = left; }
            }
            return best;
        }

        /// <summary>A skill's icon name: the letters of its name (Resources/Icons/Skills).</summary>
        public static string SkillLetters(string name)
        {
            var letters = new System.Text.StringBuilder();
            foreach (char c in name) if (char.IsLetter(c)) letters.Append(c);
            return letters.ToString();
        }

        /// <summary>The screen area (canvas anchors) covering the named HUD elements, for the tutorial's highlight.</summary>
        public Rect Area(params string[] names)
        {
            Vector2 min = Vector2.one, max = Vector2.zero;
            foreach (string n in names)
            {
                var rect = (RectTransform)_canvas.Find(n);
                if (rect == null) continue;
                min = Vector2.Min(min, rect.anchorMin);
                max = Vector2.Max(max, rect.anchorMax);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public void Handle(LaneEvent e)
        {
            if (e.Kind != LaneEventKind.Loot) return;
            Log(e.Text);
        }

        /// <summary>A time left, short: "1d 4h", "2h 05m", "12m".</summary>
        private static string Span(long seconds) =>
            seconds >= 86400 ? $"{seconds / 86400}d {seconds % 86400 / 3600}h" : seconds >= 3600 ? $"{seconds / 3600}h {seconds % 3600 / 60:00}m" : $"{System.Math.Max(1, seconds / 60)}m";

        public void Log(string text)
        {
            _log.text = text;
            _logAge = 0f;
        }

        private void Update()
        {
            if (_root == null) return;
            PlayerSession session = _root.Session;
            LaneSim lane = _root.ActiveLane;
            Inventory inv = session.Inventory;

            _level.text = inv.Level.ToString();
            _currencies[0].text = inv.Sorn.ToString("N0");
            _currencies[1].text = inv.Potions.ToString();
            _currencies[2].text = inv.Materials.ToString();
            _currencies[3].text = inv.ScrollsOfMercy.ToString();
            _currencies[4].text = inv.KhansAlloys.ToString();
            _currencies[5].text = inv.Turnstones.ToString();
            _currencies[6].text = (inv.Korshards[0] + inv.Korshards[1] + inv.Korshards[2] + inv.Korshards[3] + inv.Korshards[4]).ToString();

            string encounter = lane.IsBossEncounter ? lane.Stage.BossName.ToUpperInvariant()
                : lane.IsKorstoneEncounter ? (lane.IsElderNext ? "ELDER KORSTONE" : "KORSTONE")
                : lane.Stage.FinalEncounter == FinalEncounter.None ? "Pack" : $"Pack {lane.EncounterIndex + 1}/{lane.Stage.PacksBeforeKorstone}";
            // The farm lane counts across its loops (a new lane each one online); a boss or dungeon lane counts its own.
            bool farm = lane == session.Lane;
            _stage.text = $"{Content.StageName(lane.Stage.StageNumber)}  ·  {encounter}  ·  Korstones {(farm ? session.HuntKorstones : lane.KorstonesDestroyed)}  ·  Deaths {(farm ? session.HuntDeaths : lane.Deaths)}";
            _stage.color = lane.IsKorstoneEncounter ? Palette.Warn : Palette.Parchment;
            Bell bell = _root.LocalBell;
            string bellText;
            if (bell != Bell.None) bellText = "  ·  " + EveningBells.Name(bell).ToUpperInvariant();
            else if (_root.Server.Online && _root.Server.Bell != null) bellText = $"  ·  next bell in {_root.Server.Bell.minutesUntilNext / 60}h {_root.Server.Bell.minutesUntilNext % 60:00}m";
            else { EveningBells.Next(System.DateTime.Now, out int mins); bellText = $"  ·  next bell in {mins / 60}h {mins % 60:00}m"; }
            // Weekend events (Rules.WorldEvents) that run now, with the time they have left.
            string eventText = "";
            foreach (Net.ServerLink.WorldEventDto e in _root.Server.Events)
                if (_root.Server.EventRunning(e)) eventText += "  ·  " + e.name.ToUpperInvariant() + "  ·  " + Span(_root.Server.EventEndsIn(e));
            // A running event takes the server's address and the bell countdown's place, or the line runs over.
            _link.text = eventText.Length > 0 ? eventText.Substring(5) + (bell != Bell.None ? bellText : "") : _root.Server.Status + bellText;
            _link.color = bell != Bell.None || eventText.Length > 0 ? Palette.Sorn : _root.Server.Online ? Palette.Good : Palette.Warn;
            _banner.text = _root.ReplayBanner;
            string shown = _root.ReplayBanner;
            _banner.color = shown.StartsWith("CLEARED") || shown.StartsWith("FELLED") || shown.EndsWith("CLEARED") || shown.StartsWith("THE SMITH STRUCK") || shown.StartsWith("VICTORY") ? Palette.Good
                : shown.StartsWith("FAILED") || shown.StartsWith("HELD") || shown.StartsWith("FELL ON") || shown.StartsWith("THE PIECE BROKE") || shown.StartsWith("DEFEAT") ? Palette.Bad : Palette.Warn;

            Net.ServerLink.SettlementDto settled = _root.Server.LastSettlement;
            if (settled != null && settled != _shownSettlement && settled.offline)
            {
                _shownSettlement = settled;
                Log($"Welcome back: {settled.countedSeconds / 3600f:0.0} h away, {settled.korstones} Korstones, +{settled.sornEarned:N0} sorn"
                    + (settled.leftBehind > 0 ? $"  ·  the bag was full: {settled.leftBehind} drop{(settled.leftBehind == 1 ? "" : "s")} left behind" : ""));
            }
            else if (settled != null && settled != _shownSettlement && settled.loopsVerified > 0)
            {
                // Active play paid: the server replayed this interval's loops and credited their pace.
                _shownSettlement = settled;
                Log($"Hunting pace {settled.activeBp / 100}%  ·  {settled.loopsVerified} loop{(settled.loopsVerified == 1 ? "" : "s")} verified");
            }

            float hp = Mathf.Clamp01(lane.HeroHp / (float)lane.HeroMaxHp);
            _hpFill.anchorMax = new Vector2(hp, 1f);
            _hpText.text = lane.Phase == LanePhase.Dead ? "DEFEATED — respawning" : $"{lane.HeroHp} / {lane.HeroMaxHp}";

            HeroStats stats = session.Hero;
            _weapon.text = $"{session.Weapon.DisplayName} +{session.Weapon.UpgradeLevel}\n<size=20><color=#C2BAAD>Attack {stats.Attack}   ·   Defense {stats.Defense}   ·   Crit {stats.CritChanceBp / 100}%</color></size>";
            _weapon.color = ForgePanel.LevelColor(session.Weapon.UpgradeLevel);
            _bagFull.gameObject.SetActive(session.Inventory.Loot.Count >= Bag.Size);
            Ui.SetIcon(_weaponIcon, Ui.ItemIcon(session.Weapon));   // the weapon's own picture for its level band and the class

            _logAge += Time.deltaTime;
            Color logColor = Palette.Sorn;
            logColor.a = Mathf.Clamp01(4f - _logAge);
            _log.color = logColor;

            for (int i = 0; i < _skillButtons.Length; i++)
            {
                int ticksLeft = lane.CooldownTicksLeft(i);
                bool unlocked = lane.IsUnlocked(i);
                _skillButtons[i].interactable = unlocked && !_root.Replaying && ticksLeft == 0 && lane.Phase == LanePhase.Fighting;
                SkillDef skill = lane.Skills[i];
                if (_skillShown[i] != skill.Name)
                {
                    // A class change swaps the kit: new names, new icons.
                    _skillShown[i] = skill.Name;
                    _skillArt[i].texture = Resources.Load<Texture2D>(SkillIcon(skill.Name));
                    _skillArt[i].enabled = _skillArt[i].texture != null;
                }
                int book = Books.Id(session.Class, i);
                int grade = book < session.SkillGradeList.Count ? session.SkillGradeList[book] : 0;
                _skillNames[i].text = skill.Name.ToUpperInvariant() + (grade > 0 ? "  " + ConfirmDialog.Tint(SkillGrades.Name(grade), Palette.Sorn) : "");
                if (unlocked && lane.Mounted)
                {
                    // In the saddle: plain attacks only until he dismounts.
                    _skillArt[i].color = new Color(0.55f, 0.52f, 0.5f, 0.75f);
                    _skillSweeps[i].fillAmount = 1f;
                    _skillLabels[i].text = "";
                    _skillNames[i].text = ConfirmDialog.Tint(skill.Name.ToUpperInvariant(), Palette.Muted);
                    _autoLabels[i].text = "RIDING";
                    _autoImages[i].color = Palette.ButtonIdle;
                    _autoLamps[i].color = new Color(0.25f, 0.24f, 0.27f);
                    continue;
                }
                if (!unlocked)
                {
                    // Locked: dark art under a full sweep, the level it opens at in the middle.
                    _skillArt[i].color = new Color(0.45f, 0.45f, 0.5f, 0.6f);
                    _skillSweeps[i].fillAmount = 1f;
                    _skillLabels[i].text = "LV " + skill.UnlockLevel;
                    _skillNames[i].text = ConfirmDialog.Tint(skill.Name.ToUpperInvariant(), Palette.Muted);
                    _autoLabels[i].text = "LOCKED";
                    _autoImages[i].color = Palette.ButtonIdle;
                    _autoLamps[i].color = new Color(0.25f, 0.24f, 0.27f);
                    continue;
                }
                _skillArt[i].color = Color.white;
                _skillSweeps[i].fillAmount = skill.CooldownTicks > 0 ? Mathf.Clamp01(ticksLeft / (float)skill.CooldownTicks) : 0f;
                _skillLabels[i].text = ticksLeft == 0 ? "" : $"{ticksLeft / (float)LaneSim.TicksPerSecond:0.0}";
                _autoLabels[i].text = lane.AutoCast[i] ? "AUTO ON" : "AUTO OFF";
                _autoImages[i].color = lane.AutoCast[i] ? Palette.Safe : Palette.ButtonIdle;
                _autoLamps[i].color = lane.AutoCast[i] ? new Color(0.45f, 1f, 0.4f) : new Color(0.35f, 0.33f, 0.36f);
            }

            bool mounted = lane.Mounted;
            bool showMount = _root.Server.Online && !_root.Replaying && (mounted || MountToRide() != null);
            if (_mountButton.gameObject.activeSelf != showMount) _mountButton.gameObject.SetActive(showMount);
            _mountLabel.text = mounted ? "DISMOUNT" : "MOUNT UP";
            _mountButton.interactable = !_mountBusy;

            bool allCleared = session.HighestStageCleared >= Content.TotalStages;
            _pushLabel.text = allCleared ? "ALL CLEARED" : $"PUSH\n<size=15>{Content.StageName(session.PushTarget)}</size>";
            _pushButton.interactable = !_root.Replaying && !_root.PushBusy && !allCleared;
            _stageLabel.text = "ZONES";
            Net.ServerLink.GuildBriefDto guild = _root.Server.Guild;
            bool inGuild = _root.Server.InGuild;
            _guildLabel.text = inGuild ? "[" + guild.tag + "]" : "GUILD";
            _guildTag.text = inGuild ? "[" + guild.tag + "]" : "";
            _ticker.text = _root.Chat.Ticker.Length > 0 ? _root.Chat.Ticker : ConfirmDialog.Tint(_root.Server.Online ? "Tap to talk with the steppe." : "Chat needs the server.", Palette.Muted);
            if (inGuild) _guildTag.color = GuildPanel.ColorOf(guild.color);
            _amber.text = _root.Server.Online ? _root.Server.Amber.ToString("N0") : "";
            Net.ServerLink.TrailDto trail = _root.Server.Online ? _root.Server.Trail : null;
            bool trailReady = trail != null && TrailPanel.AnyReady(trail);
            _trailTier.text = trail == null ? "" : trailReady ? "CLAIM" : "TIER " + trail.tier;
            _trailTier.color = trailReady ? Palette.Sorn : Palette.Parchment;
            Net.ServerLink.TradeBriefDto trade = _root.Server.Online ? _root.Server.TradeBrief : null;
            bool calling = trade != null && !_root.Trade.IsOpen && (trade.state == "Open" || trade.incoming);
            if (_tradeCall.gameObject.activeSelf != calling) _tradeCall.gameObject.SetActive(calling);
            if (calling) _tradeCallLabel.text = trade.state == "Open" ? $"TRADE WITH {trade.otherName.ToUpperInvariant()}: BACK TO THE WINDOW" : $"{trade.otherName.ToUpperInvariant()} ASKS TO TRADE: ANSWER";
            int asks = _root.Server.Online ? _root.Server.FriendAsks : 0;
            int invites = _root.Server.Online ? _root.Server.GuildInvites : 0;
            if (_root.Guild.IsOpen) _seenGuildInvites = invites;
            if (invites < _seenGuildInvites) _seenGuildInvites = invites;
            if (asks < _root.Friends.SeenAsks) _root.Friends.SeenAsks = asks;
            bool newAsks = asks > _root.Friends.SeenAsks && !_root.Friends.IsOpen;
            bool newInvites = invites > _seenGuildInvites && !_root.Guild.IsOpen;
            int whispers = _root.Server.Online && !_root.Messages.IsOpen ? _root.Server.WhisperUnread : 0;
            int letters = _root.Server.Online && !_root.Mail.IsOpen ? _root.Server.MailUnread : 0;
            bool social = !calling && (newAsks || newInvites || whispers > 0 || letters > 0);
            if (_socialCall.gameObject.activeSelf != social) _socialCall.gameObject.SetActive(social);
            if (social) _socialCallLabel.text = newInvites ? "A GUILD INVITES YOU: ANSWER ON THE GUILD SCREEN"
                : newAsks ? (asks == 1 ? "A HERO ASKS TO BE FRIENDS: ANSWER" : $"{asks} HEROES ASK TO BE FRIENDS: ANSWER")
                : whispers > 0 ? (whispers == 1 ? "A NEW MESSAGE: READ IT" : $"{whispers} NEW MESSAGES: READ THEM")
                : letters == 1 ? "A LETTER HAS COME: OPEN THE MAILBOX" : $"{letters} LETTERS HAVE COME: OPEN THE MAILBOX";
            bool claim = _root.Bounties.AnyClaimable;
            _navBadges[2].gameObject.SetActive(claim);
            _navBadges[5].gameObject.SetActive(_root.Server.Online && _root.Server.AchievementsReady > 0);
            BannerLook.Show(_flag, _root.Server.Banner);
            if (_lastLevel > 0 && inv.Level > _lastLevel)
            {
                GameAudio.Instance?.Play("LaneLevelUp", 0.9f, 1f, 0f);
                Log($"Level up!  Level {inv.Level}");
            }
            _lastLevel = inv.Level;

            UpdateGoal(session);
            Color glow = Palette.Sorn;
            _pushNudge = Mathf.Max(0f, _pushNudge - Time.unscaledDeltaTime);
            glow.a = _pushNudge > 0f ? 0.75f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 0f;
            _pushGlow.color = glow;
        }

        /// <summary>
        /// The goal line: the chain step reached is kept per account in PlayerPrefs, so a met goal stays met. Checked
        /// twice a second; a chain step met while playing flashes GOAL MET (a jump on loading an account does not).
        /// </summary>
        private void UpdateGoal(PlayerSession session)
        {
            Net.ServerLink server = _root.Server;
            string key = server.Online ? (string.IsNullOrEmpty(server.PlayerName) ? null : server.PlayerName)
                : server.Status.StartsWith("LOCAL MODE") ? "local" : null;
            float now = Time.unscaledTime;
            if (key != null && key != _goalKey)
            {
                _goalKey = key;
                _goalKeySince = now;
                _goalReached = LoadGoalStep(key);
                _goal = _chainGoal = null;
                _goalCheckIn = 0f;
            }

            _goalCheckIn -= Time.unscaledDeltaTime;
            if (key != null && _goalCheckIn <= 0f)
            {
                _goalCheckIn = 0.5f;
                var world = new GoalWorld { Online = server.Online, BountyReady = _root.Bounties.AnyClaimable, InGuild = server.InGuild };
                Goal chain = Goals.OnChain(session, world, _goalReached);
                if (chain != null && chain.Step > _goalReached)
                {
                    _goalReached = chain.Step;
                    SaveGoalStep(key, _goalReached);
                }
                bool passed = _chainGoal != null && (chain == null || chain.Step > _chainGoal.Step);
                if (passed && now - _goalKeySince > 3f)
                {
                    _goalMetText = _chainGoal.Text;
                    _goalMetAt = now;
                    GameAudio.Instance?.Play("LaneLevelUp", 0.8f, 1f, 0f);
                }
                _chainGoal = chain;
                _goal = Goals.Reminder(session, world) ?? chain;
            }

            bool celebrating = now - _goalMetAt < 2.4f;
            bool tutorialGoal = _root.Tutorial != null && _root.Tutorial.ShowsGoal;
            bool show = _goalKey != null && !_root.Replaying && !_root.PushBusy
                        && (tutorialGoal || (!(_root.Tutorial != null && _root.Tutorial.Running) && (_goal != null || celebrating)));
            if (_goalPlate.gameObject.activeSelf != show) _goalPlate.gameObject.SetActive(show);
            if (!show) return;

            if (celebrating)
            {
                _goalText.text = ConfirmDialog.Tint("GOAL MET", Palette.Good) + "\n" + _goalMetText;
                _goalCount.text = "";
                float flash = Mathf.Clamp01(1f - (now - _goalMetAt) / 1.2f);
                _goalPlate.color = Color.Lerp(new Color(0.06f, 0.06f, 0.11f, 0.9f), new Color(0.55f, 0.42f, 0.12f, 0.95f), flash);
                return;
            }
            _goalPlate.color = new Color(0.06f, 0.06f, 0.11f, 0.9f);
            if (_goal == null)
            {
                _goalText.text = "<size=17>" + ConfirmDialog.Tint("NEXT GOAL", Palette.Sorn) + "</size>\nEvery goal met. The steppe is yours.";
                _goalCount.text = "";
                return;
            }
            _goalText.text = "<size=17>" + ConfirmDialog.Tint("NEXT GOAL", Palette.Sorn) + "</size>\n" + _goal.Text;
            _goalCount.text = _goal.Target > 1 ? $"{Mathf.Min(_goal.Current, _goal.Target)}/{_goal.Target}" : "";
            string icon = GoalIcon(_goal.Screen);
            if (_goalIcon.texture == null || _goalIcon.texture.name != icon)
            {
                _goalIcon.texture = Resources.Load<Texture2D>("Icons/" + icon);
                _goalIcon.enabled = _goalIcon.texture != null;
            }
        }

        private static string GoalIcon(GoalScreen screen) => screen switch
        {
            GoalScreen.Forge => "NavForge",
            GoalScreen.Gear => "NavGear",
            GoalScreen.Push => "NavPush",
            GoalScreen.Bounties => "NavBounties",
            GoalScreen.Guild => "NavGuild",
            _ => "NavZones",
        };

        private void OnGoalTap()
        {
            if (_goal == null || _root.Replaying || _root.PushBusy) return;
            switch (_goal.Screen)
            {
                case GoalScreen.Forge: _root.Forge.Open(); break;
                case GoalScreen.Gear: _root.Gear.Open(); break;
                case GoalScreen.Bounties: _root.Bounties.Open(); break;
                case GoalScreen.Guild: _root.Guild.Open(); break;
                case GoalScreen.Push:
                    _pushNudge = 2.2f;
                    Log("Tap PUSH to take the next stage.");
                    break;
                default:
                    Log("Keep hunting: every pack brings the next level closer.");
                    break;
            }
        }

        private static int LoadGoalStep(string key)
        {
            try { return PlayerPrefs.GetInt("orsuun.goalStep." + key, 0); } catch { return 0; }
        }

        private static void SaveGoalStep(string key, int step)
        {
            try { PlayerPrefs.SetInt("orsuun.goalStep." + key, step); PlayerPrefs.Save(); } catch { }
        }
    }
}
