using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Orsuun.Client
{
    /// <summary>
    /// Phone performance (owner, 27 Sep 2026: "Phone performance pass"). The lane's 3D is drawn at most MaxLaneHeight
    /// pixels tall (a 1290x2796 phone drew every pixel before; the UI stays at the screen's own resolution), the game runs
    /// at 60 frames a second while it is touched and 30 once it has been left to hunt for IdleSeconds, and the BATTERY SAVER
    /// (MENU, kept on the device) holds 30, draws the lane smaller and turns off bloom. -perflog writes the frame rate to
    /// the log every ten seconds; -renderscale x and -nopost try one setting alone.
    /// </summary>
    public sealed class Performance : MonoBehaviour
    {
        private const string SaverKey = "orsuun.saver";
        private const float IdleSeconds = 20f;
        private const float MaxLaneHeight = 1800f;

        public static bool Saver { get; private set; }

        private Camera _lane;
        private float _lastInput, _logAt;
        private int _fps = -1, _frames;
        private bool _log;
        private float _builtScale = -1f;

        public void Init(Camera lane)
        {
            _lane = lane;
            Saver = PlayerPrefs.GetInt(SaverKey, 0) == 1;
            _lastInput = _logAt = Time.realtimeSinceStartup;
            _log = Array.IndexOf(Environment.GetCommandLineArgs(), "-perflog") >= 0;
            // Phones ignore vsync and follow targetFrameRate; on the Mac vsync would override it.
            QualitySettings.vSyncCount = 0;
            Apply();
        }

        public void SetSaver(bool on)
        {
            Saver = on;
            PlayerPrefs.SetInt(SaverKey, on ? 1 : 0);
            PlayerPrefs.Save();
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
                    System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : Saver ? Mathf.Max(0.5f, scale * 0.75f) : scale;
                // MSAA stays as built: switching it at runtime breaks the frame on Metal (upside down, the lane black).
            }
            if (_lane != null) _lane.GetUniversalAdditionalCameraData().renderPostProcessing = !Saver && Array.IndexOf(args, "-nopost") < 0;
            _fps = -1;
        }

        // In the editor the pipeline asset is the project's file: put its scale back.
        private void OnDestroy()
        {
            if (_builtScale > 0f && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) urp.renderScale = _builtScale;
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
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
