using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Orsuun.Client
{
    /// <summary>
    /// A big open map (owner, 28 Sep 2026: "can we have a metin2 like big map maybe?"; shown a mockup, "Oathfields first"
    /// and the full map). A map is a wide painted field with a long trail looping round it (about 430 m on the
    /// Oathfields), a river with bridges where the trail crosses it, landmarks (a yurt village, a ruined watchtower, a
    /// bandit camp, a wolf den, a Korstone circle, birch groves), camps where monsters wait and other players hunt
    /// (FieldFolk), and grass, stones and cairns scattered everywhere. The hero tours the trail: he walks on while the
    /// lane runs between packs, and packs come at him from just up the trail, as roaming monsters do.
    /// It moves the map, not the hero: the lane's hero, fights and camera stay where they are, and the map slides and
    /// turns under him so the trail ahead always runs up the screen (the camera follows behind him round each bend).
    /// Presentation only: the lane's rules (LaneSim) and the server's replay are untouched.
    /// </summary>
    public sealed partial class FieldMap : MonoBehaviour
    {
        /// <summary>A place on a map (its own metres, x east, z north): a landmark's model, turn and height, a camp's or a
        /// named place's label (English, translated where shown), a grove's model, radius and count.</summary>
        [System.Serializable]
        public sealed class Spot
        {
            public string Name;
            public float X, Z, Yaw, Height, Radius;
            public int Count;
            /// <summary>A model's colour ("#RRGGBB", multiplied into its paint), so one model serves several maps.</summary>
            public string Tint;
            public Vector2 At => new Vector2(X, Z);
        }

        /// <summary>A map's layout (Resources/FieldMaps/&lt;name&gt;.json, also read by tools/art/field_map.py to paint its
        /// full map), in its own metres: x east, z north (a Vector2's y). The trail runs clockwise, so the loop's outside is
        /// on the hero's left, the side the camera looks across: landmarks, camps and groves stand out there, 7 to 25
        /// metres from the trail.</summary>
        [System.Serializable]
        public sealed class Layout
        {
            public string Name;
            /// <summary>Its file's name (Oathfields): the full map's painting is Content/FieldMaps/&lt;Key&gt;.</summary>
            [System.NonSerialized] public string Key;
            /// <summary>The campaign map it is drawn for (<see cref="Orsuun.Rules.MapDef.Id"/>).</summary>
            public int Map;
            /// <summary>The trail's control points, a closed loop (smoothed through them), and the river's course (open).</summary>
            public Vector2[] Trail, River;
            public float RiverWidth = 7f;
            /// <summary>The river's water and bank colours (a frozen stream, a lava channel).</summary>
            public string WaterColor = "#3D667A", BankColor = "#5C4D33";
            /// <summary>Flat patches on the ground, Name their kind (salt, snow, ice, water, mud, ash, lava, moss): an uneven
            /// ellipse of Radius by Height metres turned by Yaw. Water and ice lie in a rim of bank.</summary>
            public Spot[] Patches;
            /// <summary>Models from Content/Scenery/Models; where monsters wait and other players hunt; named places for the
            /// full map; groves of 3D props.</summary>
            public Spot[] Landmarks, Camps, Places, Groves;
            public string ScenerySet = "Steppe";
            /// <summary>The scenery model that rises in a ring round each Korstone on this map.</summary>
            public string KorstoneRing = "SteppeStone";
            /// <summary>Grass cards and stone models, scattered within <see cref="ScatterReach"/> metres of the trail (the
            /// camera never sees further).</summary>
            public int GrassCards = 900, Stones = 90;
            public float ScatterReach = 34f;
            /// <summary>The square the map covers (and its full map shows), centred on the origin.</summary>
            public float Size = 300f;
        }

        private static Dictionary<int, Layout> _layouts;

        /// <summary>The big map of a stage, or null (the lane keeps the road field there).</summary>
        public static Layout For(int stageNumber)
        {
            if (Orsuun.Rules.Content.IsZone(stageNumber) || Orsuun.Rules.Dungeons.IsFloor(stageNumber)) return null;
            if (_layouts == null)
            {
                _layouts = new Dictionary<int, Layout>();
                foreach (TextAsset file in Resources.LoadAll<TextAsset>("FieldMaps"))
                {
                    var layout = JsonUtility.FromJson<Layout>(file.text);
                    if (layout?.Trail == null || layout.Trail.Length < 4) continue;
                    layout.Key = file.name;
                    _layouts[layout.Map] = layout;
                }
            }
            return _layouts.TryGetValue(Orsuun.Rules.Content.MapOfStage(stageNumber).Id, out Layout found) ? found : null;
        }

        /// <summary>How fast the hero tours the trail while the lane runs (m/s) and how far a bend is looked ahead to turn.</summary>
        internal const float Speed = 7f;
        private const float TurnAhead = 6f, TrailWidth = 3.4f, Spacing = 1f;

        private Layout _layout;
        private string _key;
        private Transform _root;
        private readonly List<Vector3> _trail = new List<Vector3>();
        private readonly List<float> _along = new List<float>();
        private float _length, _s;
        private Vector3 _heading = Vector3.right;
        private Vector3 _hero;

        public bool Active => _root != null && _root.gameObject.activeSelf;
        public Layout Current => _layout;
        /// <summary>The map's root: its children stand in map space (FieldFolk puts other players at its camps).</summary>
        public Transform Root => _root;
        /// <summary>Where the hero is on the map (x, z metres) and which way he walks (degrees from north, clockwise).</summary>
        public Vector2 HeroOnMap => new Vector2(At(_s).x, At(_s).z);
        public float HeadingDegrees => Mathf.Atan2(_heading.x, _heading.z) * Mathf.Rad2Deg;
        public IReadOnlyList<Vector3> TrailPoints => _trail;

        /// <summary>Builds (or rebuilds) a map for the lane, the hero standing at <paramref name="hero"/> in the lane's space.</summary>
        public void Show(Layout layout, string backdropKey, Vector3 hero)
        {
            _hero = hero;
            if (layout == _layout && _root != null) { _root.gameObject.SetActive(true); return; }
            _key = backdropKey;
            Clear();
            _layout = layout;
            if (layout == null) return;
            _root = new GameObject("Map " + layout.Name).transform;
            _root.SetParent(transform, false);
            BuildTrail(layout);
            BuildGround(backdropKey);
            BuildTrailMesh(backdropKey);
            if (layout.River != null && layout.River.Length >= 2) BuildRiver(layout);
            if (layout.Patches != null) foreach (Spot patch in layout.Patches) Patch(patch);
            BuildLandmarks(layout);
            Scatter(layout);
            FlushBatches();
            _s = float.TryParse(Arg("-mapat"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                out float startAt) ? Mathf.Repeat(startAt, _length) : 0f;
            _heading = Heading(_s);
            Place();
        }

        /// <summary>Screenshots: -mapat &lt;m&gt; holds the hero that far along the trail; -mapview looks straight down on the map.</summary>
        private static string Arg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        internal static readonly bool Overview = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-mapview") >= 0;
        private static readonly bool Held = Arg("-mapat") != null;

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void Clear()
        {
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
            _layout = null;
            _trail.Clear();
            _along.Clear();
            // The meshes and materials made for the map go with it (they are not the scene's to free).
            foreach (Object made in _made) if (made != null) Destroy(made);
            _made.Clear();
            _batches.Clear();
        }

        /// <summary>A point on the trail <paramref name="ahead"/> metres on from the hero, and the way it runs there (map space).</summary>
        internal Vector3 OnTrail(float ahead) => At(_s + ahead);
        internal Vector3 AlongTrail(float ahead) => Heading(_s + ahead - TurnAhead);

        /// <summary>A frame: on the run the hero walks on and the map slides and turns under him.
        /// <paramref name="pace"/> is the hunt's speed (MENU's HUNT SPEED), so a walk between packs is as long at any speed.</summary>
        public void Tick(float dt, bool running, float pace = 1f)
        {
            if (!Active) return;
            if (running && !Held) _s = Mathf.Repeat(_s + Speed * pace * dt, _length);
            // The heading eases round bends (looked at a little ahead), so the camera swings rather than snaps.
            Vector3 want = Heading(_s);
            _heading = Vector3.Slerp(_heading, want, 1f - Mathf.Exp(-2.2f * dt)).normalized;
            Place();
            Camera cam = Camera.main;
            if (cam == null) return;
            if (Overview)
            {
                // The whole map from high above, north up, without the haze.
                _root.rotation = Quaternion.identity;
                _root.position = _hero - At(_s);
                cam.transform.SetPositionAndRotation(_hero - At(_s) + new Vector3(0f, 330f, 0f), Quaternion.Euler(90f, 0f, 0f));
                cam.farClipPlane = 600f;
                RenderSettings.fog = false;
            }
        }

        /// <summary>The map under the hero: its trail point at his feet, its heading along the lane's +x (up the screen).</summary>
        private void Place()
        {
            Quaternion turn = Quaternion.FromToRotation(new Vector3(_heading.x, 0f, _heading.z), Vector3.right);
            turn = Quaternion.Euler(0f, turn.eulerAngles.y, 0f);
            _root.rotation = turn;
            _root.position = _hero - turn * At(_s);
        }

        // ------------------------------------------------------------------ the trail

        private void BuildTrail(Layout layout)
        {
            Vector2[] p = layout.Trail;
            int n = p.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = p[(i - 1 + n) % n], b = p[i], c = p[(i + 1) % n], d = p[(i + 2) % n];
                float seg = Vector2.Distance(b, c);
                int steps = Mathf.Max(2, Mathf.CeilToInt(seg / Spacing));
                for (int k = 0; k < steps; k++)
                {
                    float t = k / (float)steps;
                    Vector2 q = CatmullRom(a, b, c, d, t);
                    _trail.Add(new Vector3(q.x, 0f, q.y));
                }
            }
            _along.Add(0f);
            for (int i = 1; i <= _trail.Count; i++)
                _along.Add(_along[i - 1] + Vector3.Distance(_trail[i - 1], _trail[i % _trail.Count]));
            _length = _along[_along.Count - 1];
        }

        private static Vector2 CatmullRom(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * b + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3);
        }

        /// <summary>The trail's point at a distance along it (map space).</summary>
        private Vector3 At(float s)
        {
            if (_trail.Count == 0) return Vector3.zero;
            s = Mathf.Repeat(s, _length);
            int lo = 0, hi = _along.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (_along[mid] <= s) lo = mid; else hi = mid;
            }
            float span = _along[lo + 1] - _along[lo];
            float t = span > 0f ? (s - _along[lo]) / span : 0f;
            return Vector3.Lerp(_trail[lo % _trail.Count], _trail[(lo + 1) % _trail.Count], t);
        }

        private Vector3 Heading(float s)
        {
            Vector3 d = At(s + TurnAhead) - At(s);
            d.y = 0f;
            return d.sqrMagnitude < 0.0001f ? Vector3.right : d.normalized;
        }

        /// <summary>The trail as a strip of the floor tile's road band, laid on the field.</summary>
        private void BuildTrailMesh(string key)
        {
            var tex = Art.Load<Texture2D>("Floors/" + key);
            var mat = NewLit();
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.92f));
            mat.SetFloat("_Smoothness", 0.05f);
            var vs = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            int n = _trail.Count;
            for (int i = 0; i <= n; i++)
            {
                Vector3 here = _trail[i % n], next = _trail[(i + 1) % n], prev = _trail[(i - 1 + n) % n];
                Vector3 dir = (next - prev).normalized;
                Vector3 side = new Vector3(-dir.z, 0f, dir.x) * (TrailWidth * 0.5f);
                float u = _along[Mathf.Min(i, _along.Count - 1)] / 6f;
                vs.Add(here + side + Vector3.up * 0.02f);
                vs.Add(here - side + Vector3.up * 0.02f);
                // The tile's road runs across its middle: the band 0.36-0.64 of it.
                uv.Add(new Vector2(u, 0.36f));
                uv.Add(new Vector2(u, 0.64f));
                if (i < n)
                {
                    int a = i * 2;
                    tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }
            var mesh = new Mesh { name = "Trail", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vs);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Part("Trail", mesh, mat);
        }

        private void BuildGround(string key)
        {
            var tex = Art.Load<Texture2D>("Floors/" + key + "Field");
            var mat = NewLit();
            float size = _layout.Size * 1.6f;
            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", new Vector2(size / 6f, size / 6f));
            }
            mat.SetColor("_BaseColor", new Color(0.9f, 0.9f, 0.9f));
            mat.SetFloat("_Smoothness", 0.04f);
            var mesh = new Mesh { name = "MapGround" };
            float h = size / 2f;
            mesh.vertices = new[] { new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), new Vector3(h, 0f, h), new Vector3(h, 0f, -h) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.RecalculateBounds();
            Part("Ground", mesh, mat);
        }

        // ------------------------------------------------------------------ the river and its bridges

        private void BuildRiver(Layout layout)
        {
            var course = new List<Vector3>();
            Vector2[] p = layout.River;
            for (int i = 0; i < p.Length - 1; i++)
            {
                Vector2 a = p[Mathf.Max(0, i - 1)], b = p[i], c = p[i + 1], d = p[Mathf.Min(p.Length - 1, i + 2)];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(b, c) / 2f));
                for (int k = 0; k < steps; k++)
                {
                    Vector2 q = CatmullRom(a, b, c, d, k / (float)steps);
                    course.Add(new Vector3(q.x, 0f, q.y));
                }
            }
            course.Add(new Vector3(p[p.Length - 1].x, 0f, p[p.Length - 1].y));
            Material water = Flat(ColorUtility.TryParseHtmlString(layout.WaterColor, out Color waterColor) ? waterColor : new Color(0.24f, 0.4f, 0.48f), 0.92f);
            Material bank = Flat(ColorUtility.TryParseHtmlString(layout.BankColor, out Color bankColor) ? bankColor : new Color(0.36f, 0.3f, 0.2f), 0.1f);
            Part("RiverBank", Strip(course, layout.RiverWidth + 2.4f, 0.01f), bank);
            Part("River", Strip(course, layout.RiverWidth, 0.03f), water);
            Reeds(course, layout.RiverWidth);
            // A bridge wherever the trail crosses the river.
            for (int i = 0; i < _trail.Count; i++)
            {
                Vector3 a = _trail[i], b = _trail[(i + 1) % _trail.Count];
                for (int j = 0; j < course.Count - 1; j++)
                    if (Cross(a, b, course[j], course[j + 1], out Vector3 hit))
                        Bridge(hit, (b - a).normalized, layout.RiverWidth + 4f);
            }
        }

        /// <summary>Clumps of the river scene's reeds along both banks, clear of the trail's crossings.</summary>
        private void Reeds(List<Vector3> course, float width)
        {
            var reeds = Art.Load<Material>("River/Reeds");
            if (reeds == null) return;
            var rng = new System.Random(course.Count * 31);
            var whole = new LaneScenery.Rect { u0 = 0f, u1 = 1f, v0 = 0f, v1 = 1f, aspect = 2f };
            for (int j = 0; j < course.Count - 1; j++)
                for (int k = 0; k < 2; k++)
                {
                    if (rng.NextDouble() < 0.35) continue;
                    Vector3 dir = (course[j + 1] - course[j]).normalized;
                    var side = new Vector3(-dir.z, 0f, dir.x) * (k == 0 ? 1f : -1f);
                    Vector3 at = course[j] + side * (width / 2f + 0.3f + (float)rng.NextDouble() * 1.4f) + dir * (float)rng.NextDouble() * 2f;
                    if (NearTrail(at, 5f)) continue;
                    Card(reeds, whole, at, 1.1f + (float)rng.NextDouble() * 0.6f, rng.Next(2) == 0, (float)rng.NextDouble() * 180f);
                }
        }

        private static bool Cross(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out Vector3 hit)
        {
            hit = Vector3.zero;
            float den = (b.x - a.x) * (d.z - c.z) - (b.z - a.z) * (d.x - c.x);
            if (Mathf.Abs(den) < 1e-5f) return false;
            float t = ((c.x - a.x) * (d.z - c.z) - (c.z - a.z) * (d.x - c.x)) / den;
            float u = ((c.x - a.x) * (b.z - a.z) - (c.z - a.z) * (b.x - a.x)) / den;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            hit = a + (b - a) * t;
            return true;
        }

        /// <summary>A plank bridge: boards across the way, two rails on posts.</summary>
        private void Bridge(Vector3 at, Vector3 along, float length)
        {
            var wood = Art.Load<Material>("River/Wood");
            Matrix4x4 bridge = Matrix4x4.TRS(at, Quaternion.LookRotation(along, Vector3.up), Vector3.one);
            Batch b = BatchFor(wood, null, at);
            void Box(Vector3 local, Vector3 size) =>
                b.Parts.Add(new CombineInstance { mesh = Cube, transform = bridge * Matrix4x4.TRS(local, Quaternion.identity, size) });
            for (float z = -length / 2f; z < length / 2f; z += 0.32f)
                Box(new Vector3(0f, 0.12f, z), new Vector3(TrailWidth + 0.6f, 0.08f, 0.28f));
            foreach (float x in new[] { -(TrailWidth / 2f + 0.25f), TrailWidth / 2f + 0.25f })
            {
                Box(new Vector3(x, 0.7f, 0f), new Vector3(0.12f, 0.1f, length));
                for (float z = -length / 2f; z <= length / 2f + 0.01f; z += length / 4f)
                    Box(new Vector3(x, 0.35f, z), new Vector3(0.16f, 0.8f, 0.16f));
            }
        }

        private static Mesh Strip(List<Vector3> course, float width, float y)
        {
            var vs = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < course.Count; i++)
            {
                Vector3 dir = (course[Mathf.Min(course.Count - 1, i + 1)] - course[Mathf.Max(0, i - 1)]).normalized;
                Vector3 side = new Vector3(-dir.z, 0f, dir.x) * (width * 0.5f);
                vs.Add(course[i] + side + Vector3.up * y);
                vs.Add(course[i] - side + Vector3.up * y);
                if (i < course.Count - 1)
                {
                    int a = i * 2;
                    tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
            }
            var mesh = new Mesh { name = "Strip", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ landmarks and scatter

        private void BuildLandmarks(Layout layout)
        {
            foreach (Spot spot in layout.Landmarks ?? new Spot[0])
                BatchModel(spot.Name, new Vector3(spot.X, 0f, spot.Z), spot.Yaw, spot.Height, spot.Tint);
            var rng = new System.Random(Seed(layout.Name));
            foreach (Spot grove in layout.Groves ?? new Spot[0])
                for (int i = 0; i < grove.Count; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = grove.Radius * Mathf.Sqrt((float)rng.NextDouble());
                    var at = new Vector3(grove.X + Mathf.Cos(a) * r, 0f, grove.Z + Mathf.Sin(a) * r);
                    float yaw = (float)rng.NextDouble() * 360f, grown = grove.Height * (0.8f + (float)rng.NextDouble() * 0.4f);
                    // No tree in the river or on the trail.
                    if (NearRiver(layout, at) || NearTrail(at, 3f)) continue;
                    BatchModel(grove.Name, at, yaw, grown, grove.Tint);
                }
        }

        /// <summary>A model standing on the map (a metre tall at scale 1, like the scenery's), or nothing without its art.</summary>
        /// <summary>A scenery model (Content/Scenery/Models, a metre tall at scale 1) under <paramref name="parent"/>, or null
        /// without its art.</summary>
        internal static Transform PlaceModel(Transform parent, string name, Vector3 at, float yaw, float height, string tint = null)
        {
            Color? shade = !string.IsNullOrEmpty(tint) && ColorUtility.TryParseHtmlString(tint, out Color c) ? c : (Color?)null;
            var prefab = Art.Load<GameObject>("Scenery/Models/" + name);
            if (prefab == null) return null;
            GameObject go = Instantiate(prefab, parent);
            go.name = name;
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * height;
            var material = Art.Load<Material>("Scenery/Models/" + name);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (material != null) r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (shade.HasValue)
                {
                    _tintBlock ??= new MaterialPropertyBlock();
                    r.GetPropertyBlock(_tintBlock);
                    _tintBlock.SetColor("_BaseColor", shade.Value);
                    r.SetPropertyBlock(_tintBlock);
                }
            }
            return go.transform;
        }

        /// <summary>Grass and shrub cards over the whole map, and the set's stones and cairns, clear of the trail.</summary>
        private void Scatter(Layout layout)
        {
            (Material material, LaneScenery.Rect[] props) = LaneScenery.SetOf(layout.ScenerySet);
            if (material == null || props == null) return;
            var rng = new System.Random(Seed(layout.Name) + 7);
            var cards = new List<LaneScenery.Rect>();
            var models = new List<LaneScenery.Rect>();
            foreach (LaneScenery.Rect r in props) (string.IsNullOrEmpty(r.model) ? cards : models).Add(r);
            int placed = 0, tries = 0;
            while (placed < layout.GrassCards && tries++ < layout.GrassCards * 4 && cards.Count > 0)
            {
                Vector3 at = BesideTrail(rng, 2.6f, layout.ScatterReach);
                if (NearTrail(at, 2.6f) || NearRiver(layout, at)) continue;
                LaneScenery.Rect r = cards[rng.Next(cards.Count)];
                Card(material, r, at, r.height * (0.8f + (float)rng.NextDouble() * 0.5f), rng.Next(2) == 0, (float)rng.NextDouble() * 180f);
                placed++;
            }
            placed = tries = 0;
            while (placed < layout.Stones && tries++ < layout.Stones * 4 && models.Count > 0)
            {
                Vector3 at = BesideTrail(rng, 3.5f, layout.ScatterReach);
                if (NearTrail(at, 3.5f) || NearRiver(layout, at)) continue;
                LaneScenery.Rect r = models[rng.Next(models.Count)];
                BatchModel(r.model, at, (float)rng.NextDouble() * 360f, r.height * 1.3f * (0.8f + (float)rng.NextDouble() * 0.4f), null);
                placed++;
            }
        }

        /// <summary>A random spot on either side of the trail, between <paramref name="near"/> and <paramref name="far"/> metres off it.</summary>
        private Vector3 BesideTrail(System.Random rng, float near, float far)
        {
            int i = rng.Next(_trail.Count);
            Vector3 dir = (_trail[(i + 1) % _trail.Count] - _trail[i]).normalized;
            var side = new Vector3(-dir.z, 0f, dir.x);
            // Denser close by, where the camera looks longest.
            float off = near + (far - near) * Mathf.Pow((float)rng.NextDouble(), 1.4f);
            return _trail[i] + side * (rng.Next(2) == 0 ? off : -off) + dir * (float)(rng.NextDouble() - 0.5) * 2f;
        }

        /// <summary>A painted card (grass, a shrub, reeds) as two crossed quads in its cell's batch: seen from any side, and
        /// never turned to the camera, so it draws with the rest.</summary>
        private void Card(Material material, LaneScenery.Rect r, Vector3 at, float height, bool flip, float yaw)
        {
            Batch b = BatchFor(material, null, at);
            float half = height / Mathf.Max(0.2f, r.aspect) / 2f;
            float u0 = flip ? r.u1 : r.u0, u1 = flip ? r.u0 : r.u1;
            for (int k = 0; k < 2; k++)
            {
                Vector3 side = Quaternion.Euler(0f, yaw + k * 90f, 0f) * Vector3.right * half, up = Vector3.up * height;
                int i0 = b.Verts.Count;
                b.Verts.Add(at - side);
                b.Verts.Add(at + side);
                b.Verts.Add(at + side + up);
                b.Verts.Add(at - side + up);
                b.Uvs.Add(new Vector2(u0, r.v0));
                b.Uvs.Add(new Vector2(u1, r.v0));
                b.Uvs.Add(new Vector2(u1, r.v1));
                b.Uvs.Add(new Vector2(u0, r.v1));
                // Lit from above like the ground they grow from, whichever side the camera sees.
                for (int v = 0; v < 4; v++) b.Normals.Add(Vector3.up);
                b.Tris.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
        }

        private bool NearTrail(Vector3 at, float clear)
        {
            float c2 = clear * clear;
            for (int i = 0; i < _trail.Count; i += 2)
                if ((_trail[i] - at).sqrMagnitude < c2) return true;
            return false;
        }

        private static MaterialPropertyBlock _tintBlock;

        // ------------------------------------------------------------------ patches on the ground

        /// <summary>A patch kind's colour and gloss.</summary>
        private static (Color color, float gloss) PatchLook(string kind) => kind switch
        {
            "salt" => (new Color(0.93f, 0.92f, 0.88f), 0.3f),
            "snow" => (new Color(0.92f, 0.95f, 1f), 0.2f),
            "ice" => (new Color(0.66f, 0.82f, 0.93f), 0.9f),
            "water" => (new Color(0.22f, 0.38f, 0.46f), 0.92f),
            "mud" => (new Color(0.3f, 0.24f, 0.18f), 0.35f),
            "ash" => (new Color(0.2f, 0.19f, 0.19f), 0.1f),
            "lava" => (new Color(1f, 0.42f, 0.08f), 0.6f),
            "moss" => (new Color(0.34f, 0.42f, 0.2f), 0.1f),
            _ => (new Color(0.5f, 0.45f, 0.35f), 0.1f),
        };

        /// <summary>A dry patch's tint over the map's own ground (so a salt pan or a mud patch keeps the ground's grain).</summary>
        private static Color GroundTint(string kind) => kind switch
        {
            "salt" => new Color(1.3f, 1.27f, 1.2f),
            "snow" => new Color(1.25f, 1.28f, 1.35f),
            "mud" => new Color(0.55f, 0.45f, 0.38f),
            "ash" => new Color(0.32f, 0.3f, 0.3f),
            "moss" => new Color(0.55f, 0.78f, 0.4f),
            _ => new Color(0.8f, 0.75f, 0.65f),
        };

        /// <summary>A flat patch: an uneven ellipse (its edge wanders, so it reads as a pan or a pool, not a disc). Water, ice
        /// and lava are smooth colour in a rim of bank; dry kinds are the map's ground, tinted.</summary>
        private void Patch(Spot spot)
        {
            (Color color, float gloss) = PatchLook(spot.Name);
            bool wet = spot.Name == "water" || spot.Name == "ice" || spot.Name == "lava";
            if (wet)
            {
                string rimKind = spot.Name == "ice" ? "snow" : spot.Name == "lava" ? "ash" : "mud";
                Part("PatchRim", Ellipse(spot, 1.15f, 0.012f), Ground(rimKind));
                Part("Patch", Ellipse(spot, 1f, 0.03f), Flat(color, gloss));
                return;
            }
            Part("Patch", Ellipse(spot, 1f, 0.015f), Ground(spot.Name));
        }

        private Material Ground(string kind)
        {
            var tex = Art.Load<Texture2D>("Floors/" + _key + "Field");
            if (tex == null) return Flat(PatchLook(kind).color, 0.1f);
            Material m = NewLit();
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", GroundTint(kind));
            m.SetFloat("_Smoothness", kind == "snow" || kind == "salt" ? 0.25f : 0.06f);
            return m;
        }

        private Material Flat(Color color, float gloss)
        {
            Material m = NewLit();
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", gloss);
            return m;
        }

        private static Mesh Ellipse(Spot spot, float grow, float y)
        {
            const int n = 40;
            var vs = new Vector3[n + 1];
            var tris = new int[n * 3];
            var q = Quaternion.Euler(0f, spot.Yaw, 0f);
            float rx = Mathf.Max(0.5f, spot.Radius) * grow, rz = Mathf.Max(0.5f, spot.Height > 0f ? spot.Height : spot.Radius) * grow;
            vs[0] = new Vector3(spot.X, y, spot.Z);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                // A wandering edge, the same each time the map is built.
                float wobble = 0.86f + 0.14f * Mathf.Sin(a * 3f + spot.X) * Mathf.Cos(a * 5f + spot.Z);
                vs[i + 1] = vs[0] + q * new Vector3(Mathf.Cos(a) * rx * wobble, 0f, Mathf.Sin(a) * rz * wobble);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = (i + 1) % n + 1;
                tris[i * 3 + 2] = i + 1;
            }
            // The ground's texture lies on it at the ground's own scale (a tile every 6 m).
            var uv = new Vector2[vs.Length];
            for (int i = 0; i < vs.Length; i++) uv[i] = new Vector2(vs[i].x / 6f, vs[i].z / 6f);
            var mesh = new Mesh { name = "Patch" };
            mesh.vertices = vs;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool NearRiver(Layout layout, Vector3 at)
        {
            if (layout.River == null) return false;
            Vector2[] p = layout.River;
            var q = new Vector2(at.x, at.z);
            for (int i = 0; i < p.Length - 1; i++)
            {
                Vector2 a = p[i], b = p[i + 1];
                float t = Mathf.Clamp01(Vector2.Dot(q - a, b - a) / Mathf.Max(0.001f, (b - a).sqrMagnitude));
                if (Vector2.Distance(q, a + (b - a) * t) < layout.RiverWidth) return true;
            }
            return false;
        }

        /// <summary>A seed from a name that is the same on every run and platform (string.GetHashCode is not).</summary>
        private static int Seed(string name)
        {
            int h = 17;
            foreach (char c in name ?? "") h = h * 31 + c;
            return h & 0x7fffffff;
        }

        /// <summary>A fresh Lit material, copied from one the art already carries (so the shader is in every build).</summary>
        private Material NewLit()
        {
            Material template = Art.Load<Material>("River/Ground");
            var m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", null);
            m.SetTextureScale("_BaseMap", Vector2.one);
            _made.Add(m);
            return m;
        }

        private void Part(string name, Mesh mesh, Material material)
        {
            _made.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
