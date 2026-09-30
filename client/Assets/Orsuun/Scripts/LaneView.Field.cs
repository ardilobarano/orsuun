using System.Collections.Generic;
using Orsuun.Rules.Combat;
using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// Fights on a big map (owner, 29 Sep 2026: "Fights at the camps"). While the hero fights, the next pack stands waiting up
    /// the trail where his walk will end: the stage's own monsters, idling in a loose group facing him, as many and of the
    /// kinds the pack will be (the count is the lane's guess until he walks on, then exact: LaneSim.PeekPackSize). When the
    /// pack spawns, each of its monsters starts where one of them stood and charges the last steps to its place. A Korstone
    /// rises inside a ring of standing stones (the layout's KorstoneRing model), which sink again when it breaks.
    /// Presentation only: the lane's rules are untouched.
    /// </summary>
    public sealed partial class LaneView
    {
        /// <summary>How far beyond the walk's end the waiting pack stands (about the ring's reach in front of the hero).</summary>
        private const float WaitBeyond = 3.2f;
        private const float RingRadius = 4.4f, RingRiseSeconds = 0.9f, RingSinkSeconds = 1.3f;

        /// <summary>
        /// The big map's elite camp now (Rules.EliteCamps; FieldFolk sets it from the server, -1 none): a pack that waits
        /// within EliteReach of it is an elite pack, marked so in the lane (tougher, harder-hitting), a head taller and
        /// burning gold, as are the monsters it spawns. Its pay is the server's (settled with the hunt).
        /// </summary>
        internal int EliteCamp = -1;
        /// <summary>The lane bound is the online farm lane (GameRoot): only its packs may be marked elite, since its loops are
        /// reported and replayed with them; a push or boss replay never is.</summary>
        internal bool MayMarkElite;
        private const float EliteReach = 28f, EliteSize = 1.3f;
        internal static readonly Color EliteGold = new Color(1.4f, 1.12f, 0.5f);
        private bool _waitingElite, _eliteFloated;

        private bool NearElite(Vector3 mapPoint)
        {
            FieldMap.Spot[] camps = _map?.Current?.Camps;
            return camps != null && EliteCamp >= 0 && EliteCamp < camps.Length
                   && Vector2.Distance(new Vector2(mapPoint.x, mapPoint.z), camps[EliteCamp].At) < EliteReach;
        }

        /// <summary>A monster of an elite pack: a head taller, its colours burning gold (bright enough for the bloom).</summary>
        internal static void DressElite(Transform mob, bool grow = true)
        {
            if (grow) mob.localScale *= EliteSize;
            foreach (Renderer r in mob.GetComponentsInChildren<Renderer>())
                if (r.sharedMaterial != null) r.sharedMaterial = Tinted(r.sharedMaterial, r.sharedMaterial.color * EliteGold);
        }

        private readonly List<Transform> _waiting = new List<Transform>();
        private int _waitingFirstId, _lastEnemyId;
        private bool _waitingExact;

        private sealed class RingStone
        {
            public Transform Root;
            public Vector3 Base;
            public float Age, SinkAge = -1f, Height;
        }

        private readonly List<RingStone> _ring = new List<RingStone>();

        /// <summary>Keeps the waiting pack in step with the lane: made when a fight begins and the next encounter is a pack,
        /// corrected to the exact size once the hero walks on, gone when the Korstone or boss comes next.</summary>
        private void UpdateWaiting(bool onMap)
        {
            if (!onMap || _sim.Phase == LanePhase.Dead) { ClearWaiting(); return; }
            int want;
            bool exact = false;
            if (_sim.Phase == LanePhase.Running)
            {
                want = _sim.PeekPackSize();
                exact = want >= 0;
                if (want < 0) want = 0;
            }
            else want = _sim.NextIsPack ? (_waiting.Count > 0 ? _waiting.Count : (_sim.Stage.PackSizeMin + _sim.Stage.PackSizeMax) / 2) : 0;
            // The pack's ids run on from the last enemy seen: a wave since the guess moves them, so the group is made again.
            // (Only once he walks on: made again mid-fight, the group would flicker with every Korstone wave.)
            int firstId = _lastEnemyId + 1;
            if (want == 0 || (exact && _waiting.Count > 0 && firstId != _waitingFirstId)) ClearWaiting();
            if (want == 0 || (_waitingExact && _waiting.Count == want)) return;
            if (_waiting.Count == 0)
            {
                _waitingFirstId = firstId;
                // Where the walk to the next pack ends, whole if the hero stands fighting, what is left of it if he walks.
                float walk = FieldMap.Speed * _sim.Stage.RunTicks / LaneSim.TicksPerSecond;
                _waitAnchor = _map.OnTrail(walk + WaitBeyond);
                _waitAlong = _map.AlongTrail(walk + WaitBeyond);
                // A pack that waits near the golden banner is elite in the lane itself (LaneSim.ElitePacks: tougher,
                // harder-hitting); the loop's report names it so the server's replay fights it too.
                if (MayMarkElite && NearElite(_waitAnchor)) _sim.MarkNextPackElite();
                _waitingElite = _sim.NextPackElite;
                _eliteFloated = false;
            }
            while (_waiting.Count > want)
            {
                Transform last = _waiting[_waiting.Count - 1];
                _waiting.RemoveAt(_waiting.Count - 1);
                if (last != null) Destroy(last.gameObject);
            }
            while (_waiting.Count < want)
            {
                int i = _waiting.Count;
                Transform mob = CosmeticMob(_waitingFirstId + i, out _);
                if (mob == null) break;
                mob.SetParent(_map.Root, true);
                var side = new Vector3(_waitAlong.z, 0f, -_waitAlong.x);
                int row = i / 4, col = i % 4;
                float jitter = Mathf.Sin(i * 12.9898f) * 0.35f;
                mob.localPosition = _waitAnchor + side * ((col - 1.5f) * 1.25f + jitter) + _waitAlong * (row * 1.3f + jitter * 0.6f);
                // Facing back down the trail toward the hero, each a little its own way.
                mob.localRotation = Quaternion.LookRotation(-_waitAlong, Vector3.up) * Quaternion.Euler(0f, jitter * 40f, 0f);
                if (_waitingElite) DressElite(mob);
                _waiting.Add(mob);
            }
            _waitingExact = exact;
        }

        private Vector3 _waitAnchor, _waitAlong = Vector3.right;

        /// <summary>Where the next monster of a pack starts on a big map: a waiting one's place, which it takes over.</summary>
        private bool TakeWaiting(out Vector3 at)
        {
            at = Vector3.zero;
            while (_waiting.Count > 0)
            {
                Transform first = _waiting[0];
                _waiting.RemoveAt(0);
                if (first == null) continue;
                at = first.position;
                Destroy(first.gameObject);
                return true;
            }
            return false;
        }

        private void ClearWaiting()
        {
            foreach (Transform t in _waiting) if (t != null) Destroy(t.gameObject);
            _waiting.Clear();
            _waitingExact = false;
        }

        /// <summary>Standing stones rise round a Korstone at <paramref name="centre"/> (lane space), leaving a gap toward the
        /// camera so the fight stays in view.</summary>
        private void RaiseRing(Vector3 centre)
        {
            SinkRing();
            string model = _map?.Current?.KorstoneRing;
            if (string.IsNullOrEmpty(model) || _map == null || !_map.Active) return;
            Vector3 toCamera = CameraFrom - centre;
            float cameraAngle = Mathf.Atan2(toCamera.z, toCamera.x) * Mathf.Rad2Deg;
            for (int k = 0; k < 7; k++)
            {
                float angle = k * 360f / 7f + 12f;
                if (Mathf.Abs(Mathf.DeltaAngle(angle, cameraAngle)) < 65f) continue;
                float a = angle * Mathf.Deg2Rad;
                float height = 2.8f + Mathf.Abs(Mathf.Sin(k * 7.1f)) * 0.9f;
                Vector3 at = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * RingRadius;
                Transform stone = FieldMap.PlaceModel(_map.Root, model, _map.Root.InverseTransformPoint(at), angle * 1.7f, height, _map.Current.KorstoneRingTint);
                if (stone == null) return;
                _ring.Add(new RingStone { Root = stone, Base = stone.localPosition, Height = height });
                stone.localPosition += Vector3.down * height;
            }
        }

        /// <summary>The ring's stones go back into the ground (the Korstone broke, or a new one rises).</summary>
        private void SinkRing()
        {
            foreach (RingStone s in _ring) if (s.SinkAge < 0f) s.SinkAge = 0f;
        }

        private void UpdateRing(float dt)
        {
            for (int i = _ring.Count - 1; i >= 0; i--)
            {
                RingStone s = _ring[i];
                if (s.Root == null) { _ring.RemoveAt(i); continue; }
                s.Age += dt;
                float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s.Age / RingRiseSeconds));
                if (s.SinkAge >= 0f)
                {
                    s.SinkAge += dt;
                    up *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s.SinkAge / RingSinkSeconds));
                    if (s.SinkAge >= RingSinkSeconds) { Destroy(s.Root.gameObject); _ring.RemoveAt(i); continue; }
                }
                // Rising, a stone trembles a little.
                float shake = up < 1f && s.SinkAge < 0f ? Mathf.Sin(Time.time * 60f + i) * 0.03f : 0f;
                s.Root.localPosition = s.Base + new Vector3(shake, -s.Height * (1f - up), 0f);
            }
        }
    }
}
