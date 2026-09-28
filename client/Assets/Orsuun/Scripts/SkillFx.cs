using System;
using System.Collections.Generic;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Orsuun.Client
{
    /// <summary>
    /// The skills' effects on the lane (owner, 26 Sep 2026: "good and different effects and animations for each of the 20
    /// skills", then "make skills effect better for master and grand levels and of course coolest for perfect"; the Skill
    /// Codex he approved). Each skill has its own effect in its class colour, fired at the cast clip's strike frame
    /// (CastClips); the grade adds layers on top: Mastered an echo and a glowing ground mark, Grand a gold overlay, the
    /// class's rune circle, a follow-up strike and shafts of light, Peerless the class spirit (the hero's own model,
    /// larger, in ghost light, playing the same move), a white-gold core, cracks in the ground, a crown sigil and a
    /// shake. Buffs keep an aura while they last (the lane's state says how long). Textures are Resources/Fx (white on
    /// alpha, tinted here); materials are copies of the additive FxSpark, some switched to alpha blending for dark shapes.
    /// </summary>
    public sealed class SkillFx : MonoBehaviour
    {
        private enum Tier { Normal, Mastered, Grand, Peerless }

        /// <summary>How big and bright a cast is drawn, and what is layered on it.</summary>
        private struct Look
        {
            public Tier Tier;
            public float Scale;
            public Color Color;
            public float Alpha;
            /// <summary>The first layer: heavy particles, stuns and the blink happen once, on it.</summary>
            public bool Main;
        }

        private sealed class Sprite
        {
            public Transform T;
            public MeshRenderer R;
            public float Start, Life;
            public Vector3 From, To;
            public Vector2 Size0, Size1;
            public float Rot0, Rot1;
            public Color Color;
            public bool Flat;
            /// <summary>Stays at full alpha until the end (auras refreshed each frame).</summary>
            public bool Hold;
            /// <summary>Faces along To - From instead of the camera's up (beams, knives).</summary>
            public bool Along;
            /// <summary>The direction an Along sprite faces (a beam's length).</summary>
            public Vector3 Dir;
            public float FadeIn;
            public bool Active;
        }

        private sealed class Line
        {
            public LineRenderer R;
            public float Start, Life;
            public bool Active;
            public Color Color;
            public bool Flicker;
        }

        private static readonly Color Gold = new Color(1f, 0.78f, 0.32f);
        /// <summary>Additive sprites are drawn at this share of their colour: layers stack under the bloom.</summary>
        private const float Brightness = 0.7f;
        private static readonly Color WhiteGold = new Color(1f, 0.95f, 0.8f);

        private LaneView _lane;
        private Material _base;
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private readonly List<Sprite> _sprites = new List<Sprite>();
        private readonly List<Line> _lines = new List<Line>();
        private readonly List<(float At, Action Do)> _later = new List<(float, Action)>();
        private Mesh _quad;
        private MaterialPropertyBlock _block;
        private ParticleSystem _dots, _feathers, _darkFeathers, _smoke, _dust;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        // Camera shake around the camera's resting place.
        private Transform _camera;
        private Vector3 _cameraRest;
        private float _shakeUntil, _shakeAmount;

        // Auras: sprites kept alive while a buff lasts.
        private Sprite _dome, _domeRim, _ward, _eagle;
        private readonly Sprite[] _cloud = new Sprite[5];
        private readonly Dictionary<int, Line> _chains = new Dictionary<int, Line>();
        private readonly Dictionary<int, float> _stunnedUntil = new Dictionary<int, float>();
        private readonly Dictionary<int, Sprite> _stunRings = new Dictionary<int, Sprite>();
        private float _auraTick;
        private float _wardFlashAt;
        private float _ringAt;
        private Look _auraLook;

        public void Init(LaneView lane)
        {
            _lane = lane;
            _base = Resources.Load<Material>("FxSpark");
            _block = new MaterialPropertyBlock();
            _quad = new Mesh { name = "FxQuad" };
            _quad.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) });
            _quad.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
            _quad.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            _quad.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            _quad.RecalculateBounds();
            EnsureSystems();
        }

        public static Color ClassColor(HeroClass cls) => cls switch
        {
            HeroClass.Kestrel => new Color(0.25f, 0.9f, 0.78f),
            HeroClass.Wraithsworn => new Color(0.68f, 0.42f, 1f),
            HeroClass.Drumcaller => new Color(0.42f, 0.7f, 1f),
            _ => new Color(1f, 0.32f, 0.18f),
        };

        /// <summary>Grade tier from the lane hero's bonus for the slot (SkillGrades: M10 +20%, G10 +50%, P +60%).</summary>
        private static Tier TierOf(int bonusPercent) =>
            bonusPercent >= SkillGrades.BonusPercent(SkillGrades.Max) ? Tier.Peerless
            : bonusPercent > SkillGrades.BonusPercent(SkillGrades.MasteredSteps) ? Tier.Grand
            : bonusPercent > 0 ? Tier.Mastered : Tier.Normal;

        private static Look LookFor(Tier tier, Color color) => tier switch
        {
            // Layers stack additively under the bloom: brightness grows gently, size and layers carry the tiers.
            Tier.Mastered => new Look { Tier = tier, Scale = 1.25f, Color = color * 1.1f, Alpha = 1f },
            Tier.Grand => new Look { Tier = tier, Scale = 1.5f, Color = Color.Lerp(color, Gold, 0.25f) * 1.15f, Alpha = 0.9f },
            Tier.Peerless => new Look { Tier = tier, Scale = 1.75f, Color = Color.Lerp(color, WhiteGold, 0.25f) * 1.2f, Alpha = 0.85f },
            _ => new Look { Tier = tier, Scale = 1f, Color = color, Alpha = 1f },
        };

        // ------------------------------------------------------------------ casts

        /// <summary>A skill was cast (LaneEventKind.SkillCast): its effect at the cast clip's strike, with the grade's layers.</summary>
        public void Cast(int slot, SkillDef skill, HeroClass cls, int bonusPercent)
        {
            Tier tier = TierOf(bonusPercent);
            Look look = LookFor(tier, ClassColor(cls));
            float impact = CastClips.ImpactSeconds(cls, slot);
            Action<Look> core = CoreOf(cls, slot, skill, bonusPercent);
            if (core == null) return;
            Later(impact, () =>
            {
                Look main = look;
                main.Main = true;
                core(main);
                // FEWER skill effects (SETTINGS): the skill's own effect, without its grade's layers.
                if (!GameSettings.FewerEffects) Layers(tier, look, core, cls, slot);
            });
            if (GameSettings.FewerEffects) return;
            if (tier == Tier.Peerless) Spirit(cls, slot, look.Color);
            if (tier >= Tier.Grand) Later(Mathf.Max(0f, impact - 0.15f), () => Runes(look, Ground(_lane.HeroGround), 2.6f * look.Scale));
        }

        /// <summary>The grade's layers over a core effect: echo, gold overlay, follow-up, white-gold core, cracks, crown, shake.</summary>
        private void Layers(Tier tier, Look look, Action<Look> core, HeroClass cls, int slot)
        {
            if (tier == Tier.Normal) return;
            // Mastered: an afterimage echo and a glow on the ground under the hero.
            Look echo = look;
            echo.Scale *= 1.12f;
            echo.Alpha = 0.35f;
            Later(0.08f, () => core(echo));
            Decal("Glow", Ground(_lane.HeroGround), 2.4f * look.Scale, look.Color * 0.8f, 0.7f);
            if (tier == Tier.Mastered) return;
            // Grand: a gold overlay, a follow-up strike and shafts of light.
            Look gold = look;
            gold.Color = Gold * 1.1f;
            gold.Scale *= 1.06f;
            gold.Alpha = 0.35f;
            core(gold);
            Look follow = look;
            follow.Scale *= 0.85f;
            Later(0.24f, () => core(follow));
            Shafts(_lane.HeroGround, Gold * 1.4f, 3, 0.7f);
            if (tier == Tier.Grand) return;
            // Peerless: a white-gold core, cracks under the target, the crown sigil and a shake.
            Look white = look;
            white.Color = WhiteGold * 1.2f;
            white.Scale *= 0.75f;
            white.Alpha = 0.35f;
            core(white);
            Vector3 target = TargetGround(aimed: true);
            Decal("Cracks", Ground(target), 3.2f, look.Color, 1.1f);
            Billboard("Crown", _lane.HeroGround + new Vector3(0f, 2.45f, -0.2f), Vector3.up * 0.2f, 0.9f, 1.1f, Gold * 2f, 1.3f, 0f, 0f, 1f);
            Shake(0.12f, 0.35f);
        }

        /// <summary>The skill's own effect, drawn at a Look (scale, colour, alpha); called once per layer.</summary>
        private Action<Look> CoreOf(HeroClass cls, int slot, SkillDef skill, int bonus)
        {
            switch (cls)
            {
                case HeroClass.Kestrel:
                    switch (slot)
                    {
                        case 0: return Heartseeker;
                        case 1: return KnifeFan;
                        case 2: return KestrelsDive;
                        case 3: return VenomCloud;
                        default: return l => ShadowStoop(l, slot == 4);
                    }
                case HeroClass.Wraithsworn:
                    switch (slot)
                    {
                        case 0: return VoidLance;
                        case 1: return GraveTide;
                        case 2: return PactFrenzy;
                        case 3: return GraveChains;
                        default: return ShroudOfNight;
                    }
                case HeroClass.Drumcaller:
                    switch (slot)
                    {
                        case 0: return SkyHammer;
                        case 1: return StormDrum;
                        case 2: return WarRhythm;
                        case 3: return HuntersBlessing;
                        default: return MirrorWard;
                    }
                default:
                    switch (slot)
                    {
                        case 0: return RendingArc;
                        case 1: return IronWhirl;
                        case 2: return BloodFury;
                        case 3: return HonedEdge;
                        default:
                            float stun = skill.DurationTicks * (100 + bonus / 2) / 100f / LaneSim.TicksPerSecond;
                            return l => BullRush(l, stun);
                    }
            }
        }

        // ---- Vanguard

        private void RendingArc(Look l)
        {
            Vector3 at = TargetCentre(aimed: true) + new Vector3(-0.35f, 0.25f, -0.3f);
            Billboard("Crescent", at, Vector3.zero, 2.0f * l.Scale, 2.6f * l.Scale, l.Color, 0.42f, 45f, -25f, l.Alpha);
            if (l.Main) Burst(_dots, at, (int)(24 * l.Scale), l.Color, 1.2f);
        }

        private void IronWhirl(Look l)
        {
            Vector3 waist = _lane.HeroGround + new Vector3(0f, 0.9f, 0f);
            var s = Spawn("Ring", waist, waist, new Vector2(3.4f, 3.4f) * l.Scale, new Vector2(4.4f, 4.4f) * l.Scale, 0.55f, l.Color, flat: true, alpha: l.Alpha);
            if (s != null) { s.Rot0 = 0f; s.Rot1 = -720f; }
            Billboard("Crescent", waist + new Vector3(0.8f, 0f, -0.4f), Vector3.zero, 1.6f * l.Scale, 2.2f * l.Scale, l.Color * 0.9f, 0.35f, 90f, -180f, l.Alpha * 0.8f);
            if (l.Main) for (int i = 0; i < 6; i++) Burst(_dust, _lane.HeroGround + new Vector3(UnityEngine.Random.Range(-1.4f, 1.8f), 0.2f, UnityEngine.Random.Range(-0.6f, 0.6f)), 2, new Color(0.62f, 0.52f, 0.4f, 0.55f), 0.5f);
        }

        private void BloodFury(Look l)
        {
            Decal("Ring", Ground(_lane.HeroGround), 1f, l.Color, 0.55f, 5.5f * l.Scale, l.Alpha);
            Billboard("Glow", _lane.HeroGround + Vector3.up * 1.1f, Vector3.zero, 2.4f * l.Scale, 3.2f * l.Scale, l.Color, 0.5f, 0f, 0f, l.Alpha);
            if (l.Main) Rise(_dots, _lane.HeroGround, (int)(30 * l.Scale), l.Color, 1.2f);
        }

        private void HonedEdge(Look l)
        {
            (Vector3 grip, Vector3 tip) = _lane.WeaponLine;
            Beam(grip, tip, 0.5f * l.Scale, WhiteGold * 1.8f, 0.4f, l.Alpha);
            Billboard("Glow", tip, Vector3.zero, 0.6f * l.Scale, 1.6f * l.Scale, l.Color, 0.35f, 0f, 0f, l.Alpha);
            if (l.Main)
                for (int i = 0; i < 5; i++) Burst(_dots, Vector3.Lerp(grip, tip, i / 4f), (int)(6 * l.Scale), WhiteGold * 1.6f, 0.8f);
        }

        private void BullRush(Look l, float stunSeconds)
        {
            Vector3 front = _lane.HeroGround + new Vector3(1.4f, 0f, 0f);
            Decal("Ring", Ground(front), 1f, l.Color, 0.45f, 4.5f * l.Scale, l.Alpha);
            if (l.Main)
            {
                for (int i = 0; i < 12; i++)
                    Burst(_dust, front + new Vector3(UnityEngine.Random.Range(-0.6f, 2.4f), 0.25f, UnityEngine.Random.Range(-0.8f, 0.8f)), 2, new Color(0.6f, 0.48f, 0.34f, 0.6f), 0.9f);
                float until = Time.time + stunSeconds;
                foreach (LaneView.Spot e in _lane.EnemySpots())
                    if (!e.Boss && !e.Korstone) _stunnedUntil[e.Id] = until;
            }
            Billboard("Crescent", front + new Vector3(0.3f, 1f, -0.3f), Vector3.zero, 1.8f * l.Scale, 2.4f * l.Scale, l.Color * 0.8f, 0.3f, 180f, 180f, l.Alpha * 0.6f);
        }

        // ---- Kestrel

        private void Heartseeker(Look l)
        {
            Vector3 from = _lane.HandPoint, to = TargetCentre(aimed: true);
            Beam(from, to + (to - from).normalized * 0.8f, 0.35f * l.Scale, l.Color * 1.3f, 0.28f, l.Alpha);
            Billboard("Glow", to, Vector3.zero, 0.5f, 1.8f * l.Scale, l.Color, 0.3f, 0f, 0f, l.Alpha);
            if (l.Main) Burst(_feathers, to, (int)(14 * l.Scale), l.Color * 1.2f, 1.1f);
        }

        private void KnifeFan(Look l)
        {
            List<LaneView.Spot> spots = _lane.EnemySpots();
            if (spots.Count == 0) return;
            int knives = Mathf.RoundToInt(7 * l.Scale);
            Vector3 from = _lane.HandPoint;
            for (int i = 0; i < knives; i++)
            {
                LaneView.Spot target = spots[i % spots.Count];
                Vector3 to = target.Centre + new Vector3(0f, UnityEngine.Random.Range(-0.3f, 0.3f), 0f);
                float delay = i * 0.025f;
                Later(delay, () =>
                {
                    Sprite k = Spawn("Knife", from, to, new Vector2(0.9f, 0.9f), new Vector2(0.9f, 0.9f), 0.22f, l.Color * 1.4f, alpha: l.Alpha);
                    if (k != null) { k.Along = true; k.Dir = to - from; }
                    Sprite trail = Spawn("Streak", Vector3.Lerp(from, to, 0.3f), Vector3.Lerp(from, to, 0.8f), new Vector2(1.6f, 0.25f), new Vector2(1.2f, 0.18f), 0.25f, l.Color, alpha: l.Alpha * 0.8f);
                    if (trail != null) { trail.Along = true; trail.Dir = to - from; }
                    if (l.Main) Later(0.2f, () => Burst(_dots, to, 5, l.Color * 1.3f, 0.8f));
                });
            }
        }

        private void KestrelsDive(Look l)
        {
            Vector3 pack = PackCentre();
            Vector3 from = _lane.HeroGround + new Vector3(-1.2f, 3.8f, 0.4f);
            Sprite bird = Spawn("Kestrel", from, pack + Vector3.up * 0.6f, new Vector2(2.2f, 2.2f) * l.Scale, new Vector2(3.0f, 3.0f) * l.Scale, 0.45f, l.Color * 1.2f, alpha: l.Alpha * 0.9f);
            if (bird != null) { bird.Rot0 = -35f; bird.Rot1 = -35f; }
            if (l.Main)
            {
                Burst(_feathers, pack + Vector3.up * 0.6f, (int)(18 * l.Scale), l.Color, 1.2f);
                for (int i = 0; i < 4; i++) Beam(_lane.HeroGround + new Vector3(-1.5f, 0.6f + i * 0.35f, 0.2f), _lane.HeroGround + new Vector3(1.2f, 0.6f + i * 0.35f, 0.2f), 0.12f, l.Color * 0.8f, 0.3f, 0.6f);
            }
        }

        private void VenomCloud(Look l)
        {
            Vector3 pack = PackCentre();
            Vector3 hand = _lane.HandPoint;
            Spawn("Glow", hand, pack + Vector3.up * 0.4f, new Vector2(0.4f, 0.4f), new Vector2(0.4f, 0.4f), 0.28f, l.Color * 1.6f, alpha: l.Alpha);
            Later(0.28f, () =>
            {
                for (int i = 0; i < 4; i++)
                    Billboard("Smoke", pack + new Vector3(UnityEngine.Random.Range(-1.2f, 1.2f), 0.5f + UnityEngine.Random.Range(0f, 0.8f), -0.4f), Vector3.up * 0.3f,
                        1.2f * l.Scale, 2.6f * l.Scale, l.Color * 0.9f, 0.9f, UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-60f, 60f), l.Alpha * 0.8f);
                if (l.Main) Rise(_dots, pack, (int)(20 * l.Scale), l.Color * 1.3f, 0.6f);
            });
        }

        private void ShadowStoop(Look l, bool blink)
        {
            Vector3 start = _lane.HeroGround + Vector3.up * 1f, mark = TargetCentre(aimed: true);
            if (l.Main)
            {
                Burst(_darkFeathers, start, (int)(20 * l.Scale), new Color(0.08f, 0.05f, 0.12f, 0.9f), 1.2f);
                Beam(start, mark, 0.6f * l.Scale, new Color(0.5f, 0.2f, 0.9f), 0.4f, 0.7f);
                if (blink) _lane.Blink(mark.x + 0.9f, 0.55f);
            }
            Billboard("Crescent", mark + new Vector3(0.2f, 0.1f, -0.3f), Vector3.zero, 1.6f * l.Scale, 2.2f * l.Scale, new Color(0.7f, 0.4f, 1f) * 1.4f, 0.35f, 150f, 210f, l.Alpha);
            Billboard("Glow", mark, Vector3.zero, 0.6f, 1.6f * l.Scale, l.Color, 0.3f, 0f, 0f, l.Alpha);
        }

        // ---- Wraithsworn

        private void VoidLance(Look l)
        {
            Vector3 from = _lane.HandPoint, to = TargetCentre(aimed: true);
            Beam(from, to, 0.45f * l.Scale, l.Color * 1.4f, 0.25f, l.Alpha);
            Later(0.12f, () =>
            {
                Billboard("Glow", to, Vector3.zero, 2.2f * l.Scale, 0.3f, new Color(0.06f, 0.02f, 0.1f, 0.95f), 0.4f, 0f, 0f, l.Alpha, dark: true);
                Billboard("Ring", to, Vector3.zero, 2.6f * l.Scale, 0.3f, l.Color * 1.3f, 0.4f, 0f, 90f, l.Alpha);
                if (l.Main) Implode(to, (int)(22 * l.Scale), l.Color * 1.5f);
            });
        }

        private void GraveTide(Look l)
        {
            foreach (LaneView.Spot e in _lane.EnemySpots())
            {
                for (int h = 0; h < 2; h++)
                {
                    Vector3 root = Ground(e.Base) + new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f), -0.4f, -0.3f);
                    float delay = UnityEngine.Random.Range(0f, 0.15f);
                    Later(delay, () => Spawn("Hand", root, root + Vector3.up * 1.2f, new Vector2(0.9f, 1.3f) * l.Scale, new Vector2(1.1f, 1.6f) * l.Scale, 0.6f, l.Color * 1.2f, alpha: l.Alpha));
                }
            }
            if (l.Main)
                foreach (LaneView.Spot e in _lane.EnemySpots()) Burst(_smoke, Ground(e.Base) + Vector3.up * 0.2f, 3, l.Color * 0.6f, 0.4f);
        }

        private void PactFrenzy(Look l)
        {
            Vector3 centre = _lane.HeroGround + Vector3.up * 1.1f;
            var s = Spawn("Runes", centre, centre, new Vector2(1.6f, 1.6f) * l.Scale, new Vector2(2.6f, 2.6f) * l.Scale, 0.6f, l.Color * 1.3f, alpha: l.Alpha);
            if (s != null) s.Rot1 = 120f;
            if (l.Main) Rise(_smoke, _lane.HeroGround, (int)(12 * l.Scale), l.Color * 0.9f, 1f);
        }

        private void GraveChains(Look l)
        {
            foreach (LaneView.Spot e in _lane.EnemySpots())
            {
                Decal("Runes", Ground(e.Base), 1.4f * l.Scale, l.Color * 1.2f, 0.6f, alpha: l.Alpha);
                if (l.Main) Burst(_dots, Ground(e.Base) + Vector3.up * 0.2f, 6, l.Color * 1.3f, 0.6f);
            }
        }

        private void ShroudOfNight(Look l)
        {
            Vector3 centre = _lane.HeroGround + Vector3.up * 1.1f;
            Billboard("Ring", centre, Vector3.zero, 1f, 3.6f * l.Scale, l.Color * 1.4f, 0.45f, 0f, 45f, l.Alpha);
            if (l.Main) Implode(centre, (int)(16 * l.Scale), l.Color);
        }

        // ---- Drumcaller

        private void SkyHammer(Look l)
        {
            Vector3 at = TargetGround(aimed: true);
            Bolt(at + Vector3.up * 6.5f, at + Vector3.up * 0.2f, 0.35f * l.Scale, l.Color * 1.6f, 0.3f);
            Billboard("Lightning", at + Vector3.up * 3.3f, Vector3.zero, 1.4f * l.Scale, 1.6f * l.Scale, l.Color * 1.8f, 0.25f, 0f, 0f, l.Alpha, stretch: 4.5f);
            Billboard("Glow", at + Vector3.up * 0.6f, Vector3.zero, 1.4f * l.Scale, 3f * l.Scale, l.Color * 1.3f, 0.35f, 0f, 0f, l.Alpha);
            Decal("Cracks", Ground(at), 2.2f * l.Scale, new Color(0.1f, 0.08f, 0.1f, 0.8f), 1.2f, alpha: l.Alpha, dark: true);
            if (l.Main) Burst(_dots, at + Vector3.up * 0.4f, (int)(26 * l.Scale), l.Color * 1.5f, 1.4f);
        }

        private void StormDrum(Look l)
        {
            Decal("Ring", Ground(_lane.HeroGround), 1f, l.Color, 0.45f, 6f * l.Scale, l.Alpha);
            Later(0.18f, () => Decal("Ring", Ground(_lane.HeroGround), 1f, l.Color, 0.45f, 7f * l.Scale, l.Alpha * 0.8f));
            List<LaneView.Spot> spots = _lane.EnemySpots();
            Vector3 prev = _lane.HandPoint;
            for (int i = 0; i < spots.Count && i < 8; i++)
            {
                Bolt(prev, spots[i].Centre, 0.14f * l.Scale, l.Color * 1.6f, 0.35f);
                prev = spots[i].Centre;
            }
        }

        private void WarRhythm(Look l)
        {
            Vector3 drum = _lane.DrumPoint;
            for (int i = 0; i < 3; i++)
                Later(i * 0.12f, () => Billboard("Ring", drum, Vector3.zero, 0.4f, 2.8f * l.Scale, Gold * 1.5f, 0.5f, 0f, 0f, l.Alpha));
            if (l.Main) Rise(_dots, _lane.HeroGround, (int)(16 * l.Scale), Gold * 1.4f, 0.8f);
        }

        private void HuntersBlessing(Look l)
        {
            // The eagle opens its wings behind the hero's shoulders.
            Vector3 above = _lane.HeroGround + new Vector3(0f, 1.9f, 0.7f);
            Billboard("Eagle", above, Vector3.up * 0.15f, 0.6f, 3.4f * l.Scale, Gold * 1.4f, 0.8f, 0f, 0f, l.Alpha);
            if (l.Main) Fall(_dots, above, (int)(24 * l.Scale), Gold * 1.5f);
        }

        private void MirrorWard(Look l)
        {
            Vector3 at = _lane.WardPoint;
            Billboard("Ring", at, Vector3.zero, 0.5f, 2.6f * l.Scale, l.Color, 0.4f, 0f, 0f, l.Alpha * 0.8f);
            Billboard("Ward", at, Vector3.zero, 0.5f, 2.2f * l.Scale, l.Color, 0.5f, 0f, 30f, l.Alpha);
        }

        // ------------------------------------------------------------------ auras and hits

        /// <summary>A blow the veil took (HeroDamaged "veiled"): the dome's rim flares.</summary>
        public void Veiled()
        {
            if (_domeRim != null) _domeRim.Start = Time.time - 0.05f;
            Burst(_dots, _lane.HeroGround + new Vector3(0.6f, 1.2f, -0.3f), 6, ClassColor(HeroClass.Wraithsworn) * 1.4f, 0.8f);
        }

        /// <summary>A blow on the Mirror Ward: it flashes and a bolt goes back to the striker.</summary>
        public void Warded(Vector3 striker)
        {
            if (Time.time < _wardFlashAt) return;   // a pack's blows land together: one flash for them
            _wardFlashAt = Time.time + 0.15f;
            Color c = ClassColor(HeroClass.Drumcaller) * 1.2f;
            Billboard("Ring", _lane.WardPoint, Vector3.zero, 1.2f, 2f, c, 0.22f, 0f, 0f, 0.45f);
            Bolt(_lane.WardPoint, striker, 0.1f, c, 0.22f);
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = _later.Count - 1; i >= 0; i--)
            {
                if (_later[i].At > now) continue;
                Action act = _later[i].Do;
                _later.RemoveAt(i);
                act();
            }
            UpdateAuras();
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            foreach (Sprite s in _sprites)
            {
                if (!s.Active) continue;
                float t = (now - s.Start) / s.Life;
                if (t >= 1f && !s.Hold) { s.Active = false; s.R.gameObject.SetActive(false); continue; }
                t = Mathf.Clamp01(t);
                float ease = 1f - (1f - t) * (1f - t);
                s.T.position = Vector3.Lerp(s.From, s.To, ease);
                Vector2 size = Vector2.Lerp(s.Size0, s.Size1, ease);
                s.T.localScale = new Vector3(size.x, size.y, 1f);
                float rot = Mathf.Lerp(s.Rot0, s.Rot1, t);
                if (s.Flat) s.T.rotation = Quaternion.Euler(90f, 0f, rot);
                else if (cam != null)
                {
                    float angle = rot;
                    if (s.Along)
                    {
                        Vector3 d = cam.InverseTransformDirection(s.Dir.sqrMagnitude > 0f ? s.Dir : s.To - s.From);
                        angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                    }
                    s.T.rotation = cam.rotation * Quaternion.Euler(0f, 0f, angle);
                }
                float fade = s.Hold ? 1f : Mathf.Clamp01(t / Mathf.Max(0.001f, s.FadeIn)) * Mathf.Clamp01((1f - t) / 0.45f);
                Color c = s.Color;
                c.a *= fade;
                _block.SetColor(BaseColor, c);
                s.R.SetPropertyBlock(_block);
            }
            foreach (Line line in _lines)
            {
                if (!line.Active) continue;
                float t = (now - line.Start) / line.Life;
                if (t >= 1f) { line.Active = false; line.R.gameObject.SetActive(false); continue; }
                Color c = line.Color;
                c.a *= (line.Flicker ? (UnityEngine.Random.value > 0.25f ? 1f : 0.3f) : 1f) * Mathf.Clamp01((1f - t) / 0.5f);
                line.R.startColor = c;
                line.R.endColor = c;
            }
            if (_camera != null)
            {
                if (now < _shakeUntil)
                {
                    float k = (_shakeUntil - now) / 0.35f * _shakeAmount;
                    _camera.position = _cameraRest + new Vector3(UnityEngine.Random.Range(-k, k), UnityEngine.Random.Range(-k, k), 0f);
                }
                else
                {
                    _camera.position = _cameraRest;
                    _camera = null;
                }
            }
        }

        /// <summary>Buffs and states that last: drawn while the lane says they hold.</summary>
        private void UpdateAuras()
        {
            LaneSim sim = _lane.Sim;
            if (sim == null) return;
            HeroClass cls = _lane.HeroClassShown;
            Color color = ClassColor(cls);
            int bonus = 0;
            for (int i = 0; i < sim.Skills.Length && i < sim.Hero.SkillGradeBonusPercent.Length; i++)
                bonus = Math.Max(bonus, sim.Hero.SkillGradeBonusPercent[i]);
            _auraLook = LookFor(TierOf(bonus), color);
            bool fighting = sim.Phase == LanePhase.Fighting;
            float now = Time.time;
            bool tick = now >= _auraTick;
            if (tick) _auraTick = now + 0.12f;
            Vector3 hero = _lane.HeroGround;

            // Hastes: each class's own aura.
            if (sim.HasteActive && tick)
            {
                switch (cls)
                {
                    case HeroClass.Vanguard: Rise(_smoke, hero, 2, color * 0.8f, 0.8f); Rise(_dots, hero, 3, color * 1.3f, 1f); break;
                    case HeroClass.Kestrel: Beam(hero + new Vector3(-1.2f, UnityEngine.Random.Range(0.4f, 1.8f), 0.3f), hero + new Vector3(0.4f, UnityEngine.Random.Range(0.4f, 1.8f), 0.3f), 0.08f, color, 0.2f, 0.6f); break;
                    case HeroClass.Wraithsworn: Rise(_smoke, hero, 2, color * 0.9f, 0.9f); break;
                    case HeroClass.Drumcaller:
                        if (now >= _ringAt) { _ringAt = now + 0.45f; Billboard("Ring", _lane.DrumPoint, Vector3.zero, 0.3f, 1.8f, Gold * 1.3f, 0.4f, 0f, 0f, 0.7f); }
                        break;
                }
            }
            // Honed Edge: sparks run along the blade.
            if (sim.EmpowerActive && tick)
            {
                (Vector3 grip, Vector3 tip) = _lane.WeaponLine;
                Burst(_dots, Vector3.Lerp(grip, tip, UnityEngine.Random.value), 2, WhiteGold * 1.6f, 0.5f);
            }
            // Hunter's Blessing: the eagle watches from above and gold motes fall.
            Hold(ref _eagle, sim.FocusActive, "Eagle", hero + new Vector3(0f, 1.95f + Mathf.Sin(now * 1.6f) * 0.06f, 0.7f), new Vector2(3f, 3f) * Mathf.Min(1.4f, _auraLook.Scale), Gold * 1.1f, 0.35f);
            if (sim.FocusActive && tick) Fall(_dots, hero + Vector3.up * 2.3f, 2, Gold * 1.3f);
            // Venom Cloud: the cloud hangs over the pack.
            bool cloud = sim.PoisonActive && fighting;
            Vector3 pack = PackCentre();
            for (int i = 0; i < _cloud.Length; i++)
            {
                float a = i * 1.3f + now * 0.4f;
                Vector3 at = pack + new Vector3(Mathf.Cos(a) * 1.1f, 0.7f + Mathf.Sin(a * 1.7f) * 0.25f, -0.5f);
                Hold(ref _cloud[i], cloud, "Smoke", at, new Vector2(2.4f, 2.4f) * _auraLook.Scale, ClassColor(HeroClass.Kestrel) * 0.75f, 0.55f, spin: a * 20f);
            }
            if (cloud && tick) Rise(_dots, pack, 2, ClassColor(HeroClass.Kestrel) * 1.2f, 0.5f);
            // Grave Chains: a chain from the ground to each bound enemy.
            UpdateChains(sim.BindActive && fighting);
            // Shroud of Night: the starry dome; Mirror Ward: the drum-skin ward.
            Vector3 centre = hero + Vector3.up * 1.15f;
            Hold(ref _dome, sim.ShieldActive, "Stars", centre, new Vector2(3.2f, 3.2f) * _auraLook.Scale, new Color(0.06f, 0.05f, 0.16f, 0.55f), 1f, dark: true, spin: now * 6f);
            Hold(ref _domeRim, sim.ShieldActive, "Ring", centre, new Vector2(3.4f, 3.4f) * _auraLook.Scale, ClassColor(HeroClass.Wraithsworn) * 1.2f, 0.8f);
            Hold(ref _ward, sim.WardActive, "Ward", _lane.WardPoint, new Vector2(2.0f, 2.0f) * Mathf.Min(1.4f, _auraLook.Scale), ClassColor(HeroClass.Drumcaller), 0.5f, spin: now * 12f);
            // Bull Rush: rings over the stunned.
            UpdateStuns(fighting);
        }

        private void UpdateChains(bool bound)
        {
            var seen = new HashSet<int>();
            if (bound)
            {
                Color c = ClassColor(HeroClass.Wraithsworn) * 1.3f * (_auraLook.Scale * 0.7f);
                foreach (LaneView.Spot e in _lane.EnemySpots())
                {
                    seen.Add(e.Id);
                    if (!_chains.TryGetValue(e.Id, out Line line) || line.R == null)
                    {
                        line = NewLine("Chain", dark: false);
                        line.R.textureMode = LineTextureMode.Tile;
                        _chains[e.Id] = line;
                    }
                    line.Active = false;   // held here, not faded by Update
                    line.R.gameObject.SetActive(true);
                    line.R.widthMultiplier = 0.22f;
                    line.R.positionCount = 2;
                    line.R.SetPosition(0, Ground(e.Base) + new Vector3(-0.35f, 0.02f, -0.3f));
                    line.R.SetPosition(1, e.Centre + new Vector3(0f, -0.1f, -0.3f));
                    line.R.startColor = c;
                    line.R.endColor = c;
                    line.R.material.SetTextureScale("_BaseMap", new Vector2(Vector3.Distance(Ground(e.Base), e.Centre) * 2f, 1f));
                }
            }
            var gone = new List<int>();
            foreach (KeyValuePair<int, Line> pair in _chains)
                if (!seen.Contains(pair.Key)) { if (pair.Value.R != null) pair.Value.R.gameObject.SetActive(false); gone.Add(pair.Key); }
            foreach (int id in gone) _chains.Remove(id);
        }

        private void UpdateStuns(bool fighting)
        {
            float now = Time.time;
            var alive = new Dictionary<int, LaneView.Spot>();
            foreach (LaneView.Spot e in _lane.EnemySpots()) alive[e.Id] = e;
            var gone = new List<int>();
            foreach (KeyValuePair<int, float> pair in _stunnedUntil)
            {
                bool on = fighting && now < pair.Value && alive.ContainsKey(pair.Key);
                _stunRings.TryGetValue(pair.Key, out Sprite ring);
                Vector3 at = on ? alive[pair.Key].Top + Vector3.up * 0.25f : Vector3.zero;
                Hold(ref ring, on, "Ring", at, new Vector2(0.7f, 0.7f), Gold * 1.4f, 0.9f, flat: true, spin: now * 360f);
                _stunRings[pair.Key] = ring;
                if (!on) gone.Add(pair.Key);
            }
            foreach (int id in gone) { _stunnedUntil.Remove(id); _stunRings.Remove(id); }
        }

        /// <summary>Keeps one sprite alive at a place while <paramref name="on"/>, and lets it go when not.</summary>
        private void Hold(ref Sprite sprite, bool on, string texture, Vector3 at, Vector2 size, Color color, float alpha, bool dark = false, bool flat = false, float spin = 0f)
        {
            if (!on)
            {
                if (sprite != null && sprite.Active && sprite.Hold)
                {
                    // Fade out over a moment from where it stands.
                    sprite.Hold = false;
                    sprite.Start = Time.time - sprite.Life * 0.6f;
                }
                sprite = null;
                return;
            }
            if (sprite == null || !sprite.Active || !sprite.Hold)
            {
                sprite = Spawn(texture, at, at, size, size, 1f, color, flat: flat, dark: dark, alpha: alpha);
                if (sprite == null) return;
                sprite.Hold = true;
                sprite.FadeIn = 0.2f;
            }
            sprite.From = sprite.To = at;
            sprite.Size0 = sprite.Size1 = size;
            sprite.Rot0 = sprite.Rot1 = spin;
            Color c = dark ? color : new Color(color.r * Brightness, color.g * Brightness, color.b * Brightness, color.a);
            c.a *= alpha;
            sprite.Color = c;
        }

        // ------------------------------------------------------------------ the Peerless spirit

        /// <summary>The class spirit: the hero's own model, larger, in ghost light, behind him, playing the same cast.</summary>
        private void Spirit(HeroClass cls, int slot, Color color)
        {
            var root = new GameObject("PeerlessSpirit").transform;
            root.SetParent(transform, false);
            GameObject body = _lane.BuildSpirit(root);
            if (body == null) { Destroy(root.gameObject); return; }
            // Behind the hero and a little sunk, so the head stays in the lane's band under the HUD.
            root.position = _lane.HeroGround + new Vector3(-0.7f, -0.35f, 1.8f);
            root.localScale = Vector3.one * 1.8f;
            Material ghost = MaterialFor("Glow", dark: false);
            var mats = new List<Material>();
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>())
            {
                Texture tex = r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseMap") ? r.sharedMaterial.GetTexture("_BaseMap") : null;
                var m = new Material(ghost);
                m.SetTexture("_BaseMap", tex != null ? tex : Texture2D.whiteTexture);
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
                mats.Add(m);
            }
            var anim = body.GetComponent<Animation>();
            if (anim != null && anim.GetClip(CastClips.ClipName(slot)) != null) anim.Play(CastClips.ClipName(slot));
            float life = CastClips.LengthSeconds(cls, slot) + 0.5f;
            StartCoroutine(FadeSpirit(root.gameObject, mats, color, life));
        }

        private static System.Collections.IEnumerator FadeSpirit(GameObject spirit, List<Material> mats, Color color, float life)
        {
            float start = Time.time;
            while (Time.time - start < life)
            {
                float t = (Time.time - start) / life;
                // The model's own texture lit in the class colour: bright enough to read as a spirit, not a shadow.
                float a = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((1f - t) / 0.4f) * 0.85f;
                Color c = color * 2.8f;
                c.a = a;
                foreach (Material m in mats) m.SetColor(BaseColor, c);
                yield return null;
            }
            Destroy(spirit);
            foreach (Material m in mats) Destroy(m);
        }

        // ------------------------------------------------------------------ building blocks

        private void Later(float seconds, Action act)
        {
            if (seconds <= 0f) act();
            else _later.Add((Time.time + seconds, act));
        }

        private void Shake(float amount, float seconds)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            if (_camera == null)
            {
                _camera = cam.transform;
                _cameraRest = _camera.position;
            }
            _shakeAmount = amount;
            _shakeUntil = Time.time + seconds;
        }

        private static Vector3 Ground(Vector3 p) => new Vector3(p.x, 0.03f, p.z);

        private Vector3 PackCentre()
        {
            List<LaneView.Spot> spots = _lane.EnemySpots();
            if (spots.Count == 0) return _lane.HeroGround + new Vector3(2.4f, 0f, 0f);
            Vector3 sum = Vector3.zero;
            foreach (LaneView.Spot s in spots) sum += s.Base;
            return sum / spots.Count;
        }

        /// <summary>The enemy an aimed skill picks (the toughest: the Korstone or boss when up), or the front one.</summary>
        private LaneView.Spot? Target(bool aimed)
        {
            LaneView.Spot? best = null;
            foreach (LaneView.Spot s in _lane.EnemySpots())
            {
                if (best == null) { best = s; continue; }
                if (aimed ? s.Hp > best.Value.Hp : s.Base.x < best.Value.Base.x) best = s;
            }
            return best;
        }

        private Vector3 TargetCentre(bool aimed) => Target(aimed)?.Centre ?? _lane.HeroGround + new Vector3(2.2f, 1f, 0f);

        private Vector3 TargetGround(bool aimed) => Target(aimed)?.Base ?? _lane.HeroGround + new Vector3(2.2f, 0f, 0f);

        private Material MaterialFor(string texture, bool dark)
        {
            string key = texture + (dark ? "#dark" : "");
            if (_materials.TryGetValue(key, out Material m)) return m;
            if (_base == null) return null;
            m = new Material(_base) { name = "Fx" + key };
            m.SetTexture("_BaseMap", Resources.Load<Texture2D>("Fx/" + texture));
            m.SetColor(BaseColor, Color.white);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            if (dark)
            {
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }
            _materials[key] = m;
            return m;
        }

        private Sprite Spawn(string texture, Vector3 from, Vector3 to, Vector2 size0, Vector2 size1, float life, Color color, bool flat = false, bool dark = false, float alpha = 1f)
        {
            Material material = MaterialFor(texture, dark);
            if (material == null) return null;
            Sprite s = null;
            foreach (Sprite free in _sprites)
                if (!free.Active) { s = free; break; }
            if (s == null)
            {
                if (_sprites.Count >= 160) return null;
                var go = new GameObject("FxSprite");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = _quad;
                var r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                s = new Sprite { T = go.transform, R = r };
                _sprites.Add(s);
            }
            s.R.sharedMaterial = material;
            s.R.gameObject.SetActive(true);
            s.Active = true;
            s.Hold = false;
            s.Along = false;
            s.Dir = Vector3.zero;
            s.Start = Time.time;
            s.Life = life;
            s.From = from;
            s.To = to;
            s.Size0 = size0;
            s.Size1 = size1;
            s.Rot0 = s.Rot1 = 0f;
            s.Flat = flat;
            s.FadeIn = 0.12f;
            Color c = dark ? color : new Color(color.r * Brightness, color.g * Brightness, color.b * Brightness);
            c.a = (dark ? color.a : 1f) * alpha;
            s.Color = c;
            s.T.position = from;
            return s;
        }

        private void Billboard(string texture, Vector3 at, Vector3 drift, float size0, float size1, Color color, float life, float rot0, float rot1, float alpha, bool dark = false, float stretch = 1f)
        {
            Sprite s = Spawn(texture, at, at + drift, new Vector2(size0, size0 * stretch), new Vector2(size1, size1 * stretch), life, color, dark: dark, alpha: alpha);
            if (s == null) return;
            s.Rot0 = rot0;
            s.Rot1 = rot1;
        }

        /// <summary>A flat mark on the ground, growing from size to endSize (0: the same) as it fades.</summary>
        private void Decal(string texture, Vector3 at, float size, Color color, float life, float endSize = 0f, float alpha = 1f, bool dark = false)
        {
            float end = endSize > 0f ? endSize : size;
            Sprite s = Spawn(texture, at, at, new Vector2(size, size), new Vector2(end, end), life, color, flat: true, dark: dark, alpha: alpha);
            if (s != null) s.Rot1 = 25f;
        }

        private void Runes(Look l, Vector3 at, float size)
        {
            Sprite s = Spawn("Runes", at, at, new Vector2(size * 0.7f, size * 0.7f), new Vector2(size, size), 1.0f, Color.Lerp(l.Color, Gold * 1.6f, 0.5f), flat: true);
            if (s != null) s.Rot1 = 90f;
        }

        private void Shafts(Vector3 ground, Color color, int count, float life)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 at = ground + new Vector3(-0.8f + i * 0.8f, 2.2f, 0.6f);
                Sprite s = Spawn("Streak", at, at + Vector3.up * 0.4f, new Vector2(4.4f, 0.6f), new Vector2(4.8f, 0.5f), life, color, alpha: 0.6f);
                if (s != null) s.Rot0 = s.Rot1 = 90f;
            }
        }

        /// <summary>A straight glowing beam from a to b (the streak stretched between them).</summary>
        private void Beam(Vector3 a, Vector3 b, float width, Color color, float life, float alpha)
        {
            Vector3 mid = (a + b) / 2f;
            float length = Vector3.Distance(a, b);
            Sprite s = Spawn("Streak", mid, mid, new Vector2(length * 0.3f, width), new Vector2(length, width * 0.7f), life, color, alpha: alpha);
            if (s == null) return;
            s.Along = true;
            s.Dir = b - a;
        }

        private Line NewLine(string texture, bool dark)
        {
            var go = new GameObject("FxLine");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<LineRenderer>();
            r.sharedMaterial = MaterialFor(texture, dark);
            r.material = new Material(r.sharedMaterial);
            r.useWorldSpace = true;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.numCapVertices = 2;
            r.alignment = LineAlignment.View;
            var line = new Line { R = r };
            _lines.Add(line);
            return line;
        }

        /// <summary>A jagged lightning bolt from a to b.</summary>
        private void Bolt(Vector3 a, Vector3 b, float width, Color color, float life)
        {
            Line line = null;
            foreach (Line l in _lines)
                if (!l.Active && !_chains.ContainsValue(l) && l.R.sharedMaterial != null && l.R.sharedMaterial.name.StartsWith("FxStreak")) { line = l; break; }
            line ??= NewLine("Streak", dark: false);
            int n = Mathf.Clamp((int)(Vector3.Distance(a, b) * 3f), 6, 24);
            line.R.positionCount = n;
            Vector3 side = Vector3.Cross((b - a).normalized, Vector3.forward).normalized;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                Vector3 p = Vector3.Lerp(a, b, t);
                if (i > 0 && i < n - 1) p += side * UnityEngine.Random.Range(-0.22f, 0.22f) + Vector3.forward * UnityEngine.Random.Range(-0.1f, 0.1f);
                line.R.SetPosition(i, p);
            }
            line.R.widthMultiplier = width;
            line.R.textureMode = LineTextureMode.Stretch;
            line.Color = color;
            line.Start = Time.time;
            line.Life = life;
            line.Flicker = true;
            line.Active = true;
            line.R.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ particles

        private ParticleSystem System(ref ParticleSystem system, string name, string texture, bool dark, float gravity, float size, ParticleSystemRenderMode mode)
        {
            if (system != null) return system;
            Material material = MaterialFor(texture, dark);
            if (material == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 1.2f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            system.Play();
            return system;
        }

        private void EnsureSystems()
        {
            System(ref _dots, "FxDots", "Glow", false, 0.4f, 0.12f, ParticleSystemRenderMode.Billboard);
            System(ref _feathers, "FxFeathers", "Feather", false, 0.25f, 0.35f, ParticleSystemRenderMode.Billboard);
            System(ref _darkFeathers, "FxDarkFeathers", "Feather", true, 0.3f, 0.4f, ParticleSystemRenderMode.Billboard);
            System(ref _smoke, "FxSmoke", "Smoke", false, -0.05f, 1.1f, ParticleSystemRenderMode.Billboard);
            System(ref _dust, "FxDust", "Smoke", true, -0.02f, 1.3f, ParticleSystemRenderMode.Billboard);
        }

        private void Burst(ParticleSystem system, Vector3 at, int count, Color color, float speed)
        {
            system = Pick(system);
            if (system == null || count <= 0) return;
            var emit = new ParticleSystem.EmitParams { position = at, startColor = color, applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                emit.velocity = (UnityEngine.Random.onUnitSphere + Vector3.up * 0.6f).normalized * UnityEngine.Random.Range(1.2f, 4f) * speed;
                emit.position = at + UnityEngine.Random.insideUnitSphere * 0.15f;
                system.Emit(emit, 1);
            }
        }

        /// <summary>Particles rising from around a point on the ground (auras, war cries).</summary>
        private void Rise(ParticleSystem system, Vector3 ground, int count, Color color, float speed)
        {
            system = Pick(system);
            if (system == null || count <= 0) return;
            var emit = new ParticleSystem.EmitParams { startColor = color };
            for (int i = 0; i < count; i++)
            {
                emit.position = ground + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(0f, 1.2f), UnityEngine.Random.Range(-0.4f, 0.4f));
                emit.velocity = new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), UnityEngine.Random.Range(1f, 2.4f) * speed, 0f);
                system.Emit(emit, 1);
            }
        }

        /// <summary>Motes drifting down from a point (blessings).</summary>
        private void Fall(ParticleSystem system, Vector3 from, int count, Color color)
        {
            system = Pick(system);
            if (system == null || count <= 0) return;
            var emit = new ParticleSystem.EmitParams { startColor = color };
            for (int i = 0; i < count; i++)
            {
                emit.position = from + new Vector3(UnityEngine.Random.Range(-1.2f, 1.2f), UnityEngine.Random.Range(-0.2f, 0.4f), UnityEngine.Random.Range(-0.3f, 0.3f));
                emit.velocity = new Vector3(0f, UnityEngine.Random.Range(-1.4f, -0.6f), 0f);
                system.Emit(emit, 1);
            }
        }

        /// <summary>Particles drawn in toward a point (implosions).</summary>
        private void Implode(Vector3 at, int count, Color color)
        {
            ParticleSystem system = _dots;
            if (system == null) return;
            var emit = new ParticleSystem.EmitParams { startColor = color, startLifetime = 0.35f };
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(1f, 1.8f);
                emit.position = at + offset;
                emit.velocity = -offset / 0.35f;
                system.Emit(emit, 1);
            }
        }

        private ParticleSystem Pick(ParticleSystem requested) => requested != null ? requested : _dots;
    }
}
