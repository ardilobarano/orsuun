using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Orsuun.Client
{
    /// <summary>
    /// Weather and life on a big map (owner, 29 Sep 2026: "Weather and life"). Each layout names its air (Weather, one or two
    /// of motes, snow, embers, ash, fireflies, leaves, dust, midges, wisps, gold joined by '+'): particles over the part of
    /// the map the camera sees, simulated in the map's own space so they stay put on the ground as the hero walks and the
    /// map turns. Birds (a layout flag) cross high up in a flock now and then, flapping. Presentation only; graphics LOW or
    /// FEWER skill effects halve the air.
    /// </summary>
    public sealed class MapWeather : MonoBehaviour
    {
        /// <summary>The air over the camera's view, in the lane's space (the hero stands at x -1.6, the camera looks up +x).
        /// Sizes are large for what they are: the camera stands some 20 m off, where a real snowflake is under a pixel.</summary>
        private static readonly Vector3 AirCentre = new Vector3(8f, 0f, 5f), AirSize = new Vector3(44f, 0f, 32f);

        private sealed class Air
        {
            public string Texture = "Fx/Glow";
            public bool Additive = true;
            public Color From = Color.white, To = Color.white;
            public float SizeMin = 0.15f, SizeMax = 0.3f, Rate = 30f, LifeMin = 6f, LifeMax = 10f;
            /// <summary>Where they start: 0 near the ground, 1 spread up to the top, 2 at the top (falling kinds).</summary>
            public int From0 = 1;
            public Vector3 Velocity;
            public float Noise, Spin, Pulse;
            public int Max = 500;
            /// <summary>Falls between the camera and the hero (snow: far flakes vanish against snow on the ground).</summary>
            public bool Near;
        }

        private static readonly Vector3 NearCentre = new Vector3(-2f, 0f, -3f), NearSize = new Vector3(30f, 0f, 22f);

        private static Air Preset(string kind) => kind switch
        {
            // Pale seed fluff drifting over the golden grass (light added to gold would not show).
            "motes" => new Air { Additive = false, From = new Color(1f, 1f, 0.95f, 0.9f), To = new Color(0.95f, 0.92f, 0.8f, 0.7f), SizeMin = 0.2f, SizeMax = 0.45f, Rate = 22f, Noise = 0.25f, Near = true,
                Velocity = new Vector3(0.2f, 0.05f, 0.1f) },
            // A little blue-grey, or it vanishes against the snow on the ground.
            "snow" => new Air { Additive = false, From = new Color(0.9f, 0.94f, 1f, 1f), To = new Color(0.72f, 0.8f, 0.94f, 0.95f), SizeMin = 0.25f,
                SizeMax = 0.55f, Rate = 110f, LifeMin = 7f, LifeMax = 9f, From0 = 2, Velocity = new Vector3(0.4f, -1.2f, 0.2f), Noise = 0.35f, Max = 900, Near = true },
            "embers" => new Air { From = new Color(1f, 0.6f, 0.18f, 1f), To = new Color(1f, 0.32f, 0.08f, 0.8f), SizeMin = 0.18f, SizeMax = 0.38f,
                Rate = 40f, LifeMin = 4f, LifeMax = 7f, From0 = 0, Velocity = new Vector3(0.3f, 0.9f, 0f), Noise = 0.6f },
            "ash" => new Air { Additive = false, From = new Color(0.25f, 0.23f, 0.22f, 0.85f), To = new Color(0.45f, 0.42f, 0.4f, 0.7f),
                SizeMin = 0.2f, SizeMax = 0.35f, Rate = 50f, LifeMin = 9f, LifeMax = 12f, From0 = 2, Velocity = new Vector3(0.5f, -0.5f, 0f), Noise = 0.5f, Spin = 90f },
            "fireflies" => new Air { From = new Color(0.75f, 1f, 0.35f, 0.95f), To = new Color(1f, 0.95f, 0.4f, 0.8f), SizeMin = 0.2f, SizeMax = 0.35f,
                Rate = 14f, LifeMin = 6f, LifeMax = 10f, From0 = 0, Velocity = new Vector3(0f, 0.15f, 0f), Noise = 0.9f, Pulse = 3f },
            "leaves" => new Air { Texture = "leaf", Additive = false, From = new Color(0.78f, 0.14f, 0.1f), To = new Color(0.95f, 0.38f, 0.14f),
                SizeMin = 0.4f, SizeMax = 0.65f, Rate = 26f, LifeMin = 11f, LifeMax = 14f, From0 = 2, Velocity = new Vector3(0.6f, -0.7f, 0.2f),
                Noise = 0.8f, Spin = 160f },
            "dust" => new Air { Additive = false, From = new Color(0.92f, 0.82f, 0.62f, 0.22f), To = new Color(0.85f, 0.75f, 0.55f, 0.12f),
                SizeMin = 1.25f, SizeMax = 2.75f, Rate = 30f, LifeMin = 5f, LifeMax = 8f, From0 = 0, Velocity = new Vector3(2.4f, 0.1f, 0.6f), Noise = 0.3f },
            "midges" => new Air { Additive = false, From = new Color(0.1f, 0.1f, 0.08f, 0.8f), To = new Color(0.2f, 0.2f, 0.15f, 0.6f),
                SizeMin = 0.1f, SizeMax = 0.15f, Rate = 60f, LifeMin = 4f, LifeMax = 7f, From0 = 0, Noise = 1.4f },
            "wisps" => new Air { From = new Color(0.7f, 0.5f, 1f, 0.8f), To = new Color(0.5f, 0.6f, 1f, 0.5f), SizeMin = 0.18f, SizeMax = 0.4f,
                Rate = 20f, LifeMin = 6f, LifeMax = 9f, From0 = 0, Velocity = new Vector3(0f, 0.45f, 0f), Noise = 0.5f, Pulse = 1.5f },
            "gold" => new Air { From = new Color(1f, 0.82f, 0.35f, 0.7f), To = new Color(1f, 0.7f, 0.2f, 0.4f), SizeMin = 0.18f, SizeMax = 0.32f,
                Rate = 24f, Noise = 0.3f, Velocity = new Vector3(0f, 0.1f, 0f) },
            _ => null,
        };

        private readonly List<ParticleSystem> _air = new List<ParticleSystem>();
        private readonly List<Object> _made = new List<Object>();
        private ParticleSystem _birds;
        private string _shown;
        private float _nextFlock;
        private bool _caws;
        private static Texture2D _leaf, _bird;

        /// <summary>The air and birds of a map (null or "" for none); rebuilt only when they change.</summary>
        public void Show(FieldMap map)
        {
            FieldMap.Layout layout = map != null && map.Active ? map.Current : null;
            string want = layout == null ? "" : (layout.Weather ?? "") + (layout.Birds ? "|birds" : "");
            if (want == _shown) return;
            Clear();
            _shown = want;
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-perflog") >= 0) Debug.Log("PERF weather " + want);
            if (layout == null) return;
            foreach (string kind in (layout.Weather ?? "").Split('+'))
            {
                Air air = Preset(kind.Trim());
                if (air != null) _air.Add(MakeAir(air, map.Root));
            }
            _caws = layout.ScenerySet != "Steppe" || (layout.Weather ?? "").Contains("embers");
            if (layout.Birds)
            {
                _birds = MakeBirds();
                _nextFlock = Time.time + Random.Range(1.5f, 4f);
            }
        }

        private void Clear()
        {
            foreach (ParticleSystem ps in _air) if (ps != null) Destroy(ps.gameObject);
            _air.Clear();
            if (_birds != null) Destroy(_birds.gameObject);
            _birds = null;
            foreach (Object o in _made) if (o != null) Destroy(o);
            _made.Clear();
            _shown = null;
        }

        private Material Material(string texture, bool additive)
        {
            var spark = Resources.Load<Material>("FxSpark");
            if (spark == null) return null;
            var m = new Material(spark);
            _made.Add(m);
            Texture tex = texture == "leaf" ? Leaf() : texture == "bird" ? Bird() : Resources.Load<Texture2D>(texture);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            if (!additive)
            {
                // The spark is additive (light only adds): dark leaves, ash and birds blend over what is behind them.
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }
            return m;
        }

        private ParticleSystem MakeAir(Air air, Transform mapRoot)
        {
            var ps = new GameObject("Air").AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform, false);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            float share = GameSettings.Graphics == GameSettings.Quality.Low || GameSettings.FewerEffects ? 0.5f : 1f;
            float top = air.From0 == 2 ? 9f : air.From0 == 1 ? 3f : 0.4f;
            ps.transform.localPosition = (air.Near ? NearCentre : AirCentre) + new Vector3(0f, top, 0f);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(air.LifeMin, air.LifeMax);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(air.SizeMin, air.SizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(air.From, air.To);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = Mathf.RoundToInt(air.Max * share);
            // In the map's own space: what lies on the air stays over the same ground as the map slides and turns.
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            main.customSimulationSpace = mapRoot;
            main.prewarm = true;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = air.Rate * share;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            Vector3 area = air.Near ? NearSize : AirSize;
            shape.scale = new Vector3(area.x, air.From0 == 1 ? 6f : 1f, area.z);
            ParticleSystem.VelocityOverLifetimeModule v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.World;
            v.x = air.Velocity.x;
            v.y = air.Velocity.y;
            v.z = air.Velocity.z;
            if (air.Noise > 0f)
            {
                ParticleSystem.NoiseModule noise = ps.noise;
                noise.enabled = true;
                noise.strength = air.Noise;
                noise.frequency = 0.35f;
                noise.scrollSpeed = 0.2f;
                noise.quality = ParticleSystemNoiseQuality.Low;
            }
            if (air.Spin > 0f)
            {
                ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(-air.Spin * Mathf.Deg2Rad, air.Spin * Mathf.Deg2Rad);
            }
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            if (air.Pulse > 0f)
            {
                // Fireflies and wisps swell and fade as they drift.
                ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
                size.enabled = true;
                var curve = new AnimationCurve();
                for (int k = 0; k <= 8; k++) curve.AddKey(k / 8f, 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(k / 8f * Mathf.PI * air.Pulse)));
                size.size = new ParticleSystem.MinMaxCurve(1f, curve);
            }
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Material(air.Texture, air.Additive);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        /// <summary>A flock's birds: flapping dark silhouettes (two frames) crossing high over the map.</summary>
        private ParticleSystem MakeBirds()
        {
            var ps = new GameObject("Birds").AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform, false);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            // Playing all along with no emission of its own (a stopped system would not move what Update emits).
            main.loop = true;
            main.startLifetime = 18f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1f, 1.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.08f, 0.07f, 0.07f, 0.9f), new Color(0.15f, 0.13f, 0.12f, 0.85f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 1;
            sheet.cycleCount = 40;
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.2f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Material("bird", false);
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        private void Update()
        {
            if (_birds == null || Time.time < _nextFlock) return;
            _nextFlock = Time.time + Random.Range(18f, 34f);
            // Their calls (MapSounds' flock one-shots): crows and carrion birds over harder ground, songbirds over the steppe.
            GameAudio.Instance?.Play(_caws ? "BirdsCaw" : "BirdsSong", 0.55f, 5f, 0.08f);
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-perflog") >= 0) Debug.Log("PERF flock");
            // A loose V from one side of the view to the other, high over the far ground.
            bool fromLeft = Random.value < 0.5f;
            var heading = new Vector3(fromLeft ? 1f : -1f, 0f, Random.Range(-0.35f, 0.35f)).normalized;
            // Across the middle of the view, some 12 m in front of the camera: higher up the screen is behind the HUD's banners.
            Vector3 start = new Vector3(fromLeft ? -30f : 24f, Random.Range(4.8f, 6.2f), Random.Range(-6f, -1f));
            int count = Random.Range(5, 10);
            for (int i = 0; i < count; i++)
            {
                var side = new Vector3(-heading.z, 0f, heading.x);
                var emit = new ParticleSystem.EmitParams
                {
                    position = transform.TransformPoint(start - heading * (Mathf.Abs(i - count / 2) * 1.4f) + side * ((i - count / 2) * 1.2f)
                                                        + Vector3.up * Random.Range(-0.4f, 0.4f)),
                    velocity = heading * Random.Range(3.4f, 4.2f),
                    applyShapeToPosition = false,
                };
                _birds.Emit(emit, 1);
            }
        }

        private void OnDestroy() => Clear();

        /// <summary>A leaf: a pointed oval with a darker vein, white on clear (tinted by each particle).</summary>
        private static Texture2D Leaf()
        {
            if (_leaf != null) return _leaf;
            const int n = 64;
            _leaf = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Leaf", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    // Pointed at both ends: the half width shrinks toward the tips.
                    float half = 0.42f * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)) * (1f - 0.35f * Mathf.Abs(v));
                    float edge = half - Mathf.Abs(u);
                    float a = Mathf.Clamp01(edge * 30f) * (Mathf.Abs(v) < 0.95f ? 1f : 0f);
                    float shade = Mathf.Abs(u) < 0.03f ? 0.6f : 1f - 0.25f * Mathf.Abs(u) / 0.42f;
                    _leaf.SetPixel(x, y, new Color(shade, shade, shade, a));
                }
            _leaf.Apply();
            return _leaf;
        }

        /// <summary>A bird in two frames side by side: wings up, wings level.</summary>
        private static Texture2D Bird()
        {
            if (_bird != null) return _bird;
            const int w = 128, h = 64;
            _bird = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Bird", wrapMode = TextureWrapMode.Clamp };
            var clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) _bird.SetPixel(x, y, clear);
            for (int frame = 0; frame < 2; frame++)
            {
                float lift = frame == 0 ? 0.55f : 0.08f;
                for (int x = 4; x < 60; x++)
                {
                    float t = (x - 32f) / 28f;
                    // Each wing a curve from the body out and up (or level), thinning toward its tip.
                    float yWing = 32f + Mathf.Abs(t) * lift * 26f - Mathf.Abs(t) * Mathf.Abs(t) * 6f;
                    float thick = 3.2f * (1f - Mathf.Abs(t)) + 0.8f;
                    for (int y = 0; y < h; y++)
                    {
                        float d = Mathf.Abs(y - yWing);
                        if (d < thick) _bird.SetPixel(frame * 64 + x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(thick - d)));
                    }
                }
                // The body.
                for (int y = 28; y < 36; y++) for (int x = 29; x < 35; x++) _bird.SetPixel(frame * 64 + x, y, Color.white);
            }
            _bird.Apply();
            return _bird;
        }
    }
}
