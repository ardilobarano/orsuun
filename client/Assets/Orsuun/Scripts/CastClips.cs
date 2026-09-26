using System.Collections.Generic;
using System.Globalization;
using Orsuun.Rules.Combat;
using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// The skills' cast animations (owner, 26 Sep 2026: "good and different effects and animations for each of the 20
    /// skills"), built at run time as legacy clips "Cast0".."Cast4" on the shared skeleton of art/blender/rig.py, so every
    /// hero model (Vanguard armour looks, class bands, costumes) plays them without a new export. Keys are written as in
    /// rig.py: degrees about each bone's local axes (x swings the bone's tip forward) and the hips' offset in metres
    /// after "|" (y up, z forward). A Blender pose rotation lands on the imported bone mirrored in x (measured on the
    /// models: x keeps its sign, y and z flip), so the keys read exactly like rig.py's.
    /// </summary>
    public static class CastClips
    {
        public const float Fps = 24f;

        private static readonly string[] Bones =
        {
            "hips", "spine", "chest", "neck", "head",
            "shoulder.R", "upper_arm.R", "forearm.R", "hand.R",
            "shoulder.L", "upper_arm.L", "forearm.L", "hand.L",
            "thigh.L", "shin.L", "foot.L", "thigh.R", "shin.R", "foot.R",
        };

        private sealed class Def
        {
            public Def(int length, int impact, string keys)
            {
                Length = length;
                Impact = impact;
                Keys = keys;
            }

            public int Length { get; }
            /// <summary>The frame the blow lands or the power is released: the effect starts here.</summary>
            public int Impact { get; }
            public string Keys { get; }
        }

        // One row per class, in SkillDef.For order. Frame: bone x y z, ... | hips offset.
        private static readonly Dictionary<HeroClass, Def[]> Defs = new Dictionary<HeroClass, Def[]>
        {
            [HeroClass.Vanguard] = new[]
            {
                // Rending Arc: a leap into a diagonal overhead cleave, landing in a lunge.
                new Def(18, 8, "0:; 4: upper_arm.R 150 0 0, forearm.R 30 0 0, hand.R -30 0 0, chest -10 10 0, spine -6 0 0, thigh.L 30 0 0, shin.L -40 0 0 | 0 0.18 0;" +
                               " 8: upper_arm.R 40 0 0, forearm.R 10 0 0, hand.R 95 0 0, chest 22 -12 0, spine 12 0 0, thigh.L 45 0 0, shin.L -30 0 0, thigh.R -20 0 0 | 0 -0.12 0.25;" +
                               " 12: upper_arm.R 30 0 0, hand.R 80 0 0, chest 16 -10 0, spine 8 0 0, thigh.L 40 0 0, shin.L -35 0 0, thigh.R -15 0 0 | 0 -0.1 0.2; 18:"),
                // Iron Whirl: two full turns with the glaive held wide and low.
                new Def(22, 6, "0:; 2: upper_arm.R 85 0 0, hand.R 60 0 0, upper_arm.L 40 0 0, chest 8 0 0 | 0 -0.08 0;" +
                               " 5: hips 0 -90 0, upper_arm.R 85 0 0, hand.R 60 0 0, upper_arm.L 40 0 0, chest 8 0 0 | 0 -0.08 0;" +
                               " 8: hips 0 -180 0, upper_arm.R 85 0 0, hand.R 60 0 0, upper_arm.L 40 0 0, chest 8 0 0 | 0 -0.08 0;" +
                               " 11: hips 0 -270 0, upper_arm.R 85 0 0, hand.R 60 0 0, upper_arm.L 40 0 0, chest 8 0 0 | 0 -0.08 0;" +
                               " 14: hips 0 -360 0, upper_arm.R 85 0 0, hand.R 60 0 0, upper_arm.L 40 0 0, chest 8 0 0 | 0 -0.08 0;" +
                               " 16: hips 0 -450 0, upper_arm.R 85 0 0, hand.R 60 0 0, upper_arm.L 40 0 0, chest 8 0 0 | 0 -0.08 0;" +
                               " 18: hips 0 -540 0, upper_arm.R 80 0 0, hand.R 50 0 0, upper_arm.L 30 0 0, chest 6 0 0 | 0 -0.06 0;" +
                               " 20: hips 0 -630 0, upper_arm.R 40 0 0, hand.R 20 0 0 | 0 -0.02 0; 22: hips 0 -720 0"),
                // Blood Fury: a war cry, head back, glaive and shield raised; then the glaive levelled.
                new Def(24, 6, "0:; 5: chest -18 0 0, neck -25 0 0, head -10 0 0, spine -8 0 0, upper_arm.R 130 0 0, forearm.R 20 0 0, upper_arm.L 70 0 0, forearm.L 30 0 0 | 0 -0.04 0;" +
                               " 14: chest -15 0 0, neck -22 0 0, spine -6 0 0, upper_arm.R 125 0 0, forearm.R 15 0 0, upper_arm.L 65 0 0, forearm.L 30 0 0 | 0 -0.04 0;" +
                               " 19: upper_arm.R 70 0 0, hand.R 60 0 0, chest 8 0 0 | 0 -0.03 0.05; 24:"),
                // Honed Edge: the glaive held level, the free hand drawn along the blade, one test swing.
                new Def(26, 12, "0:; 6: upper_arm.R 70 0 0, forearm.R 20 0 0, hand.R 70 0 0, upper_arm.L 60 0 0, forearm.L 50 0 0, chest 6 0 0;" +
                                " 12: upper_arm.R 70 0 0, forearm.R 20 0 0, hand.R 70 0 0, upper_arm.L 85 0 0, forearm.L 5 0 0, chest 8 -10 0;" +
                                " 18: upper_arm.R 110 0 0, hand.R 30 0 0, upper_arm.L 20 0 0, chest -4 10 0;" +
                                " 22: upper_arm.R 40 0 0, hand.R 90 0 0, chest 14 -12 0, spine 6 0 0 | 0 -0.05 0.08; 26:"),
                // Bull Rush: shoulder down behind the shield, a dash into the pack and back.
                new Def(22, 7, "0:; 4: spine 25 0 0, chest 18 0 0, upper_arm.L 75 0 0, forearm.L 20 0 0, upper_arm.R -20 0 0, thigh.L 50 0 0, shin.L -40 0 0, thigh.R -30 0 0 | 0 -0.12 0.3;" +
                               " 7: spine 25 0 0, chest 18 0 0, upper_arm.L 80 0 0, forearm.L 20 0 0, upper_arm.R -20 0 0, thigh.L -20 0 0, thigh.R 45 0 0, shin.R -40 0 0 | 0 -0.1 0.9;" +
                               " 11: spine 20 0 0, chest 14 0 0, upper_arm.L 70 0 0, thigh.L 40 0 0, thigh.R -25 0 0 | 0 -0.1 1.1;" +
                               " 16: spine 8 0 0, upper_arm.L 30 0 0 | 0 -0.04 0.5; 22:"),
            },
            [HeroClass.Kestrel] = new[]
            {
                // Heartseeker: coil, a low lunge and a double stab.
                new Def(16, 6, "0:; 3: upper_arm.R -30 0 0, upper_arm.L -30 0 0, chest -6 0 0, thigh.R -20 0 0 | 0 -0.15 -0.1;" +
                               " 6: upper_arm.R 85 0 0, upper_arm.L 80 0 0, chest 18 0 0, spine 10 0 0, thigh.L 60 0 0, shin.L -55 0 0, thigh.R -35 0 0, shin.R -10 0 0 | 0 -0.22 0.55;" +
                               " 9: upper_arm.R 60 0 0, forearm.R 30 0 0, upper_arm.L 65 0 0, forearm.L 30 0 0, chest 16 0 0, spine 10 0 0, thigh.L 60 0 0, shin.L -55 0 0, thigh.R -35 0 0 | 0 -0.22 0.55;" +
                               " 11: upper_arm.R 90 0 0, upper_arm.L 88 0 0, chest 18 0 0, spine 10 0 0, thigh.L 60 0 0, shin.L -55 0 0, thigh.R -35 0 0 | 0 -0.22 0.6; 16:"),
                // Knife Fan: a twist back and a sweeping throw across the body.
                new Def(16, 7, "0:; 4: chest -6 -40 0, upper_arm.R -40 0 0, forearm.R 30 0 0 | 0 -0.05 0;" +
                               " 7: chest 10 40 0, upper_arm.R 95 0 0, hand.R 20 0 0, upper_arm.L -20 0 0 | 0 -0.05 0.05;" +
                               " 10: chest 8 30 0, upper_arm.R 70 0 0 | 0 -0.03 0.05; 16:"),
                // Kestrel's Dive: a crouch, a leap with the arms high, and a falcon's dive onto the pack.
                new Def(24, 14, "0:; 5: thigh.L 50 0 0, shin.L -80 0 0, thigh.R 50 0 0, shin.R -80 0 0, upper_arm.R -40 0 0, upper_arm.L -40 0 0 | 0 -0.2 0;" +
                                " 10: upper_arm.R 170 0 0, upper_arm.L 170 0 0, spine -10 0 0, chest -10 0 0, thigh.L -10 0 0, thigh.R -10 0 0, shin.L -20 0 0, shin.R -20 0 0 | 0 0.9 0.3;" +
                                " 14: upper_arm.R 100 0 0, upper_arm.L 100 0 0, spine 35 0 0, chest 20 0 0, thigh.L -30 0 0, thigh.R -40 0 0 | 0 0.1 0.9;" +
                                " 18: upper_arm.R 60 0 0, upper_arm.L 60 0 0, spine 15 0 0, thigh.L 50 0 0, shin.L -60 0 0 | 0 -0.15 0.8; 24:"),
                // Venom Cloud: the vial drawn back overhead and thrown, then a step back.
                new Def(18, 8, "0:; 4: upper_arm.R 170 0 0, forearm.R 60 0 0, chest -10 15 0 | 0 0 -0.1;" +
                               " 8: upper_arm.R 60 0 0, chest 15 -15 0, thigh.L 30 0 0 | 0 -0.04 0.05;" +
                               " 12: upper_arm.R 30 0 0, chest 8 0 0 | 0 0 -0.15; 18:"),
                // Shadow Stoop: a low crouch, a spring with both knives high, and a stab straight down (the lane moves her behind the mark).
                new Def(20, 10, "0:; 4: thigh.L 70 0 0, shin.L -110 0 0, thigh.R 70 0 0, shin.R -110 0 0, spine 25 0 0 | 0 -0.3 0;" +
                                " 7: upper_arm.R 170 0 0, upper_arm.L 170 0 0, spine -5 0 0 | 0 0.3 0;" +
                                " 10: upper_arm.R 60 0 0, forearm.R 30 0 0, upper_arm.L 60 0 0, forearm.L 30 0 0, spine 35 0 0, chest 20 0 0 | 0 -0.2 0;" +
                                " 14: upper_arm.R 55 0 0, forearm.R 30 0 0, upper_arm.L 55 0 0, forearm.L 30 0 0, spine 30 0 0, chest 18 0 0 | 0 -0.2 0; 20:"),
            },
            [HeroClass.Wraithsworn] = new[]
            {
                // Void Lance: the void hand drawn back, then thrust out as the lance leaves it.
                new Def(16, 6, "0:; 3: upper_arm.L -30 0 0, forearm.L 60 0 0, chest -8 25 0, upper_arm.R 20 0 0;" +
                               " 6: upper_arm.L 90 0 0, hand.L 10 0 0, chest 12 -30 0, spine 6 0 0, thigh.L 30 0 0 | 0 -0.05 0.15;" +
                               " 10: upper_arm.L 85 0 0, chest 10 -25 0, thigh.L 25 0 0 | 0 -0.04 0.12; 16:"),
                // Grave Tide: the sword raised overhead and driven down into the ground on one knee.
                new Def(22, 10, "0:; 5: upper_arm.R 165 0 0, forearm.R 20 0 0, hand.R 20 0 0, chest -12 0 0, spine -6 0 0 | 0 0.05 0;" +
                                " 10: upper_arm.R 60 0 0, hand.R 90 0 0, spine 35 0 0, chest 25 0 0, thigh.L 70 0 0, shin.L -80 0 0, thigh.R -10 0 0, shin.R -100 0 0 | 0 -0.35 0.1;" +
                                " 15: upper_arm.R 58 0 0, hand.R 90 0 0, spine 33 0 0, chest 24 0 0, thigh.L 70 0 0, shin.L -80 0 0, thigh.R -10 0 0, shin.R -100 0 0 | 0 -0.35 0.1; 22:"),
                // Pact Frenzy: arms out and head back as the pact takes him, then two quick cuts.
                new Def(24, 5, "0:; 5: upper_arm.R 60 0 0, forearm.R 20 0 0, upper_arm.L 60 0 0, forearm.L 20 0 0, chest -15 0 0, neck -25 0 0;" +
                               " 10: upper_arm.R 62 0 0, upper_arm.L 62 0 0, chest -12 0 0, neck -22 0 0;" +
                               " 14: upper_arm.R 150 0 0, forearm.R 30 0 0, chest -6 15 0;" +
                               " 17: upper_arm.R 30 0 0, hand.R -15 0 0, chest 14 -20 0, spine 8 0 0 | 0 -0.05 0.1;" +
                               " 20: upper_arm.R 120 0 0, chest -5 15 0; 24:"),
                // Grave Chains: both hands out, then a clenched pull back that holds the chains.
                new Def(20, 7, "0:; 4: upper_arm.L 90 0 0, upper_arm.R 60 0 0, chest 6 0 0;" +
                               " 7: upper_arm.L 80 0 0, forearm.L 70 0 0, upper_arm.R 50 0 0, forearm.R 40 0 0, chest -12 0 0, spine -6 0 0 | 0 0 -0.12;" +
                               " 14: upper_arm.L 78 0 0, forearm.L 72 0 0, upper_arm.R 48 0 0, forearm.R 42 0 0, chest -12 0 0, spine -6 0 0 | 0 0 -0.12; 20:"),
                // Shroud of Night: arms crossed and head bowed, then opened wide as the night falls.
                new Def(22, 8, "0:; 4: upper_arm.R 60 0 0, forearm.R 110 0 0, upper_arm.L 60 0 0, forearm.L 110 0 0, neck 20 0 0, chest 8 0 0;" +
                               " 8: upper_arm.R 110 0 0, upper_arm.L 110 0 0, chest -10 0 0, neck -15 0 0;" +
                               " 16: upper_arm.R 105 0 0, upper_arm.L 105 0 0, chest -8 0 0, neck -12 0 0; 22:"),
            },
            [HeroClass.Drumcaller] = new[]
            {
                // Sky Hammer: the staff raised high, then the drum struck hard.
                new Def(16, 7, "0:; 4: upper_arm.R 165 0 0, forearm.R 10 0 0, upper_arm.L 40 0 0, forearm.L 30 0 0, chest -10 0 0;" +
                               " 7: upper_arm.R 55 0 0, upper_arm.L 55 0 0, forearm.L 50 0 0, chest 14 0 0, spine 8 0 0 | 0 -0.08 0;" +
                               " 11: upper_arm.R 60 0 0, chest 10 0 0 | 0 -0.04 0; 16:"),
                // Storm Drum: two heavy beats, the knees giving on each.
                new Def(20, 5, "0:; 3: upper_arm.R 90 0 0, forearm.R 30 0 0, upper_arm.L 45 0 0, forearm.L 40 0 0, chest -4 0 0;" +
                               " 5: upper_arm.R 40 0 0, upper_arm.L 45 0 0, forearm.L 40 0 0, chest 10 0 0 | 0 -0.06 0;" +
                               " 9: upper_arm.R 95 0 0, upper_arm.L 45 0 0, forearm.L 40 0 0, chest -2 0 0;" +
                               " 11: upper_arm.R 35 0 0, upper_arm.L 45 0 0, forearm.L 40 0 0, chest 12 0 0 | 0 -0.08 0; 20:"),
                // War Rhythm: a swaying drum dance, a beat on each step.
                new Def(24, 4, "0:; 4: hips 0 15 0, chest 0 -10 0, upper_arm.R 70 0 0, upper_arm.L 45 0 0, forearm.L 40 0 0, thigh.L 25 0 0 | 0 -0.05 0;" +
                               " 8: hips 0 -15 0, chest 0 10 0, upper_arm.R 30 0 0, upper_arm.L 45 0 0, forearm.L 40 0 0, thigh.R 25 0 0;" +
                               " 12: hips 0 15 0, chest 0 -10 0, upper_arm.R 70 0 0, upper_arm.L 45 0 0, forearm.L 40 0 0, thigh.L 25 0 0 | 0 -0.05 0;" +
                               " 16: hips 0 -15 0, chest 0 10 0, upper_arm.R 30 0 0, upper_arm.L 45 0 0, forearm.L 40 0 0, thigh.R 25 0 0;" +
                               " 20: upper_arm.R 60 0 0, upper_arm.L 40 0 0 | 0 -0.03 0; 24:"),
                // Hunter's Blessing: the bell staff lifted straight up, the face to the sky.
                new Def(22, 8, "0:; 6: upper_arm.R 175 0 0, chest -14 0 0, neck -20 0 0, spine -6 0 0, upper_arm.L 30 0 0;" +
                               " 14: upper_arm.R 172 0 0, chest -12 0 0, neck -18 0 0, spine -5 0 0, upper_arm.L 30 0 0; 22:"),
                // Mirror Ward: the staff planted forward and the drum raised as a shield.
                new Def(18, 7, "0:; 4: upper_arm.R 110 0 0, forearm.R 20 0 0;" +
                               " 7: upper_arm.R 70 0 0, hand.R 20 0 0, upper_arm.L 85 0 0, forearm.L 10 0 0, chest 6 0 0, thigh.L 30 0 0 | 0 -0.06 0.1;" +
                               " 13: upper_arm.R 68 0 0, hand.R 20 0 0, upper_arm.L 82 0 0, forearm.L 10 0 0, chest 6 0 0, thigh.L 30 0 0 | 0 -0.06 0.1; 18:"),
            },
        };

        public static string ClipName(int slot) => "Cast" + slot;

        private static Def DefOf(HeroClass cls, int slot)
        {
            Def[] row = Defs.TryGetValue(cls, out Def[] found) ? found : Defs[HeroClass.Vanguard];
            return row[Mathf.Clamp(slot, 0, row.Length - 1)];
        }

        /// <summary>Seconds from the start of a cast to its strike, when the effect lands.</summary>
        public static float ImpactSeconds(HeroClass cls, int slot) => DefOf(cls, slot).Impact / Fps;

        public static float LengthSeconds(HeroClass cls, int slot) => DefOf(cls, slot).Length / Fps;

        /// <summary>
        /// Adds the class's cast clips to a hero model's Animation. Call it right after the model is instantiated, before
        /// any clip has played: the bones' pose at that moment is the rest pose the keys are measured from.
        /// </summary>
        public static void Ensure(Animation anim, HeroClass cls)
        {
            if (anim == null || anim.GetClip(ClipName(0)) != null) return;
            var bones = new Dictionary<string, Transform>();
            foreach (Transform t in anim.GetComponentsInChildren<Transform>(true))
            {
                string name = Strip(t.name);
                if (!bones.ContainsKey(name)) bones[name] = t;
            }
            if (!bones.ContainsKey("hips")) return;
            var rest = new Dictionary<string, Quaternion>();
            var paths = new Dictionary<string, string>();
            foreach (string bone in Bones)
            {
                if (!bones.TryGetValue(bone, out Transform t)) continue;
                rest[bone] = t.localRotation;
                paths[bone] = PathFrom(anim.transform, t);
            }
            Vector3 hipsRest = bones["hips"].localPosition;
            Def[] row = Defs.TryGetValue(cls, out Def[] found) ? found : Defs[HeroClass.Vanguard];
            for (int slot = 0; slot < row.Length; slot++)
            {
                AnimationClip clip = Build(row[slot], rest, paths, hipsRest);
                clip.name = ClipName(slot);
                anim.AddClip(clip, ClipName(slot));
            }
        }

        private static AnimationClip Build(Def def, Dictionary<string, Quaternion> rest, Dictionary<string, string> paths, Vector3 hipsRest)
        {
            var clip = new AnimationClip { legacy = true, frameRate = Fps, wrapMode = WrapMode.Once };
            List<(int Frame, Dictionary<string, Vector3> Rot, Vector3 Hips)> keys = Parse(def.Keys);
            foreach (KeyValuePair<string, Quaternion> bone in rest)
            {
                var cx = new AnimationCurve();
                var cy = new AnimationCurve();
                var cz = new AnimationCurve();
                var cw = new AnimationCurve();
                Quaternion previous = bone.Value;
                foreach ((int frame, Dictionary<string, Vector3> rot, Vector3 _) in keys)
                {
                    Vector3 deg = rot.TryGetValue(bone.Key, out Vector3 d) ? d : Vector3.zero;
                    Quaternion q = bone.Value * Delta(deg);
                    // Keep neighbouring keys on the same side of the quaternion sphere (a spin passes through -q).
                    if (Quaternion.Dot(q, previous) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    previous = q;
                    float t = frame / Fps;
                    cx.AddKey(t, q.x);
                    cy.AddKey(t, q.y);
                    cz.AddKey(t, q.z);
                    cw.AddKey(t, q.w);
                }
                Smooth(cx);
                Smooth(cy);
                Smooth(cz);
                Smooth(cw);
                string path = paths[bone.Key];
                clip.SetCurve(path, typeof(Transform), "localRotation.x", cx);
                clip.SetCurve(path, typeof(Transform), "localRotation.y", cy);
                clip.SetCurve(path, typeof(Transform), "localRotation.z", cz);
                clip.SetCurve(path, typeof(Transform), "localRotation.w", cw);
            }
            var px = new AnimationCurve();
            var py = new AnimationCurve();
            var pz = new AnimationCurve();
            foreach ((int frame, Dictionary<string, Vector3> _, Vector3 hips) in keys)
            {
                float t = frame / Fps;
                Vector3 p = hipsRest + new Vector3(-hips.x, hips.y, hips.z);
                px.AddKey(t, p.x);
                py.AddKey(t, p.y);
                pz.AddKey(t, p.z);
            }
            Smooth(px);
            Smooth(py);
            Smooth(pz);
            clip.SetCurve(paths["hips"], typeof(Transform), "localPosition.x", px);
            clip.SetCurve(paths["hips"], typeof(Transform), "localPosition.y", py);
            clip.SetCurve(paths["hips"], typeof(Transform), "localPosition.z", pz);
            clip.EnsureQuaternionContinuity();
            return clip;
        }

        /// <summary>A rig.py pose rotation (degrees about x, then y, then z of the bone) as the imported bone takes it.</summary>
        private static Quaternion Delta(Vector3 deg) =>
            Quaternion.AngleAxis(-deg.z, Vector3.forward) * Quaternion.AngleAxis(-deg.y, Vector3.up) * Quaternion.AngleAxis(deg.x, Vector3.right);

        private static void Smooth(AnimationCurve curve)
        {
            for (int i = 1; i < curve.length - 1; i++) curve.SmoothTangents(i, 0f);
        }

        /// <summary>"4: bone x y z, bone x y z | hx hy hz; 8: ..." into keys; bones left out of a key are at rest.</summary>
        private static List<(int, Dictionary<string, Vector3>, Vector3)> Parse(string text)
        {
            var keys = new List<(int, Dictionary<string, Vector3>, Vector3)>();
            foreach (string raw in text.Split(';'))
            {
                string entry = raw.Trim();
                if (entry.Length == 0) continue;
                int colon = entry.IndexOf(':');
                int frame = int.Parse(entry.Substring(0, colon).Trim(), CultureInfo.InvariantCulture);
                string body = entry.Substring(colon + 1);
                Vector3 hips = Vector3.zero;
                int bar = body.IndexOf('|');
                if (bar >= 0)
                {
                    hips = Vec(body.Substring(bar + 1).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries), 0);
                    body = body.Substring(0, bar);
                }
                var rot = new Dictionary<string, Vector3>();
                foreach (string part in body.Split(','))
                {
                    string[] bits = part.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (bits.Length < 4) continue;
                    rot[bits[0]] = Vec(bits, 1);
                }
                keys.Add((frame, rot, hips));
            }
            return keys;
        }

        private static Vector3 Vec(string[] bits, int at) => new Vector3(
            float.Parse(bits[at], CultureInfo.InvariantCulture),
            float.Parse(bits[at + 1], CultureInfo.InvariantCulture),
            float.Parse(bits[at + 2], CultureInfo.InvariantCulture));

        private static string PathFrom(Transform root, Transform bone)
        {
            string path = bone.name;
            for (Transform t = bone.parent; t != null && t != root; t = t.parent) path = t.name + "/" + path;
            return path;
        }

        /// <summary>Blender suffixes duplicate names ("hips.001"); bones are matched on the bare name.</summary>
        private static string Strip(string name)
        {
            int dot = name.LastIndexOf('.');
            return dot > 0 && name.Length - dot == 4 && int.TryParse(name.Substring(dot + 1), out _) ? name.Substring(0, dot) : name;
        }
    }
}
