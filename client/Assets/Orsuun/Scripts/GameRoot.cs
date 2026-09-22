using System;
using System.Collections;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;

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
        public Net.ServerLink Server { get; private set; }
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
            Hud = new GameObject("Hud").AddComponent<Hud>();
            Hud.Init(this);

            // Dev switch for screenshots and demos: Orsuun.exe -forge
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-forge") >= 0) Forge.Open();
        }

        private Bell _localBellApplied = Bell.None;

        private void Update()
        {
            // Local mode rings the bells from the PC clock; online the server's state applies them.
            if (!Server.Online && !Replaying)
            {
                Bell now = EveningBells.Active(DateTime.Now);
                if (now != _localBellApplied) { _localBellApplied = now; Session.ApplyBell(now); }
            }

            LaneSim lane = ActiveLane;
            if (Lane.Sim != lane) Lane.Bind(lane);

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
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
        }
    }
}
