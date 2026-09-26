using Orsuun.Rules;
using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// Upgrade glow (owner, 23 Sep 2026): only the weapon and the body armour are visible on the character and glow,
    /// each by its own level from +7 up (classic MMO upgrade shine). Helmet, shield, jewellery and shoes are stats only:
    /// they can be forged like the weapon but have no look and no glow. Maps a level to the EmberGlow shader's _Glow.
    /// </summary>
    public static class UpgradeGlow
    {
        public const int FirstGlowLevel = 7;
        public static readonly int GlowId = Shader.PropertyToID("_Glow");

        public static float ForLevel(int upgradeLevel) =>
            upgradeLevel < FirstGlowLevel ? 0f : upgradeLevel == 7 ? 0.35f : upgradeLevel == 8 ? 0.65f : 1f;

        /// <summary>True for the slots that show on the character and glow: the weapon and the body armour.</summary>
        public static bool IsVisible(EquipSlot slot) => slot == EquipSlot.Weapon || slot == EquipSlot.Armor;

        /// <summary>Glow for each EquipSlot index, written into <paramref name="buffer"/>: 0 for empty and stat-only slots.</summary>
        public static float[] PerSlot(PlayerSession session, float[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = IsVisible((EquipSlot)i) && session.Equipped((EquipSlot)i) is ItemState item ? ForLevel(item.UpgradeLevel) : 0f;
            return buffer;
        }

    }

    /// <summary>
    /// Star sparkles off a glowing piece (owner, 26 Sep 2026: "make it more like metin2 like glitter"): a particle system
    /// on the piece's renderer that emits small four-point stars from its surface, twinkling in and out in the level's
    /// colour. None below +7; a few at +7, more at +8, a rising shower at +9. The shader's own glitter twinkles on the
    /// surface; these stand just off it.
    /// </summary>
    public sealed class GearSparkle : MonoBehaviour
    {
        private ParticleSystem _ps;
        private float _glow = -1f;
        private bool _weapon;
        private static Material _material;

        /// <summary>The sparkles of a piece's renderer, made the first time they are asked for.</summary>
        public static GearSparkle On(Renderer piece, bool weapon)
        {
            var sparkle = piece.GetComponentInChildren<GearSparkle>();
            if (sparkle != null) return sparkle;
            var go = new GameObject("GearSparkle") { layer = piece.gameObject.layer };
            go.transform.SetParent(piece.transform, false);
            sparkle = go.AddComponent<GearSparkle>();
            sparkle._weapon = weapon;
            sparkle.Build(piece);
            return sparkle;
        }

        private void Build(Renderer piece)
        {
            if (_material == null)
            {
                Material spark = Resources.Load<Material>("FxSpark");
                if (spark == null) return;
                _material = new Material(spark) { name = "GearSparkle" };
                _material.SetTexture("_BaseMap", Resources.Load<Texture2D>("Fx/Sparkle"));
                _material.SetColor("_BaseColor", Color.white);
            }
            _ps = gameObject.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = _ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI / 4f);
            main.maxParticles = 90;
            ParticleSystem.ShapeModule shape = _ps.shape;
            shape.enabled = true;
            if (piece is SkinnedMeshRenderer skinned)
            {
                shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                shape.skinnedMeshRenderer = skinned;
            }
            else if (piece is MeshRenderer mesh)
            {
                shape.shapeType = ParticleSystemShapeType.MeshRenderer;
                shape.meshRenderer = mesh;
            }
            shape.normalOffset = 0.02f;
            ParticleSystem.SizeOverLifetimeModule size = _ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f)));
            ParticleSystem.RotationOverLifetimeModule spin = _ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Sets the piece's glow (UpgradeGlow.ForLevel): 0 stops the sparkles.</summary>
        public void Set(float glow)
        {
            if (_ps == null || Mathf.Approximately(glow, _glow)) return;
            _glow = glow;
            if (glow <= 0.001f)
            {
                _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }
            // Pale gold at +7, gold at +8, ember-gold at +9, as the shader's levels; white-hot at the core of each star.
            Color level = glow < 0.5f ? new Color(1f, 0.95f, 0.8f) : glow < 0.8f ? new Color(1f, 0.82f, 0.4f) : new Color(1f, 0.6f, 0.22f);
            ParticleSystem.MainModule main = _ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(level, Color.white, 0.5f) * 1.6f, level * 1.3f);
            ParticleSystem.EmissionModule emission = _ps.emission;
            float rate = glow < 0.5f ? 8f : glow < 0.8f ? 16f : 30f;
            emission.rateOverTime = _weapon ? rate * 0.8f : rate;
            ParticleSystem.VelocityOverLifetimeModule rise = _ps.velocityOverLifetime;
            rise.enabled = glow >= 0.8f;
            rise.space = ParticleSystemSimulationSpace.World;
            rise.x = new ParticleSystem.MinMaxCurve(0f);
            rise.y = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            rise.z = new ParticleSystem.MinMaxCurve(0f);
            if (!_ps.isPlaying) _ps.Play();
        }
    }
}
