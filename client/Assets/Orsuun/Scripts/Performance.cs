using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Orsuun.Client
{
    /// <summary>
    /// SETTINGS (owner, 28 Sep 2026: "Graphics & text settings": "MENU options for graphics quality (low/medium/high), fewer
    /// skill effects, larger text"), kept on the device. Graphics LOW is the old BATTERY SAVER (30 frames a second, the lane
    /// drawn smaller, no bloom), MEDIUM draws the lane a little smaller, HIGH as built. FEWER skill effects draws each skill's
    /// own effect without its grade's layers (SkillFx). Text LARGE and LARGER let every label grow by a fifth and two fifths
    /// where its box has room (labels fit their text: LocText).
    /// </summary>
    public static class GameSettings
    {
        public enum Quality { Low, Medium, High }

        private const string GraphicsKey = "orsuun.graphics", EffectsKey = "orsuun.fewerfx", TextKey = "orsuun.textsize", OldSaverKey = "orsuun.saver",
            FpsKey = "orsuun.showfps";

        public static readonly float[] TextScales = { 1f, 1.2f, 1.4f };

        public static Quality Graphics { get; private set; } = Quality.High;
        public static bool FewerEffects { get; private set; }
        public static int TextSize { get; private set; }
        /// <summary>A frame-rate readout in the corner (29 Sep 2026, with the phone speed pass).</summary>
        public static bool ShowFps { get; private set; }
        public static float TextScale => TextScales[Mathf.Clamp(TextSize, 0, TextScales.Length - 1)];

        /// <summary>Raised on any change: Performance applies the graphics, every LocText its size.</summary>
        public static event Action Changed;

        public static void Load()
        {
            try
            {
                // The BATTERY SAVER (until 28 Sep 2026) becomes LOW.
                int old = PlayerPrefs.GetInt(OldSaverKey, 0);
                Graphics = (Quality)Mathf.Clamp(PlayerPrefs.GetInt(GraphicsKey, old == 1 ? (int)Quality.Low : (int)Quality.High), 0, 2);
                FewerEffects = PlayerPrefs.GetInt(EffectsKey, 0) == 1;
                TextSize = Mathf.Clamp(PlayerPrefs.GetInt(TextKey, 0), 0, TextScales.Length - 1);
                ShowFps = PlayerPrefs.GetInt(FpsKey, 0) == 1;
            }
            catch (Exception) { }
            // Screenshots: -graphics low|medium|high, -fewerfx, -textsize 0..2 (not saved).
            string[] args = Environment.GetCommandLineArgs();
            int g = Array.IndexOf(args, "-graphics");
            if (g >= 0 && g + 1 < args.Length && Enum.TryParse(args[g + 1], true, out Quality q)) Graphics = q;
            if (Array.IndexOf(args, "-fewerfx") >= 0) FewerEffects = true;
            int t = Array.IndexOf(args, "-textsize");
            if (t >= 0 && t + 1 < args.Length && int.TryParse(args[t + 1], out int size)) TextSize = Mathf.Clamp(size, 0, TextScales.Length - 1);
        }

        public static void SetGraphics(Quality quality) { Graphics = quality; Save(); }
        public static void SetFewerEffects(bool fewer) { FewerEffects = fewer; Save(); }
        public static void SetTextSize(int size) { TextSize = Mathf.Clamp(size, 0, TextScales.Length - 1); Save(); }
        public static void SetShowFps(bool show) { ShowFps = show; Save(); }

        private static void Save()
        {
            try
            {
                PlayerPrefs.SetInt(GraphicsKey, (int)Graphics);
                PlayerPrefs.SetInt(EffectsKey, FewerEffects ? 1 : 0);
                PlayerPrefs.SetInt(TextKey, TextSize);
                PlayerPrefs.SetInt(FpsKey, ShowFps ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch (Exception) { }
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Phone performance (owner, 27 Sep 2026: "Phone performance pass"). The lane's 3D is drawn at most MaxLaneHeight
    /// pixels tall (a 1290x2796 phone drew every pixel before; the UI stays at the screen's own resolution), the game runs
    /// at 60 frames a second while it is touched and 30 once it has been left to hunt for IdleSeconds, and graphics LOW
    /// (SETTINGS on MENU, once the BATTERY SAVER) holds 30, draws the lane smaller and turns off bloom; MEDIUM draws it a
    /// little smaller. -perflog writes the frame rate to the log every ten seconds; -renderscale x and -nopost try one
    /// setting alone. SETTINGS' FRAME RATE (or -fps) shows the frames of the last second in the corner.
    /// </summary>
    public sealed class Performance : MonoBehaviour
    {
        private const float IdleSeconds = 20f;
        private const float MaxLaneHeight = 1800f;

        public static bool Saver => GameSettings.Graphics == GameSettings.Quality.Low;

        private Camera _lane;
        private float _lastInput, _logAt;
        private int _fps = -1, _frames;
        private bool _log;
        private float _builtScale = -1f;

        public void Init(Camera lane)
        {
            _lane = lane;
            GameSettings.Changed += Apply;
            _lastInput = _logAt = Time.realtimeSinceStartup;
            _log = Array.IndexOf(Environment.GetCommandLineArgs(), "-perflog") >= 0;
            // Phones ignore vsync and follow targetFrameRate; on the Mac vsync would override it.
            QualitySettings.vSyncCount = 0;
            Apply();
        }

        private void Apply()
        {
            string[] args = Environment.GetCommandLineArgs();
            int forced = Array.IndexOf(args, "-renderscale");
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                if (_builtScale < 0f) _builtScale = urp.renderScale;
                float scale = Mathf.Clamp(MaxLaneHeight / Mathf.Max(1, Screen.height), 0.5f, 1f);
                urp.renderScale = forced >= 0 && forced + 1 < args.Length && float.TryParse(args[forced + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float f) ? f
                    : GameSettings.Graphics == GameSettings.Quality.Low ? Mathf.Max(0.5f, scale * 0.75f)
                    : GameSettings.Graphics == GameSettings.Quality.Medium ? Mathf.Max(0.5f, scale * 0.87f) : scale;
                // MSAA stays as built: switching it at runtime breaks the frame on Metal (upside down, the lane black).
            }
            if (_lane != null) _lane.GetUniversalAdditionalCameraData().renderPostProcessing = !Saver && Array.IndexOf(args, "-nopost") < 0;
            _fps = -1;
        }

        // In the editor the pipeline asset is the project's file: put its scale back.
        private void OnDestroy()
        {
            GameSettings.Changed -= Apply;
            if (_builtScale > 0f && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = _builtScale;
        }

        private Text _fpsText;
        private int _fpsFrames;
        private float _fpsAt;

        /// <summary>The corner readout: frames counted over each second, on a canvas above every screen.</summary>
        private void Readout(float now)
        {
            bool show = GameSettings.ShowFps || Array.IndexOf(Environment.GetCommandLineArgs(), "-fps") >= 0;
            if (_fpsText == null)
            {
                if (!show) return;
                Transform canvas = Ui.Canvas("FpsCanvas", 70).transform;
                _fpsText = Ui.Raw(Ui.Label("Fps", canvas, 0.72f, 0.905f, 0.985f, 0.935f, "", 22, TextAnchor.MiddleRight, Palette.Good));
                _fpsText.raycastTarget = false;
                _fpsText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                _fpsAt = now;
            }
            if (_fpsText.gameObject.activeSelf != show) _fpsText.gameObject.SetActive(show);
            if (!show) return;
            _fpsFrames++;
            if (now - _fpsAt < 1f) return;
            float rate = _fpsFrames / (now - _fpsAt);
            _fpsText.text = rate.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " FPS";
            _fpsText.color = rate >= _fps * 0.9f ? Palette.Good : rate >= _fps * 0.6f ? Palette.Warn : Palette.Bad;
            _fpsFrames = 0;
            _fpsAt = now;
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            Readout(now);
            if (Input.touchCount > 0 || Input.GetMouseButton(0) || Input.anyKeyDown) _lastInput = now;
            int fps = Saver || now - _lastInput > IdleSeconds ? 30 : 60;
            if (fps != _fps)
            {
                _fps = fps;
                Application.targetFrameRate = fps;
            }
            if (!_log) return;
            _frames++;
            if (now - _logAt < 10f) return;
            float urpScale = GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset u ? u.renderScale : 1f;
            Debug.Log($"PERF fps={_frames / (now - _logAt):0.0} target={_fps} scale={urpScale:0.00} screen={Screen.width}x{Screen.height} saver={Saver}");
            _frames = 0;
            _logAt = now;
        }
    }
}
