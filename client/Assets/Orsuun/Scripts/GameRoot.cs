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
        public TradePanel Trade { get; private set; }
        public WardrobePanel Wardrobe { get; private set; }
        public CharacterPanel Characters { get; private set; }
        public DepotPanel Depot { get; private set; }
        public SmithPanel Smith { get; private set; }
        public RuneLockPanel RuneLock { get; private set; }
        public ChatPanel Chat { get; private set; }
        public FriendsPanel Friends { get; private set; }
        public MessagesPanel Messages { get; private set; }
        public MarketPanel Market { get; private set; }
        public AccountPanel Account { get; private set; }
        public GameNotifications Notifications { get; private set; }
        public Tutorial Tutorial { get; private set; }
        public int SpeedMultiplier { get; set; } = 1;

        /// <summary>The bell in force: the server's when online, else the local clock's (the local session uses it too).</summary>
        public Bell LocalBell => Server.Online ? Server.ActiveBell : EveningBells.Active(DateTime.Now);

        /// <summary>The lane on screen: the parked farm lane, or a push replay while one runs.</summary>
        public LaneSim ActiveLane => _replay ?? Session.Lane;
        public bool Replaying => _replay != null;
        public string ReplayBanner { get; private set; } = "";
        public bool PushBusy { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<GameRoot>() == null)
                new GameObject("GameRoot").AddComponent<GameRoot>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;

            // Grey-box only: the shipped game rolls on the server. A time seed is fine for a local playtest.
            Session = new PlayerSession(new XorShiftRandom((ulong)DateTime.UtcNow.Ticks));
            Session.Lane.AutoCast[1] = true;

            GameAudio.Create();
            Notifications = new GameObject("GameNotifications").AddComponent<GameNotifications>();
            BuildCameras();
            Lane = new GameObject("LaneView").AddComponent<LaneView>();
            Lane.Init(Session.Lane);

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
            Chat = new GameObject("ChatPanel").AddComponent<ChatPanel>();
            Chat.Init(this);
            Hud = new GameObject("Hud").AddComponent<Hud>();
            Hud.Init(this);
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
            if (Array.IndexOf(cmd, "-notitle") >= 0 || (Array.IndexOf(cmd, "-shot") >= 0 && Array.IndexOf(cmd, "-title") < 0)) Title.Skip();
            _tutorialPending = Array.IndexOf(cmd, "-tutorial") >= 0 || Array.IndexOf(cmd, "-tutorialStep") >= 0
                               || (!Tutorial.Finished && Array.IndexOf(cmd, "-shot") < 0);

            // Dev switch for screenshots and demos: Orsuun.exe -forge
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-forge") >= 0) Forge.Open();

            // Dev switches for screenshots: -sampleloot fills a local bag; -gear opens the Gear screen; -confirm asks to forge.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-sampleloot") >= 0 && !Server.Online) SampleLoot();
            // -class <Name> starts local play as that class (-kestrel kept for old scripts).
            string cls = Arg("-class") ?? (Array.IndexOf(Environment.GetCommandLineArgs(), "-kestrel") >= 0 ? "Kestrel" : null);
            if (cls != null && !Server.Online && Enum.TryParse(cls, out HeroClass chosen)) Session.SetClass(chosen);
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
            // Screenshots: -oath shows the Banner oath, -war the War of Banners, -bounties the bounty board.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-oath") >= 0) Oath.Open();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-war") >= 0) War.Open();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-bounties") >= 0) Bounties.Open();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-guild") >= 0) Guild.Open();
            // -chat, -zones, -shards, -market (-sell, -mylistings, -marketbooks, -sellbook) and -account open those screens for screenshots.
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
            string fxDemo = Arg("-fxdemo");
            if (fxDemo != null) StartCoroutine(Forge.Demo(fxDemo));

            // Dev switch: -shot <png> [-shotAfter seconds] saves the screen and quits (tools/screenshot-mac.sh). With
            // -castshow <slot> the shot waits for that skill's cast instead (-castdelay: seconds after its strike).
            string shot = Arg("-shot");
            _castShow = int.TryParse(Arg("-castshow"), out int castSlot) ? castSlot : -1;
            if (shot != null && _castShow < 0) StartCoroutine(ShotAndQuit(shot, float.TryParse(Arg("-shotAfter"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float after) ? after : 8f));
            if (shot != null && _castShow >= 0) _castFallback = StartCoroutine(ShotAndQuit(shot, 60f));   // gives up after a minute
        }

        private string _messagesTo;
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

        private static IEnumerator ShotAndQuit(string path, float after)
        {
            yield return new WaitForSecondsRealtime(after);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1.5f);
            Application.Quit();
        }

        private bool _openGuildWar;
        private int _duelLane = -1;
        private int _keepIndex = -1;
        private bool _openPits;
        private int _pitFight = -1;
        private int _caravanTab = -1;
        private bool _openWardrobe;
        private bool _openDepot;
        private bool _openTrail;
        private bool _openTrade;
        private bool _enterDungeon;
        private int _dungeonToEnter = 1;
        private readonly float[] _glowBySlot = new float[8];
        private Bell _localBellApplied = Bell.None;
        private bool _tutorialPending;
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
            bool shot = Array.IndexOf(Environment.GetCommandLineArgs(), "-shot") >= 0 && _firstRun == null;
            // The way in (owner, 25 Sep 2026): a new install (or a phone signed out) starts at the sign-in screen, the
            // oath follows for an account not yet sworn, then the character screen. -account forces the sign-in screen.
            if (!Title.Waiting && Server.Connected && !Account.Showing && !_accountShown
                && (_accountAsked || AccountPanel.FirstScreen(Server) && !shot))
            {
                _accountShown = true;
                Account.Open();
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
            // permission is asked then too, once.
            if (_tutorialPending && !Title.Showing && !Account.Showing && !Oath.Showing && !Characters.IsOpen)
            {
                Notifications.AskOnce();
                _tutorialPending = false;
                // Dev switch: -tutorialStep <n> opens the guide at a step (screenshots).
                Tutorial.Begin(int.TryParse(Arg("-tutorialStep"), out int step) ? step : 0);
            }

        }

        private void Update()
        {
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

            LaneSim lane = ActiveLane;
            if (Lane.Sim != lane) Lane.Bind(lane);
            // The other classes' looks follow the armour's level band (the Vanguard's armour and glaive have their own).
            ItemState armor = Session.Equipped(EquipSlot.Armor);
            // A worn skin shows its own band in its tint; mounts and companions follow the hero (the wardrobe).
            WardrobeDef skin = null, mount = null, companion = null;
            foreach (WardrobeDef piece in Session.Worn)
                if (piece.Kind == WardrobeKind.Skin) skin = piece; else if (piece.Kind == WardrobeKind.Mount) mount = piece; else companion = piece;
            // A skin shows its own costume model where one has been made, otherwise an armour band in the skin's tint.
            string skinModel = skin != null ? LaneView.SkinModel(Session.Class, skin.Look) : null;
            bool tinted = skin != null && skinModel == null && LaneView.SkinLooks.ContainsKey(skin.Look);
            (int Band, Color Tint) skinLook = tinted ? LaneView.SkinLooks[skin.Look] : (0, Color.white);
            int band = tinted ? skinLook.Band : armor != null ? ItemLooks.Tier(armor.ItemLevel) : 0;
            Lane.SetHeroClass(Session.Class, band, Session.Class == HeroClass.Vanguard ? null : skinModel);
            Ui.IconClass = Session.Class;
            Lane.SetLooks(skinModel != null && Session.Class == HeroClass.Vanguard ? skinModel : tinted ? "Armor_T" + band : armor?.LookId, Session.Weapon.LookId);
            Lane.SetWardrobe(mount?.Look, companion?.Look, skinLook.Tint);
            Lane.SetGear(UpgradeGlow.PerSlot(Session, _glowBySlot));

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
            Hud.Log(chest + PoolNote(bossId));
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
            Lane.SetRival(Enum.TryParse(duel.defenderClass, out HeroClass rival) ? rival : HeroClass.Vanguard, duel.defenderBand);
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
            Lane.SetRival(Enum.TryParse(duel.defenderClass, out HeroClass rival) ? rival : HeroClass.Vanguard, duel.defenderBand);
            _replay = BossRun.Create(champion, hero, new Inventory(), duel.seed);
            ReplayBanner = "THE PITS  ·  " + duel.champion;
            int guard = BossRun.MaxTicks;
            while (_replay.BossesKilled == 0 && _replay.Deaths == 0 && guard-- > 0) yield return null;

            int moved = fight.ratingAfter - fight.ratingBefore;
            ReplayBanner = (duel.won ? "VICTORY  ·  " : "DEFEAT  ·  ") + (moved >= 0 ? "+" : "") + moved;
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
            cam.fieldOfView = 25f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
            // Frames x from about -3 to 6: hero on the left, a full pack and the Korstone on the right. Raised 0.8 m
            // (owner, 26 Sep 2026: "put the char and the mobs a bit lower") so heads, bosses and skill effects clear
            // the HUD's goal plate and banners.
            cam.transform.position = new Vector3(1.5f, 5.4f, -19.5f);
            cam.transform.LookAt(new Vector3(1.5f, 1.9f, 0f));

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
