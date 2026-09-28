using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// The lane camera's life (owner, 28 Sep 2026: picked "Action camera" and "Hit feel"): it leans in toward the hero on
    /// crits, skills and a boss's blows, shakes on crits, heavy blows and a Korstone's break, and eases back out while the
    /// hero walks between packs, so more of the map is in view. It works from the lane's own pose (LaneView.CameraFrom,
    /// CameraTo, CameraFov) and always settles back to it. Presentation only. FEWER skill effects (GameSettings) keeps the
    /// lean but drops the shake.
    /// </summary>
    public sealed class ActionCamera : MonoBehaviour
    {
        private static ActionCamera _instance;
        private Camera _camera;
        private LaneView _lane;
        private float _lean, _leanTarget, _leanUntil, _shake, _wide;

        public void Init(LaneView lane)
        {
            _instance = this;
            _camera = GetComponent<Camera>();
            _lane = lane;
        }

        /// <summary>Leans in (0 none, 1 all the way) for a moment; a stronger lean wins over a weaker one still running.</summary>
        public static void Lean(float amount, float seconds)
        {
            if (_instance == null) return;
            if (amount < _instance._leanTarget && Time.time < _instance._leanUntil) return;
            _instance._leanTarget = amount;
            _instance._leanUntil = Time.time + seconds;
        }

        /// <summary>Shakes the view by up to <paramref name="metres"/>, dying away in about half a second.</summary>
        public static void Shake(float metres)
        {
            if (_instance == null || GameSettings.FewerEffects) return;
            _instance._shake = Mathf.Max(_instance._shake, metres);
        }

        private void LateUpdate()
        {
            if (_camera == null || FieldMap.Overview) return;
            float dt = Time.deltaTime;
            if (Time.time >= _leanUntil) _leanTarget = 0f;
            // In fast, back out slowly.
            _lean = Mathf.MoveTowards(_lean, _leanTarget, dt * (_leanTarget > _lean ? 5f : 1.5f));
            _wide = Mathf.MoveTowards(_wide, _lane != null && _lane.RunningNow ? 1f : 0f, dt * 0.7f);
            float lean = Mathf.SmoothStep(0f, 1f, _lean), wide = Mathf.SmoothStep(0f, 1f, _wide);

            Vector3 from = LaneView.CameraFrom, to = LaneView.CameraTo;
            // A lean looks nearer the hero and comes a quarter closer; walking pulls back and up a little.
            Vector3 focus = Vector3.Lerp(to, LaneView.HeroChest, 0.4f * lean);
            Vector3 eye = focus + (from - to) * (1f - 0.26f * lean + 0.1f * wide) + Vector3.up * (1.4f * wide);
            _shake = Mathf.MoveTowards(_shake, 0f, dt * Mathf.Max(0.25f, _shake * 6f));
            Vector3 jolt = _shake > 0.001f ? Random.insideUnitSphere * _shake : Vector3.zero;
            transform.position = eye + jolt;
            transform.LookAt(focus + jolt * 0.5f);
            _camera.fieldOfView = LaneView.CameraFov - 3f * lean + 2f * wide;
        }
    }
}
