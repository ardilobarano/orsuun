using System.Collections.Generic;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// A hero's figure as the stages and the town square draw it: the Vanguard's armour look with its weapon laid as the
    /// lane lays it, another class's band look, or a worn skin's costume (a second look wearing its own cut where one is
    /// drawn), and the pieces that shine with their slot (Glow).
    /// </summary>
    public sealed class HeroFigure
    {
        private static readonly Vector3 VanguardBuild = new Vector3(1.1f, 1.03f, 1.1f);

        public GameObject Root;
        public readonly List<Renderer> Renderers = new List<Renderer>();
        /// <summary>The armour and weapon pieces (weapon true) that take the upgrade glow; a skin's costume has none.</summary>
        public readonly List<(Renderer renderer, bool weapon)> Pieces = new List<(Renderer, bool)>();
        public Animation Anim;

        /// <summary>Builds a figure under <paramref name="parent"/>; without its art it stays an empty object.</summary>
        public static HeroFigure Build(Transform parent, HeroClass cls, int armorBand, int weaponBand, string skinLook, bool secondLook,
            bool weapon = true, string name = "StageHero")
        {
            var f = new HeroFigure { Root = new GameObject(name) };
            f.Root.transform.SetParent(parent, false);
            string skin = LaneView.SkinModel(cls, skinLook, secondLook);
            if (cls == HeroClass.Vanguard)
            {
                f.Root.transform.localScale = VanguardBuild;
                string armorUsed = null;
                GameObject armorPrefab = skin == null && secondLook ? LaneView.LoadLook(LaneView.AltName("Armor_T" + armorBand), out armorUsed) : null;
                if (armorPrefab == null) armorPrefab = LaneView.LoadLook(skin ?? "Armor_T" + armorBand, out armorUsed);
                GameObject weaponPrefab = LaneView.LoadLook("Weapon_T" + weaponBand, out string weaponUsed);
                if (armorPrefab == null) return f;
                GameObject armor = Object.Instantiate(armorPrefab, f.Root.transform);
                Dress(armor, "Looks/" + armorUsed, f.Renderers);
                if (skin == null) foreach (Renderer r in armor.GetComponentsInChildren<Renderer>()) f.Pieces.Add((r, false));
                f.Anim = armor.GetComponent<Animation>();
                if (weaponPrefab != null && weapon)
                {
                    GameObject held = Object.Instantiate(weaponPrefab, f.Root.transform);
                    Dress(held, "Looks/" + weaponUsed, f.Renderers);
                    foreach (Renderer r in held.GetComponentsInChildren<Renderer>()) f.Pieces.Add((r, true));
                    // As the lane lays it: a glaive along this armour's pole, a sword rising from its fist.
                    LaneView.LayWeapon(held, armor.transform, f.Root.transform, f.Anim != null, LaneView.KindOfLook(weaponUsed));
                }
            }
            else
            {
                string look = skin ?? LaneView.ClassLookName(cls, armorBand, secondLook);
                var prefab = look == null ? null : Art.Load<GameObject>("Models/Classes/" + look);
                if (prefab == null) return f;
                GameObject body = Object.Instantiate(prefab, f.Root.transform);
                Dress(body, "Looks/" + look, f.Renderers);
                if (skin == null)
                    foreach (Renderer r in body.GetComponentsInChildren<Renderer>())
                        if (LaneView.SlotOf(r.name) is int slot && slot >= 0) f.Pieces.Add((r, slot == (int)EquipSlot.Weapon));
                f.Anim = body.GetComponent<Animation>();
            }
            return f;
        }

        /// <summary>Plays the idle (if the model has one), animated even off screen when <paramref name="always"/>.</summary>
        public void Idle(bool always)
        {
            if (Anim == null || Anim.GetClip("Idle") == null) return;
            Anim.cullingType = always ? AnimationCullingType.AlwaysAnimate : AnimationCullingType.BasedOnRenderers;
            Anim.Play("Idle");
        }

        /// <summary>Stands the figure's feet on <paramref name="spot"/>, centred over it; returns its bounds there.</summary>
        public Bounds Stand(Vector3 spot)
        {
            Bounds b = Measure(Renderers, spot);
            Root.transform.position += new Vector3(spot.x - b.center.x, spot.y - b.min.y, spot.z - b.center.z);
            return Measure(Renderers, spot);
        }

        /// <summary>Lights the pieces by their glow through property blocks (the looks' materials are shared) and sparkles.</summary>
        public static void Glow(List<(Renderer renderer, bool weapon)> pieces, MaterialPropertyBlock block, float armorGlow, float weaponGlow)
        {
            foreach ((Renderer r, bool weapon) in pieces)
            {
                if (r == null) continue;
                float glow = weapon ? weaponGlow : armorGlow;
                r.GetPropertyBlock(block);
                block.SetFloat(UpgradeGlow.GlowId, glow);
                r.SetPropertyBlock(block);
                if (glow > 0f || r.GetComponentInChildren<GearSparkle>() != null) GearSparkle.On(r, weapon).Set(glow);
            }
        }

        private static void Dress(GameObject part, string material, List<Renderer> renderers)
        {
            var shared = Art.Load<Material>(material);
            foreach (Renderer r in part.GetComponentsInChildren<Renderer>())
            {
                if (shared != null) r.sharedMaterial = shared;
                renderers.Add(r);
            }
        }

        /// <summary>World bounds of the meshes at the bind pose (a skinned renderer's own bounds cover its skeleton).</summary>
        public static Bounds Measure(List<Renderer> renderers, Vector3 stage)
        {
            bool first = true;
            Bounds b = default;
            foreach (Renderer r in renderers)
            {
                Mesh mesh = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                Bounds local = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    Vector3 world = r.transform.TransformPoint(p);
                    if (first) { b = new Bounds(world, Vector3.zero); first = false; }
                    else b.Encapsulate(world);
                }
            }
            return first ? new Bounds(stage + Vector3.up, Vector3.one * 2f) : b;
        }
    }
}
