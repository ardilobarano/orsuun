using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// How a Korstone looks at its level (owner, 24 Sep 2026: "cooler and scarier; change colour and shades as the
    /// stones level up"). Five tiers of twenty levels, each with its own crack colour and stone shade; three carved
    /// shapes (runed monolith, chained twin spire, crowned obelisk) that grow more dreadful with the tier, and an Elder
    /// takes the next shape up. The molten cracks painted into each shape's texture are repainted in the tier colour by
    /// EmberGlow's _CrackRemap.
    /// </summary>
    public static class KorstoneLook
    {
        public readonly struct Tier
        {
            public Tier(string name, Color glow, Color hot, Color stone, float intensity)
            {
                Name = name;
                Glow = glow;
                Hot = hot;
                Stone = stone;
                Intensity = intensity;
            }

            public string Name { get; }
            public Color Glow { get; }
            public Color Hot { get; }
            /// <summary>Multiplies the stone's texture: warm soot, blood-black, void-black, grave-black, pitch.</summary>
            public Color Stone { get; }
            public float Intensity { get; }
        }

        public static readonly Tier[] Tiers =
        {
            new Tier("Ember", new Color(1f, 0.40f, 0.06f), new Color(1f, 0.78f, 0.35f), new Color(0.55f, 0.45f, 0.40f), 1.7f),
            new Tier("Blood", new Color(0.95f, 0.05f, 0.04f), new Color(1f, 0.35f, 0.25f), new Color(0.45f, 0.28f, 0.28f), 2.0f),
            new Tier("Void", new Color(0.55f, 0.10f, 1f), new Color(0.88f, 0.62f, 1f), new Color(0.32f, 0.27f, 0.42f), 2.2f),
            new Tier("Grave", new Color(0.12f, 1f, 0.50f), new Color(0.72f, 1f, 0.86f), new Color(0.24f, 0.32f, 0.30f), 2.2f),
            new Tier("Khan", new Color(1f, 0.70f, 0.12f), new Color(1f, 1f, 0.85f), new Color(0.18f, 0.17f, 0.16f), 2.6f),
        };

        private static readonly string[] Shapes = { "A", "A", "B", "B", "C" };

        /// <summary>Tier from the stage's level: 1-20 Ember, 21-40 Blood, 41-60 Void, 61-80 Grave, 81+ Khan.</summary>
        public static int TierFor(int level) => Mathf.Clamp((level - 1) / 20, 0, Tiers.Length - 1);

        public static string ShapeFor(int tier, bool elder) => elder ? Shapes[Mathf.Min(Shapes.Length - 1, tier + 2)] : Shapes[tier];
    }

    /// <summary>
    /// The living parts of a Korstone: shards orbiting its crown, embers rising from its base, a pulsing ground glow and
    /// light in the tier colour, cracks that flare when struck, and a ring of sparks racing out on every wave.
    /// </summary>
    public sealed class KorstoneFx : MonoBehaviour
    {
        private Material _stone;
        private float _baseIntensity;
        private float _flare;
        private Transform[] _shards;
        private float[] _phase;
        private float[] _radius;
        private float[] _heightFrac;
        private Light _light;
        private Material _groundGlow;
        private Color _glow;
        private ParticleSystem _ring;
        private float _height;

        /// <summary>Builds the effects around a stone `height` world units tall, in the tier's colour.</summary>
        public void Init(Material stone, KorstoneLook.Tier tier, float height, int tierIndex, Material spark)
        {
            _stone = stone;
            _baseIntensity = tier.Intensity;
            _glow = tier.Glow;
            _height = height;

            // Shards orbit the upper half; the higher the tier, the more of them. They are plain dark rock with thin
            // seams in the tier colour (the stone's texture on a cube would glow all over).
            var shardMaterial = new Material(stone);
            shardMaterial.SetTexture("_BaseMap", null);
            shardMaterial.SetColor("_Tint", new Color(0.06f, 0.055f, 0.06f));
            shardMaterial.SetFloat("_CrackRemap", 0f);
            shardMaterial.SetFloat("_CrackAlways", 1f);
            shardMaterial.SetFloat("_CrackScale", 9f);
            shardMaterial.SetFloat("_CrackWidth", 0.03f);
            int count = 4 + tierIndex * 2;
            _shards = new Transform[count];
            _phase = new float[count];
            _radius = new float[count];
            _heightFrac = new float[count];
            for (int i = 0; i < count; i++)
            {
                GameObject shard = LaneView.MakePrimitive(PrimitiveType.Cube, "Shard");
                shard.transform.SetParent(transform, false);
                float s = Random.Range(0.10f, 0.26f);
                shard.transform.localScale = new Vector3(s, s * Random.Range(1.4f, 2.6f), s) / transform.localScale.x;
                shard.transform.localRotation = Random.rotation;
                shard.GetComponent<Renderer>().sharedMaterial = shardMaterial;
                _shards[i] = shard.transform;
                _phase[i] = i * Mathf.PI * 2f / count + Random.Range(-0.3f, 0.3f);
                _radius[i] = Random.Range(0.75f, 1.25f);
                _heightFrac[i] = Random.Range(0.45f, 0.95f);
            }

            if (spark != null)
            {
                // Ground glow: a flat additive disc under the stone.
                GameObject disc = LaneView.MakePrimitive(PrimitiveType.Quad, "GroundGlow");
                disc.transform.SetParent(transform, false);
                disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                disc.transform.localScale = Vector3.one * (4.5f / transform.localScale.x);
                _groundGlow = new Material(spark);
                disc.GetComponent<Renderer>().sharedMaterial = _groundGlow;
                disc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // Embers and motes rising from the base.
                var aura = new GameObject("Aura").AddComponent<ParticleSystem>();
                aura.transform.SetParent(transform, false);
                aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = aura.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
                main.startColor = new ParticleSystem.MinMaxGradient(tier.Glow, tier.Hot);
                main.gravityModifier = -0.06f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 160;
                ParticleSystem.EmissionModule emission = aura.emission;
                emission.rateOverTime = 10f + tierIndex * 5f;
                ParticleSystem.ShapeModule shape = aura.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 1.1f;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                ParticleSystem.ColorOverLifetimeModule fade = aura.colorOverLifetime;
                fade.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
                fade.color = g;
                var r = aura.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = spark;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                aura.Play();

                // The wave ring: bursts along the ground.
                _ring = new GameObject("WaveRing").AddComponent<ParticleSystem>();
                _ring.transform.SetParent(transform, false);
                _ring.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule rm = _ring.main;
                rm.loop = false;
                rm.startLifetime = 0.7f;
                rm.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
                rm.simulationSpace = ParticleSystemSimulationSpace.World;
                rm.maxParticles = 200;
                ParticleSystem.EmissionModule re = _ring.emission;
                re.enabled = false;
                ParticleSystem.ColorOverLifetimeModule rf = _ring.colorOverLifetime;
                rf.enabled = true;
                rf.color = g;
                var rr = _ring.GetComponent<ParticleSystemRenderer>();
                rr.sharedMaterial = spark;
                rr.renderMode = ParticleSystemRenderMode.Stretch;
                rr.velocityScale = 0.08f;
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _ring.Play();
            }

            _light = new GameObject("KorstoneLight").AddComponent<Light>();
            _light.transform.SetParent(transform, false);
            _light.transform.localPosition = new Vector3(0f, 1.2f / transform.localScale.x, -0.8f / transform.localScale.x);
            _light.type = LightType.Point;
            _light.color = tier.Glow;
            _light.range = 6f + tierIndex;
            _light.shadows = LightShadows.None;
        }

        /// <summary>Struck: the cracks blaze for a moment.</summary>
        public void Flare() => _flare = 1f;

        /// <summary>A wave of Hollowed bursts out: a ring of sparks races across the ground.</summary>
        public void Wave(int particles = 70, float speed = 7f)
        {
            if (_ring == null) return;
            Vector3 centre = transform.position + Vector3.up * 0.15f;
            var emit = new ParticleSystem.EmitParams { startColor = _glow };
            for (int i = 0; i < particles; i++)
            {
                float a = i * Mathf.PI * 2f / particles;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                emit.position = centre + dir * 0.6f;
                emit.velocity = dir * speed * Random.Range(0.85f, 1.15f) + Vector3.up * 0.4f;
                _ring.Emit(emit, 1);
            }
            _flare = 1f;
        }

        /// <summary>Editor stills: place the shards and light as a running frame would, and grow the ember aura.</summary>
        public void Settle()
        {
            Step(1.3f, 0f);
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>()) ps.Simulate(2.5f, true, true);
        }

        private void Update() => Step(Time.time, Time.deltaTime);

        private void Step(float t, float dt)
        {
            _flare = Mathf.MoveTowards(_flare, 0f, dt * 2.5f);
            float breathe = 0.88f + 0.12f * Mathf.Sin(t * 2.1f);
            if (_stone != null) _stone.SetFloat("_Intensity", _baseIntensity * breathe * (1f + _flare * 1.8f));

            Vector3 centre = transform.position;
            for (int i = 0; i < _shards.Length; i++)
            {
                float a = _phase[i] + t * (0.35f + 0.05f * i);
                float y = _height * _heightFrac[i] + Mathf.Sin(t * 1.3f + _phase[i] * 3f) * 0.12f;
                _shards[i].position = centre + new Vector3(Mathf.Cos(a) * _radius[i], y, Mathf.Sin(a) * _radius[i]);
                _shards[i].Rotate(new Vector3(20f, 35f, 10f) * dt, Space.Self);
            }

            if (_light != null) _light.intensity = (2.2f + 1.0f * Mathf.Sin(t * 1.7f)) * (1f + _flare * 1.5f);
            if (_groundGlow != null)
            {
                Color c = _glow * (0.55f + 0.25f * Mathf.Sin(t * 1.7f) + _flare * 0.6f);
                c.a = 1f;
                _groundGlow.SetColor("_BaseColor", c);
            }
        }
    }
}
