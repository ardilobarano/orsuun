using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The Forge's big item picture (docs/concept/screens/mockup-forge.jpg): the weapon or body armour on the anvil as
    /// its real look for its level band (the Vanguard's glaive and armour looks, or the other classes' own models),
    /// turning slowly and glowing by its own level. The stat-only slots show their painted icon large instead. An
    /// offscreen camera far below the lane renders it into a texture, only while the picture is on screen.
    /// </summary>
    public sealed class ItemPreview : MonoBehaviour
    {
        private static readonly Vector3 Stage = new Vector3(0f, -400f, 0f);
        private const float Fov = 26f;
        /// <summary>The picture's shape: wide, like the card it sits in (a fitter keeps it so on any screen).</summary>
        private const float Aspect = 2.6f;

        private Camera _camera;
        private RenderTexture _texture;
        private RawImage _view;
        private RawImage _icon;
        private Transform _pivot;
        private GameObject _model;
        private Material[] _materials = new Material[0];
        private Renderer[] _glowing = new Renderer[0];
        private HeroClass _shownClass;
        private EquipSlot _shownSlot;
        private int _shownTier = -1;
        private bool _weapon;
        /// <summary>The Vanguard's glaive lies across the picture; everything else stands and turns.</summary>
        private bool _lying;
        private float _glow = -1f;
        private float _distance = 6f;

        /// <summary>
        /// Builds the picture inside <paramref name="box"/> (canvas space). The camera, light and model live under this
        /// component's own root object (not the canvas, whose scale would shrink them): switch it off with the screen.
        /// </summary>
        public void Init(RectTransform box)
        {
            _texture = new RenderTexture(1040, 400, 24, RenderTextureFormat.ARGB32) { name = "ItemPreview", antiAliasing = 2 };
            _view = Ui.Rect("Picture", box, 0f, 0f, 1f, 1f).gameObject.AddComponent<RawImage>();
            var fit = _view.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = Aspect;
            _view.texture = _texture;
            _view.raycastTarget = false;
            RectTransform iconBox = Ui.Rect("IconBox", box, 0.25f, 0.06f, 0.75f, 0.94f);
            _icon = Ui.Icon("Icon", iconBox, 0f, 0f, 1f, 1f, "Weapon");

            _camera = new GameObject("ItemPreviewCamera").AddComponent<Camera>();
            _camera.transform.SetParent(transform, false);
            _camera.fieldOfView = Fov;
            _camera.aspect = Aspect;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.05f, 0.045f, 0.09f);
            _camera.targetTexture = _texture;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 60f;
            _camera.enabled = false;
            // Bloom makes the +7..+9 shine read as light, as it does in the lane.
            _camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            // A warm key light from the upper left, like the forge fire; the lane's sun fills the rest.
            var key = new GameObject("ItemPreviewLight").AddComponent<Light>();
            key.transform.SetParent(transform, false);
            key.transform.position = Stage + new Vector3(-3f, 3f, -4f);
            key.type = LightType.Point;
            key.range = 30f;
            key.intensity = 3f;
            key.color = new Color(1f, 0.82f, 0.6f);

            _pivot = new GameObject("ItemPreviewPivot").transform;
            _pivot.SetParent(transform, false);
            _pivot.position = Stage;
        }

        /// <summary>Shows <paramref name="item"/> (null hides the picture); call every frame while the screen is open.</summary>
        public void Show(ItemState item, HeroClass cls)
        {
            bool visible = item != null && gameObject.activeInHierarchy;
            if (!visible)
            {
                _camera.enabled = false;
                return;
            }

            bool looks = UpgradeGlow.IsVisible(item.Slot);
            int tier = looks ? ItemLooks.Tier(item.ItemLevel) : 0;
            if (cls != _shownClass || item.Slot != _shownSlot || tier != _shownTier)
            {
                _shownClass = cls;
                _shownSlot = item.Slot;
                _shownTier = tier;
                Build(item, cls, looks);
            }

            bool model = _model != null;
            _view.enabled = model;
            _camera.enabled = model;
            _icon.enabled = !model;
            if (!model)
            {
                Ui.SetIcon(_icon, Ui.ItemIcon(item));
                return;
            }

            float glow = UpgradeGlow.ForLevel(item.UpgradeLevel);
            if (!Mathf.Approximately(glow, _glow))
            {
                _glow = glow;
                foreach (Material m in _materials) m.SetFloat(UpgradeGlow.GlowId, glow);
                foreach (Renderer r in _glowing)
                    if (glow > 0f || r.GetComponentInChildren<GearSparkle>() != null) GearSparkle.On(r, _weapon).Set(glow);
            }

            // The glaive lies across the picture and sways; armour stands and turns.
            float t = Time.unscaledTime;
            _pivot.rotation = _lying ? Quaternion.Euler(0f, 18f * Mathf.Sin(t * 0.7f), -74f) : Quaternion.Euler(0f, 160f + t * 24f, 0f);
            _camera.transform.position = Stage + new Vector3(0f, 0f, -_distance);
            _camera.transform.LookAt(Stage);
        }

        /// <summary>The model for a weapon or armour piece: the Vanguard's look, or the class model for its band.</summary>
        private static GameObject Pick(ItemState item, HeroClass cls, out string material)
        {
            if (cls == HeroClass.Vanguard)
            {
                GameObject look = LaneView.LoadLook(item.LookId, out material);
                return look;
            }
            material = LaneView.ClassLookName(cls, ItemLooks.Tier(item.ItemLevel));
            return material == null ? null : Art.Load<GameObject>("Models/Classes/" + material);
        }

        private void Build(ItemState item, HeroClass cls, bool looks)
        {
            if (_model != null)
            {
                foreach (Renderer r in _model.GetComponentsInChildren<Renderer>()) Destroy(r.material);
                Destroy(_model);
            }
            _model = null;
            _materials = new Material[0];
            _glowing = new Renderer[0];
            _glow = -1f;
            if (!looks) return;

            GameObject prefab = Pick(item, cls, out string materialName);
            if (prefab == null) return;
            _weapon = item.Slot == EquipSlot.Weapon;
            _pivot.rotation = Quaternion.identity;
            _model = Instantiate(prefab, _pivot);
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.identity;
            var shared = Art.Load<Material>("Looks/" + materialName);

            // The other classes' blades and staves are part of their model (knives sit in both hands), so they show whole:
            // only the parts of the piece on the anvil glow.
            _lying = _weapon && cls == HeroClass.Vanguard;
            var kept = new System.Collections.Generic.List<Renderer>();
            var glowing = new System.Collections.Generic.List<Material>();
            var glowingRenderers = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in _model.GetComponentsInChildren<Renderer>())
            {
                if (shared != null) r.sharedMaterial = shared;
                kept.Add(r);
                bool isWeapon = LaneView.SlotOf(r.name) == (int)EquipSlot.Weapon;
                if (cls == HeroClass.Vanguard || isWeapon == _weapon) { glowing.Add(r.material); glowingRenderers.Add(r); }
                else r.material.SetFloat(UpgradeGlow.GlowId, 0f);
            }
            if (kept.Count == 0) { Destroy(_model); _model = null; return; }
            _materials = glowing.ToArray();
            _glowing = glowingRenderers.ToArray();

            var anim = _model.GetComponent<Animation>();
            if (anim != null && anim.GetClip("Idle") != null) anim.Play("Idle");

            // Centre it on the pivot, then fit the camera to it as it will be shown (lying or standing).
            Bounds b = Measure(kept);
            _model.transform.position -= b.center - Stage;
            _pivot.rotation = _lying ? Quaternion.Euler(0f, 0f, -74f) : Quaternion.identity;
            b = Measure(kept);
            float half = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float width = _lying ? b.extents.x : Mathf.Max(b.extents.x, b.extents.z);
            _distance = Mathf.Max(b.extents.y / half, width / (half * Aspect)) * 1.08f + b.extents.z * 0.5f;
        }

        /// <summary>
        /// World bounds of the meshes themselves: a skinned renderer's own bounds cover its whole skeleton and read far
        /// too big, so the mesh's bind-pose box is carried through the renderer's transform instead.
        /// </summary>
        private static Bounds Measure(System.Collections.Generic.List<Renderer> renderers)
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
            return first ? renderers[0].bounds : b;
        }

        private void OnDestroy()
        {
            if (_texture != null) _texture.Release();
        }
    }
}
