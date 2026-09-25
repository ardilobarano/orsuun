using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// Title and loading screen: the key art with the logo (Resources/Art/Title), the title theme, the connection
    /// status while the server login runs, and "tap to begin". Tapping crossfades into the hunt music. On a new install
    /// (or a phone signed out) it only waits for the server and then gives way by itself to the sign-in screen (owner,
    /// 25 Sep 2026: the game starts at the sign-in screen).
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
        /// <summary>Up and not yet fading: the screens after it open underneath as soon as it starts to fade.</summary>
        public bool Waiting => Showing && !_leaving;

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
            // A new install waits for the server, then gives way to the sign-in screen by itself.
            if (!_tap.gameObject.activeSelf && !_root.Server.Connected) return;
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
            bool gate = AccountPanel.FirstScreen(_root.Server);
            if (gate && _root.Server.Connected && !_leaving && Time.unscaledTime - _shownAt > 1.5f) Begin();
            // Waiting for the server before the sign-in screen: nothing to tap yet (offline, it can still be tapped into local play).
            _tap.gameObject.SetActive(!gate || !status.StartsWith("connecting") && !_root.Server.Connected);
            _status.text = _root.Server.Connected ? "Ready" : status.StartsWith("connecting") ? "Connecting to the steppe..." : status;
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
