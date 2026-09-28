using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Orsuun.Client
{
    /// <summary>
    /// The town square as a place (owner, 28 Sep 2026: picked "Town square in 3D": "A 3D town hub (Emberhearth) where
    /// Forgemaster Dorun, Ilke of the Scales, Elder Tamir, Pitmaster Bora stand; tap one to open the Forge, Caravan, skills
    /// or the Pits. Rug Stalls would sit here"). Built round the town's stage (HeroStage from behind, the hero at the
    /// square's near end facing +z): a paved square (Content/Town/Paving), the painted far side of the town standing
    /// where it ends (Content/Town/Square: halls, yurts, the gate tower, market awnings), the four townsfolk as painted
    /// cards by their stations (the forge, the merchant's stall, the lectern, the weapon rack; Content/Town/Folk and
    /// Props), the Banner's colours on two poles, lantern posts lit for the evening and up to six players' rugs lying
    /// along the aisle (Content/Town/Rugs, one per Banner). Cards turn to face the square's camera.
    /// </summary>
    public sealed class TownScene : MonoBehaviour
    {
        [Serializable] private sealed class Rect { public float u0, u1, v0, v1, aspect, height; }
        [Serializable] private sealed class Rects { public Rect[] folk, props; }

        /// <summary>Where the square's camera stands and looks (from the hero's spot).</summary>
        public static readonly Vector3 CameraFrom = new Vector3(0f, 6.4f, -14.5f), CameraTo = new Vector3(0f, 0.4f, 9f);
        private const float FarZ = 38f, FarWidth = 36f, FarShift = -3f, FarCrop = 0.14f;

        /// <summary>The townsfolk, left to right: Forgemaster Dorun, Ilke of the Scales, Elder Tamir, Pitmaster Bora.</summary>
        public static readonly Vector3[] Folk =
        {
            new Vector3(-3.3f, 0f, 5.2f), new Vector3(-1.15f, 0f, 7.4f), new Vector3(1.15f, 0f, 7.4f), new Vector3(3.3f, 0f, 5.2f),
        };

        /// <summary>Where the rugs lie (up to six), nearest first, either side of the aisle; each is 1.9 m by 1.25 m.</summary>
        public static readonly Vector3[] RugSpots =
        {
            new Vector3(-1.75f, 0f, -2.1f), new Vector3(1.75f, 0f, -2.1f), new Vector3(-2.0f, 0f, 0.7f), new Vector3(2.0f, 0f, 0.7f),
            new Vector3(-2.15f, 0f, 3.3f), new Vector3(2.15f, 0f, 3.3f),
        };
        public const float RugLength = 1.9f, RugWidth = 1.25f;

        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        private Vector3 _at;
        private Transform _root;
        private Rects _rects;
        private Material _folk, _props, _rugs, _shade;
        private MaterialPropertyBlock _block;
        private readonly List<MeshRenderer> _bannerCloths = new List<MeshRenderer>();
        private readonly MeshRenderer[] _rugCards = new MeshRenderer[6];
        private readonly List<(Light light, float rest, float phase)> _flames = new List<(Light, float, float)>();
        private readonly PlaceMood _mood = new PlaceMood();
        private bool _shown;
        private Transform _heroShade;
        private static Mesh _card, _flat;

        public void Init(Vector3 at)
        {
            _at = at;
            _block = new MaterialPropertyBlock();
            _root = new GameObject("TownPlace").transform;
            _root.SetParent(transform, false);
            _root.position = at;
            var json = Art.Load<TextAsset>("Town/Rects");
            if (json != null) _rects = JsonUtility.FromJson<Rects>(json.text);
            _folk = Art.Load<Material>("Town/Folk");
            _props = Art.Load<Material>("Town/Props");
            _rugs = Art.Load<Material>("Town/Rugs");
            _shade = Art.Load<Material>("Town/Shade");
            Build();
            _root.gameObject.SetActive(false);
        }

        /// <summary>The square on screen: the lane's cameras rest; the sun is low beyond the town, warm, as painted.</summary>
        public void Show()
        {
            if (_shown) return;
            _shown = true;
            _root.gameObject.SetActive(true);
            _mood.Enter(Quaternion.Euler(38f, 200f, 0f), new Color(1f, 0.8f, 0.6f), 1.15f, new Color(0.58f, 0.48f, 0.44f));
        }

        public void Hide()
        {
            if (!_shown) return;
            _shown = false;
            _root.gameObject.SetActive(false);
            _mood.Leave();
        }

        /// <summary>A point of the square (local, from the hero's spot) in the world.</summary>
        public Vector3 World(Vector3 local) => _at + local;

        /// <summary>A townsman's card (width, height in metres), for finding him on the screen.</summary>
        public Vector2 FolkSize(int i)
        {
            if (_rects?.folk == null || i >= _rects.folk.Length) return new Vector2(0.8f, 1.9f);
            Rect r = _rects.folk[i];
            return new Vector2(r.height / Mathf.Max(0.2f, r.aspect), r.height);
        }

        /// <summary>The Banner's colours on the square's poles (Ember red, Sky blue, Gold yellow; none shows red).</summary>
        public void SetBanner(Rules.Banner banner)
        {
            int cell = banner == Rules.Banner.Sky ? 6 : banner == Rules.Banner.Gold ? 7 : 4;
            foreach (MeshRenderer cloth in _bannerCloths) Show(cloth, _rects?.props, cell);
        }

        /// <summary>The rugs laid out now: a Banner for each spot, or null for a bare spot.</summary>
        public void SetRugs(IReadOnlyList<Rules.Banner?> rugs)
        {
            for (int i = 0; i < _rugCards.Length; i++)
            {
                MeshRenderer r = _rugCards[i];
                if (r == null) continue;
                bool laid = rugs != null && i < rugs.Count && rugs[i].HasValue;
                r.gameObject.SetActive(laid);
                if (!laid) continue;
                Rules.Banner b = rugs[i].Value;
                int cell = b == Rules.Banner.Ember ? 0 : b == Rules.Banner.Sky ? 1 : b == Rules.Banner.Gold ? 2 : 3;
                r.GetPropertyBlock(_block);
                _block.SetVector(BaseMapSt, new Vector4(0.25f, 1f, cell * 0.25f, 0f));
                r.SetPropertyBlock(_block);
            }
        }

        private void Update()
        {
            if (!_shown) return;
            float t = Time.time;
            foreach ((Light light, float rest, float phase) in _flames)
                light.intensity = rest * (0.85f + Mathf.PerlinNoise(t * 3f + phase, phase) * 0.3f);
        }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            var paving = Art.Load<Material>("Town/Paving");
            if (paving != null) Part("Paving", Ground(), paving);
            var far = Art.Load<Material>("Town/Square");
            if (far != null) Part("FarSide", FarSide(), far);
            if (_rects == null || _folk == null || _props == null) return;
            Vector3 eye = CameraFrom;
            // Stations first, so the townsfolk stand in front of their own.
            Card("Forge", _props, _rects.props, 0, new Vector3(-4.15f, 0f, 6.2f), eye);
            Card("Stall", _props, _rects.props, 1, new Vector3(-1.75f, 0f, 8.6f), eye);
            Card("Lectern", _props, _rects.props, 2, new Vector3(2.0f, 0f, 8.2f), eye);
            Card("Rack", _props, _rects.props, 3, new Vector3(4.15f, 0f, 6.2f), eye);
            _bannerCloths.Add(Card("BannerL", _props, _rects.props, 4, new Vector3(-5.3f, 0f, 12.5f), eye));
            _bannerCloths.Add(Card("BannerR", _props, _rects.props, 4, new Vector3(5.3f, 0f, 12.5f), eye, mirror: true));
            Card("LanternL", _props, _rects.props, 5, new Vector3(-3.5f, 0f, 1.6f), eye, mirror: true);
            Card("LanternR", _props, _rects.props, 5, new Vector3(3.5f, 0f, 1.6f), eye);
            for (int i = 0; i < Folk.Length && i < _rects.folk.Length; i++)
            {
                Card("Folk" + i, _folk, _rects.folk, i, Folk[i], eye);
                Shade(Folk[i], 0.9f);
            }
            _heroShade = Shade(Vector3.zero, 0.95f);
            // The lanterns' glow on the stones, the forge's coals.
            Glow(new Vector3(-3.1f, 2.1f, 1.6f), new Color(1f, 0.62f, 0.3f), 1.3f, 5f);
            Glow(new Vector3(3.1f, 2.1f, 1.6f), new Color(1f, 0.62f, 0.3f), 1.3f, 5f);
            Glow(new Vector3(-3.95f, 0.6f, 5.8f), new Color(1f, 0.35f, 0.12f), 1.6f, 3.5f);
            for (int i = 0; i < RugSpots.Length && _rugs != null; i++)
            {
                GameObject rug = Part("Rug" + i, Flat, _rugs);
                rug.transform.localPosition = RugSpots[i] + Vector3.up * 0.012f;
                // The rug's length runs across the square; a little askew, as rugs are thrown down.
                rug.transform.localRotation = Quaternion.Euler(0f, 90f + (i % 2 == 0 ? -4f : 5f) + i, 0f);
                rug.transform.localScale = new Vector3(RugWidth, 1f, RugLength);
                _rugCards[i] = rug.GetComponent<MeshRenderer>();
                rug.SetActive(false);
            }
        }

        private GameObject Part(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>A painted card standing at a point, turned to face the camera (mirror: flipped left to right).</summary>
        private MeshRenderer Card(string name, Material material, Rect[] rects, int index, Vector3 at, Vector3 eye, bool mirror = false)
        {
            if (rects == null || index >= rects.Length) return null;
            Rect r = rects[index];
            GameObject go = Part(name, CardMesh, material);
            float width = r.height / Mathf.Max(0.2f, r.aspect);
            go.transform.localPosition = at;
            Vector3 away = at - eye;
            away.y = 0f;
            go.transform.localRotation = Quaternion.LookRotation(away.normalized, Vector3.up);
            go.transform.localScale = new Vector3(width * (mirror ? -1f : 1f), r.height, 1f);
            var renderer = go.GetComponent<MeshRenderer>();
            Show(renderer, rects, index);
            return renderer;
        }

        private void Show(MeshRenderer renderer, Rect[] rects, int index)
        {
            if (renderer == null || rects == null || index >= rects.Length) return;
            Rect r = rects[index];
            renderer.GetPropertyBlock(_block);
            _block.SetVector(BaseMapSt, new Vector4(r.u1 - r.u0, r.v1 - r.v0, r.u0, r.v0));
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>A soft dark patch on the stones under a figure, so the cards stand on the square.</summary>
        private Transform Shade(Vector3 at, float size)
        {
            if (_shade == null) return null;
            GameObject go = Part("Shade", Flat, _shade);
            go.transform.localPosition = at + Vector3.up * 0.02f;
            go.transform.localScale = new Vector3(size * 1.3f, 1f, size * 0.8f);
            return go.transform;
        }

        /// <summary>The hero's shade follows him across the square (his place off the spot).</summary>
        public void SetHeroAt(Vector3 local)
        {
            if (_heroShade != null) _heroShade.localPosition = local + Vector3.up * 0.02f;
        }

        private void Glow(Vector3 at, Color color, float intensity, float range)
        {
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(_root, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            _flames.Add((light, intensity, _flames.Count * 3.7f));
        }

        /// <summary>The paving: one broad quad from behind the camera to the painted far side, a stone tile every 3 m.</summary>
        private static Mesh Ground()
        {
            const float x0 = -30f, x1 = 30f, z0 = -20f, z1 = FarZ + 0.5f, tile = 3f;
            var mesh = new Mesh { name = "Paving" };
            mesh.vertices = new[] { new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), new Vector3(x1, 0f, z0) };
            mesh.uv = new[] { new Vector2(x0 / tile, z0 / tile), new Vector2(x0 / tile, z1 / tile), new Vector2(x1 / tile, z1 / tile), new Vector2(x1 / tile, z0 / tile) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The painted far side standing where the paving ends, its own painted paving cut away (FarCrop), so the
        /// halls and stalls stand on the square's stones.</summary>
        private static Mesh FarSide()
        {
            float height = FarWidth * 1152f / 2688f * (1f - FarCrop);
            float x0 = FarShift - FarWidth / 2f, x1 = FarShift + FarWidth / 2f, y0 = -0.05f, y1 = y0 + height, z = FarZ;
            var mesh = new Mesh { name = "FarSide" };
            mesh.vertices = new[] { new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x1, y1, z), new Vector3(x1, y0, z) };
            mesh.uv = new[] { new Vector2(0f, FarCrop), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, FarCrop) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A card one unit wide and tall, standing on its bottom edge, facing -z.</summary>
        private static Mesh CardMesh
        {
            get
            {
                if (_card != null) return _card;
                _card = new Mesh { name = "TownCard" };
                _card.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(-0.5f, 1f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(0.5f, 0f, 0f) };
                _card.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                _card.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                _card.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                _card.RecalculateBounds();
                return _card;
            }
        }

        /// <summary>A unit square lying on the ground, facing up (its v runs along +z).</summary>
        private static Mesh Flat
        {
            get
            {
                if (_flat != null) return _flat;
                _flat = new Mesh { name = "TownFlat" };
                _flat.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
                _flat.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                _flat.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                _flat.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                _flat.RecalculateBounds();
                return _flat;
            }
        }
    }
}
