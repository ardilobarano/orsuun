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
        [Serializable] internal sealed class Rect { public float u0, u1, v0, v1, aspect, height; public bool low; public string model; }
        [Serializable] internal sealed class Sets { public Rect[] steppe, mountain, desert, forest, ruins; }

        /// <summary>A scenery set's material and props (the big maps scatter them too: FieldMap), or nulls without its art.</summary>
        internal static (Material material, Rect[] props) SetOf(string name)
        {
            var json = Art.Load<TextAsset>("Scenery/Rects");
            Sets sets = json == null ? null : JsonUtility.FromJson<Sets>(json.text);
            Rect[] props = sets == null ? null : name switch
            {
                "Mountain" => sets.mountain, "Desert" => sets.desert, "Forest" => sets.forest, "Ruins" => sets.ruins, _ => sets.steppe,
            };
            return (Art.Load<Material>("Scenery/" + name), props);
        }
#pragma warning restore CS0649

        private sealed class Prop
        {
            public Transform T;
            public MeshRenderer R;
            public bool Near;
            // A prop with a 3D model (Content/Scenery/Models) stands as that instead of its card.
            public GameObject Model;
            public string ModelName;
        }

        // The field (28 Sep 2026): props on both sides of the road, the far side's anything, the camera's side low ones
        // and only up the road, clear of the line from the camera to the hero.
        private const int FarProps = 28, NearProps = 10;
        /// <summary>Seen from the field's camera, 25 m off, props stand a third again as tall as by the old side lane.</summary>
        private const float FieldScale = 1.35f;
        /// <summary>The models' painted side is their +z (a card shows its -z): half a turn more than a card facing the camera.</summary>
        private const float ModelYaw = 180f;

        /// <summary>A card's turn about y so its face (-z) looks at the lane's camera (LaneView.CameraFrom).</summary>
        private static Quaternion FacingCamera(Vector3 at)
        {
            Vector3 away = at - LaneView.CameraFrom;
            away.y = 0f;
            return away.sqrMagnitude < 0.01f ? Quaternion.identity : Quaternion.LookRotation(away.normalized, Vector3.up);
        }
        private const float LeftEdge = -16f, Span = 44f, Speed = 6f;
        private const float FarZMin = 2.8f, FarZMax = 16f, NearZMin = -3f, NearZMax = -10f, NearXMin = 3f;
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

        /// <summary>Whether the current set's art is there (LaneView hides the scenery on a big map either way).</summary>
        public bool HasArt { get; private set; }

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
            HasArt = ready;
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
            float height = r.height * (0.85f + (float)_rng.NextDouble() * 0.3f) * (p.Near ? 0.8f : 1f) * FieldScale;
            float width = height / Mathf.Max(0.2f, r.aspect);
            float z = p.Near ? NearZMin + (float)_rng.NextDouble() * (NearZMax - NearZMin) : FarZMin + (float)_rng.NextDouble() * (FarZMax - FarZMin);
            // The camera's side keeps clear of the line from the camera to the hero.
            if (p.Near && x < NearXMin) x = NearXMin + (float)_rng.NextDouble() * 6f;
            p.T.localPosition = new Vector3(x, 0f, z);
            p.T.localRotation = FacingCamera(p.T.localPosition);
            p.T.localScale = new Vector3(width * (_rng.Next(2) == 0 ? 1f : -1f), height, 1f);
            bool model = ShowModel(p, r.model, new Vector3(x, 0f, z), height);
            p.R.enabled = !model;
            if (model) return;
            p.R.sharedMaterial = _material;
            p.R.GetPropertyBlock(_block);
            _block.SetVector(BaseMapSt, new Vector4(r.u1 - r.u0, r.v1 - r.v0, r.u0, r.v0));
            p.R.SetPropertyBlock(_block);
        }

        /// <summary>Stands a prop's 3D model where its card would stand (turned toward the camera, a little each way), or
        /// hides the prop's model when it has none (or its art is missing).</summary>
        private bool ShowModel(Prop p, string name, Vector3 at, float height)
        {
            if (string.IsNullOrEmpty(name))
            {
                if (p.Model != null) p.Model.SetActive(false);
                return false;
            }
            if (p.ModelName != name)
            {
                if (p.Model != null) Destroy(p.Model);
                p.Model = null;
                p.ModelName = name;
                var prefab = Art.Load<GameObject>("Scenery/Models/" + name);
                if (prefab == null) return false;
                p.Model = Instantiate(prefab, transform);
                p.Model.name = name;
                var material = Art.Load<Material>("Scenery/Models/" + name);
                foreach (Renderer r in p.Model.GetComponentsInChildren<Renderer>())
                {
                    if (material != null) r.sharedMaterial = material;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
            if (p.Model == null) return false;
            p.Model.SetActive(true);
            p.Model.transform.localPosition = at;
            p.Model.transform.localRotation = Quaternion.Euler(0f, FacingCamera(at).eulerAngles.y + ModelYaw + (float)(_rng.NextDouble() - 0.5) * 60f, 0f);
            p.Model.transform.localScale = Vector3.one * height;
            return true;
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
                else
                {
                    p.T.localPosition = at;
                    p.T.localRotation = FacingCamera(at);
                    if (p.Model != null && p.Model.activeSelf) p.Model.transform.localPosition = new Vector3(at.x, 0f, at.z);
                }
            }
        }

        /// <summary>A card one unit wide and tall, standing on its bottom edge, facing the lane's camera (-z).</summary>
        internal static Mesh Quad
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
