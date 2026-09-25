using System.Collections.Generic;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The character screen's hero (25 Sep 2026, Metin2's character select): one whole hero as the lane shows it (the
    /// class model for its armour band, the Vanguard's armour and glaive looks, or a worn skin's costume) standing in its
    /// idle, turning slowly, rendered by an offscreen camera far below the lane into a transparent texture that sits on
    /// the screen's painted scene.
    /// </summary>
    public sealed class HeroStage : MonoBehaviour
    {
        private static readonly Vector3 Stage = new Vector3(0f, -800f, 0f);
        private static readonly Vector3 VanguardBuild = new Vector3(1.1f, 1.03f, 1.1f);
        private const float Fov = 24f;
        private const float Aspect = 0.8f;

        private Camera _camera;
        private RenderTexture _texture;
        private RawImage _view;
        private Transform _pivot;
        private GameObject _model;
        private string _shown;
        private float _distance = 8f;
        private float _centreY = 1.1f;

        public void Init(RectTransform box)
        {
            _texture = new RenderTexture(640, 800, 24, RenderTextureFormat.ARGB32) { name = "HeroStage", antiAliasing = 2 };
            _view = Ui.Rect("Hero", box, 0f, 0f, 1f, 1f).gameObject.AddComponent<RawImage>();
            var fit = _view.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = Aspect;
            _view.texture = _texture;
            _view.raycastTarget = false;

            _camera = new GameObject("HeroStageCamera").AddComponent<Camera>();
            _camera.transform.SetParent(transform, false);
            _camera.fieldOfView = Fov;
            _camera.aspect = Aspect;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);   // the screen's scene shows through
            _camera.targetTexture = _texture;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 60f;
            _camera.enabled = false;

            var key = new GameObject("HeroStageLight").AddComponent<Light>();
            key.transform.SetParent(transform, false);
            key.transform.position = Stage + new Vector3(-2.5f, 3.5f, -4f);
            key.type = LightType.Point;
            key.range = 30f;
            key.intensity = 3.2f;
            key.color = new Color(1f, 0.86f, 0.68f);
            var rim = new GameObject("HeroStageRim").AddComponent<Light>();
            rim.transform.SetParent(transform, false);
            rim.transform.position = Stage + new Vector3(3f, 2.5f, 3f);
            rim.type = LightType.Point;
            rim.range = 20f;
            rim.intensity = 2f;
            rim.color = new Color(0.6f, 0.7f, 1f);

            _pivot = new GameObject("HeroStagePivot").transform;
            _pivot.SetParent(transform, false);
            _pivot.position = Stage;
        }

        /// <summary>Shows a hero (null: none); call every frame while the screen is open.</summary>
        public void Show(HeroClass? cls, int armorBand, int weaponBand, string skinLook)
        {
            bool visible = cls.HasValue && gameObject.activeInHierarchy;
            _camera.enabled = visible;
            _view.enabled = visible;
            if (!visible) return;
            string key = cls + "/" + armorBand + "/" + weaponBand + "/" + skinLook;
            if (key != _shown)
            {
                _shown = key;
                Build(cls.Value, armorBand, weaponBand, string.IsNullOrEmpty(skinLook) ? null : skinLook);
            }
            _pivot.rotation = Quaternion.Euler(0f, 180f + Mathf.Sin(Time.unscaledTime * 0.5f) * 28f, 0f);
            _camera.transform.position = Stage + new Vector3(0f, _centreY, -_distance);
            _camera.transform.LookAt(Stage + new Vector3(0f, _centreY, 0f));
        }

        private void Build(HeroClass cls, int armorBand, int weaponBand, string skinLook)
        {
            if (_model != null) Destroy(_model);
            _model = new GameObject("StageHero");
            _model.transform.SetParent(_pivot, false);
            var renderers = new List<Renderer>();
            Animation anim = null;
            string skin = LaneView.SkinModel(cls, skinLook);

            if (cls == HeroClass.Vanguard)
            {
                _model.transform.localScale = VanguardBuild;
                GameObject armorPrefab = LaneView.LoadLook(skin ?? "Armor_T" + armorBand, out string armorUsed);
                GameObject weaponPrefab = LaneView.LoadLook("Weapon_T" + weaponBand, out string weaponUsed);
                if (armorPrefab == null) return;
                GameObject armor = Instantiate(armorPrefab, _model.transform);
                Dress(armor, "Looks/" + armorUsed, renderers);
                anim = armor.GetComponent<Animation>();
                if (weaponPrefab != null)
                {
                    GameObject weapon = Instantiate(weaponPrefab, _model.transform);
                    Dress(weapon, "Looks/" + weaponUsed, renderers);
                    Transform grip = FindDeep(armor.transform, "WeaponBase"), tip = FindDeep(armor.transform, "WeaponTip");
                    Renderer[] blade = weapon.GetComponentsInChildren<Renderer>();
                    if (grip != null && tip != null && blade.Length > 0)
                    {
                        // As the lane lays it: from the grip to the tip of this armour's pole, stretched along it only.
                        Transform w = weapon.transform;
                        w.rotation = Quaternion.identity;
                        w.localScale = Vector3.one;
                        w.position = Vector3.zero;
                        float length = blade[0].bounds.size.y;
                        Vector3 axis = tip.position - grip.position;
                        w.rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized) * _model.transform.rotation;
                        w.localScale = new Vector3(1f, length > 0.01f ? axis.magnitude / length : 1f, 1f);
                        w.position = grip.position;
                        if (anim != null) w.SetParent(grip, true);
                    }
                }
            }
            else
            {
                string name = skin ?? LaneView.ClassLookName(cls, armorBand);
                var prefab = name == null ? null : Resources.Load<GameObject>("Models/Classes/" + name);
                if (prefab == null) return;
                GameObject body = Instantiate(prefab, _model.transform);
                Dress(body, "Looks/" + name, renderers);
                anim = body.GetComponent<Animation>();
            }
            if (anim != null && anim.GetClip("Idle") != null)
            {
                anim.cullingType = AnimationCullingType.AlwaysAnimate;
                anim.Play("Idle");
            }

            // Feet on the stage floor, centred; the camera frames the whole figure.
            Bounds b = Measure(renderers);
            _model.transform.position += new Vector3(Stage.x - b.center.x, Stage.y - b.min.y, Stage.z - b.center.z);
            b = Measure(renderers);
            float half = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            _centreY = b.extents.y;
            _distance = Mathf.Max(b.extents.y / half, Mathf.Max(b.extents.x, b.extents.z) / (half * Aspect)) * 1.12f + b.extents.z;
        }

        private static void Dress(GameObject part, string material, List<Renderer> renderers)
        {
            var shared = Resources.Load<Material>(material);
            foreach (Renderer r in part.GetComponentsInChildren<Renderer>())
            {
                if (shared != null) r.sharedMaterial = shared;
                renderers.Add(r);
            }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name || parent.name.StartsWith(name + ".")) return parent;   // Blender suffixes duplicates
            foreach (Transform child in parent)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>World bounds of the meshes at the bind pose (a skinned renderer's own bounds cover its skeleton).</summary>
        private static Bounds Measure(List<Renderer> renderers)
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
            return first ? new Bounds(Stage + Vector3.up, Vector3.one * 2f) : b;
        }

        private void OnDestroy()
        {
            if (_texture != null) _texture.Release();
        }
    }
}
