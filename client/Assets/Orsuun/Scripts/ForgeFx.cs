using System.Collections;
using System.Collections.Generic;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The anvil moment over the Forge (GDD section 6, Presentation): the item on its own screen, hammer beats with
    /// sparks, then the outcome. Success flashes and throws rays in the new level's glow colour, a lost level dulls and
    /// shakes the piece, the Anvil Ward rings, and an Oathbreak splits the item's icon into flying shards. Everything is
    /// uGUI built in code on an overlay canvas above the Forge; sounds come from Resources/Audio (Forge*.wav).
    /// </summary>
    public sealed class ForgeFx : MonoBehaviour
    {
        private const int SparkPool = 140;
        private const int ShardGrid = 4;

        private sealed class Spark
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float Gravity;
            public float Age;
            public float Life;
            public Color Color;
        }

        private sealed class Shard
        {
            public RectTransform Rect;
            public RawImage Image;
            public Vector2 Velocity;
            public float Spin;
        }

        private GameObject _canvas;
        private RectTransform _stage;
        private Image _backdrop;
        private Image _flash;
        private RawImage _glow;
        private RawImage _rays;
        private RawImage _ring;
        private RawImage _item;
        private Image _frame;
        private Text _headline;
        private Text _subline;
        private Text _hint;
        private RectTransform _sparkLayer;
        private readonly List<Spark> _sparks = new List<Spark>();
        private readonly List<Shard> _shards = new List<Shard>();
        private AudioSource _audio;
        private AudioClip _clang, _success, _nine, _lost, _ward, _shatter;
        private CanvasGroup _group;
        private bool _tapped;
        private float _shake;
        private float _ringAge = 1f;
        private Color _ringColor;
        private float _raysAlpha;
        private float _flashAlpha;
        private Color _flashColor;

        public bool Showing => _canvas != null && _canvas.activeSelf;

        public void Init()
        {
            _canvas = Ui.Canvas("ForgeFxCanvas", 20).gameObject;
            _group = _canvas.AddComponent<CanvasGroup>();
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            // The backdrop is also the tap target that ends the hold.
            _backdrop = Ui.Panel("Backdrop", canvas, 0f, 0f, 1f, 1f, new Color(0.02f, 0.02f, 0.05f, 0.94f));
            var tap = _backdrop.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => _tapped = true);

            _stage = Ui.Rect("Stage", canvas, 0f, 0f, 1f, 1f);
            _rays = Raw("Rays", _stage, 0.5f, 0.56f, 1.25f, Texture(TextureKind.Rays));
            _glow = Raw("Glow", _stage, 0.5f, 0.56f, 0.95f, Texture(TextureKind.Glow));
            _ring = Raw("Ring", _stage, 0.5f, 0.56f, 0.6f, Texture(TextureKind.Ring));
            // The icons carry their own dark square: seat them in a bronze inventory-slot frame.
            RectTransform frame = Ui.Rect("Frame", _stage, 0.5f, 0.56f, 0.5f, 0.56f);
            frame.sizeDelta = new Vector2(1080f * 0.465f, 1080f * 0.465f);
            _frame = frame.gameObject.AddComponent<Image>();
            _frame.color = Palette.Trim;
            _frame.raycastTarget = false;
            var bevel = _frame.gameObject.AddComponent<Outline>();
            bevel.effectColor = new Color(0.25f, 0.16f, 0.08f);
            bevel.effectDistance = new Vector2(4f, -4f);
            _item = Raw("Item", _stage, 0.5f, 0.56f, 0.44f, null);
            _sparkLayer = Ui.Rect("Sparks", _stage, 0f, 0f, 1f, 1f);

            _headline = Ui.Title("Headline", _stage, 0.04f, 0.24f, 0.96f, 0.33f, "", 96, TextAnchor.MiddleCenter, Palette.Parchment, carved: false);
            _subline = Ui.Label("Subline", _stage, 0.06f, 0.16f, 0.94f, 0.235f, "", 36, TextAnchor.MiddleCenter, Palette.Muted);
            _hint = Ui.Label("Hint", _stage, 0.1f, 0.06f, 0.9f, 0.1f, "tap to continue", 26, TextAnchor.MiddleCenter, Palette.Muted);

            _flash = Ui.Panel("Flash", canvas, 0f, 0f, 1f, 1f, Color.clear);
            _flash.raycastTarget = false;

            for (int i = 0; i < SparkPool; i++)
            {
                Image img = Ui.Panel("Spark", _sparkLayer, 0.5f, 0.5f, 0.5f, 0.5f, Color.clear);
                img.raycastTarget = false;
                img.rectTransform.sizeDelta = new Vector2(7f, 7f);
                img.gameObject.SetActive(false);
                _sparks.Add(new Spark { Rect = img.rectTransform, Image = img });
            }

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _clang = Resources.Load<AudioClip>("Audio/ForgeClang");
            _success = Resources.Load<AudioClip>("Audio/ForgeSuccess");
            _nine = Resources.Load<AudioClip>("Audio/ForgeNine");
            _lost = Resources.Load<AudioClip>("Audio/ForgeLost");
            _ward = Resources.Load<AudioClip>("Audio/ForgeWard");
            _shatter = Resources.Load<AudioClip>("Audio/ForgeShatter");

            _canvas.SetActive(false);
        }

        /// <summary>The item appears and the hammer falls: two beats on an ordinary attempt, five on +7 and above.</summary>
        public IEnumerator Beats(EquipSlot slot, int levelBefore, float duration, string line)
        {
            Show(slot, levelBefore);
            _subline.text = line;
            _subline.color = Palette.Muted;
            int beats = duration >= 2f ? 5 : 2;
            float glowFrom = 0.25f + 0.05f * levelBefore;
            for (int b = 0; b < beats; b++)
            {
                // Beats close in toward the reveal: the last gap is about half the first.
                float gap = duration / beats * Mathf.Lerp(1.3f, 0.7f, beats == 1 ? 1f : b / (float)(beats - 1));
                Strike(glowFrom + (1f - glowFrom) * (b + 1) / beats * 0.6f);
                yield return Wait(gap);
            }
        }

        /// <summary>A short pulse while the server answers.</summary>
        public void Hold() => SetGlow(_glow.color, 0.9f);

        /// <summary>No result came back (server error): the moment just fades.</summary>
        public IEnumerator Cancel() => Fade();

        /// <summary>Plays the outcome and waits for a tap (or a timeout), then hides.</summary>
        public IEnumerator Reveal(ForgeResult result, string pieceName)
        {
            _hint.text = "";
            float hold;
            switch (result.Outcome)
            {
                case ForgeOutcome.Success:
                    bool nine = result.LevelAfter >= ItemState.MaxUpgradeLevel;
                    Color c = result.LevelAfter >= 7 ? ForgePanel.LevelColor(result.LevelAfter) : new Color(1f, 0.85f, 0.45f);
                    Play(nine ? _nine : _success, 1f);
                    Flash(new Color(1f, 0.93f, 0.75f), nine ? 1f : 0.8f);
                    _raysAlpha = nine ? 1f : 0.75f;
                    _rays.color = c;
                    SetGlow(c, 1f);
                    Burst(nine ? 130 : 60, c, 900f, 0f, 1.1f);
                    Headline(nine ? "+9!" : "SUCCESS  +" + result.LevelAfter, c);
                    _subline.text = nine ? "The whole server hears the hammer." : "The oath holds.";
                    _subline.color = Palette.Parchment;
                    _item.color = Color.white;
                    hold = nine ? 4f : 2.2f;
                    if (nine) StartCoroutine(SecondFlash(c));
                    break;

                case ForgeOutcome.LevelLost:
                    Play(_lost, 1f);
                    _shake = 0.35f;
                    _item.color = new Color(0.45f, 0.42f, 0.42f);
                    _frame.color = Palette.Trim * 0.55f;
                    SetGlow(new Color(0.55f, 0.18f, 0.12f), 0.45f);
                    Burst(26, new Color(0.75f, 0.35f, 0.15f), 260f, 1400f, 0.9f);
                    Headline("-1", Palette.Warn);
                    _subline.text = "The metal sulks.  Back to +" + result.LevelAfter;
                    _subline.color = Palette.Parchment;
                    hold = 2f;
                    break;

                case ForgeOutcome.LevelKept:
                    Play(_ward, 1f);
                    Ring(new Color(0.65f, 0.85f, 1f));
                    SetGlow(new Color(0.55f, 0.75f, 1f), 0.8f);
                    Headline("WARD HOLDS", new Color(0.7f, 0.88f, 1f));
                    _subline.text = "The Anvil Ward took the blow.  Level kept.";
                    _subline.color = Palette.Parchment;
                    hold = 2f;
                    break;

                default: // Oathbreak
                    Play(_shatter, 1f);
                    Flash(new Color(0.85f, 0.12f, 0.08f), 0.85f);
                    _shake = 0.8f;
                    Shatter();
                    SetGlow(new Color(0.5f, 0.06f, 0.04f), 0.7f);
                    Burst(90, new Color(1f, 0.45f, 0.15f), 1100f, 700f, 1.3f);
                    Headline("OATHBREAK", Palette.Bad, carved: true);
                    _subline.text = $"Your +{result.LevelBefore} {pieceName} is gone.";
                    _subline.color = Palette.Parchment;
                    hold = 4f;
                    break;
            }

            // Short beat before a tap counts, so the reveal is never skipped by the tap that started it.
            yield return Wait(0.6f);
            _tapped = false;
            _hint.text = "tap to continue";
            for (float t = 0f; t < hold && !_tapped; t += Time.unscaledDeltaTime) yield return null;
            yield return Fade();
        }

        private void Show(EquipSlot slot, int level)
        {
            _canvas.SetActive(true);
            _tapped = false;
            _shake = 0f;
            _raysAlpha = 0f;
            _flashAlpha = 0f;
            _ringAge = 1f;
            _item.texture = Resources.Load<Texture2D>("Icons/" + slot);
            _item.color = Color.white;
            _item.enabled = _item.texture != null;
            _item.rectTransform.localScale = Vector3.one;
            _frame.enabled = _item.enabled;
            _frame.color = Palette.Trim;
            foreach (Shard s in _shards) Destroy(s.Rect.gameObject);
            _shards.Clear();
            foreach (Spark s in _sparks) s.Rect.gameObject.SetActive(false);
            _headline.text = "";
            _hint.text = "";
            SetGlow(level >= 7 ? ForgePanel.LevelColor(level) : new Color(1f, 0.55f, 0.2f), 0.25f + 0.05f * level);
            _backdrop.color = new Color(0.02f, 0.02f, 0.05f, 0.94f);
            SetAlpha(1f);
        }

        private void Strike(float glow)
        {
            Play(_clang, Random.Range(0.94f, 1.08f));
            _item.rectTransform.localScale = Vector3.one * 1.1f;
            Ring(new Color(1f, 0.7f, 0.35f));
            SetGlow(_glow.color, glow);
            Burst(16, new Color(1f, 0.62f, 0.2f), 650f, 1500f, 0.55f, fromBelow: true);
        }

        private void Ring(Color color)
        {
            _ringAge = 0f;
            _ringColor = color;
        }

        private void Flash(Color color, float alpha)
        {
            _flashColor = color;
            _flashAlpha = alpha;
        }

        private IEnumerator SecondFlash(Color c)
        {
            yield return Wait(0.45f);
            Flash(c, 0.6f);
            Burst(70, c, 1000f, 0f, 1.2f);
            _shake = 0.2f;
        }

        private void SetGlow(Color color, float intensity)
        {
            color.a = Mathf.Clamp01(intensity);
            _glow.color = color;
        }

        private void Headline(string text, Color color, bool carved = false)
        {
            _headline.text = text;
            _headline.color = color;
            _headline.font = carved ? Ui.CarvedFont : Ui.TitleFont;
            _headline.rectTransform.localScale = Vector3.one * 1.6f;
        }

        /// <summary>Sparks from the item's centre, or from its lower edge (the anvil face) for hammer beats.</summary>
        private void Burst(int count, Color color, float speed, float gravity, float life, bool fromBelow = false)
        {
            RectTransform itemRect = _item.rectTransform;
            // Sparks live in a full-screen layer anchored at its centre: place them from the item's world position.
            Vector2 origin = (Vector2)_sparkLayer.InverseTransformPoint(itemRect.position)
                + (fromBelow ? new Vector2(0f, -itemRect.rect.height * 0.3f) : Vector2.zero);
            int started = 0;
            foreach (Spark s in _sparks)
            {
                if (started >= count) break;
                if (s.Rect.gameObject.activeSelf) continue;
                float angle = fromBelow ? Random.Range(20f, 160f) : Random.Range(0f, 360f);
                float v = speed * Random.Range(0.35f, 1f);
                s.Velocity = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * v;
                s.Gravity = gravity;
                s.Age = 0f;
                s.Life = life * Random.Range(0.6f, 1.2f);
                s.Color = Color.Lerp(color, Color.white, Random.Range(0f, 0.45f));
                s.Rect.anchoredPosition = origin + Random.insideUnitCircle * 18f;
                s.Rect.gameObject.SetActive(true);
                started++;
            }
        }

        /// <summary>The item's icon cut into a grid of pieces that fly apart.</summary>
        private void Shatter()
        {
            if (_item.texture == null) return;
            RectTransform itemRect = _item.rectTransform;
            float w = itemRect.rect.width, h = itemRect.rect.height;
            for (int y = 0; y < ShardGrid; y++)
            {
                for (int x = 0; x < ShardGrid; x++)
                {
                    var go = new GameObject("Shard", typeof(RectTransform));
                    var rect = go.GetComponent<RectTransform>();
                    rect.SetParent(_stage, false);
                    rect.anchorMin = rect.anchorMax = itemRect.anchorMin;
                    rect.sizeDelta = new Vector2(w / ShardGrid, h / ShardGrid);
                    Vector2 offset = new Vector2((x + 0.5f) / ShardGrid - 0.5f, (y + 0.5f) / ShardGrid - 0.5f);
                    rect.anchoredPosition = itemRect.anchoredPosition + new Vector2(offset.x * w, offset.y * h);
                    var img = go.AddComponent<RawImage>();
                    img.texture = _item.texture;
                    img.uvRect = new Rect(x / (float)ShardGrid, y / (float)ShardGrid, 1f / ShardGrid, 1f / ShardGrid);
                    img.raycastTarget = false;
                    Vector2 dir = offset.sqrMagnitude > 0.001f ? offset.normalized : Random.insideUnitCircle.normalized;
                    _shards.Add(new Shard
                    {
                        Rect = rect,
                        Image = img,
                        Velocity = (dir + Random.insideUnitCircle * 0.35f) * Random.Range(500f, 1100f) + Vector2.up * 350f,
                        Spin = Random.Range(-540f, 540f),
                    });
                }
            }
            _item.enabled = false;
            _frame.enabled = false;
        }

        private IEnumerator Fade()
        {
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                SetAlpha(1f - t / 0.25f);
                yield return null;
            }
            _canvas.SetActive(false);
            SetAlpha(1f);
        }

        private void SetAlpha(float a) => _group.alpha = a;

        private void Play(AudioClip clip, float pitch)
        {
            if (clip == null) return;
            _audio.pitch = pitch;
            _audio.PlayOneShot(clip);
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        private void Update()
        {
            if (!Showing) return;
            float dt = Time.unscaledDeltaTime;

            // Item punch settles; headline pops in.
            Transform item = _item.rectTransform;
            item.localScale = Vector3.Lerp(item.localScale, Vector3.one, 1f - Mathf.Exp(-14f * dt));
            _frame.rectTransform.localScale = item.localScale;
            _headline.rectTransform.localScale = Vector3.Lerp(_headline.rectTransform.localScale, Vector3.one, 1f - Mathf.Exp(-12f * dt));

            // Shake decays.
            _shake = Mathf.Max(0f, _shake - dt);
            _stage.anchoredPosition = _shake > 0f ? Random.insideUnitCircle * (60f * _shake) : Vector2.zero;

            // Ring expands and fades.
            _ringAge += dt;
            float ringT = Mathf.Clamp01(_ringAge / 0.4f);
            _ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 2.3f, ringT);
            Color rc = _ringColor;
            rc.a = (1f - ringT) * 0.9f;
            _ring.color = rc;

            // Rays turn slowly while they fade.
            _raysAlpha = Mathf.Max(0f, _raysAlpha - dt * 0.35f);
            _rays.rectTransform.Rotate(0f, 0f, 18f * dt);
            Color ray = _rays.color;
            ray.a = _raysAlpha;
            _rays.color = ray;

            // Glow breathes.
            float breathe = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.06f;
            _glow.rectTransform.localScale = Vector3.one * breathe;

            _flashAlpha = Mathf.Max(0f, _flashAlpha - dt * 2.2f);
            Color f = _flashColor;
            f.a = _flashAlpha;
            _flash.color = f;

            foreach (Spark s in _sparks)
            {
                if (!s.Rect.gameObject.activeSelf) continue;
                s.Age += dt;
                if (s.Age >= s.Life)
                {
                    s.Rect.gameObject.SetActive(false);
                    continue;
                }
                s.Velocity += Vector2.down * (s.Gravity * dt);
                s.Velocity *= 1f - 1.2f * dt;
                s.Rect.anchoredPosition += s.Velocity * dt;
                // Streaks point along their flight and stretch with speed.
                float speed = s.Velocity.magnitude;
                s.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(s.Velocity.y, s.Velocity.x) * Mathf.Rad2Deg);
                s.Rect.localScale = new Vector3(1f + speed / 180f, 1f, 1f);
                Color c = s.Color;
                c.a = 1f - s.Age / s.Life;
                s.Image.color = c;
            }

            for (int i = _shards.Count - 1; i >= 0; i--)
            {
                Shard s = _shards[i];
                s.Velocity += Vector2.down * (1500f * dt);
                s.Rect.anchoredPosition += s.Velocity * dt;
                s.Rect.Rotate(0f, 0f, s.Spin * dt);
                Color c = s.Image.color;
                c.a = Mathf.Max(0f, c.a - dt * 0.7f);
                s.Image.color = c;
            }
        }

        // ---- procedural textures: soft glow, light rays, thin ring ----

        private enum TextureKind { Glow, Rays, Ring }

        private static Texture2D Texture(TextureKind kind)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a;
                    switch (kind)
                    {
                        case TextureKind.Glow:
                            a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                            break;
                        case TextureKind.Rays:
                            float angle = Mathf.Atan2(dy, dx);
                            float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 6f)), 12f) + 0.5f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 6f + 0.26f)), 30f);
                            a = Mathf.Clamp01(rays) * Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 4f);
                            break;
                        default:
                            a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.08f);
                            break;
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>A square RawImage centred at (x, y) of the screen, sized as a fraction of the screen width.</summary>
        private static RawImage Raw(string name, Transform parent, float x, float y, float widthFraction, Texture texture)
        {
            RectTransform rect = Ui.Rect(name, parent, x, y, x, y);
            var canvasWidth = 1080f;
            rect.sizeDelta = new Vector2(canvasWidth * widthFraction, canvasWidth * widthFraction);
            var img = rect.gameObject.AddComponent<RawImage>();
            img.texture = texture;
            img.raycastTarget = false;
            img.color = texture == null ? Color.white : Color.clear;
            return img;
        }
    }
}
