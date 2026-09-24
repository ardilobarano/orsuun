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
        public ZonePanel Zones { get; private set; }
        public SocketPanel Sockets { get; private set; }
        public TurnHelperPanel TurnHelper { get; private set; }
        public Net.ServerLink Server { get; private set; }
        public TitleScreen Title { get; private set; }
        public MenuPanel Menu { get; private set; }
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
            Zones = new GameObject("ZonePanel").AddComponent<ZonePanel>();
            Zones.Init(this);
            Sockets = new GameObject("SocketPanel").AddComponent<SocketPanel>();
            Sockets.Init(this);
            TurnHelper = new GameObject("TurnHelperPanel").AddComponent<TurnHelperPanel>();
            TurnHelper.Init(this);
            Hud = new GameObject("Hud").AddComponent<Hud>();
            Hud.Init(this);
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
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-menu") >= 0) Menu.Open();
            // -turnhelper [pick] opens the turning helper over the Forge (pick: with the etching list open).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-turnhelper") >= 0)
            {
                Forge.Open();
                TurnHelper.Open();
                if (Arg("-turnhelper") == "pick") TurnHelper.OpenPicker(0);
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-anvilbag") >= 0 && Session.Inventory.Loot.Count > 0) Session.PutOnAnvil(Session.Inventory.Loot[4]);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-confirm") >= 0) { Forge.Open(); Forge.StartAttempt(ForgeMethod.ForgeAlone); }

            // Dev switch: -fxdemo <outcome> plays the Forge's anvil moment with a made-up result (screenshots).
            string fxDemo = Arg("-fxdemo");
            if (fxDemo != null) StartCoroutine(Forge.Demo(fxDemo));

            // Dev switch: -shot <png> [-shotAfter seconds] saves the screen and quits (tools/screenshot-mac.sh).
            string shot = Arg("-shot");
            if (shot != null) StartCoroutine(ShotAndQuit(shot, float.TryParse(Arg("-shotAfter"), out float after) ? after : 8f));
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

        private readonly float[] _glowBySlot = new float[8];
        private Bell _localBellApplied = Bell.None;
        private bool _tutorialPending;

        private void Update()
        {
            // The first session's guide starts once the title screen is gone.
            if (_tutorialPending && !Title.Showing)
            {
                _tutorialPending = false;
                // Dev switch: -tutorialStep <n> opens the guide at a step (screenshots).
                Tutorial.Begin(int.TryParse(Arg("-tutorialStep"), out int step) ? step : 0);
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
            Lane.SetHeroClass(Session.Class, armor != null ? ItemLooks.Tier(armor.ItemLevel) : 0);
            Lane.SetLooks(Session.Equipped(EquipSlot.Armor)?.LookId, Session.Weapon.LookId);
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
            Hud.Log(chest);
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
            // Frames x from about -3 to 6: hero on the left, a full pack and the Korstone on the right.
            cam.transform.position = new Vector3(1.5f, 4.6f, -19.5f);
            cam.transform.LookAt(new Vector3(1.5f, 1.1f, 0f));

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
