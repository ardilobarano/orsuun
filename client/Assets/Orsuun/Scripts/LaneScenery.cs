using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Orsuun.Client
{
    /// <summary>
    /// Scenery along the hunt (owner, 28 Sep 2026: picked "3D scenery on the hunt": "Trees, rocks, grass and ruins along the
    /// lane's road for each map, so the hunt feels like a real place too"). Painted props (Content/Scenery: five sets of six,
    /// Steppe, Mountain, Desert, Forest, Ruins, each an atlas with its props' rectangles and heights in Rects.json) stand as
    /// cards in the lane's 3D: a row beyond the road and a few low ones before it, facing the camera. While the hero runs
    /// they pass at the floor's speed and come round again from the right as other props; each backdrop names its set.
    /// </summary>
    public sealed class LaneScenery : MonoBehaviour
    {
#pragma warning disable CS0649   // filled by JsonUtility
        [Serializable] private sealed class Rect { public float u0, u1, v0, v1, aspect, height; public bool low; }
        [Serializable] private sealed class Sets { public Rect[] steppe, mountain, desert, forest, ruins; }
#pragma warning restore CS0649

        private sealed class Prop
        {
            public Transform T;
            public MeshRenderer R;
            public bool Near;
        }

        private const int FarProps = 8, NearProps = 3;
        private const float LeftEdge = -11f, Span = 24f, Speed = 6f;
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        private readonly List<Prop> _props = new List<Prop>();
        private Sets _sets;
        private Rect[] _set;
        private Material _material;
        private string _setName;
        private MaterialPropertyBlock _block;
        private System.Random _rng = new System.Random(17);
        private static Mesh _quad;

        /// <summary>The set a lane backdrop is dressed with (LaneView's keys).</summary>
        public static string SetFor(string backdrop) => backdrop switch
        {
            "FrostPasture" or "CarversArchive" => "Mountain",
            "SaltFlats" or "CinderMarches" or "SunkenBazaar" or "SilkWarren" => "Desert",
            "Whisperwood" or "Bloodbirch" or "DrownedSteppe" => "Forest",
            "ColossusGraves" or "HollowThrone" or "HollowSpire" or "ThousandMarkers" => "Ruins",
            _ => "Steppe",
        };

        public void Init()
        {
            _block = new MaterialPropertyBlock();
            var json = Art.Load<TextAsset>("Scenery/Rects");
            if (json != null) _sets = JsonUtility.FromJson<Sets>(json.text);
            for (int i = 0; i < FarProps + NearProps; i++)
            {
                var go = new GameObject("Prop" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = Quad;
                var r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                _props.Add(new Prop { T = go.transform, R = r, Near = i >= FarProps });
            }
            gameObject.SetActive(false);
        }

        /// <summary>Dresses the lane for a backdrop (a key LaneView uses); a set without its art leaves the lane bare.</summary>
        public void SetBackdrop(string backdrop)
        {
            string name = SetFor(backdrop);
            if (name == _setName) return;
            _setName = name;
            _material = Art.Load<Material>("Scenery/" + name);
            _set = _sets == null ? null : name switch
            {
                "Mountain" => _sets.mountain, "Desert" => _sets.desert, "Forest" => _sets.forest, "Ruins" => _sets.ruins, _ => _sets.steppe,
            };
            bool ready = _material != null && _set != null && _set.Length > 0;
            gameObject.SetActive(ready);
            if (!ready) return;
            // Spread along the lane from the left edge, each row evenly with a little jitter.
            for (int i = 0; i < _props.Count; i++)
            {
                Prop p = _props[i];
                int n = p.Near ? NearProps : FarProps, k = p.Near ? i - FarProps : i;
                Place(p, LeftEdge + (k + 0.5f) * Span / n + (float)(_rng.NextDouble() - 0.5) * 1.2f);
            }
        }

        /// <summary>A prop takes a new look and depth at x: beyond the road any prop, before it only low ones.</summary>
        private void Place(Prop p, float x)
        {
            Rect r;
            int guard = 0;
            do r = _set[_rng.Next(_set.Length)]; while (p.Near && !r.low && ++guard < 20);
            float height = r.height * (0.85f + (float)_rng.NextDouble() * 0.3f) * (p.Near ? 0.65f : 1f);
            float width = height / Mathf.Max(0.2f, r.aspect);
            float z = p.Near ? -2.2f - (float)_rng.NextDouble() * 0.9f : 2.6f + (float)_rng.NextDouble() * 2.2f;
            p.T.localPosition = new Vector3(x, 0f, z);
            p.T.localScale = new Vector3(width * (_rng.Next(2) == 0 ? 1f : -1f), height, 1f);
            p.R.sharedMaterial = _material;
            p.R.GetPropertyBlock(_block);
            _block.SetVector(BaseMapSt, new Vector4(r.u1 - r.u0, r.v1 - r.v0, r.u0, r.v0));
            p.R.SetPropertyBlock(_block);
        }

        /// <summary>A frame of the lane: while the hero runs the props pass at the floor's speed.</summary>
        public void Tick(float dt, bool running)
        {
            if (!running || _set == null) return;
            foreach (Prop p in _props)
            {
                Vector3 at = p.T.localPosition;
                at.x -= Speed * dt;
                if (at.x < LeftEdge) Place(p, at.x + Span + (float)(_rng.NextDouble() - 0.5) * 1.5f);
                else p.T.localPosition = at;
            }
        }

        /// <summary>A card one unit wide and tall, standing on its bottom edge, facing the lane's camera (-z).</summary>
        private static Mesh Quad
        {
            get
            {
                if (_quad != null) return _quad;
                _quad = new Mesh { name = "SceneryCard" };
                _quad.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(0.5f, 0f, 0f) };
                _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                _quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                _quad.RecalculateBounds();
                return _quad;
            }
        }
    }
}
