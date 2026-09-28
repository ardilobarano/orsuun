using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Orsuun.Client
{
    /// <summary>
    /// Old Nergui's river as a place (owner, 28 Sep 2026: "the background of fishing map so bad, dont make it like a
    /// background. make it a reel place"). Built in 3D around the river stage (HeroStage from behind, the hero at the
    /// jetty's end facing +z): a planked jetty on posts running back past the camera, the water (Orsuun/Water: the painted
    /// horizon mirrored in its ripples, the sun's glitter, rings where the float lands and a fish bites), the left bank
    /// with its reeds and rocks, Old Nergui on his upturned boat by a fire (Models/Mobs/Nergui, from Tripo), and far off
    /// the painted dusk horizon (Content/River/Horizon) standing where the river ends. While it shows, the lane's cameras
    /// rest and the sun is the evening's, low ahead of the hero (Show / Hide put the lane's back).
    /// Everything is made here from the textures in Content/River (wood, ground, stone, reeds), so the scene needs no
    /// model but Nergui's.
    /// </summary>
    public sealed class RiverScene : MonoBehaviour
    {
        /// <summary>Where the water lies, below the jetty's deck (the hero stands at the stage's height).</summary>
        public const float WaterDrop = 0.45f;
        private const float HorizonZ = 150f, HorizonWidth = 130f;

        private Vector3 _at;
        private Material _water;
        private Transform _root;
        private readonly List<(Transform t, float phase, Quaternion rest)> _reeds = new List<(Transform, float, Quaternion)>();
        private Light _fire;
        private Transform[] _flames;
        private Vector3 _fireAt;
        private bool _shown;
        private Light _sun;
        private Quaternion _sunRotation;
        private Color _sunColor, _ambient;
        private float _sunIntensity;
        private SphericalHarmonicsL2 _ambientProbe;
        private Camera _laneCamera, _backdropCamera;

        public float WaterY => _at.y - WaterDrop;

        /// <summary>Where Old Nergui sits (world), for the -riverview nergui screenshot.</summary>
        public Vector3 NerguiAt { get; private set; }

        public void Init(Vector3 at)
        {
            _at = at;
            _root = new GameObject("RiverPlace").transform;
            _root.SetParent(transform, false);
            _root.position = at;
            Build();
            _root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ showing

        /// <summary>The river on screen: the lane's cameras rest and the evening sun is lit.</summary>
        public void Show()
        {
            if (_shown) return;
            _shown = true;
            _root.gameObject.SetActive(true);
            _laneCamera = GameObject.Find("LaneCamera")?.GetComponent<Camera>();
            _backdropCamera = GameObject.Find("BackdropCamera")?.GetComponent<Camera>();
            if (_laneCamera != null) _laneCamera.enabled = false;
            if (_backdropCamera != null) _backdropCamera.enabled = false;
            _sun = GameObject.Find("Sun")?.GetComponent<Light>();
            if (_sun != null)
            {
                _sunRotation = _sun.transform.rotation;
                _sunColor = _sun.color;
                _sunIntensity = _sun.intensity;
                // Low ahead of the hero and a little to the left, where the painted sun sets.
                _sun.transform.rotation = Quaternion.Euler(13f, 170f, 0f);
                _sun.color = new Color(1f, 0.7f, 0.46f);
                _sun.intensity = 1.35f;
            }
            _ambient = RenderSettings.ambientLight;
            _ambientProbe = RenderSettings.ambientProbe;
            SetAmbient(new Color(0.5f, 0.42f, 0.46f));
        }

        public void Hide()
        {
            if (!_shown) return;
            _shown = false;
            _root.gameObject.SetActive(false);
            if (_laneCamera != null) _laneCamera.enabled = true;
            if (_backdropCamera != null) _backdropCamera.enabled = true;
            if (_sun != null)
            {
                _sun.transform.rotation = _sunRotation;
                _sun.color = _sunColor;
                _sun.intensity = _sunIntensity;
            }
            RenderSettings.ambientLight = _ambient;
            RenderSettings.ambientProbe = _ambientProbe;
        }

        private static void SetAmbient(Color color)
        {
            RenderSettings.ambientLight = color;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(color);
            RenderSettings.ambientProbe = probe;
        }

        /// <summary>A ring spreading on the water from a point (the float landing, a fish biting).</summary>
        public void Ripple(Vector3 world, bool second = false)
        {
            if (_water == null) return;
            _water.SetVector(second ? "_Ripple2" : "_Ripple", new Vector4(world.x, world.y, world.z, Time.timeSinceLevelLoad));
        }

        private void Update()
        {
            if (!_shown) return;
            float t = Time.time;
            // Reeds lean in the evening wind, each on its own beat.
            foreach ((Transform reed, float phase, Quaternion rest) in _reeds)
                reed.localRotation = rest * Quaternion.Euler(Mathf.Sin(t * 1.3f + phase) * 3f, 0f, Mathf.Sin(t * 0.9f + phase * 1.7f) * 2.5f);
            if (_fire != null)
            {
                float flicker = Mathf.PerlinNoise(t * 3.1f, 0.3f);
                _fire.intensity = 1.6f + flicker * 1.4f;
                for (int i = 0; i < _flames.Length; i++)
                {
                    float s = 0.55f + Mathf.PerlinNoise(t * 4f + i * 7.3f, i) * 0.5f;
                    _flames[i].localScale = new Vector3(s * 0.8f, s * 1.3f, 1f) * 0.5f;
                    _flames[i].position = _fireAt + new Vector3((i - 1) * 0.08f, 0.18f + s * 0.12f, 0f);
                }
            }
        }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            Material wood = Art.Load<Material>("River/Wood"), ground = Art.Load<Material>("River/Ground"),
                stone = Art.Load<Material>("River/Stone"), reeds = Art.Load<Material>("River/Reeds");
            BuildWater();
            BuildHorizon();
            if (wood != null) Part("Jetty", Jetty(), wood);
            if (ground != null) Part("Bank", Bank(), ground);
            if (stone != null) Part("Rocks", Rocks(), stone);
            if (reeds != null) PlaceReeds(reeds);
            PlaceNergui();
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

        private void BuildWater()
        {
            var shared = Art.Load<Material>("River/Water");
            if (shared == null) return;
            _water = new Material(shared);
            var mesh = new Mesh { name = "Water" };
            // One big quad from behind the camera to the painted horizon; the shader does the rest.
            float y = -WaterDrop, x0 = -HorizonWidth, x1 = HorizonWidth, z0 = -40f, z1 = HorizonZ + 2f;
            mesh.vertices = new[] { new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.bounds = new Bounds(new Vector3(0, y, (z0 + z1) / 2), new Vector3(x1 - x0, 1, z1 - z0));
            GameObject go = Part("Water", mesh, _water);
            _water.SetTexture("_NormalMap", RippleNormals(256));
            _water.SetVector("_Origin", new Vector4(_at.x, _at.y, _at.z, 0));
            float height = HorizonWidth * 1152f / 2688f;
            _water.SetVector("_SkyRect", new Vector4(_at.x, _at.y + HorizonBottom, HorizonWidth, height));
            _water.SetFloat("_SkyZ", _at.z + HorizonZ);
            // Toward the painted sun (a little left of centre, low).
            _water.SetVector("_SunDir", new Vector4(-0.19f, 0.21f, 1f, 0f));
            _water.SetFloat("_WaveScale", 0.55f);
            _water.SetFloat("_WaveStrength", 0.2f);
            _water.SetFloat("_Reflect", 0.72f);
            go.name = "Water";
        }

        private const float HorizonBottom = -WaterDrop - 0.25f;

        private void BuildHorizon()
        {
            var mat = Art.Load<Material>("River/Horizon");
            if (mat == null) return;
            float height = HorizonWidth * 1152f / 2688f;
            var mesh = new Mesh { name = "Horizon" };
            float x0 = -HorizonWidth / 2, x1 = HorizonWidth / 2, y0 = HorizonBottom, y1 = HorizonBottom + height, z = HorizonZ;
            mesh.vertices = new[] { new Vector3(x0, y0, z), new Vector3(x0, y1, z), new Vector3(x1, y1, z), new Vector3(x1, y0, z) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            Part("Horizon", mesh, mat);
        }

        /// <summary>A tiling ripple normal map: a few crossing waves of whole periods, so it repeats seamlessly.</summary>
        private static Texture2D RippleNormals(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "RiverRipples", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var rng = new System.Random(7);
            const int waves = 9;
            var dirs = new Vector2[waves];
            var phases = new float[waves];
            var amps = new float[waves];
            for (int w = 0; w < waves; w++)
            {
                // Whole numbers of periods across the tile in each axis keep it seamless.
                dirs[w] = new Vector2(rng.Next(-4, 5), rng.Next(1, 6));
                phases[w] = (float)rng.NextDouble() * Mathf.PI * 2f;
                amps[w] = 1f / (1f + dirs[w].magnitude * 0.6f);
            }
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
                float dx = 0f, dy = 0f;
                for (int w = 0; w < waves; w++)
                {
                    float c = Mathf.Cos(dirs[w].x * u + dirs[w].y * v + phases[w]) * amps[w];
                    dx += c * dirs[w].x;
                    dy += c * dirs[w].y;
                }
                var n = new Vector3(-dx * 0.12f, -dy * 0.12f, 1f).normalized;
                pixels[y * size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.y * 0.5f + 0.5f) * 255), (byte)((n.z * 0.5f + 0.5f) * 255), 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        // ------------------------------------------------------------------ the jetty

        /// <summary>
        /// The jetty: planks across its width from the end (z +1.3, a little past the hero) back past the camera, each a
        /// strip of the wood texture, laid a little unevenly; two stringers under them; posts in pairs down into the river,
        /// the last pair standing up past the deck with rope wound round.
        /// </summary>
        private static Mesh Jetty()
        {
            var b = new MeshBuilder();
            var rng = new System.Random(3);
            const float halfWidth = 1.35f, plank = 0.26f, gap = 0.025f, thick = 0.07f;
            float z = 1.3f;
            int n = 0;
            while (z > -16f)
            {
                float jitter = (float)(rng.NextDouble() - 0.5);
                float length = halfWidth * 2f + jitter * 0.12f;
                var centre = new Vector3(jitter * 0.05f, -thick / 2f + (float)(rng.NextDouble() - 0.5) * 0.012f, z - plank / 2f);
                var turn = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 1.2f, (float)(rng.NextDouble() - 0.5) * 1.6f, (float)(rng.NextDouble() - 0.5) * 0.8f);
                // Each plank takes one of the texture's eight painted boards, along its grain.
                int board = rng.Next(8);
                b.Plank(centre, new Vector3(length, thick, plank), turn, board / 8f, (board + 1) / 8f, (float)rng.NextDouble());
                z -= plank + gap;
                n++;
            }
            // Stringers under the deck.
            foreach (float x in new[] { -1.05f, 1.05f })
                b.Plank(new Vector3(x, -0.2f, -7.5f), new Vector3(17.8f, 0.22f, 0.16f), Quaternion.Euler(0f, 90f, 0f), 0.25f, 0.375f, 0.3f);
            // Posts in pairs every three metres; the end pair stands 0.7 m above the deck.
            for (float pz = 1.15f; pz > -16f; pz -= 3f)
            {
                bool end = pz > 1f;
                foreach (float x in new[] { -halfWidth - 0.04f, halfWidth + 0.04f })
                {
                    float top = end ? 0.72f : 0.02f;
                    b.Post(new Vector3(x, (top - 2.6f) / 2f, pz), 0.12f + (float)rng.NextDouble() * 0.02f, top + 2.6f, 9);
                    if (end)
                        for (int wrap = 0; wrap < 4; wrap++)
                            b.Ring(new Vector3(x, 0.28f + wrap * 0.055f, pz), 0.14f, 0.022f, 12, 5);
                }
            }
            return b.Mesh("Jetty");
        }

        // ------------------------------------------------------------------ the bank

        /// <summary>The left bank: rolling ground rising from under the water to a low rise, its shoreline wavering in
        /// toward the jetty out along the river (where Nergui sits) and away behind.</summary>
        private static Mesh Bank()
        {
            const float x0 = -40f, x1 = -1.5f, z0 = -30f, z1 = 70f, step = 0.8f;
            int nx = Mathf.CeilToInt((x1 - x0) / step) + 1, nz = Mathf.CeilToInt((z1 - z0) / step) + 1;
            var vertices = new Vector3[nx * nz];
            var uv = new Vector2[nx * nz];
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float x = x0 + ix * step, z = z0 + iz * step;
                float shore = ShoreX(z);
                // Above the water inland, sloping under it at the shoreline.
                float inland = shore - x;
                float h = Mathf.Clamp(inland * 0.35f, -1.2f, 0.9f) - WaterDrop + 0.05f;
                h += (Mathf.PerlinNoise(x * 0.18f + 11f, z * 0.18f) - 0.5f) * 0.5f * Mathf.Clamp01(inland / 3f);
                vertices[iz * nx + ix] = new Vector3(x, h, z);
                uv[iz * nx + ix] = new Vector2(x * 0.28f, z * 0.28f);
            }
            var tris = new List<int>();
            for (int iz = 0; iz < nz - 1; iz++)
            for (int ix = 0; ix < nx - 1; ix++)
            {
                int a = iz * nx + ix, b = a + 1, c = a + nx, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
            var mesh = new Mesh { name = "Bank", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Where the left bank meets the water, by distance along the river.</summary>
        public static float ShoreX(float z) =>
            Mathf.Lerp(-3.2f, -2.2f, Mathf.Clamp01((z - 3f) / 7f)) + (Mathf.PerlinNoise(z * 0.12f, 3.7f) - 0.5f) * 1.2f;

        /// <summary>Ground height of the bank at a point (for placing things on it).</summary>
        private static float BankY(float x, float z)
        {
            float inland = ShoreX(z) - x;
            return Mathf.Clamp(inland * 0.35f, -1.2f, 0.9f) - WaterDrop + 0.05f
                   + (Mathf.PerlinNoise(x * 0.18f + 11f, z * 0.18f) - 0.5f) * 0.5f * Mathf.Clamp01(inland / 3f);
        }

        // ------------------------------------------------------------------ rocks and reeds

        private static Mesh Rocks()
        {
            var b = new MeshBuilder();
            var rng = new System.Random(11);
            // Half sunk at the shore and out in the stream on the right.
            var spots = new[]
            {
                new Vector4(ShoreX(3f) + 0.3f, -WaterDrop, 3f, 0.55f), new Vector4(ShoreX(9f) + 0.5f, -WaterDrop, 9f, 0.8f),
                new Vector4(ShoreX(19f) + 0.2f, -WaterDrop, 19f, 1.1f), new Vector4(ShoreX(28f) + 0.4f, -WaterDrop, 28f, 1.4f),
                new Vector4(2.3f, -WaterDrop, 8.5f, 0.5f), new Vector4(5.6f, -WaterDrop, 31f, 1.4f), new Vector4(-1.9f, -WaterDrop, 5.5f, 0.4f),
            };
            foreach (Vector4 s in spots) b.Rock(new Vector3(s.x, s.y, s.z), s.w, rng);
            return b.Mesh("Rocks");
        }

        private void PlaceReeds(Material material)
        {
            var rng = new System.Random(5);
            var spots = new List<Vector4>();
            // Along the left shore, thicker out along the river, and a few by the jetty's end.
            for (float z = -2f; z < 44f; z += 1.6f + (float)rng.NextDouble() * 1.6f)
            {
                float x = ShoreX(z) + (float)(rng.NextDouble() - 0.35) * 0.9f;
                spots.Add(new Vector4(x, Mathf.Max(-WaterDrop - 0.05f, BankY(x, z)), z, 1.1f + (float)rng.NextDouble() * 0.8f));
            }
            spots.Add(new Vector4(2.0f, -WaterDrop - 0.05f, 0.6f, 1.25f));
            spots.Add(new Vector4(2.55f, -WaterDrop - 0.05f, -1.2f, 1.45f));
            spots.Add(new Vector4(-2.05f, -WaterDrop - 0.05f, -0.4f, 1.1f));
            spots.Add(new Vector4(2.1f, -WaterDrop - 0.05f, 8.9f, 1.1f));
            spots.Add(new Vector4(5.2f, -WaterDrop - 0.05f, 30.4f, 1.5f));
            foreach (Vector4 s in spots)
            {
                var clump = new GameObject("Reeds").transform;
                clump.SetParent(_root, false);
                clump.localPosition = new Vector3(s.x, s.y, s.z);
                Quaternion rest = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f);
                clump.localRotation = rest;
                clump.localScale = Vector3.one * s.w;
                var go = clump.gameObject;
                go.AddComponent<MeshFilter>().sharedMesh = ReedCard;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                _reeds.Add((clump, (float)rng.NextDouble() * 6f, rest));
            }
        }

        private static Mesh _reedCard;

        /// <summary>Two crossed cards, the painted clump on each, standing on their bottom edge (0.5 m wide, 1 m tall).</summary>
        private static Mesh ReedCard
        {
            get
            {
                if (_reedCard != null) return _reedCard;
                var b = new MeshBuilder();
                for (int k = 0; k < 2; k++)
                {
                    Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
                    b.Quad(q * new Vector3(-0.25f, 0f, 0f), q * new Vector3(-0.25f, 1f, 0f), q * new Vector3(0.25f, 1f, 0f), q * new Vector3(0.25f, 0f, 0f));
                }
                return _reedCard = b.Mesh("ReedCard");
            }
        }

        // ------------------------------------------------------------------ Old Nergui

        /// <summary>Nergui's turn on the bank (the model faces its -x: turned so he looks out over the water, a little toward
        /// the jetty), and his fire's place in his own frame (the ring of stones by his right knee).</summary>
        private float NerguiYaw = 207f;
        private static readonly Vector3 FireOffset = new Vector3(0.24f, 0.06f, 0.52f);

        /// <summary>Old Nergui on his upturned boat on the left bank, out along the river, facing the water, his fire
        /// flickering (a warm light and three additive flames).</summary>
        private void PlaceNergui()
        {
            const float z = 10.5f;
            float x = ShoreX(z) - 0.95f;
            var prefab = Art.Load<GameObject>("Models/Mobs/Nergui");
            var material = Art.Load<Material>("Mobs/Nergui");
            var spot = new Vector3(x, BankY(x, z), z);
            NerguiAt = _root.TransformPoint(spot);
            string[] args = System.Environment.GetCommandLineArgs();
            int yawArg = System.Array.IndexOf(args, "-nerguiyaw");
            if (yawArg >= 0 && yawArg + 1 < args.Length && float.TryParse(args[yawArg + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y)) NerguiYaw = y;
            if (prefab != null)
            {
                GameObject nergui = Instantiate(prefab, _root);
                nergui.name = "Nergui";
                nergui.transform.localPosition = spot;
                nergui.transform.localRotation = Quaternion.Euler(0f, NerguiYaw, 0f);
                foreach (Renderer r in nergui.GetComponentsInChildren<Renderer>())
                {
                    if (material != null) r.sharedMaterial = material;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            // His fire, in front of the boat toward the water.
            _fireAt = _root.TransformPoint(spot + Quaternion.Euler(0f, NerguiYaw, 0f) * FireOffset);
            _fire = new GameObject("Fire").AddComponent<Light>();
            _fire.transform.SetParent(_root, false);
            _fire.transform.position = _fireAt + Vector3.up * 0.5f;
            _fire.type = LightType.Point;
            _fire.color = new Color(1f, 0.55f, 0.2f);
            _fire.range = 7f;
            _fire.intensity = 2f;
            var spark = Resources.Load<Material>("FxSpark");
            var glow = Resources.Load<Texture2D>("Fx/Glow");
            _flames = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var flame = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(flame.GetComponent<Collider>());
                flame.name = "Flame";
                flame.transform.SetParent(_root, false);
                var r = flame.GetComponent<Renderer>();
                if (spark != null)
                {
                    r.sharedMaterial = new Material(spark);
                    if (glow != null) r.sharedMaterial.SetTexture("_BaseMap", glow);
                    r.sharedMaterial.SetColor("_BaseColor", i == 1 ? new Color(1f, 0.75f, 0.35f) : new Color(1f, 0.42f, 0.12f));
                }
                r.shadowCastingMode = ShadowCastingMode.Off;
                // A quad shows its face toward -z: the river's camera is always behind the hero, looking up the river.
                _flames[i] = flame.transform;
            }
        }

        // ------------------------------------------------------------------ mesh helpers

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<Vector2> _uv = new List<Vector2>();
            private readonly List<int> _t = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2? uv0 = null, Vector2? uv1 = null)
            {
                Vector2 u0 = uv0 ?? Vector2.zero, u1 = uv1 ?? Vector2.one;
                int i = _v.Count;
                _v.AddRange(new[] { a, b, c, d });
                _uv.AddRange(new[] { new Vector2(u0.x, u0.y), new Vector2(u0.x, u1.y), new Vector2(u1.x, u1.y), new Vector2(u1.x, u0.y) });
                _t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            /// <summary>A box whose top and bottom show the texture strip u0..u1 along its length (x), the grain.</summary>
            public void Plank(Vector3 centre, Vector3 size, Quaternion turn, float u0, float u1, float v0)
            {
                Vector3 h = size / 2f;
                Vector3 P(float x, float y, float z) => centre + turn * new Vector3(x * h.x, y * h.y, z * h.z);
                float vLen = size.x * 0.45f;
                var top0 = new Vector2(u0, v0);
                var top1 = new Vector2(u1, v0 + vLen);
                // Top, bottom: the board's face; sides and ends: thin strips of the same board.
                Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), new Vector2(u0, v0), new Vector2(u1, v0 + vLen));
                Quad(P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), P(-1, -1, -1), top0, top1);
                float edge = (u1 - u0) * 0.25f;
                Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), new Vector2(u0, v0), new Vector2(u0 + edge, v0 + vLen));
                Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), new Vector2(u0, v0), new Vector2(u0 + edge, v0 + vLen));
                Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), new Vector2(u0, v0), new Vector2(u1, v0 + 0.02f));
                Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), new Vector2(u0, v0), new Vector2(u1, v0 + 0.02f));
            }

            /// <summary>An upright post (a many-sided cylinder), the wood's grain running up it.</summary>
            public void Post(Vector3 centre, float radius, float height, int sides)
            {
                float y0 = centre.y - height / 2f, y1 = centre.y + height / 2f;
                for (int s = 0; s < sides; s++)
                {
                    float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                    var p0 = new Vector3(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius);
                    var p1 = new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);
                    float u0 = 0.5f + s / (float)sides * 0.125f, u1 = 0.5f + (s + 1) / (float)sides * 0.125f;
                    Quad(centre + p1 + Vector3.up * (y0 - centre.y), centre + p1 + Vector3.up * (y1 - centre.y),
                        centre + p0 + Vector3.up * (y1 - centre.y), centre + p0 + Vector3.up * (y0 - centre.y),
                        new Vector2(u1, 0f), new Vector2(u0, height * 0.4f));
                }
                // The cut top.
                int c = _v.Count;
                _v.Add(new Vector3(centre.x, y1, centre.z));
                _uv.Add(new Vector2(0.56f, 0.5f));
                for (int s = 0; s <= sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    _v.Add(new Vector3(centre.x + Mathf.Cos(a) * radius, y1, centre.z + Mathf.Sin(a) * radius));
                    _uv.Add(new Vector2(0.56f + Mathf.Cos(a) * 0.05f, 0.5f + Mathf.Sin(a) * 0.05f));
                }
                for (int s = 0; s < sides; s++) _t.AddRange(new[] { c, c + 2 + s, c + 1 + s });
            }

            /// <summary>A rope ring round a post (a thin torus).</summary>
            public void Ring(Vector3 centre, float radius, float tube, int around, int across)
            {
                int start = _v.Count;
                for (int i = 0; i <= around; i++)
                {
                    float a = i * Mathf.PI * 2f / around;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    for (int j = 0; j <= across; j++)
                    {
                        float b = j * Mathf.PI * 2f / across;
                        _v.Add(centre + dir * (radius + Mathf.Cos(b) * tube) + Vector3.up * (Mathf.Sin(b) * tube));
                        _uv.Add(new Vector2(0.3f + j / (float)across * 0.02f, i / (float)around * 0.3f));
                    }
                }
                for (int i = 0; i < around; i++)
                for (int j = 0; j < across; j++)
                {
                    int a = start + i * (across + 1) + j, b = a + across + 1;
                    _t.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            }

            /// <summary>A lumpy river stone (a noisy sphere, flattened, sunk to a third of its height).</summary>
            public void Rock(Vector3 centre, float size, System.Random rng)
            {
                const int lat = 8, lon = 12;
                int start = _v.Count;
                float seed = (float)rng.NextDouble() * 50f;
                for (int i = 0; i <= lat; i++)
                {
                    float phi = i * Mathf.PI / lat;
                    for (int j = 0; j <= lon; j++)
                    {
                        float theta = j * Mathf.PI * 2f / lon;
                        var dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                        float bump = 0.78f + Mathf.PerlinNoise(dir.x * 1.8f + seed, dir.z * 1.8f + dir.y + seed) * 0.45f;
                        var p = new Vector3(dir.x * 1.15f, dir.y * 0.62f, dir.z) * (size * 0.5f * bump);
                        _v.Add(centre + p + Vector3.up * (size * 0.12f));
                        _uv.Add(new Vector2(j / (float)lon * 2f, i / (float)lat));
                    }
                }
                for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = start + i * (lon + 1) + j, b = a + lon + 1;
                    _t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            }

            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = _v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(_v);
                mesh.SetUVs(0, _uv);
                mesh.SetTriangles(_t, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
