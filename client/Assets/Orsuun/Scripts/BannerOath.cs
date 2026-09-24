using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The oath (world bible: "a new character swears to one Banner"): the three flags side by side with their creed and
    /// culture. Tap one, then SWEAR. Shown once, online, after the title screen and before the first-session guide.
    /// </summary>
    public sealed class BannerOath : MonoBehaviour
    {
        private GameRoot _root;
        private GameObject _canvas;
        private readonly Image[] _frames = new Image[3];
        private Text _choice;
        private Button _swear;
        private Text _status;
        private Banner _picked = Banner.None;
        private bool _busy;

        public bool Showing => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("OathCanvas", 30).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Oath");
            Ui.Title("Title", canvas, 0.05f, 0.9f, 0.95f, 0.96f, "SWEAR TO A BANNER", 50, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Label("Lead", canvas, 0.06f, 0.85f, 0.94f, 0.9f,
                "Three Banners fight over one question: what should be done with the Korstones? Your Banner sets your side in the War of Banners and the fortress sieges.",
                24, TextAnchor.MiddleCenter, Palette.Parchment);

            for (int i = 0; i < Banners.All.Length; i++)
            {
                BannerDef def = Banners.All[i];
                float x0 = 0.03f + i * 0.32f;
                float x1 = x0 + 0.30f;
                _frames[i] = Ui.Framed("Col" + i, canvas, x0, 0.28f, x1, 0.84f, Palette.PanelDark);
                Transform col = _frames[i].transform;
                RawImage flag = BannerLook.FlagImage("Flag", col, 0.1f, 0.42f, 0.9f, 0.98f);
                BannerLook.Show(flag, def.Id);
                Ui.Title("Name", col, 0.03f, 0.33f, 0.97f, 0.41f, def.Name.ToUpperInvariant(), 30, TextAnchor.MiddleCenter, BannerLook.Color(def.Id));
                Ui.Label("Creed", col, 0.05f, 0.25f, 0.95f, 0.33f, "\"" + def.Creed + "\"", 24, TextAnchor.MiddleCenter, Palette.Sorn);
                Ui.Label("Culture", col, 0.06f, 0.07f, 0.94f, 0.25f, def.Culture, 20, TextAnchor.UpperCenter, Palette.Parchment);
                Ui.Label("Capital", col, 0.05f, 0.01f, 0.95f, 0.07f, "Capital: " + def.Capital, 18, TextAnchor.MiddleCenter, Palette.Muted);
                var pick = _frames[i].gameObject.AddComponent<Button>();
                pick.targetGraphic = _frames[i];
                Banner id = def.Id;
                pick.onClick.AddListener(() => _picked = id);
            }

            _choice = Ui.Label("Choice", canvas, 0.05f, 0.21f, 0.95f, 0.27f, "", 30, TextAnchor.MiddleCenter, Palette.Parchment);
            _swear = Ui.Button("Swear", canvas, 0.2f, 0.12f, 0.8f, 0.2f, "SWEAR THE OATH", 36, Palette.ButtonForge, Swear, out _);
            _status = Ui.Label("Status", canvas, 0.05f, 0.05f, 0.95f, 0.11f, "The oath holds for the season.", 22, TextAnchor.MiddleCenter, Palette.Muted);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _picked = Banner.None;
            _canvas.SetActive(true);
        }

        private void Swear()
        {
            if (_busy || _picked == Banner.None) return;
            _busy = true;
            _status.text = "Swearing...";
            StartCoroutine(_root.Server.Swear(_picked, error =>
            {
                _busy = false;
                if (error != null)
                {
                    _status.text = ConfirmDialog.Tint(error, Palette.Bad);
                    return;
                }
                GameAudio.Instance?.Play("ForgeSuccess", 0.8f, 0.5f, 0f);
                _root.Hud.Log("You swore to the " + BannerLook.Name(_picked) + ".");
                _canvas.SetActive(false);
            }));
        }

        private void Update()
        {
            if (!_canvas.activeSelf) return;
            for (int i = 0; i < _frames.Length; i++)
            {
                bool on = Banners.All[i].Id == _picked;
                Color c = on ? BannerLook.Color(_picked) * 0.45f : Palette.PanelDark;
                c.a = 1f;
                _frames[i].color = c;
            }
            _choice.text = _picked == Banner.None ? "Tap a Banner." : "You will ride under the " + BannerLook.Name(_picked) + ".";
            _choice.color = _picked == Banner.None ? Palette.Muted : BannerLook.Color(_picked);
            _swear.interactable = !_busy && _picked != Banner.None;
        }
    }
}
