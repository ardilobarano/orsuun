using System.Collections.Generic;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The character screen's hero (25 Sep 2026, Metin2's character select): one whole hero as the lane shows it (the
    /// class model for its armour band, the Vanguard's armour and glaive looks, or a worn skin's costume) standing in its
    /// idle, turning slowly, rendered by an offscreen camera far below the lane into a transparent texture that sits on
    /// the screen's painted scene. A finger turns it all the way round (owner, 25 Sep 2026: "add turning characters with
    /// sliding with hand"); let go, it spins on a little, and after a few seconds untouched it turns back to the front.
    /// </summary>
    public sealed class HeroStage : MonoBehaviour
    {
        /// <summary>Where a stage stands, far below the lane; each screen with a stage gives its own spot.</summary>
        public static readonly Vector3 Below = new Vector3(0f, -800f, 0f);
        private static readonly Vector3 VanguardBuild = new Vector3(1.1f, 1.03f, 1.1f);
        private const float Fov = 24f;
        private const float Aspect = 0.8f;

        private Vector3 _at = Below;   // this stage's spot
        private Camera _camera;
        private RenderTexture _texture;
        private RawImage _view;
        private Transform _pivot;
        private GameObject _model;
        private string _shown;
        private float _distance = 8f;
        private float _centreY = 1.1f;
        /// <summary>A whole turn for a drag across this share of the screen's width.</summary>
        private const float TurnPerWidth = 1.1f;
        /// <summary>Seconds untouched before the hero turns back to the front and sways again.</summary>
        private const float RestAfter = 3f;
        private float _yaw;          // the finger's turn, degrees
        private float _spin;         // degrees a second, after letting go
        private float _touchedAt = -100f;
        private bool _held;
        /// <summary>Above 1 the camera comes closer than the whole-figure framing (the inventory's smaller frame).</summary>
        public float Zoom { get; set; } = 1f;

        public void Init(RectTransform box, Vector3? at = null)
        {
            _at = at ?? Below;
            _texture = new RenderTexture(640, 800, 24, RenderTextureFormat.ARGB32) { name = "HeroStage", antiAliasing = 2 };
            _view = Ui.Rect("Hero", box, 0f, 0f, 1f, 1f).gameObject.AddComponent<RawImage>();
            var fit = _view.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = Aspect;
            _view.texture = _texture;
            _view.raycastTarget = true;
            _view.gameObject.AddComponent<Turner>().Stage = this;

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
            key.transform.position = _at + new Vector3(-2.5f, 3.5f, -4f);
            key.type = LightType.Point;
            key.range = 30f;
            key.intensity = 3.2f;
            key.color = new Color(1f, 0.86f, 0.68f);
            var rim = new GameObject("HeroStageRim").AddComponent<Light>();
            rim.transform.SetParent(transform, false);
            rim.transform.position = _at + new Vector3(3f, 2.5f, 3f);
            rim.type = LightType.Point;
            rim.range = 20f;
            rim.intensity = 2f;
            rim.color = new Color(0.6f, 0.7f, 1f);

            _pivot = new GameObject("HeroStagePivot").transform;
            _pivot.SetParent(transform, false);
            _pivot.position = _at;
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
            float dt = Time.unscaledDeltaTime;
            float idle = _held ? 0f : Time.unscaledTime - _touchedAt;
            // A finger held still stops the turn it would hand on.
            if (_held) _spin *= Mathf.Exp(-12f * dt);
            else
            {
                _yaw += _spin * dt;
                _spin *= Mathf.Exp(-3f * dt);
                if (idle > RestAfter)
                {
                    _yaw = Mathf.DeltaAngle(0f, _yaw) * Mathf.Exp(-2.5f * dt);
                    _spin = 0f;
                }
            }
            // The slow sway fades out under the finger and back in once the hero is at rest.
            float sway = Mathf.Sin(Time.unscaledTime * 0.5f) * 28f * Mathf.Clamp01((idle - RestAfter) / 1.5f);
            _pivot.rotation = Quaternion.Euler(0f, 180f + _yaw + sway, 0f);
            _camera.transform.position = _at + new Vector3(0f, _centreY, -_distance / Mathf.Max(0.1f, Zoom));
            _camera.transform.LookAt(_at + new Vector3(0f, _centreY, 0f));
        }

        private void Drag(float dx, bool held)
        {
            _held = held;
            _touchedAt = Time.unscaledTime;
            if (!held) return;
            // A finger moving right carries the hero's near side right (a turn to the left, seen from above).
            float turn = -dx / Mathf.Max(1f, Screen.width) * 360f / TurnPerWidth;
            _yaw += turn;
            float dt = Mathf.Max(0.001f, Time.unscaledDeltaTime);
            _spin = Mathf.Lerp(_spin, turn / dt, 0.5f);
        }

        /// <summary>
        /// Screenshots (-dragturn px): a finger dragged across the hero's middle through the event system, from whatever
        /// the raycast finds on top there (logged), so a panel covering the hero would show up.
        /// </summary>
        public System.Collections.IEnumerator DragForShot(float dx)
        {
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            var corners = new Vector3[4];
            _view.rectTransform.GetWorldCorners(corners);
            data.position = (corners[0] + corners[2]) / 2f;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            GameObject top = hits.Count > 0 ? hits[0].gameObject : null;
            GameObject target = ExecuteEvents.GetEventHandler<IDragHandler>(top);
            Debug.Log("dragturn: top " + (top != null ? top.name : "none") + ", drag handler " + (target != null ? target.name : "none"));
            if (target == null) yield break;
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
            const int steps = 15;
            for (int i = 0; i < steps; i++)
            {
                yield return null;
                data.delta = new Vector2(dx / steps, 0f);
                data.position += data.delta;
                ExecuteEvents.Execute(target, data, ExecuteEvents.dragHandler);
            }
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        }

        /// <summary>Takes the finger on the hero's picture.</summary>
        private sealed class Turner : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
        {
            public HeroStage Stage;

            public void OnPointerDown(PointerEventData e)
            {
                Stage._spin = 0f;
                Stage.Drag(0f, true);
            }

            public void OnDrag(PointerEventData e) => Stage.Drag(e.delta.x, true);

            public void OnPointerUp(PointerEventData e) => Stage.Drag(0f, false);

            // The screen closed under the finger: no pointer-up comes.
            private void OnDisable()
            {
                if (Stage != null) Stage._held = false;
            }
        }

        private void Build(HeroClass cls, int armorBand, int weaponBand, string skinLook)
        {
            _yaw = 0f;
            _spin = 0f;
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
            Bounds b = Measure(renderers, _at);
            _model.transform.position += new Vector3(_at.x - b.center.x, _at.y - b.min.y, _at.z - b.center.z);
            b = Measure(renderers, _at);
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
        private static Bounds Measure(List<Renderer> renderers, Vector3 stage)
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

        private void OnDestroy()
        {
            if (_texture != null) _texture.Release();
        }
    }
}
