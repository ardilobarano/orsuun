using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// The oath (world bible: "a new character swears to one Banner"): the three flags side by side with their creed and
    /// culture. Tap one, then SWEAR. The Banner is the account's (every hero rides under it): a new account swears at the
    /// character screen, after signing in or up and before its first hero (owner, 25 Sep 2026); an older account not yet
    /// sworn is asked in the game.
    /// </summary>
    public sealed class BannerOath : MonoBehaviour
    {
        private GameRoot _root;
        private GameObject _canvas;
        private readonly Image[] _frames = new Image[3];
        private Text _choice;
        private Button _swear;
        private Text _swearLabel;
        private Text _status;
        private Text _title;
        private Button _cancel;
        private ConfirmDialog _confirm;
        /// <summary>Changing Banners (once a season, for Oathstones) rather than the first oath.</summary>
        private bool _changing;
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
            _title = Ui.Title("Title", canvas, 0.05f, 0.9f, 0.95f, 0.96f, "SWEAR TO A BANNER", 50, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
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
                // Changing Banners, the one sworn now cannot be picked again.
                pick.onClick.AddListener(() => { if (!_changing || id != _root.Server.Banner) _picked = id; });
            }

            _choice = Ui.Label("Choice", canvas, 0.05f, 0.21f, 0.95f, 0.27f, "", 30, TextAnchor.MiddleCenter, Palette.Parchment);
            _swear = Ui.Button("Swear", canvas, 0.2f, 0.12f, 0.8f, 0.2f, "SWEAR THE OATH", 36, Palette.ButtonForge, Swear, out _swearLabel);
            _status = Ui.Label("Status", canvas, 0.05f, 0.06f, 0.95f, 0.115f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);
            _status.supportRichText = true;
            _cancel = Ui.Button("Cancel", canvas, 0.3f, 0.008f, 0.7f, 0.055f, "KEEP MY BANNER", 22, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);
            _confirm = new GameObject("OathConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _changing = false;
            _picked = Banner.None;
            _title.text = "SWEAR TO A BANNER";
            _swearLabel.text = "SWEAR THE OATH";
            _status.text = "Every hero of your account rides under it. The oath holds for the season.";
            _cancel.gameObject.SetActive(false);
            _canvas.SetActive(true);
        }

        /// <summary>
        /// Changing Banners (world bible: a defection once a season at a cost): pick another Banner and swear anew for
        /// Rules.Banners.ChangeOathstones Oathstones, paid by the hero played now.
        /// </summary>
        public void OpenChange()
        {
            _changing = true;
            _picked = Banner.None;
            _title.text = "CHANGE YOUR BANNER";
            _swearLabel.text = $"SWEAR ANEW  ·  {Banners.ChangeOathstones} OATHSTONES";
            _status.text = $"Once a season, for the whole account. Points already won stay with their Banner. You hold {_root.Session.Inventory.Oathstones} Oathstones.";
            _cancel.gameObject.SetActive(true);
            _canvas.SetActive(true);
        }

        /// <summary>Screenshots of the way in (-firstrun oath).</summary>
        public void SwearForShot(Banner banner)
        {
            _picked = banner;
            Swear();
        }

        private void Swear()
        {
            if (_busy || _picked == Banner.None) return;
            if (_changing)
            {
                Banner next = _picked;
                string problem = Banners.ChangeProblem(_root.Server.Banner, next, null, "", _root.Session.Inventory.Oathstones);
                if (problem != null) { _status.text = ConfirmDialog.Tint(problem, Palette.Bad); return; }
                _confirm.Show("Ride under the " + BannerLook.Name(next) + "?",
                    $"Every hero of your account leaves the {BannerLook.Name(_root.Server.Banner)}. It costs {Banners.ChangeOathstones} Oathstones, and the Banner can change again only next season.",
                    "SWEAR ANEW", Palette.ButtonForge, () => ChangeTo(next));
                return;
            }
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

        private void ChangeTo(Banner next)
        {
            _busy = true;
            _status.text = "Swearing anew...";
            StartCoroutine(_root.Server.ChangeBanner(next, error =>
            {
                _busy = false;
                if (error != null)
                {
                    _status.text = ConfirmDialog.Tint(error, Palette.Bad);
                    return;
                }
                GameAudio.Instance?.Play("ForgeSuccess", 0.8f, 0.5f, 0f);
                _root.Hud.Log("You ride under the " + BannerLook.Name(next) + " now.");
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
                // Changing Banners: the one sworn now is dimmed.
                if (_changing && Banners.All[i].Id == _root.Server.Banner) c *= 0.4f;
                c.a = 1f;
                _frames[i].color = c;
            }
            _choice.text = _picked == Banner.None ? (_changing ? "Tap the Banner you would ride under." : "Tap a Banner.") : "You will ride under the " + BannerLook.Name(_picked) + ".";
            _choice.color = _picked == Banner.None ? Palette.Muted : BannerLook.Color(_picked);
            _swear.interactable = !_busy && _picked != Banner.None;
        }
    }
}
