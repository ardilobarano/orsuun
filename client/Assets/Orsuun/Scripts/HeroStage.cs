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
        /// <summary>The shown hero's pieces with their slot (weapon or armour), and the glow each was last given.</summary>
        private readonly List<(Renderer renderer, bool weapon)> _pieces = new List<(Renderer, bool)>();
        private float _armorGlow = -1f, _weaponGlow = -1f;
        private MaterialPropertyBlock _block;
        /// <summary>Above 1 the camera comes closer than the whole-figure framing (the inventory's smaller frame).</summary>
        public float Zoom { get; set; } = 1f;
        private float _aspect = Aspect;

        /// <summary>
        /// Old Nergui's river (owner, 28 Sep 2026: "we need to see the char from behind"): the hero stands with his back to
        /// the camera looking out over the painted water, the camera above and behind him, his weapon put away and a rod in
        /// his fist, its line running to a float the river screen moves (SetFloat).
        /// </summary>
        public bool FromBehind { get; private set; }

        /// <summary>The camera placed elsewhere (world from, to): the town square's view, the river's corners for screenshots.</summary>
        public (Vector3 from, Vector3 to)? ViewOverride { get; set; }

        /// <summary>From behind: where the hero stands off his spot and which way he faces (degrees about y, 0 away from
        /// the camera). The town square walks him to the one he goes to see.</summary>
        public Vector3 Walk { get; set; }
        public float Facing { get; set; }
        private bool _rodInHand;
        private Transform _hand, _rod, _float;
        private LineRenderer _line;
        private Animation _anim;
        private float _height = 2f;
        private Vector3? _floatAt;

        /// <summary>A stage at <paramref name="at"/>; from behind with a rod (the river) or without (the town square, his
        /// weapon in hand).</summary>
        public void Init(RectTransform box, Vector3? at = null, bool fromBehind = false, bool rod = true)
        {
            _at = at ?? Below;
            FromBehind = fromBehind;
            _rodInHand = fromBehind && rod;
            // From behind, the view fills a phone screen; otherwise the hero's portrait frame.
            _aspect = fromBehind ? 9f / 16f : Aspect;
            _texture = fromBehind
                ? new RenderTexture(720, 1280, 24, RenderTextureFormat.ARGB32) { name = "RiverStage", antiAliasing = 2 }
                : new RenderTexture(640, 800, 24, RenderTextureFormat.ARGB32) { name = "HeroStage", antiAliasing = 2 };
            _view = Ui.Rect("Hero", box, 0f, 0f, 1f, 1f).gameObject.AddComponent<RawImage>();
            var fit = _view.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = fromBehind ? AspectRatioFitter.AspectMode.EnvelopeParent : AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = _aspect;
            _view.texture = _texture;
            _view.raycastTarget = !fromBehind;
            if (!fromBehind) _view.gameObject.AddComponent<Turner>().Stage = this;

            _camera = new GameObject("HeroStageCamera").AddComponent<Camera>();
            _camera.transform.SetParent(transform, false);
            _camera.fieldOfView = fromBehind ? 44f : Fov;
            _camera.aspect = _aspect;
            if (fromBehind)
            {
                // The river is a place (RiverScene): it reaches to the painted horizon, fills the frame and glints under the bloom.
                _camera.farClipPlane = 260f;
                _camera.backgroundColor = new Color(0.62f, 0.36f, 0.3f, 1f);
                UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(_camera).renderPostProcessing = true;
            }
            _camera.clearFlags = CameraClearFlags.SolidColor;
            if (!fromBehind) _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);   // the screen's scene shows through
            _camera.targetTexture = _texture;
            _camera.nearClipPlane = 0.1f;
            if (!fromBehind) _camera.farClipPlane = 60f;
            _camera.enabled = false;

            var key = new GameObject("HeroStageLight").AddComponent<Light>();
            key.transform.SetParent(transform, false);
            key.transform.position = _at + new Vector3(-2.5f, 3.5f, -4f);
            key.type = LightType.Point;
            key.range = 30f;
            key.intensity = fromBehind ? 1.4f : 3.2f;
            key.color = new Color(1f, 0.86f, 0.68f);
            var rim = new GameObject("HeroStageRim").AddComponent<Light>();
            rim.transform.SetParent(transform, false);
            rim.transform.position = _at + new Vector3(3f, 2.5f, 3f);
            rim.type = LightType.Point;
            rim.range = 20f;
            rim.intensity = 2f;
            rim.color = new Color(0.6f, 0.7f, 1f);
            if (fromBehind)
            {
                // The evening sun ahead of him rims his shoulders gold.
                rim.transform.position = _at + new Vector3(-1.2f, 3.2f, 4.5f);
                rim.intensity = 3.5f;
                rim.range = 9f;
                rim.color = new Color(1f, 0.66f, 0.35f);
            }

            _pivot = new GameObject("HeroStagePivot").transform;
            _pivot.SetParent(transform, false);
            _pivot.position = _at;
        }

        /// <summary>
        /// Shows a hero (null: none); call every frame while the screen is open. The glows (UpgradeGlow.ForLevel of the
        /// worn armour and weapon) light the pieces as the lane does, sparkles and all.
        /// </summary>
        public void Show(HeroClass? cls, int armorBand, int weaponBand, string skinLook, float armorGlow = 0f, float weaponGlow = 0f, bool secondLook = false)
        {
            bool visible = cls.HasValue && gameObject.activeInHierarchy;
            _camera.enabled = visible;
            _view.enabled = visible;
            if (!visible) return;
            string key = cls + "/" + armorBand + "/" + weaponBand + "/" + skinLook + "/" + secondLook;
            if (key != _shown)
            {
                _shown = key;
                Build(cls.Value, armorBand, weaponBand, string.IsNullOrEmpty(skinLook) ? null : skinLook, secondLook);
            }
            if (!Mathf.Approximately(armorGlow, _armorGlow) || !Mathf.Approximately(weaponGlow, _weaponGlow)) Glow(armorGlow, weaponGlow);
            if (FromBehind)
            {
                // His back to us, looking out over the water; the camera above and behind his shoulder.
                _pivot.position = _at + Walk;
                _pivot.rotation = Quaternion.Euler(0f, Facing, 0f);
                // Framed so he stands on the jetty's end (a third of the way up the screen, a third of it tall).
                if (ViewOverride is (Vector3 from, Vector3 to))
                {
                    _camera.transform.position = from;
                    _camera.transform.LookAt(to);
                    return;
                }
                _camera.transform.position = _at + new Vector3(0f, 0.9f * _height, -4.3f * _height);
                _camera.transform.LookAt(_at + new Vector3(0f, 0.7f * _height, 0f));
                return;
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

        private void Build(HeroClass cls, int armorBand, int weaponBand, string skinLook, bool secondLook)
        {
            _yaw = 0f;
            _spin = 0f;
            _pieces.Clear();
            _armorGlow = _weaponGlow = -1f;
            if (_model != null) Destroy(_model);
            HeroFigure figure = HeroFigure.Build(_pivot, cls, armorBand, weaponBand, skinLook, secondLook, weapon: !_rodInHand);
            _model = figure.Root;
            List<Renderer> renderers = figure.Renderers;
            _pieces.AddRange(figure.Pieces);
            Animation anim = figure.Anim;
            _anim = anim;
            if (_rodInHand) TakeRod(renderers);
            if (anim != null && anim.GetClip("Idle") != null)
            {
                anim.cullingType = AnimationCullingType.AlwaysAnimate;
                anim.Play("Idle");
            }

            // Feet on the stage floor, centred; the camera frames the whole figure.
            Bounds b = figure.Stand(_pivot.position);
            float half = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            _centreY = b.extents.y;
            _height = Mathf.Max(0.5f, b.size.y);
            _distance = Mathf.Max(b.extents.y / half, Mathf.Max(b.extents.x, b.extents.z) / (half * Aspect)) * 1.12f + b.extents.z;
        }

        /// <summary>From behind: the weapon put away (the Vanguard's is not laid; the other classes' weapon parts hidden) and
        /// a birch rod in the fist (the Vanguard's WeaponGrip, else hand.R), with its line and float.</summary>
        private void TakeRod(List<Renderer> renderers)
        {
            foreach (Renderer r in renderers)
                if (LaneView.SlotOf(r.name) == (int)EquipSlot.Weapon) r.enabled = false;
            _hand = FindDeep(_model.transform, "WeaponGrip") ?? FindDeep(_model.transform, "hand.R") ?? FindDeep(_model.transform, "Hand.R");
            _rod = MakePart(PrimitiveType.Cylinder, "Rod", new Color(0.55f, 0.43f, 0.3f));
            _float = MakePart(PrimitiveType.Sphere, "Float", new Color(0.92f, 0.22f, 0.14f));
            if (_line == null)
            {
                _line = new GameObject("RodLine").AddComponent<LineRenderer>();
                _line.transform.SetParent(transform, false);
                _line.useWorldSpace = true;
                _line.positionCount = 2;
                _line.numCapVertices = 1;
                _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _line.receiveShadows = false;
                var grey = Art.Load<Material>("GreyBox");
                if (grey != null) { _line.sharedMaterial = grey; _line.material.color = new Color(0.85f, 0.82f, 0.72f); }
            }
            _floatAt = null;
        }

        private Transform MakePart(PrimitiveType type, string name, Color color)
        {
            GameObject go = LaneView.MakePrimitive(type, name);
            go.transform.SetParent(_model.transform, false);
            var r = go.GetComponent<Renderer>();
            var grey = Art.Load<Material>("GreyBox");
            if (grey != null) r.sharedMaterial = LaneView.Tinted(grey, color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>The shown hero's height (world units), for placing things around him.</summary>
        public float Height => _height;

        /// <summary>A stage point on the screen (an overlay canvas's world space is the screen's pixels).</summary>
        public Vector3 ScreenOf(Vector3 world)
        {
            Vector3 v = _camera.WorldToViewportPoint(world);
            var corners = new Vector3[4];
            _view.rectTransform.GetWorldCorners(corners);
            return new Vector3(Mathf.LerpUnclamped(corners[0].x, corners[2].x, v.x), Mathf.LerpUnclamped(corners[0].y, corners[2].y, v.y), 0f);
        }

        /// <summary>The rod's tip, where the line leaves it (world).</summary>
        public Vector3 RodTip { get; private set; }

        /// <summary>Where a cast float rests on the water (world): out in front, on the painted river.</summary>
        public Vector3 FloatRest => _at + new Vector3(1.65f * _height, -RiverScene.WaterDrop + 0.02f, 10f * _height);

        /// <summary>The float's place (world), or null: out of the water, hanging at the rod's tip.</summary>
        public void SetFloat(Vector3? at) => _floatAt = at;

        /// <summary>Loops a clip if the model has it (the town square's walk: Run, then Idle).</summary>
        public void Loop(string clip)
        {
            if (_anim == null || _anim.GetClip(clip) == null || _anim.IsPlaying(clip)) return;
            _anim.CrossFade(clip, 0.15f);
        }

        /// <summary>Plays a clip once (the cast and the strike use the attack), then the idle again.</summary>
        public void PlayOnce(string clip)
        {
            if (_anim == null || _anim.GetClip(clip) == null) return;
            _anim.Stop();
            _anim.Play(clip);
            if (_anim.GetClip("Idle") != null) _anim.CrossFadeQueued("Idle", 0.25f);
        }

        private void LateUpdate()
        {
            if (!_rodInHand || _rod == null || _hand == null) return;
            // The rod follows the fist through the idle, pointing out and up over the water.
            float length = 1.35f * _height;
            Vector3 dir = new Vector3(0.18f, 0.55f, 1f).normalized;
            Vector3 grip = _hand.position;
            _rod.position = grip + dir * (length * 0.42f);
            _rod.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            float thick = 0.012f * _height;
            _rod.localScale = new Vector3(thick, length * 0.5f, thick) / Mathf.Max(0.01f, _model.transform.lossyScale.x);
            RodTip = grip + dir * (length * 0.92f);
            Vector3 bob = _floatAt ?? RodTip + Vector3.down * (0.35f * _height);
            _float.position = bob;
            // The float grows with its distance, so it reads out on the water as it does at the rod.
            float far = Mathf.Clamp01((bob - grip).magnitude / (8f * _height));
            _float.localScale = Vector3.one * (Mathf.Lerp(0.06f, 0.24f, far) * _height) / Mathf.Max(0.01f, _model.transform.lossyScale.x);
            _line.startWidth = 0.005f * _height;
            _line.endWidth = 0.012f * _height;
            _line.SetPosition(0, RodTip);
            _line.SetPosition(1, bob);
        }

        /// <summary>Lights the pieces by their glow through property blocks (the looks' materials are shared) and sparkles.</summary>
        private void Glow(float armorGlow, float weaponGlow)
        {
            _armorGlow = armorGlow;
            _weaponGlow = weaponGlow;
            if (_block == null) _block = new MaterialPropertyBlock();
            HeroFigure.Glow(_pieces, _block, armorGlow, weaponGlow);
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

        private void OnDestroy()
        {
            if (_texture != null) _texture.Release();
        }
    }
}
