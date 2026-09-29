using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace Orsuun.Client
{
    /// <summary>
    /// Trail caches on a big map (owner, 29 Sep 2026: "Trail caches"; Rules.TrailCaches). When the server says one is due, a
    /// small iron-bound chest lies beside the trail ahead, glowing and marked OPEN; the hero walks up to it and a tap opens
    /// it: the lid swings up in a burst of light and the server's roll comes in the lane's log. Passed unopened, it lies
    /// ahead again. Presentation and a request only: the server decides what is inside. -cacheshot shows one anyway.
    /// </summary>
    public sealed class TrailCache : MonoBehaviour
    {
        // Near enough to show below the HUD's banners at once (farther up the lane they cover it).
        private const float Ahead = 9f, Aside = 3f;

        private GameRoot _root;
        private LaneView _lane;
        private Transform _chest, _lid;
        private TextMesh _label;
        private ParticleSystem _glow;
        private Material _wood, _iron, _gold, _spark;
        private bool _opening;
        private float _openedAt = -1f;
        private readonly bool _shot = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-cacheshot") >= 0;

        public void Init(GameRoot root, LaneView lane)
        {
            _root = root;
            _lane = lane;
        }

        private void Update()
        {
            if (_root == null || _lane == null) return;
            FieldMap map = _lane.Map;
            if (_openedAt >= 0f)
            {
                // Open: the lid swings up, the light bursts, and the chest sinks away.
                float t = Time.time - _openedAt;
                if (_lid != null) _lid.localRotation = Quaternion.Euler(-Mathf.Min(1f, t * 3f) * 105f, 0f, 0f);
                if (_chest != null && t > 1.8f) _chest.localPosition += Vector3.down * Time.deltaTime * 0.9f;
                if (t > 3f) { Clear(); _openedAt = -1f; }
                return;
            }
            bool due = _shot || _root.Server.CacheReady;
            bool on = map != null && map.Active && due && !_root.Server.AtRiver && !_root.Town.IsOpen && !_root.Server.WaitingForHero;
            if (!on)
            {
                if (_chest != null && !_opening) Clear();
                return;
            }
            // Placed ahead when due, and again when the hero has walked past it unopened.
            if (_chest == null || _chest.parent != map.Root) Place(map, Ahead);
            else if (_chest.position.x < -9f) Place(map, Ahead + 4f);
            Camera cam = Camera.main;
            if (_label != null && cam != null)
            {
                _label.transform.rotation = cam.transform.rotation;
                _label.transform.localPosition = new Vector3(0f, 1.35f + Mathf.Sin(Time.time * 3f) * 0.1f, 0f);
            }
            if (!_opening && Input.GetMouseButtonDown(0) && Tapped(Input.mousePosition)) Open();
        }

        private bool Tapped(Vector3 screen)
        {
            if (EventSystem.current != null && (Input.touchCount > 0 ? EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)
                    : EventSystem.current.IsPointerOverGameObject())) return false;
            Camera cam = Camera.main;
            if (cam == null || !cam.enabled || _chest == null) return false;
            Vector3 low = cam.WorldToScreenPoint(_chest.position), high = cam.WorldToScreenPoint(_chest.position + Vector3.up * 2.2f);
            if (low.z <= 0f) return false;
            float reach = Mathf.Max(48f, Screen.height * 0.045f);
            return screen.y >= low.y - reach * 0.5f && screen.y <= high.y + reach * 0.3f && Mathf.Abs(screen.x - (low.x + high.x) / 2f) <= reach;
        }

        private void Open()
        {
            if (_shot && !_root.Server.CacheReady) { Burst(); return; }
            _opening = true;
            StartCoroutine(_root.Server.OpenCache((found, error) =>
            {
                _opening = false;
                if (error != null) { _root.Hud.Log(error); return; }
                Burst();
                _root.Hud.Log(Loc.T("Trail cache") + ": " + Loc.T(found));
                GameAudio.Instance?.Play("LaneLoot", 0.9f, 0.2f, 0f);
            }));
        }

        private void Burst()
        {
            _openedAt = Time.time;
            if (_label != null) _label.gameObject.SetActive(false);
            if (_glow == null) return;
            ParticleSystem.EmissionModule e = _glow.emission;
            e.rateOverTime = 0f;
            _glow.Emit(new ParticleSystem.EmitParams { startSize = 0.25f, startLifetime = 1.2f, applyShapeToPosition = true }, 40);
        }

        /// <summary>The chest on the trail's left, <paramref name="ahead"/> metres on, facing the trail.</summary>
        private void Place(FieldMap map, float ahead)
        {
            if (_chest == null) Build();
            Vector3 along = map.AlongTrail(ahead);
            var left = new Vector3(-along.z, 0f, along.x);
            _chest.SetParent(map.Root, false);
            _chest.localPosition = map.OnTrail(ahead) + left * Aside;
            _chest.localRotation = Quaternion.LookRotation(-left, Vector3.up);
        }

        private void Build()
        {
            // Warm oak (the river's weathered planks read grey at this distance).
            _wood ??= Plain(new Color(0.52f, 0.3f, 0.14f), 0.25f);
            _iron ??= Plain(new Color(0.22f, 0.21f, 0.2f), 0.35f);
            _gold ??= Plain(new Color(1f, 0.78f, 0.3f), 0.7f);
            _chest = new GameObject("TrailCache").transform;
            // The body, two iron bands, the lid on its hinge at the back, a gold lock at the front (+z faces the trail).
            Box(_chest, new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.6f, 0.7f), _wood);
            foreach (float x in new[] { -0.36f, 0.36f }) Box(_chest, new Vector3(x, 0.31f, 0f), new Vector3(0.09f, 0.63f, 0.73f), _iron);
            _lid = new GameObject("Lid").transform;
            _lid.SetParent(_chest, false);
            _lid.localPosition = new Vector3(0f, 0.6f, -0.35f);
            Box(_lid, new Vector3(0f, 0.11f, 0.36f), new Vector3(1.14f, 0.22f, 0.74f), _wood);
            foreach (float x in new[] { -0.36f, 0.36f }) Box(_lid, new Vector3(x, 0.115f, 0.36f), new Vector3(0.09f, 0.24f, 0.76f), _iron);
            Box(_chest, new Vector3(0f, 0.5f, 0.37f), new Vector3(0.16f, 0.2f, 0.06f), _gold);
            _chest.localScale = Vector3.one * 1.25f;

            var label = new GameObject("Open");
            label.transform.SetParent(_chest, false);
            _label = label.AddComponent<TextMesh>();
            _label.font = Ui.TitleFont;
            label.GetComponent<MeshRenderer>().sharedMaterial = Ui.TitleFont.material;
            _label.text = Loc.T("OPEN");
            _label.fontSize = 64;
            _label.fontStyle = FontStyle.Bold;
            _label.characterSize = 0.04f;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.color = Palette.Sorn;

            _glow = new GameObject("Glow").AddComponent<ParticleSystem>();
            _glow.transform.SetParent(_chest, false);
            _glow.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            _glow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _glow.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f, 0.9f), new Color(1f, 0.7f, 0.25f, 0.7f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 80;
            ParticleSystem.EmissionModule emission = _glow.emission;
            emission.rateOverTime = 10f;
            ParticleSystem.ShapeModule shape = _glow.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1f, 0.1f, 0.6f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var r = _glow.GetComponent<ParticleSystemRenderer>();
            var spark = Resources.Load<Material>("FxSpark");
            if (spark != null)
            {
                _spark ??= new Material(spark);
                var tex = Resources.Load<Texture2D>("Fx/Glow");
                if (tex != null) _spark.SetTexture("_BaseMap", tex);
                r.sharedMaterial = _spark;
            }
            r.shadowCastingMode = ShadowCastingMode.Off;
            _glow.Play();
        }

        private static void Box(Transform parent, Vector3 at, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = at;
            box.transform.localScale = size;
            var r = box.GetComponent<Renderer>();
            if (material != null) r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Material Plain(Color color, float gloss)
        {
            Material template = Art.Load<Material>("River/Ground");
            var m = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", gloss);
            return m;
        }

        private void Clear()
        {
            if (_chest != null) Destroy(_chest.gameObject);
            _chest = null;
            _lid = null;
            _label = null;
            _glow = null;
        }
    }
}
