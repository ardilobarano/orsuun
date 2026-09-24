using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// Title and loading screen: the key art with the logo (Resources/Art/Title), the title theme, the connection
    /// status while the server login runs, and "tap to begin". Tapping crossfades into the hunt music.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        private GameRoot _root;
        private GameObject _canvas;
        private CanvasGroup _group;
        private Text _tap;
        private Text _status;
        private float _shownAt;
        private bool _leaving;
        private float _leaveT;

        public bool Showing => _canvas != null && _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("TitleCanvas", 40).gameObject;
            _group = _canvas.AddComponent<CanvasGroup>();
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Panel("Black", canvas, 0f, 0f, 1f, 1f, Color.black);
            var art = Resources.Load<Texture2D>("Art/Title");
            if (art != null)
            {
                // Full width from the top edge: the logo is never cropped; on tall phones the extra height is the
                // dark ground under the art, where "tap to begin" sits anyway.
                RectTransform box = Ui.Rect("ArtBox", canvas, 0f, 1f, 1f, 1f);
                box.pivot = new Vector2(0.5f, 1f);
                var image = box.gameObject.AddComponent<RawImage>();
                image.texture = art;
                image.raycastTarget = false;
                var fit = box.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
                fit.aspectRatio = art.width / (float)art.height;
            }
            Image shade = Ui.Panel("Shade", canvas, 0f, 0f, 1f, 0.2f, new Color(0f, 0f, 0.02f, 0.55f));
            shade.raycastTarget = false;
            _tap = Ui.Title("Tap", canvas, 0.1f, 0.085f, 0.9f, 0.135f, "TAP TO BEGIN", 52, TextAnchor.MiddleCenter, Palette.Sorn);
            _status = Ui.Label("Status", canvas, 0.05f, 0.045f, 0.95f, 0.075f, "", 24, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Label("Version", canvas, 0.02f, 0.01f, 0.98f, 0.035f, "v" + Application.version, 20, TextAnchor.MiddleRight, Palette.Muted);

            // The whole screen is the button.
            Image hit = Ui.Panel("Hit", canvas, 0f, 0f, 1f, 1f, Color.clear);
            var button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(Begin);

            _shownAt = Time.unscaledTime;
            GameAudio.Instance?.Music("MusicTitle");
        }

        private void Begin()
        {
            if (_leaving || Time.unscaledTime - _shownAt < 0.8f) return;
            _leaving = true;
            GameAudio.Instance?.Music("MusicHunt");
            GameAudio.Instance?.Play("ForgeClang", 0.8f, 0.5f, 0f);
        }

        /// <summary>Skips the screen at once (dev switches, screenshots).</summary>
        public void Skip()
        {
            GameAudio.Instance?.Music("MusicHunt");
            _canvas.SetActive(false);
        }

        private void Update()
        {
            if (!Showing) return;
            string status = _root.Server.Status;
            _status.text = _root.Server.Online ? "Ready  ·  " + status : status.StartsWith("connecting") ? "Connecting to the steppe..." : status;
            Color c = Palette.Sorn;
            c.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
            _tap.color = c;
            if (!_leaving) return;
            _leaveT += Time.unscaledDeltaTime / 0.6f;
            _group.alpha = 1f - _leaveT;
            if (_leaveT >= 1f) _canvas.SetActive(false);
        }
    }
}
