using System;
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

        public PlayerSession Session { get; private set; }
        public LaneView Lane { get; private set; }
        public Hud Hud { get; private set; }
        public ForgePanel Forge { get; private set; }
        public int SpeedMultiplier { get; set; } = 1;

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

            Forge = new GameObject("ForgePanel").AddComponent<ForgePanel>();
            Forge.Init(this);
            Hud = new GameObject("Hud").AddComponent<Hud>();
            Hud.Init(this);

            // Dev switch for screenshots and demos: Orsuun.exe -forge
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-forge") >= 0) Forge.Open();
        }

        private void Update()
        {
            _accumulator = Mathf.Min(_accumulator + Time.deltaTime * LaneSim.TicksPerSecond * SpeedMultiplier, 200f);
            while (_accumulator >= 1f)
            {
                _accumulator -= 1f;
                Session.Lane.Tick();
                foreach (LaneEvent e in Session.Lane.DrainEvents())
                {
                    Lane.Handle(e);
                    Hud.Handle(e);
                }
            }
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
