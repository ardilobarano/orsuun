using System;
using System.Collections;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Orsuun.Client
{
    /// <summary>
    /// Boots the grey-box without any authored scene content: session, cameras, lane view and UI are
    /// all built here. The rules library decides everything; this side only steps it and draws it.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        /// <summary>Upper share of the screen that shows the lane; the HUD owns the rest.</summary>
        public const float LaneViewportBottom = 0.45f;

        private float _accumulator;
        private LaneSim _replay;

        public PlayerSession Session { get; private set; }
        public LaneView Lane { get; private set; }
        public Hud Hud { get; private set; }
        public ForgePanel Forge { get; private set; }
        public GearPanel Gear { get; private set; }
        public SkillsPanel Skills { get; private set; }
        public ZonePanel Zones { get; private set; }
        public SocketPanel Sockets { get; private set; }
        public TurnHelperPanel TurnHelper { get; private set; }
        public Net.ServerLink Server { get; private set; }
        public TitleScreen Title { get; private set; }
        public MenuPanel Menu { get; private set; }
        public BannerOath Oath { get; private set; }
        public WarPanel War { get; private set; }
        public BountyPanel Bounties { get; private set; }
        public GuildPanel Guild { get; private set; }
        public GuildWarPanel GuildWar { get; private set; }
        public PitsPanel Pits { get; private set; }
        public CaravanPanel Caravan { get; private set; }
        public TrailPanel Trail { get; private set; }
        public DailyPanel Daily { get; private set; }
        public TradePanel Trade { get; private set; }
        public WardrobePanel Wardrobe { get; private set; }
        public CharacterPanel Characters { get; private set; }
        public DepotPanel Depot { get; private set; }
        public SmithPanel Smith { get; private set; }
        public RuneLockPanel RuneLock { get; private set; }
        public ChatPanel Chat { get; private set; }
        public FriendsPanel Friends { get; private set; }
        public MessagesPanel Messages { get; private set; }
        public MailPanel Mail { get; private set; }
        public AchievementsPanel Achievements { get; private set; }
        public GuildRaidPanel GuildRaid { get; private set; }
        public MarketPanel Market { get; private set; }
        public AccountPanel Account { get; private set; }
        public GameNotifications Notifications { get; private set; }
        /// <summary>Amber in the App Store and Google Play (phones; the Mac keeps the playtest's free packs).</summary>
        public StoreFront Store { get; private set; }
        public LeaderboardPanel Leaderboards { get; private set; }
        public InvitePanel Invites { get; private set; }
        public RiverPanel River { get; private set; }
        public BannerkinPanel Kin { get; private set; }
        public RugPanel Rugs { get; private set; }
        public TownPanel Town { get; private set; }
        public TipCard Tips { get; private set; }
        public FieldFolk Field { get; private set; }
        public FieldMapPanel MapScreen { get; private set; }
        /// <summary>The Commander up on the big map now, standing at its landmark (map world bosses).</summary>
        public MapCommander Commander { get; private set; }
        /// <summary>The hunting party: an invite waiting, the members, LEAVE (Rules.Parties).</summary>
        public PartyPanel Party { get; private set; }
        /// <summary>INSPECT, WHISPER, TRADE, ADD FRIEND and INVITE TO PARTY for a hero met in the world.</summary>
        public HeroActions HeroCard { get; private set; }
        /// <summary>A screenshot run (-shot) or a recording (-clip): the sign-in, the guide and the story cards stay away.</summary>
        internal static readonly bool ShotRun = Array.IndexOf(Environment.GetCommandLineArgs(), "-shot") >= 0
                                                || Array.IndexOf(Environment.GetCommandLineArgs(), "-clip") >= 0;
        /// <summary>Another hero's gear and standing (from chat, the leaderboards and the Pits' board).</summary>
        public InspectPanel Inspect { get; private set; }
        /// <summary>The story cards: a map opening, its boss falling.</summary>
        public StoryPanel Story { get; private set; }
        public Tutorial Tutorial { get; private set; }
        public int SpeedMultiplier { get; set; } = 1;

        /// <summary>The bell in force: the server's when online, else the local clock's (the local session uses it too).</summary>
        public Bell LocalBell => Server.Online ? Server.ActiveBell : EveningBells.Active(DateTime.Now);

        /// <summary>The lane on screen: the parked farm lane, or a push replay while one runs.</summary>
        public LaneSim ActiveLane => _replay ?? Session.Lane;

        /// <summary>Whether a screen that opens by level is open for the hero playing (Rules.Unlocks); a guild member,
        /// or a hero a guild has invited, always has the guild.</summary>
        public bool Unlocked(Feature feature) => Unlocks.Open(feature, Session.Inventory.Level)
            || feature == Feature.Guild && (Server.InGuild || Server.GuildInvites > 0);
        public bool Replaying => _replay != null;
        public string ReplayBanner { get; private set; } = "";
        public bool PushBusy { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // The heavy art comes first (downloaded on a phone's first launch, ArtLoader), then the game.
            if (FindFirstObjectByType<GameRoot>() == null)
                ArtLoader.Begin(() => new GameObject("GameRoot").AddComponent<GameRoot>());
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Loc.Init();   // before any text is made
            GameSettings.Load();

            // Grey-box only: the shipped game rolls on the server. A time seed is fine for a local playtest.
            Session = new PlayerSession(new XorShiftRandom((ulong)DateTime.UtcNow.Ticks));
            Session.Lane.AutoCast[1] = true;

            GameAudio.Create();
            Notifications = new GameObject("GameNotifications").AddComponent<GameNotifications>();
            Store = new GameObject("StoreFront").AddComponent<StoreFront>();
            Store.Init(this);
            Story = new GameObject("StoryPanel").AddComponent<StoryPanel>();
            Story.Init(this);
            Tips = new GameObject("TipCard").AddComponent<TipCard>();
            Tips.Init(this);
            Leaderboards = new GameObject("LeaderboardPanel").AddComponent<LeaderboardPanel>();
            Leaderboards.Init(this);
            Invites = new GameObject("InvitePanel").AddComponent<InvitePanel>();
            Invites.Init(this);
            Kin = new GameObject("BannerkinPanel").AddComponent<BannerkinPanel>();
            Kin.Init(this);
            Rugs = new GameObject("RugPanel").AddComponent<RugPanel>();
            Rugs.Init(this);
            // Stays off its canvas: it shows itself whenever the server says the hero is at the river.
            River = new GameObject("RiverPanel").AddComponent<RiverPanel>();
            River.Init(this);
            Town = new GameObject("TownPanel").AddComponent<TownPanel>();
            Town.Init(this);
            Inspect = new GameObject("InspectPanel").AddComponent<InspectPanel>();
            Inspect.Init(this);
            HeroCard = new GameObject("HeroActions").AddComponent<HeroActions>();
            HeroCard.Init(this);
            BuildCameras();
            gameObject.AddComponent<Performance>().Init(GameObject.Find("LaneCamera")?.GetComponent<Camera>());
            Lane = new GameObject("LaneView").AddComponent<LaneView>();
            Lane.Init(Session.Lane);
            // The lane camera leans in on big moments and eases out on the walk (ActionCamera).
            GameObject.Find("LaneCamera")?.AddComponent<ActionCamera>().Init(Lane);
            // Other players hunting the same map, beside the field's road (FieldFolk).
            Field = new GameObject("FieldFolk").AddComponent<FieldFolk>();
            Field.Init(this, Lane);
            // A trail cache beside a big map's trail when one is due (TrailCache).
            new GameObject("TrailCache").AddComponent<TrailCache>().Init(this, Lane);
            // A big map's own sounds by where the hero is (MapSounds).
            new GameObject("MapSounds").AddComponent<MapSounds>().Init(this, Lane);

            Server = new GameObject("ServerLink").AddComponent<Net.ServerLink>();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-local") < 0) Server.Init(Session);
            else Server.MarkLocal();

            Forge = new GameObject("ForgePanel").AddComponent<ForgePanel>();
            Forge.Init(this);
            Gear = new GameObject("GearPanel").AddComponent<GearPanel>();
            Gear.Init(this);
            Skills = new GameObject("SkillsPanel").AddComponent<SkillsPanel>();
            Skills.Init(this);
            Zones = new GameObject("ZonePanel").AddComponent<ZonePanel>();
            Zones.Init(this);
            Sockets = new GameObject("SocketPanel").AddComponent<SocketPanel>();
            Sockets.Init(this);
            TurnHelper = new GameObject("TurnHelperPanel").AddComponent<TurnHelperPanel>();
            TurnHelper.Init(this);
            War = new GameObject("WarPanel").AddComponent<WarPanel>();
            War.Init(this);
            Bounties = new GameObject("BountyPanel").AddComponent<BountyPanel>();
            Bounties.Init(this);
            Guild = new GameObject("GuildPanel").AddComponent<GuildPanel>();
            Guild.Init(this);
            GuildWar = new GameObject("GuildWarPanel").AddComponent<GuildWarPanel>();
            GuildWar.Init(this);
            Pits = new GameObject("PitsPanel").AddComponent<PitsPanel>();
            Pits.Init(this);
            Caravan = new GameObject("CaravanPanel").AddComponent<CaravanPanel>();
            Caravan.Init(this);
            Trail = new GameObject("TrailPanel").AddComponent<TrailPanel>();
            Trail.Init(this);
            Daily = new GameObject("DailyPanel").AddComponent<DailyPanel>();
            Daily.Init(this);
            Trade = new GameObject("TradePanel").AddComponent<TradePanel>();
            Trade.Init(this);
            Wardrobe = new GameObject("WardrobePanel").AddComponent<WardrobePanel>();
            Wardrobe.Init(this);
            Characters = new GameObject("CharacterPanel").AddComponent<CharacterPanel>();
            Characters.Init(this);
            Depot = new GameObject("DepotPanel").AddComponent<DepotPanel>();
            Depot.Init(this);
            Smith = new GameObject("SmithPanel").AddComponent<SmithPanel>();
            Smith.Init(this);
            RuneLock = new GameObject("RuneLockPanel").AddComponent<RuneLockPanel>();
            RuneLock.Init();
            Market = new GameObject("MarketPanel").AddComponent<MarketPanel>();
            Market.Init(this);
            Friends = new GameObject("FriendsPanel").AddComponent<FriendsPanel>();
            Friends.Init(this);
            Messages = new GameObject("MessagesPanel").AddComponent<MessagesPanel>();
            Messages.Init(this);
            Mail = new GameObject("MailPanel").AddComponent<MailPanel>();
            Mail.Init(this);
            Achievements = new GameObject("AchievementsPanel").AddComponent<AchievementsPanel>();
            Achievements.Init(this);
            GuildRaid = new GameObject("GuildRaidPanel").AddComponent<GuildRaidPanel>();
            GuildRaid.Init(this);
            Chat = new GameObject("ChatPanel").AddComponent<ChatPanel>();
            Chat.Init(this);
            Hud = new GameObject("Hud").AddComponent<Hud>();
            Hud.Init(this);
            // A big map's minimap (on the HUD) and its full map.
            MapScreen = new GameObject("FieldMapPanel").AddComponent<FieldMapPanel>();
            MapScreen.Init(this);
            // The hunting party's card and its chip under the minimap (Rules.Parties).
            Party = new GameObject("PartyPanel").AddComponent<PartyPanel>();
            Party.Init(this);
            // A Commander up on a big map stands at its landmark and calls every hero there (MapCommander).
            Commander = new GameObject("MapCommander").AddComponent<MapCommander>();
            Commander.Init(this, Lane);
            Oath = new GameObject("BannerOath").AddComponent<BannerOath>();
            Oath.Init(this);
            Account = new GameObject("AccountPanel").AddComponent<AccountPanel>();
            Account.Init(this);
            Notifications.Init(this);
            Menu = new GameObject("MenuPanel").AddComponent<MenuPanel>();
            Menu.Init(this);
            Tutorial = new GameObject("Tutorial").AddComponent<Tutorial>();
            Tutorial.Init(this);
            Title = new GameObject("TitleScreen").AddComponent<TitleScreen>();
            Title.Init(this);
            // Screenshots and demos skip the title unless asked for it, and the tutorial unless -tutorial.
            string[] cmd = Environment.GetCommandLineArgs();
            if (Array.IndexOf(cmd, "-notitle") >= 0 || (ShotRun && Array.IndexOf(cmd, "-title") < 0)) Title.Skip();
            _tutorialPending = Array.IndexOf(cmd, "-tutorial") >= 0 || Array.IndexOf(cmd, "-tutorialStep") >= 0
                               || (!Tutorial.Finished && !ShotRun);

            // Dev switch for screenshots and demos: Orsuun.exe -forge
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-forge") >= 0) Forge.Open();

            // Dev switches for screenshots: -sampleloot fills a local bag; -gear opens the Gear screen; -confirm asks to forge.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sampleloot") >= 0 && !Server.Online) SampleLoot();
            // -class <Name> starts local play as that class (-kestrel kept for old scripts).
            string cls = Arg("-class") ?? (Array.IndexOf(Environment.GetCommandLineArgs(), "-kestrel") >= 0 ? "Kestrel" : null);
            if (cls != null && !Server.Online && Enum.TryParse(cls, out HeroClass chosen)) Session.SetClass(chosen);
            // -figure Man|Woman plays a class's second look locally (the class's first look otherwise).
            if (!Server.Online) Session.Figure = Enum.TryParse(Arg("-figure"), out Figure figure) ? figure : ItemLooks.NativeFigure(Session.Class);
            // -wear <look,look,...> wears wardrobe pieces by look key in local play (screenshots of mounts, companions, skins).
            if (Arg("-wear") != null && !Server.Online)
            {
                var wear = new System.Collections.Generic.List<WardrobeDef>();
                foreach (string look in Arg("-wear").Split(','))
                {
                    WardrobeDef def = Array.Find(Orsuun.Rules.Wardrobe.All, d => d.Look == look);
                    if (def != null) wear.Add(def);
                }
                Session.SetWorn(wear);
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gear") >= 0) Gear.Open();
            // -skills opens SKILLS; -bagtab <n> opens the inventory on a tab (1 gear, 2 books, 3 materials), -bagcard its first tile's card.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-skills") >= 0) Skills.Open();
            // -messages opens MESSAGES (-messagesto <name> the conversation with that hero).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-messages") >= 0)
            {
                if (Arg("-messagesto") != null) _messagesTo = Arg("-messagesto");
                else Messages.Open();
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-menu") >= 0) Menu.Open();
            // -mail opens the MAILBOX (sample letters in local play; -mailletter shows the first one open).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-mail") >= 0)
            {
                if (!Server.Online) Server.SampleMail();
                Mail.Open();
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-mailletter") >= 0) Mail.OpenFirst();
            }
            // Screenshots: -oath shows the Banner oath, -war the War of Banners, -bounties the bounty board.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-oath") >= 0) Oath.Open();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-war") >= 0) War.Open();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-bounties") >= 0) Bounties.Open();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-guild") >= 0) Guild.Open();
            // -chat, -zones, -shards, -market (-sell, -mylistings, -marketbooks, -sellbook, -marketgoods, -sellgood) and -account open
            // those screens for screenshots.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-chat") >= 0) Chat.Open();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-zones") >= 0) Zones.Open();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-shards") >= 0) Sockets.Open();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-market") >= 0)
            {
                Market.Open();
                Market.ShotView(Environment.GetCommandLineArgs());
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-account") >= 0) _accountAsked = true;
            // -turnhelper [pick|add|demo|run] opens the turning helper over the Forge (with the etching list or the piece
            // list open, or three more pieces added; run also starts turning them locally with 600 Turnstones).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-turnhelper") >= 0)
            {
                Forge.Open();
                TurnHelper.Open();
                if (Arg("-turnhelper") == "pick") TurnHelper.OpenPicker(0);
                if (Arg("-turnhelper") == "add") TurnHelper.OpenAdder();
                if (Arg("-turnhelper") == "demo" || Arg("-turnhelper") == "run") TurnHelper.AddForShot(3);
                if (Arg("-turnhelper") == "run" && !Server.Online)
                {
                    Session.Inventory.Turnstones = 600;
                    TurnHelper.StartForShot();
                }
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-anvilbag") >= 0 && Session.Inventory.Loot.Count > 0) Session.PutOnAnvil(Session.Inventory.Loot[4]);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-confirm") >= 0) { Forge.Open(); Forge.StartAttempt(ForgeMethod.ForgeAlone); }

            // Screenshots online: -guildwar opens GUILD WAR, -duel <lane> fights a war duel, -keep <n> opens a fortress keep,
            // each once the server has answered.
            _openGuildWar = Array.IndexOf(Environment.GetCommandLineArgs(), "-guildwar") >= 0;
            _duelLane = int.TryParse(Arg("-duel"), out int duelLane) ? duelLane : -1;
            _keepIndex = int.TryParse(Arg("-keep"), out int keepIndex) ? keepIndex : -1;
            // -pits opens THE PITS, -pitfight <n> fights its nth challenger.
            _openPits = Array.IndexOf(Environment.GetCommandLineArgs(), "-pits") >= 0;
            _pitFight = int.TryParse(Arg("-pitfight"), out int pitFight) ? pitFight : -1;
            // -caravan <tab> opens THE CARAVAN on a tab (0 skins .. 3 Amber), -wardrobe the WARDROBE, once online.
            _caravanTab = int.TryParse(Arg("-caravan"), out int caravanTab) ? caravanTab : -1;
            _openWardrobe = Array.IndexOf(Environment.GetCommandLineArgs(), "-wardrobe") >= 0;
            _openDepot = Array.IndexOf(Environment.GetCommandLineArgs(), "-depot") >= 0;
            _openTrail = Array.IndexOf(Environment.GetCommandLineArgs(), "-trail") >= 0;
            _openAchievements = Array.IndexOf(Environment.GetCommandLineArgs(), "-achievements") >= 0;
            _openRaid = Array.IndexOf(Environment.GetCommandLineArgs(), "-raid") >= 0;
            _openBoard = Arg("-leaderboard");
            _inspectShot = Arg("-inspect");
            _openInvite = Array.IndexOf(cmd, "-invite") >= 0;
            _goFishing = Array.IndexOf(cmd, "-river") >= 0;
            _openKin = Array.IndexOf(cmd, "-kin") >= 0;
            _openRugs = Array.IndexOf(cmd, "-rugs") >= 0;
            _openTown = Array.IndexOf(cmd, "-town") >= 0;
            if (Array.IndexOf(cmd, "-settings") >= 0) Menu.OpenSettingsForShot();
            _raidFight = Array.IndexOf(Environment.GetCommandLineArgs(), "-raidfight") >= 0;
            _openTrade = Array.IndexOf(Environment.GetCommandLineArgs(), "-trade") >= 0;
            // -friends opens FRIENDS once online (screenshots); -oathchange the change of Banner.
            _openFriends = Array.IndexOf(cmd, "-friends") >= 0;
            _openOathChange = Array.IndexOf(cmd, "-oathchange") >= 0;
            // Screenshots of the way in: -firstrun shows the sign-in screen, the oath and the character screen even with
            // -shot; "-firstrun guest" then plays as a guest, "-firstrun oath" also swears to the Sky Banner.
            _firstRun = Array.IndexOf(cmd, "-firstrun") >= 0 ? Arg("-firstrun") ?? "" : null;
            // -dungeon enters the Hollow Spire once online; -smith opens the Chained Smith with a dummy run (screenshots).
            _enterDungeon = Array.IndexOf(Environment.GetCommandLineArgs(), "-dungeon") >= 0;
            // Dev switch: -dungeon [id] enters that dungeon (1 the Hollow Spire, 2 Silkmother's Warren, 3 the Carvers' Archive).
            _dungeonToEnter = int.TryParse(Arg("-dungeon"), out int dungeonToEnter) && Dungeons.Find(dungeonToEnter) != null ? dungeonToEnter : Dungeons.All[0].Id;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-runelock") >= 0) RuneLock.Open(7, _ => { });
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-smith") >= 0) Smith.Open(_ => { });

            // Dev switch: -stage <n> parks local play at campaign stage n with the ones before it cleared, the hero at the
            // stage's level in an Epic +9 set of its item level, so the fight stays on screen (screenshots of the maps).
            // -plus <n> forges the set to +n instead (screenshots of the upgrade glow); the weapon carries sample rolls.
            if (int.TryParse(Arg("-stage"), out int parkAt) && !Server.Online && parkAt >= 1 && parkAt <= Content.TotalStages)
            {
                int gearLevel = Math.Max(1, Content.Stage(parkAt).GearItemLevel);
                int plus = int.TryParse(Arg("-plus"), out int p) ? Math.Max(0, Math.Min(ItemState.MaxUpgradeLevel, p)) : 9;
                var set = new System.Collections.Generic.List<ItemState>();
                for (int slot = 0; slot < 8; slot++) set.Add(new ItemState(gearLevel, Rarity.Epic, (EquipSlot)slot) { UpgradeLevel = plus });
                WeaponRolls.Roll(set[0], new XorShiftRandom((ulong)parkAt));
                Session.Inventory.Xp = Content.XpPerLevelSquare * gearLevel * gearLevel;
                Session.ApplyRemote(Session.Inventory, set, 0, parkAt - 1, parkAt);
            }

            // Dev switch: -boss <id> fights that Commander at once in local play (screenshots of the Commanders).
            if (int.TryParse(Arg("-boss"), out int bossId) && !Server.Online && Content.Boss(bossId) != null) FightBoss(bossId);

            // Dev switch: -fxdemo <outcome> plays the Forge's anvil moment with a made-up result (screenshots).
            if (Enum.TryParse(Arg("-unlockshow") ?? "", out Feature unlockShow)) Hud.ShowUnlockForShot(unlockShow);
            if (int.TryParse(Arg("-story"), out int storyBeat)) Story.ShowForShot(storyBeat);
            string fxDemo = Arg("-fxdemo");
            if (fxDemo != null) StartCoroutine(Forge.Demo(fxDemo));

            // Dev switch: -shot <png> [-shotAfter seconds] saves the screen and quits (tools/screenshot-mac.sh). With
            // -castshow <slot> the shot waits for that skill's cast instead (-castdelay: seconds after its strike).
            string shot = Arg("-shot");
            _castShow = int.TryParse(Arg("-castshow"), out int castSlot) ? castSlot : -1;
            if (shot != null && _castShow < 0) StartCoroutine(ShotAndQuit(shot, float.TryParse(Arg("-shotAfter"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float after) ? after : 8f));
            if (shot != null && _castShow >= 0) _castFallback = StartCoroutine(ShotAndQuit(shot, 60f));   // gives up after a minute
            // Dev switch: -shotEvery <seconds> <folder> saves the screen that often as <folder>/<seconds since start>.png (a
            // new player's first minutes, played through without quitting).
            int every = Array.IndexOf(cmd, "-shotEvery");
            if (every >= 0 && every + 2 < cmd.Length && float.TryParse(cmd[every + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float period) && period > 0f)
                StartCoroutine(ShotEvery(period, cmd[every + 2]));
            // Dev switch: -clip <start> <seconds> <fps> <folder> saves numbered JPEG frames from <start> s for <seconds> s,
            // then quits: the README's gameplay clip (tools/art/make_clip.swift joins them into a GIF).
            int clip = Array.IndexOf(cmd, "-clip");
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (clip >= 0 && clip + 4 < cmd.Length && float.TryParse(cmd[clip + 1], System.Globalization.NumberStyles.Float, inv, out float clipStart)
                && float.TryParse(cmd[clip + 2], System.Globalization.NumberStyles.Float, inv, out float clipSeconds) && int.TryParse(cmd[clip + 3], out int clipFps))
                StartCoroutine(Clip(clipStart, clipSeconds, Mathf.Clamp(clipFps, 1, 30), cmd[clip + 4]));
        }

        private static IEnumerator Clip(float start, float seconds, int fps, string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(start);
            int frames = Mathf.CeilToInt(seconds * fps);
            float next = Time.realtimeSinceStartup;
            for (int i = 0; i < frames; i++)
            {
                yield return new WaitForEndOfFrame();
                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, $"{i:00000}.jpg"), shot.EncodeToJPG(90));
                Destroy(shot);
                next += 1f / fps;
                while (Time.realtimeSinceStartup < next) yield return null;
            }
            Application.Quit();
        }

        private string _messagesTo;

        /// <summary>-locmiss &lt;file&gt;: what found no translation this run, one piece a line (for tr.txt).</summary>
        private void OnApplicationQuit()
        {
            string path = Arg("-locmiss");
            if (path != null && Loc.Misses.Count > 0) System.IO.File.AppendAllLines(path, Loc.Misses);
        }
        private int _castShow = -1;
        private bool _castShot;
        private Coroutine _castFallback;

        /// <summary>Screenshots of a skill (-castshow): cast it once the hero fights, and shoot just after its strike.</summary>
        private void CastShow()
        {
            if (_castShow < 0 || _castShot || Time.realtimeSinceStartup < 7f || Replaying) return;
            LaneSim lane = Session.Lane;
            if (lane.Phase != LanePhase.Fighting || _castShow >= lane.Skills.Length || !lane.IsUnlocked(_castShow)) return;
            if (!Session.Cast(_castShow)) return;
            _castShot = true;
            string shot = Arg("-shot");
            float delay = float.TryParse(Arg("-castdelay"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float d) ? d : 0.12f;
            if (_castFallback != null) StopCoroutine(_castFallback);
            if (shot != null) StartCoroutine(ShotAndQuit(shot, CastClips.ImpactSeconds(Session.Class, _castShow) + delay));
        }

        /// <summary>Local-only demo bag: a spread of slots and rarities, some with etchings and levels.</summary>
        private void SampleLoot()
        {
            var rng = new XorShiftRandom(11);
            var etch = new EtchingService();
            for (int i = 0; i < 14; i++)
            {
                var slot = (EquipSlot)(i % 8);
                var item = new ItemState(10 + i * 3, (Rarity)(i % 5), slot) { UpgradeLevel = i % 4 == 0 ? i % 7 : 0 };
                for (int e = 0; e < (int)item.Rarity; e++) etch.TryAdd(item, EtchingPool.For(slot), NeedleKind.EtchingNeedle, rng);
                Session.Inventory.Loot.Add(item);
            }
        }

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static IEnumerator ShotEvery(float period, string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            float start = Time.realtimeSinceStartup;
            while (true)
            {
                yield return new WaitForSecondsRealtime(period);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, $"{Time.realtimeSinceStartup - start:0000}.png"));
            }
        }

        private static IEnumerator ShotAndQuit(string path, float after)
        {
            yield return new WaitForSecondsRealtime(after);
            yield return new WaitForEndOfFrame();
            // -shotScale n renders the shot n times larger (close looks at small things, e.g. a rider in the saddle).
            ScreenCapture.CaptureScreenshot(path, int.TryParse(Arg("-shotScale"), out int scale) ? Mathf.Clamp(scale, 1, 4) : 1);
            yield return new WaitForSecondsRealtime(1.5f);
            Application.Quit();
        }

        private bool _openGuildWar;
        private int _duelLane = -1;
        private int _keepIndex = -1;
        private bool _openPits;
        private int _pitFight = -1;
        private int _caravanTab = -1;
        private bool _openAchievements, _openRaid, _raidFight;
        private string _openBoard, _inspectShot;
        private bool _openInvite, _goFishing, _openKin, _openRugs, _openTown;
        private bool _openWardrobe;
        private bool _openDepot;
        private bool _openTrail;
        private bool _dailyShown;
        private bool _openTrade;
        private bool _enterDungeon;
        private int _dungeonToEnter = 1;
        private readonly float[] _glowBySlot = new float[8];
        private Bell _localBellApplied = Bell.None;
        private bool _tutorialPending;
        private bool _askNotifications;
        private bool _oathAsked;
        private bool _accountAsked;
        private string _firstRun;
        private bool _openFriends;
        private bool _openOathChange;
        private bool _firstRunGuest;
        private bool _firstRunSworn;
        private bool _accountShown;
        private int _accountGeneration;

        /// <summary>
        /// The way in: title, sign-in screen, oath, character screen. Run after the coroutines of the frame (LateUpdate), so a
        /// screen closed by a server answer is followed by the next one in the same frame, with no flash of the lane between.
        /// </summary>
        private void LateUpdate()
        {
            // Another account on this device (sign in, sign out, deletion): the sign-in screen and the oath are asked afresh.
            if (_accountGeneration != Server.AccountGeneration)
            {
                _accountGeneration = Server.AccountGeneration;
                _oathAsked = false;
                _accountShown = false;
            }
            bool shot = ShotRun && _firstRun == null;
            // The way in (owner, 25 Sep 2026): a new install (or a phone signed out) starts at the sign-in screen, the
            // oath follows for an account not yet sworn, then the character screen. -account forces the sign-in screen.
            if (!Title.Waiting && Server.Connected && !Account.Showing && !_accountShown
                && (_accountAsked || AccountPanel.FirstScreen(Server) && !shot))
            {
                _accountShown = true;
                Account.Open();
                Account.ShotMode(Arg("-account"));   // "-account signin|create|reset" for screenshots
            }
            if ((_firstRun == "guest" || _firstRun == "oath") && Account.Showing && !_firstRunGuest)
            {
                _firstRunGuest = true;
                Account.PlayAsGuestForShot();
            }
            // Not yet sworn: the oath comes next, once the title and account screens are gone (at the character screen;
            // in the game only for accounts sworn before that).
            if (!_oathAsked && !Title.Waiting && !Account.Showing && Server.Connected && Server.Banner == Rules.Banner.None && !shot)
            {
                _oathAsked = true;
                Oath.Open();
            }
            if (_firstRun == "oath" && Oath.Showing && !_firstRunSworn)
            {
                _firstRunSworn = true;
                Oath.SwearForShot(Rules.Banner.Sky);
            }
            // The character screen (25 Sep 2026): after the title, the account screen and the oath, until a hero is chosen.
            // It covers the game from the moment the title starts to fade, "Connecting..." until the server answers, so
            // the lane never shows before a hero is chosen (owner, 25 Sep 2026).
            Characters.SetVisible(Server.WaitingForHero && !Title.Waiting && !Account.Showing && !Oath.Showing);
            // The first session's guide starts once the title screen (and the oath) is gone; the notification
            // permission is asked once it ends (done or skipped), not over its first step (27 Sep 2026).
            // The login calendar opens itself once a session when a gift waits (not over the first session's guide, nor
            // over screenshots unless -daily asks for it).
            if (!_dailyShown && Server.Online && !Server.WaitingForHero && Server.Daily != null && Server.Daily.claimable
                && !Title.Showing && !Account.Showing && !Oath.Showing && !Characters.IsOpen && !_tutorialPending
                && (Array.IndexOf(Environment.GetCommandLineArgs(), "-daily") >= 0
                    || (Tutorial.Finished && !ShotRun)))
            {
                _dailyShown = true;
                Daily.Open();
            }
            if (_tutorialPending && !Title.Showing && !Account.Showing && !Oath.Showing && !Characters.IsOpen)
            {
                _askNotifications = true;
                _tutorialPending = false;
                // Dev switch: -tutorialStep <n> opens the guide at a step (screenshots).
                Tutorial.Begin(int.TryParse(Arg("-tutorialStep"), out int step) ? step : 0);
            }
            if (_askNotifications && !Tutorial.Running)
            {
                _askNotifications = false;
                Notifications.AskOnce();
            }

        }

        private float _musicCheck;
        private bool _bossOnLane;

        /// <summary>
        /// Music (owner, 27 Sep 2026: "Music per map"): each map's theme under its backdrop (Content/Music, downloaded;
        /// the old hunt track until it is there), the boss loop and a sting while a boss stands on the lane. The title
        /// screen keeps its own.
        /// </summary>
        private void UpdateMusic()
        {
            if (GameAudio.Instance == null || Title.Showing || Lane.BackdropKeyNow == null) return;
            _musicCheck -= Time.unscaledDeltaTime;
            if (_musicCheck > 0f) return;
            _musicCheck = 0.5f;
            // The map's own quiet loop under its theme (owner, 28 Sep 2026: "Map ambience"); the town and the river have theirs.
            string ambience = Town.IsOpen || Server.AtRiver ? null : MapAmbience(Lane.BackdropKeyNow);
            if (ambience != _ambienceNow)
            {
                if (_ambienceNow != null) GameAudio.Instance.Ambience(_ambienceNow, 0f, 2f);
                _ambienceNow = ambience;
            }
            if (ambience != null) GameAudio.Instance.Ambience(ambience, AmbienceVolume, 2f);
            // The town square has its own theme (the map's until it has downloaded); the lane's bosses go unheard there.
            if (Town.IsOpen) { GameAudio.Instance.Music("MusicTown", MapMusic(Lane.BackdropKeyNow)); return; }
            LaneSim lane = ActiveLane;
            bool boss = lane.IsBossEncounter && lane.Phase == LanePhase.Fighting;
            if (boss && !_bossOnLane) GameAudio.Instance.Play("StingBoss", 0.9f, 0f, 0f);
            _bossOnLane = boss;
            GameAudio.Instance.Music(boss ? "MusicBoss" : MapMusic(Lane.BackdropKeyNow), "MusicHunt");
        }

        private string _ambienceNow;
        private const float AmbienceVolume = 0.3f;

        /// <summary>The quiet loop under a lane backdrop's theme (Content/Ambience), maps of a kind sharing one.</summary>
        private static string MapAmbience(string backdrop) => "Amb" + (backdrop switch
        {
            "FrostPasture" => "Mountain", "SaltFlats" => "Salt", "CinderMarches" => "Cinder", "Whisperwood" => "Whisper",
            "Bloodbirch" => "Birch", "DrownedSteppe" => "Swamp", "ColossusGraves" or "ThousandMarkers" => "Graves", "SunkenBazaar" => "Bazaar",
            "HollowThrone" or "HollowSpire" or "SilkWarren" or "CarversArchive" => "Deep",
            _ => "Steppe",
        });

        /// <summary>The theme for a lane backdrop: a map's own, dungeons and fields borrowing a fitting one.</summary>
        private static string MapMusic(string backdrop) => "MusicMap" + (backdrop switch
        {
            "CommanderGround" => 2, "SaltFlats" => 3, "FrostPasture" => 4, "CinderMarches" => 5, "Whisperwood" => 6,
            "Bloodbirch" => 7, "DrownedSteppe" => 8, "ColossusGraves" => 9, "SunkenBazaar" => 10, "ThousandMarkers" => 11,
            "HollowThrone" => 12, "HollowSpire" => 6, "SilkWarren" => 8, "CarversArchive" => 9,
            _ => 1,
        }).ToString("00");

        /// <summary>The short fanfare of a fight won (a push, a Commander, a raid boss, a dungeon, a duel).</summary>
        private static void Victory() => GameAudio.Instance?.Play("StingVictory", 0.9f, 0f, 0f);

        private void Update()
        {
            UpdateMusic();
            CastShow();
            // Screenshots: the conversation opens once the hero is on line (its id comes from the server).
            if (_messagesTo != null && Server.Online && !Server.WaitingForHero)
            {
                Messages.OpenWith(null, _messagesTo);
                _messagesTo = null;
            }
            if (Server.Online && Server.InGuild && (_openGuildWar || _duelLane >= 0))
            {
                if (_openGuildWar) GuildWar.Open();
                if (_duelLane >= 0) FightDuel(_duelLane);
                _openGuildWar = false;
                _duelLane = -1;
            }
            if (Server.Online && _enterDungeon && Server.DungeonRunsLeft > 0)
            {
                _enterDungeon = false;
                EnterDungeon(_dungeonToEnter);
            }
            // Dev switch: -achievements opens ACHIEVEMENTS once the hero is online (screenshots).
            if (Server.Online && _openAchievements)
            {
                _openAchievements = false;
                Achievements.Open();
            }
            // Screenshots: -leaderboard <level|stage|pits|guilds>[:week] opens LEADERBOARDS; -inspect <hero id|me> a hero's gear.
            if (Server.Online && !string.IsNullOrEmpty(_openBoard))
            {
                string[] board = _openBoard.Split(':');
                _openBoard = null;
                Leaderboards.OpenForShot(board[0], board.Length > 1 && board[1] == "week");
            }
            // Screenshots: -river goes to Old Nergui's river once online (-rivershot creel|rod|bite opens a part of it).
            if (Server.Online && _goFishing && !Server.WaitingForHero)
            {
                _goFishing = false;
                if (!Server.AtRiver) River.Go();
            }
            // Screenshots: -town opens the town square once online (-townwalk <0-3> walks to a townsman).
            if (Server.Online && _openTown && !Server.WaitingForHero)
            {
                _openTown = false;
                Town.Open();
            }
            if (Server.Online && _openRugs && !Server.WaitingForHero)
            {
                _openRugs = false;
                Rugs.Open();
            }
            if (Server.Online && _openKin && !Server.WaitingForHero)
            {
                _openKin = false;
                Kin.OpenForShot();
            }
            if (Server.Online && _openInvite)
            {
                _openInvite = false;
                Invites.Open();
            }
            if (Server.Online && !string.IsNullOrEmpty(_inspectShot) && !string.IsNullOrEmpty(Server.AccountId))
            {
                Inspect.Open(_inspectShot == "me" ? Server.AccountId : _inspectShot);
                _inspectShot = null;
            }
            // Dev switch: -raid opens GUILD RAID once the hero is online (screenshots).
            if (Server.Online && _openRaid)
            {
                _openRaid = false;
                GuildRaid.Open();
            }
            // Dev switch: -raidfight fights the guild raid once online (screenshots of the replay).
            if (Server.Online && _raidFight && !PushBusy)
            {
                _raidFight = false;
                FightRaid();
            }
            if (Server.Online && Server.Wardrobe != null && (_caravanTab >= 0 || _openWardrobe || _openDepot))
            {
                if (_caravanTab >= 0) Caravan.Open(_caravanTab); else if (_openDepot) Depot.Open(); else Wardrobe.Open();
                _caravanTab = -1;
                _openWardrobe = false;
                _openDepot = false;
            }
            // Dev switch: -trail opens the Campaign Trail (screenshots).
            if (Server.Online && _openTrail && Server.Trail != null)
            {
                _openTrail = false;
                Trail.Open();
            }
            if (Server.Online && _openFriends && Server.Trail != null)
            {
                _openFriends = false;
                Friends.Open();
            }
            if (Server.Online && _openOathChange && Server.Banner != Rules.Banner.None)
            {
                _openOathChange = false;
                Oath.OpenChange();
            }
            // Dev switch: -trade opens the trade window (screenshots).
            if (Server.Online && _openTrade && Server.Trail != null)
            {
                _openTrade = false;
                Trade.Open();
            }
            if (Server.Online && _openPits)
            {
                _openPits = false;
                Pits.Open();
            }
            if (Server.Online && _pitFight >= 0 && Server.Pits?.challengers != null && _pitFight < Server.Pits.challengers.Length)
            {
                FightPit(Server.Pits.challengers[_pitFight].id);
                _pitFight = -1;
            }
            if (Server.Online && _keepIndex >= 0)
            {
                War.Open();
                War.ShowKeep(_keepIndex);
                _keepIndex = -1;
            }

            // Local mode rings the bells from the PC clock; online the server's state applies them.
            if (!Server.Online && !Replaying)
            {
                Bell now = EveningBells.Active(DateTime.Now);
                if (now != _localBellApplied) { _localBellApplied = now; Session.ApplyBell(now); }
            }

            // No hunting at Old Nergui's river: the lane stands still while the hero fishes (RiverPanel).
            if (Server.AtRiver) return;
            LaneSim lane = ActiveLane;
            if (Lane.Sim != lane) Lane.Bind(lane);
            // The other classes' looks follow the armour's level band (the Vanguard's armour and glaive have their own).
            ItemState armor = Session.Equipped(EquipSlot.Armor);
            // A worn skin shows its own band in its tint; mounts and companions follow the hero (the wardrobe).
            WardrobeDef skin = null, mount = null, companion = null;
            foreach (WardrobeDef piece in Session.Worn)
                if (piece.Kind == WardrobeKind.Skin) skin = piece; else if (piece.Kind == WardrobeKind.Mount) mount = piece; else companion = piece;
            // A skin shows its own costume model where one has been made, otherwise an armour band in the skin's tint.
            // A second look (the class's other figure) wears its own cut of a costume; one not drawn yet tints the band armour.
            bool second = Session.SecondLook;
            string skinModel = skin != null ? LaneView.SkinModel(Session.Class, skin.Look, second) : null;
            bool tinted = skin != null && skinModel == null && LaneView.SkinLooks.ContainsKey(skin.Look);
            (int Band, Color Tint) skinLook = tinted ? LaneView.SkinLooks[skin.Look] : (0, Color.white);
            int band = tinted ? skinLook.Band : armor != null ? ItemLooks.Tier(armor.ItemLevel) : 0;
            Lane.SetHeroClass(Session.Class, band, Session.Class == HeroClass.Vanguard ? null : skinModel, second);
            Ui.IconClass = Session.Class;
            ItemLooks.ShownClass = Session.Class;   // pieces carry the playing class's names (knives for a Kestrel)
            Lane.SetLooks(skinModel != null && Session.Class == HeroClass.Vanguard ? skinModel : tinted ? "Armor_T" + band : armor?.LookId, Session.Weapon.LookId);
            Lane.SetWardrobe(mount?.Look, companion?.Look, skinLook.Tint);
            ItemState kinRobe = Session.KinPiece(EquipSlot.Armor);
            Lane.SetKin(Session.KinJoined, kinRobe != null ? ItemLooks.Tier(kinRobe.ItemLevel) : 0);
            Lane.SetGear(UpgradeGlow.PerSlot(Session, _glowBySlot));

            Lane.Pace = SpeedMultiplier;
            // Only the online farm lane's packs may be marked elite (its loops are reported and replayed); never a replay.
            Lane.MayMarkElite = Server.Online && !Replaying && Session.LaneSeeded && lane == Session.Lane;
            _accumulator = Mathf.Min(_accumulator + Time.deltaTime * LaneSim.TicksPerSecond * SpeedMultiplier, 200f);
            while (_accumulator >= 1f)
            {
                _accumulator -= 1f;
                lane.Tick();
                foreach (LaneEvent e in lane.DrainEvents())
                {
                    Lane.Handle(e);
                    Hud.Handle(e);
                }
                if (_replay != null && (_replay.Clears > 0 || _replay.BossesKilled > 0 || _replay.Deaths > 0)) break;
                // Online the farm lane runs one seeded loop per encounter cycle; a finished one is queued for the
                // heartbeat and the next loop's lane takes over (its enemies are all down at that moment).
                if (_replay == null && Session.CloseLoopIfDone())
                {
                    lane = Session.Lane;
                    Lane.Bind(lane);
                }
            }
            // A full bag leaves new drops behind (owner, 26 Sep 2026).
            foreach (ItemState left in Session.LeaveBehindOverflow(piece => Server.Online && Server.IdOf(piece) != null))
                Hud.Log($"Bag full: {left.DisplayName} left behind");
        }

        /// <summary>Moves the farm lane to an unlocked stage or zone, on the server when connected.</summary>
        public void Park(int parkId)
        {
            if (Replaying || PushBusy) return;
            if (Server.Online) StartCoroutine(Server.Park(parkId, error => { if (error != null) Hud.Log(error); }));
            else
            {
                try { Session.Park(parkId); }
                catch (InvalidOperationException ex) { Hud.Log(ex.Message); }
            }
        }

        /// <summary>The shared pool after a server fight, for the log: what the server has left, or who slew it.</summary>
        private string PoolNote(int bossId)
        {
            if (!Server.Online) return "";
            foreach (Net.ServerLink.BossStatusDto b in Server.Bosses)
                if (b.bossId == bossId && b.hpMax > 0)
                    return b.slain ? "  ·  slain by " + b.slainBy : $"  ·  {b.hpLeft:N0} of {b.hpMax:N0} HP left for the server";
            return "";
        }

        /// <summary>Fights a Commander: the server (or the local session) scores it, then the lane replays the seed.</summary>
        public void FightBoss(int bossId)
        {
            if (Replaying || PushBusy) return;
            StartCoroutine(BossSequence(bossId));
        }

        private IEnumerator BossSequence(int bossId)
        {
            PushBusy = true;
            BossDef boss = Content.Boss(bossId);
            HeroStats hero = Session.Hero;
            ulong seed;
            int rank;
            string chest;
            int potions = Session.Inventory.Potions;
            Bell bell = LocalBell;

            if (Server.Online)
            {
                Net.ServerLink.BossFightResultDto result = null;
                string failure = null;
                yield return Server.FightBoss(bossId, (r, e) => { result = r; failure = e; });
                if (result == null)
                {
                    Hud.Log(failure ?? "No answer from the server.");
                    PushBusy = false;
                    yield break;
                }
                seed = result.seed;
                rank = result.rank;
                chest = result.chest;
                potions = result.potionsAtStart;
                bell = (Bell)Enum.Parse(typeof(Bell), result.bell);
            }
            else
            {
                Session.FightBoss(boss, out seed, out rank, out chest);
            }

            _replay = BossRun.Create(boss, hero, new Inventory { Potions = potions }, seed, bell);
            ReplayBanner = boss.Name.ToUpperInvariant();
            int guard = BossRun.MaxTicks;
            while (_replay.BossesKilled == 0 && _replay.Deaths == 0 && guard-- > 0) yield return null;

            ReplayBanner = (_replay.BossesKilled > 0 ? "SLAIN  ·  " : "FLED  ·  ") + "rank " + rank + " of 20";
            if (_replay.BossesKilled > 0) Victory();
            Hud.Log(chest + PoolNote(bossId));
            yield return new WaitForSecondsRealtime(2.5f);

            _replay = null;
            ReplayBanner = "";
            PushBusy = false;
        }

        /// <summary>A guild raid fight (Rules.GuildRaids): the server rolls it, then the lane replays the seed.</summary>
        public void FightRaid()
        {
            if (Replaying || PushBusy || !Server.Online) return;
            StartCoroutine(RaidSequence());
        }

        private IEnumerator RaidSequence()
        {
            PushBusy = true;
            Net.ServerLink.GuildRaidFightDto result = null;
            string failure = null;
            yield return Server.FightRaid((r, e) => { result = r; failure = e; });
            if (result == null)
            {
                Hud.Log(failure ?? "No answer from the server.");
                PushBusy = false;
                yield break;
            }
            StageConfig stage = GuildRaids.Stage(result.raid.map);
            _replay = BossRun.Create(stage, Session.Hero, new Inventory { Potions = result.potionsAtStart }, result.seed);
            ReplayBanner = "GUILD RAID  ·  " + (stage.BossName ?? "").ToUpperInvariant();
            int guard = BossRun.MaxTicks;
            while (_replay.BossesKilled == 0 && _replay.Deaths == 0 && guard-- > 0) yield return null;

            ReplayBanner = result.raid.slain ? "THE RAID BOSS HAS FALLEN" : $"{result.damage.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} DAMAGE";
            if (result.raid.slain) Victory();
            Hud.Log(result.raid.message);
            yield return new WaitForSecondsRealtime(2.5f);

            _replay = null;
            ReplayBanner = "";
            PushBusy = false;
        }

        /// <summary>A siege fight at a fortress: the server scores it, then the lane replays the seed.</summary>
        public void FightSiege(int fortressId)
        {
            if (Replaying || PushBusy || !Server.Online) return;
            StartCoroutine(SiegeSequence(fortressId, keep: false));
        }

        /// <summary>A fight at a fortress keep during the Sunday siege (storming it, or holding it); replayed like a siege.</summary>
        public void FightKeep(int fortressId)
        {
            if (Replaying || PushBusy || !Server.Online) return;
            StartCoroutine(SiegeSequence(fortressId, keep: true));
        }

        /// <summary>Enters a dungeon: the server fights the floors, the lane replays them; the Chained Smith asks halfway.</summary>
        public void EnterDungeon(int dungeonId)
        {
            if (Replaying || PushBusy || !Server.Online) return;
            StartCoroutine(DungeonSequence(dungeonId));
        }

        /// <summary>Goes back to a run left waiting at the Chained Smith.</summary>
        public void ContinueDungeon()
        {
            if (Replaying || PushBusy || !Server.Online || Server.DungeonRunAtSmith == 0) return;
            StartCoroutine(SmithSequence(Server.DungeonRunAtSmith, Server.DungeonPausedId));
        }

        private IEnumerator DungeonSequence(int dungeonId)
        {
            PushBusy = true;
            Net.ServerLink.DungeonResultDto result = null;
            string failure = null;
            yield return Server.DungeonEnter(dungeonId, (r, e) => { result = r; failure = e; });
            if (result == null)
            {
                Hud.Log(failure ?? "No answer from the server.");
                PushBusy = false;
                yield break;
            }
            yield return ReplayFloors(result);
            if (result.atSmith) yield return AskSmith(result.runId, result.dungeonId);
            else yield return EndRun(result);
            PushBusy = false;
        }

        private IEnumerator SmithSequence(long runId, int dungeonId)
        {
            PushBusy = true;
            yield return AskSmith(runId, dungeonId);
            PushBusy = false;
        }

        /// <summary>Each fought floor, in order, from the server's seeds; the hero starts every floor whole.</summary>
        private IEnumerator ReplayFloors(Net.ServerLink.DungeonResultDto run)
        {
            DungeonDef dungeon = Dungeons.Find(run.dungeonId);
            if (dungeon == null || run.floors == null) yield break;
            HeroStats hero = Session.Hero;
            foreach (Net.ServerLink.DungeonFloorDto floor in run.floors)
            {
                _replay = StageRun.Create(Dungeons.Floor(dungeon, floor.floor, run.level), hero, new Inventory { Potions = floor.potionsAtStart }, floor.seed);
                ReplayBanner = dungeon.Name.ToUpperInvariant() + "  ·  " + Dungeons.FloorName(dungeon, floor.floor).ToUpperInvariant();
                int guard = StageRun.MaxTicks;
                while (_replay.Clears == 0 && _replay.Deaths == 0 && guard-- > 0) yield return null;
                ReplayBanner = floor.cleared ? $"FLOOR {floor.floor} CLEARED" : $"FELL ON FLOOR {floor.floor}";
                yield return new WaitForSecondsRealtime(1.2f);
            }
            _replay = null;
            ReplayBanner = "";
        }

        /// <summary>The Chained Smith: the player's choice goes to the server, then the rest of the run is replayed.</summary>
        /// <summary>The run's pause: the Chained Smith, or the Carvers' rune lock (Rules.Dungeons.DungeonPause).</summary>
        private IEnumerator AskSmith(long runId, int dungeonId)
        {
            if (Dungeons.Find(dungeonId)?.Pause == DungeonPause.RuneLock)
            {
                yield return AskRuneLock(runId);
                yield break;
            }
            string answer = null;
            ReplayBanner = "THE CHAINED SMITH";
            Smith.Open(a => answer = a);
            while (answer == null) yield return null;
            ReplayBanner = "";

            Net.ServerLink.DungeonResultDto result = null;
            string failure = null;
            yield return Server.DungeonSmith(runId, answer, (r, e) => { result = r; failure = e; });
            if (result == null)
            {
                // The run stays at the smith; ZONES offers CONTINUE.
                Hud.Log(failure ?? "No answer from the server.");
                yield break;
            }
            if (result.smith != null && !string.IsNullOrEmpty(result.smithItem))
            {
                bool success = result.smith.outcome == ForgeOutcome.Success.ToString();
                bool broke = result.smith.outcome == ForgeOutcome.Oathbreak.ToString();
                ReplayBanner = success ? $"THE SMITH STRUCK TRUE  ·  +{result.smith.levelAfter}" : broke ? "THE PIECE BROKE" : $"THE SMITH FAILED  ·  +{result.smith.levelAfter}";
                GameAudio.Instance?.Play(success ? "ForgeSuccess" : broke ? "ForgeShatter" : "ForgeLost", 1f, 0.5f, 0f);
                Hud.Log(result.text);
                yield return new WaitForSecondsRealtime(2.2f);
            }
            yield return ReplayFloors(result);
            yield return EndRun(result);
        }

        /// <summary>The rune lock: the rune chosen (or none) goes to the server, then the rest of the run is replayed.</summary>
        private IEnumerator AskRuneLock(long runId)
        {
            string rune = null;
            ReplayBanner = "THE RUNE LOCK";
            RuneLock.Open(runId, r => rune = r);
            while (rune == null) yield return null;
            ReplayBanner = "";

            Net.ServerLink.DungeonResultDto result = null;
            string failure = null;
            yield return Server.DungeonSmith(runId, "", (r, e) => { result = r; failure = e; }, rune);
            if (result == null)
            {
                // The run stays at the lock; ZONES offers CONTINUE.
                Hud.Log(failure ?? "No answer from the server.");
                yield break;
            }
            bool open = result.text != null && result.text.Contains("vault door opens");
            ReplayBanner = open ? "THE VAULT OPENS" : rune.Length == 0 ? "THE VAULT STAYS SHUT" : "THE RUNE DOES NOT TURN";
            GameAudio.Instance?.Play(open ? "ForgeSuccess" : "ForgeLost", 1f, 0.5f, 0f);
            yield return new WaitForSecondsRealtime(2f);
            yield return ReplayFloors(result);
            yield return EndRun(result);
        }

        private IEnumerator EndRun(Net.ServerLink.DungeonResultDto run)
        {
            DungeonDef dungeon = Dungeons.Find(run.dungeonId);
            string name = dungeon != null ? dungeon.Name.ToUpperInvariant() : "THE DUNGEON";
            ReplayBanner = run.cleared ? name + " CLEARED" : run.fellOn > 0 ? $"FELL ON FLOOR {run.fellOn}" : "";
            if (run.cleared) Victory();
            if (run.cleared) GameAudio.Instance?.Play("LaneKorstoneBreak", 1f, 0.5f, 0f);
            Hud.Log(run.text);
            yield return new WaitForSecondsRealtime(2.5f);
            ReplayBanner = "";
        }

        /// <summary>A guild war duel on a lane: the server decides it, the lane replays it, and the war screen comes back.</summary>
        public void FightDuel(int lane)
        {
            if (Replaying || PushBusy || !Server.Online) return;
            StartCoroutine(DuelSequence(lane));
        }

        private IEnumerator DuelSequence(int lane)
        {
            PushBusy = true;
            HeroStats hero = Session.Hero;
            Net.ServerLink.DuelResultDto duel = null;
            string failure = null;
            yield return Server.GuildWarFight(lane, (r, e) => { duel = r; failure = e; });
            if (duel == null)
            {
                GuildWar.Say(failure ?? "No answer from the server.");
                PushBusy = false;
                yield break;
            }
            GuildWar.Close();
            // The champion the server shaped for this duel, dressed as the defender; no draughts, no bell.
            var champion = new BossDef(Duels.ChampionId, Content.GorakWarCamp, duel.champion, 1, duel.championHp, duel.championAttack, BossMechanic.None, 0, "");
            HeroClass rivalClass = Enum.TryParse(duel.defenderClass, out HeroClass rival) ? rival : HeroClass.Vanguard;
            Lane.SetRival(rivalClass, duel.defenderBand, duel.champion,
                Enum.TryParse(duel.defenderFigure, out Figure rivalFigure) && ItemLooks.SecondLook(rivalClass, rivalFigure));
            _replay = BossRun.Create(champion, hero, new Inventory(), duel.seed);
            ReplayBanner = "WAR  ·  " + duel.champion;
            int guard = BossRun.MaxTicks;
            while (_replay.BossesKilled == 0 && _replay.Deaths == 0 && guard-- > 0) yield return null;

            string laneName = GuildWars.LaneNames[Mathf.Clamp(duel.lane, 0, GuildWars.Lanes - 1)].ToUpperInvariant();
            ReplayBanner = (duel.won ? "FELLED  ·  " : "HELD  ·  ") + laneName;
            Hud.Log(duel.text);
            yield return new WaitForSecondsRealtime(2.5f);

            _replay = null;
            ReplayBanner = "";
            PushBusy = false;
            GuildWar.Open();
        }

        /// <summary>A Pit fight: the server decides it against a challenger's snapshot, the lane replays it, the Pits come back.</summary>
        public void FightPit(string opponentId)
        {
            if (Replaying || PushBusy || !Server.Online) return;
            StartCoroutine(PitSequence(opponentId));
        }

        private IEnumerator PitSequence(string opponentId)
        {
            PushBusy = true;
            HeroStats hero = Session.Hero;
            Net.ServerLink.PitFightDto fight = null;
            string failure = null;
            yield return Server.PitFight(opponentId, (r, e) => { fight = r; failure = e; });
            if (fight?.duel == null)
            {
                Pits.Say(failure ?? "No answer from the server.");
                PushBusy = false;
                yield break;
            }
            Pits.Close();
            Net.ServerLink.DuelResultDto duel = fight.duel;
            var champion = new BossDef(Duels.ChampionId, Content.GorakWarCamp, duel.champion, 1, duel.championHp, duel.championAttack, BossMechanic.None, 0, "");
            HeroClass rivalClass = Enum.TryParse(duel.defenderClass, out HeroClass rival) ? rival : HeroClass.Vanguard;
            Lane.SetRival(rivalClass, duel.defenderBand, duel.champion,
                Enum.TryParse(duel.defenderFigure, out Figure rivalFigure) && ItemLooks.SecondLook(rivalClass, rivalFigure));
            _replay = BossRun.Create(champion, hero, new Inventory(), duel.seed);
            ReplayBanner = "THE PITS  ·  " + duel.champion;
            int guard = BossRun.MaxTicks;
            while (_replay.BossesKilled == 0 && _replay.Deaths == 0 && guard-- > 0) yield return null;

            int moved = fight.ratingAfter - fight.ratingBefore;
            ReplayBanner = (duel.won ? "VICTORY  ·  " : "DEFEAT  ·  ") + (moved >= 0 ? "+" : "") + moved;
            if (duel.won) Victory();
            GameAudio.Instance?.Play(duel.won ? "LaneLevelUp" : "LaneHeroHurt", 1f, 0.5f, 0f);
            Hud.Log(duel.text);
            yield return new WaitForSecondsRealtime(2.5f);

            _replay = null;
            ReplayBanner = "";
            PushBusy = false;
            Pits.Open();
        }

        private IEnumerator SiegeSequence(int fortressId, bool keep)
        {
            PushBusy = true;
            HeroStats hero = Session.Hero;
            Net.ServerLink.SiegeResultDto result = null;
            string failure = null;
            if (keep) yield return Server.KeepFight(fortressId, (r, e) => { result = r; failure = e; });
            else yield return Server.Siege(fortressId, (r, e) => { result = r; failure = e; });
            var champion = result == null ? null : Fortresses.FromChampionId(result.bossId);
            if (result == null || champion == null)
            {
                Hud.Log(failure ?? "No answer from the server.");
                War.Say(failure ?? "");
                PushBusy = false;
                yield break;
            }
            BossDef def = Fortresses.Champion(champion.Value.fortress, champion.Value.phase);
            var bell = (Bell)Enum.Parse(typeof(Bell), result.bell);
            _replay = BossRun.Create(def, hero, new Inventory { Potions = result.potionsAtStart }, result.seed, bell);
            ReplayBanner = keep ? (result.defending ? "HOLD THE KEEP  ·  " : "STORM THE KEEP  ·  ") + champion.Value.fortress.Name.ToUpperInvariant()
                : (result.defending ? "DEFEND  ·  " : "SIEGE  ·  ") + def.Name.ToUpperInvariant();
            int guard = BossRun.MaxTicks;
            while (_replay.BossesKilled == 0 && _replay.Deaths == 0 && guard-- > 0) yield return null;

            ReplayBanner = keep ? (result.defending ? "KEEP MENDED" : $"{result.damage:N0} DAMAGE")
                : result.captured ? "FORTRESS TAKEN" : result.phaseBroken ? result.phase.ToUpperInvariant() + " BROKEN" : result.defending ? "WALL MENDED" : $"{result.damage:N0} DAMAGE";
            Hud.Log(result.text);
            if (result.captured || result.phaseBroken) GameAudio.Instance?.Play("LaneKorstoneBreak", 1f, 0.5f, 0f);
            yield return new WaitForSecondsRealtime(2.5f);

            _replay = null;
            ReplayBanner = "";
            PushBusy = false;
        }

        /// <summary>Asks for the next stage's verdict, then replays the scored fight seed for seed.</summary>
        public void Push()
        {
            if (Replaying || PushBusy) return;
            StartCoroutine(PushSequence());
        }

        private IEnumerator PushSequence()
        {
            PushBusy = true;
            int stage = Session.PushTarget;
            HeroStats hero = Session.Hero;
            ulong seed;
            bool cleared;
            int potions = Session.Inventory.Potions;
            Bell bell = LocalBell;

            if (Server.Online)
            {
                Net.ServerLink.PushResultDto result = null;
                string failure = null;
                yield return Server.Push((r, e) => { result = r; failure = e; });
                if (result == null)
                {
                    Hud.Log(failure ?? "No answer from the server.");
                    PushBusy = false;
                    yield break;
                }
                stage = result.stage;
                seed = result.seed;
                cleared = result.cleared;
                potions = result.potionsAtStart;
                bell = (Bell)Enum.Parse(typeof(Bell), result.bell);
            }
            else
            {
                cleared = Session.Push(out seed).Cleared;
            }

            // The replay lane loots into a scratch inventory: the real one already holds the server's answer.
            _replay = StageRun.Create(EveningBells.Apply(Content.Stage(stage), bell), hero, new Inventory { Potions = potions }, seed);
            ReplayBanner = "PUSH  ·  " + Content.StageName(stage);
            int guard = StageRun.MaxTicks;
            while (_replay.Clears == 0 && _replay.Deaths == 0 && guard-- > 0) yield return null;

            ReplayBanner = (cleared ? "CLEARED  ·  " : "FAILED  ·  ") + Content.StageName(stage);
            if (cleared) Victory();
            yield return new WaitForSecondsRealtime(2f);

            _replay = null;
            ReplayBanner = "";
            PushBusy = false;
        }

        private static void BuildCameras()
        {
            // Clears the whole screen so the area under the HUD is never left undefined.
            var backdrop = new GameObject("BackdropCamera").AddComponent<Camera>();
            backdrop.depth = -10;
            backdrop.clearFlags = CameraClearFlags.SolidColor;
            backdrop.backgroundColor = Palette.Background;
            backdrop.cullingMask = 0;

            var cam = new GameObject("LaneCamera").AddComponent<Camera>();
            cam.gameObject.AddComponent<AudioListener>();
            cam.tag = "MainCamera";
            cam.rect = new Rect(0f, LaneViewportBottom, 1f, 1f - LaneViewportBottom);
            cam.fieldOfView = LaneView.CameraFov;
            // Nothing past the haze is drawn (the backdrop stands at 62 m).
            cam.farClipPlane = LaneView.FogEnd + 25f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
            // The field (LaneView.CameraFrom, 28 Sep 2026): behind the hero, up and to his left, looking up the road.
            cam.transform.position = LaneView.CameraFrom;
            cam.transform.LookAt(LaneView.CameraTo);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(RenderSettings.ambientLight);
            RenderSettings.ambientProbe = ambient;

            // Bloom makes the ember glow read as light rather than orange paint (URP; profile in Resources/PostFX).
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var profile = Resources.Load<VolumeProfile>("PostFX");
            if (profile != null)
            {
                var volume = new GameObject("PostFX").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }
        }
    }
}
