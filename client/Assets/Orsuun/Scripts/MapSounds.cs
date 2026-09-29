using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// A big map's sounds (owner, 29 Sep 2026: "Map sounds"): loops that follow what the hero passes, over the map's own
    /// ambience (GameRoot.MapAmbience). Running water swells near the river and its bridges (a lava river crackles like fire
    /// instead), fire crackles near the war drums' braziers, the fire shrines and the forges, war drums boom near a war
    /// camp's drum towers, gusts howl while it snows and a low hum fills the dungeon halls. Birds call as a flock crosses
    /// (MapWeather). All fade away in town, at the river and off a big map.
    /// </summary>
    public sealed class MapSounds : MonoBehaviour
    {
        private static readonly string[] Layers = { "RiverWater", "RiverFire", "WarDrums", "Gusts", "HallHum" };
        private readonly float[] _want = new float[5];
        private GameRoot _root;
        private LaneView _lane;
        private float _next;

        public void Init(GameRoot root, LaneView lane)
        {
            _root = root;
            _lane = lane;
        }

        private void Update()
        {
            if (_root == null || GameAudio.Instance == null || Time.time < _next) return;
            _next = Time.time + 0.25f;
            System.Array.Clear(_want, 0, _want.Length);
            FieldMap map = _lane.Map;
            bool on = map != null && map.Active && map.Current != null && !_root.Town.IsOpen && !_root.Server.AtRiver && !_root.Server.WaitingForHero;
            if (on) Listen(map.Current, map.HeroOnMap);
            for (int i = 0; i < Layers.Length; i++) GameAudio.Instance.Ambience(Layers[i], _want[i], 1.5f);
        }

        private void Listen(FieldMap.Layout layout, Vector2 hero)
        {
            if (layout.River != null && layout.River.Length >= 2)
            {
                float d = Mathf.Max(0f, Distance(hero, layout.River) - layout.RiverWidth / 2f);
                float near = Mathf.Clamp01(1f - d / 30f);
                bool lava = layout.WaterColor == "#FF6A1A";
                if (lava) _want[1] = Mathf.Max(_want[1], near * 0.5f);
                else _want[0] = near * near * 0.55f;
            }
            foreach (FieldMap.Spot s in layout.Landmarks ?? new FieldMap.Spot[0])
            {
                float d = Vector2.Distance(hero, s.At);
                if (s.Name == "WarDrum") _want[2] = Mathf.Max(_want[2], Mathf.Clamp01(1f - d / 55f) * 0.6f);
                if (s.Name == "WarDrum" || s.Name == "AshShrine" || s.Name == "LavaForge")
                    _want[1] = Mathf.Max(_want[1], Mathf.Clamp01(1f - d / 28f) * 0.5f);
            }
            if ((layout.Weather ?? "").Contains("snow")) _want[3] = 0.4f;
            if (layout.Dungeon > 0) _want[4] = 0.45f;
        }

        /// <summary>How far a point is from a course of points (metres).</summary>
        private static float Distance(Vector2 p, Vector2[] course)
        {
            float best = float.MaxValue;
            for (int i = 0; i < course.Length - 1; i++)
            {
                Vector2 a = course[i], b = course[i + 1];
                float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / Mathf.Max(0.001f, (b - a).sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + (b - a) * t));
            }
            return best;
        }
    }
}
