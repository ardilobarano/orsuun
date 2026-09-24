using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// SIGN UP / SIGN IN (owner, 24 Sep 2026). Shown once after the title screen on a guest's first launch, and from
    /// MENU (ACCOUNT). CONTINUE WITH APPLE / GOOGLE links the hero being played to that login, or switches this phone to
    /// the hero already linked to it; CREATE ACCOUNT saves an email and password to the hero; SIGN IN switches this phone
    /// to an account made elsewhere; PLAY AS GUEST carries on without one. Signed in, it shows how, links the other
    /// provider, and signs out.
    /// </summary>
    public sealed class AccountPanel : MonoBehaviour
    {
        /// <summary>Set once the player has chosen (guest, sign up or sign in), so the screen does not come back by itself.</summary>
        public const string ChosenKey = "orsuun.accountChosen";

        private enum Mode { Choose, Create, SignIn, SignedIn }

        private GameRoot _root;
        private GameObject _canvas;
        private ConfirmDialog _confirm;
        private Mode _mode;
        private Text _heading;
        private Text _lead;
        private GameObject _choose;
        private GameObject _form;
        private GameObject _signedIn;
        private InputField _email;
        private InputField _password;
        private InputField _repeat;
        private Button _submit;
        private Text _submitLabel;
        private Text _switchLabel;
        private Text _who;
        private Text _message;
        private bool _busy;
        private Button _apple;
        private Button _google;
        private Button _linkApple;
        private Button _linkGoogle;
        private Text _orLabel;

        public bool Showing => _canvas != null && _canvas.activeSelf;

        public static bool Chosen
        {
            get { try { return PlayerPrefs.GetInt(ChosenKey, 0) == 1; } catch { return false; } }
        }

        private static void MarkChosen()
        {
            PlayerPrefs.SetInt(ChosenKey, 1);
            PlayerPrefs.Save();
        }

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("AccountCanvas", 35).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Gate");
            Ui.Title("Game", canvas, 0.05f, 0.86f, 0.95f, 0.93f, "ORSUUN", 72, TextAnchor.MiddleCenter, Palette.Sorn, carved: true, ribbon: false);
            Ui.Title("Sub", canvas, 0.05f, 0.825f, 0.95f, 0.86f, "WAR OF BANNERS", 30, TextAnchor.MiddleCenter, Palette.Trim, carved: true);
            Ui.Trim("Rule", canvas, 0.25f, 0.815f, 0.75f, 0.818f);
            // A card behind the heading and its lead, so they read over the painted gate.
            Ui.Framed("LeadCard", canvas, 0.05f, 0.662f, 0.95f, 0.806f, new Color(0.08f, 0.08f, 0.13f, 0.88f));
            _heading = Ui.Title("Heading", canvas, 0.05f, 0.75f, 0.95f, 0.8f, "", 40, TextAnchor.MiddleCenter, Palette.Parchment);
            _lead = Ui.Label("Lead", canvas, 0.08f, 0.67f, 0.92f, 0.75f, "", 26, TextAnchor.MiddleCenter, Palette.Parchment);

            _choose = Ui.Rect("Choose", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform c = _choose.transform;
            // Apple's and Google's buttons in their own colours: black, and white with dark letters.
            _apple = Ui.Button("Apple", c, 0.15f, 0.585f, 0.85f, 0.645f, "CONTINUE WITH APPLE", 30, new Color(0.04f, 0.04f, 0.05f), () => External("apple"), out _);
            _google = Ui.Button("Google", c, 0.15f, 0.51f, 0.85f, 0.57f, "CONTINUE WITH GOOGLE", 30, new Color(0.97f, 0.97f, 0.97f), () => External("google"), out Text googleLabel);
            googleLabel.color = new Color(0.15f, 0.15f, 0.18f);
            googleLabel.GetComponent<Shadow>().enabled = false;
            _orLabel = Ui.Label("Or", c, 0.15f, 0.47f, 0.85f, 0.505f, "or with an email", 22, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Create", c, 0.15f, 0.405f, 0.49f, 0.465f, "CREATE ACCOUNT", 24, Palette.ButtonForge, () => SetMode(Mode.Create), out _);
            Ui.Button("SignIn", c, 0.51f, 0.405f, 0.85f, 0.465f, "SIGN IN", 24, Palette.Safe, () => SetMode(Mode.SignIn), out _);
            Ui.Button("Guest", c, 0.15f, 0.32f, 0.85f, 0.38f, "PLAY AS GUEST", 28, Palette.ButtonIdle, PlayAsGuest, out _);
            Ui.Label("Switch", c, 0.1f, 0.24f, 0.9f, 0.31f,
                "If that Apple or Google login already has a hero, this phone switches to it. If not, it is linked to the hero you play now.",
                20, TextAnchor.MiddleCenter, Palette.Muted);

            _form = Ui.Rect("Form", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform f = _form.transform;
            _email = Ui.Input("Email", f, 0.1f, 0.58f, 0.9f, 0.64f, "Email", 30, AccountRules.MaxEmail);
            _email.contentType = InputField.ContentType.EmailAddress;
            _password = Ui.Input("Password", f, 0.1f, 0.505f, 0.9f, 0.565f, "Password (8 or more)", 30, AccountRules.MaxPassword);
            _password.contentType = InputField.ContentType.Password;
            _repeat = Ui.Input("Repeat", f, 0.1f, 0.43f, 0.9f, 0.49f, "Password again", 30, AccountRules.MaxPassword);
            _repeat.contentType = InputField.ContentType.Password;
            _submit = Ui.Button("Submit", f, 0.15f, 0.33f, 0.85f, 0.4f, "", 34, Palette.ButtonForge, Submit, out _submitLabel);
            Ui.Button("Switch", f, 0.15f, 0.25f, 0.85f, 0.31f, "", 24, Palette.ButtonIdle,
                () => SetMode(_mode == Mode.Create ? Mode.SignIn : Mode.Create), out _switchLabel);
            Ui.Button("BackToChoice", f, 0.3f, 0.18f, 0.7f, 0.235f, "BACK", 24, Palette.DevGrey, BackFromForm, out _);

            _signedIn = Ui.Rect("SignedIn", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform s = _signedIn.transform;
            _who = Ui.Title("Who", s, 0.08f, 0.58f, 0.92f, 0.66f, "", 30, TextAnchor.MiddleCenter, Palette.Sorn);
            _linkApple = Ui.Button("LinkApple", s, 0.15f, 0.505f, 0.85f, 0.56f, "ALSO LINK APPLE", 26, new Color(0.04f, 0.04f, 0.05f), () => External("apple"), out _);
            _linkGoogle = Ui.Button("LinkGoogle", s, 0.15f, 0.435f, 0.85f, 0.49f, "ALSO LINK GOOGLE", 26, new Color(0.97f, 0.97f, 0.97f), () => External("google"), out Text linkGoogleLabel);
            linkGoogleLabel.color = new Color(0.15f, 0.15f, 0.18f);
            linkGoogleLabel.GetComponent<Shadow>().enabled = false;
            Ui.Button("SignOut", s, 0.15f, 0.345f, 0.85f, 0.405f, "SIGN OUT", 30, Palette.Danger, AskSignOut, out _);
            Ui.Button("Done", s, 0.15f, 0.265f, 0.85f, 0.325f, "BACK TO THE HUNT", 28, Palette.ButtonIdle, Close, out _);

            _message = Ui.Label("Message", canvas, 0.08f, 0.1f, 0.92f, 0.17f, "", 26, TextAnchor.MiddleCenter, Palette.Muted);
            _message.supportRichText = true;
            _canvas.SetActive(false);
            _confirm = new GameObject("AccountConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
            root.Server.ExternalFinished += OnExternalFinished;
        }

        /// <summary>Test builds (-devauth) put the Development server's stand-in provider behind the Google button.</summary>
        private static readonly bool DevAuth = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-devauth") >= 0;

        private bool Has(string provider) => System.Array.IndexOf(_root.Server.Providers, provider) >= 0;

        private bool Offered(string provider) => Has(provider) || (provider == "google" && DevAuth && Has("dev"));

        private bool Linked(string provider) => System.Array.IndexOf(_root.Server.Logins, provider) >= 0
                                                || (provider == "google" && DevAuth && System.Array.IndexOf(_root.Server.Logins, "dev") >= 0);

        private void External(string provider)
        {
            if (_busy) return;
            if (!_root.Server.Online) { Say("Offline: accounts need the server."); return; }
            string actual = provider == "google" && DevAuth && !Has("google") ? "dev" : provider;
            _busy = true;
            Say("Finish signing in on the page that opens...", Palette.Muted);
            StartCoroutine(_root.Server.BeginExternal(actual, error =>
            {
                _busy = false;
                if (error != null) Say(error);
            }));
        }

        private void OnExternalFinished(string message, string error)
        {
            if (error != null)
            {
                if (_canvas.activeSelf) Say(error);
                else _root.Hud.Log(error);
                return;
            }
            MarkChosen();
            _root.Hud.Log(message);
            Close();
        }

        private void Update()
        {
            if (!_canvas.activeSelf) return;
            _apple.gameObject.SetActive(Offered("apple"));
            _google.gameObject.SetActive(Offered("google"));
            _orLabel.gameObject.SetActive(Offered("apple") || Offered("google"));
            _linkApple.gameObject.SetActive(Offered("apple") && !Linked("apple"));
            _linkGoogle.gameObject.SetActive(Offered("google") && !Linked("google"));
            _apple.interactable = _google.interactable = !_busy;
        }

        public void Open()
        {
            _message.text = "";
            _canvas.SetActive(true);
            SetMode(_root.Server.Registered ? Mode.SignedIn : Mode.Choose);
        }

        public void Close() => _canvas.SetActive(false);

        private void SetMode(Mode mode)
        {
            _mode = mode;
            _message.text = "";
            _choose.SetActive(mode == Mode.Choose);
            _form.SetActive(mode == Mode.Create || mode == Mode.SignIn);
            _signedIn.SetActive(mode == Mode.SignedIn);
            _repeat.gameObject.SetActive(mode == Mode.Create);
            _password.text = "";
            _repeat.text = "";
            switch (mode)
            {
                case Mode.Choose:
                    _heading.text = "Keep your hero safe";
                    _lead.text = "A guest hero lives only on this phone. With an account you can play it on any phone, and get it back if this one is lost.";
                    break;
                case Mode.Create:
                    _heading.text = "Create an account";
                    _lead.text = "Your email and a password are saved to the hero you are playing now. Nothing is lost.";
                    _submitLabel.text = "CREATE ACCOUNT";
                    _switchLabel.text = "I already have an account";
                    break;
                case Mode.SignIn:
                    _heading.text = "Sign in";
                    _lead.text = "This phone switches to the hero saved with that email.";
                    _submitLabel.text = "SIGN IN";
                    _switchLabel.text = "I need a new account";
                    break;
                case Mode.SignedIn:
                    _heading.text = "Your account";
                    var ways = new System.Collections.Generic.List<string>();
                    if (Linked("apple")) ways.Add("Apple");
                    if (Linked("google")) ways.Add("Google");
                    if (!string.IsNullOrEmpty(_root.Server.Email)) ways.Add(_root.Server.Email);
                    _lead.text = "This hero is saved. Sign in with it on any phone.";
                    _who.text = "Saved with " + string.Join(" and ", ways);
                    break;
            }
        }

        private void BackFromForm()
        {
            if (_root.Server.Registered) Close();
            else SetMode(Mode.Choose);
        }

        private void PlayAsGuest()
        {
            MarkChosen();
            Close();
        }

        /// <summary>Whether the guest on this phone has anything that signing in elsewhere would leave behind.</summary>
        private bool GuestHasProgress()
        {
            Inventory inv = _root.Session.Inventory;
            return !_root.Server.Registered && (inv.Level > 1 || _root.Session.HighestStageCleared > 0 || inv.Loot.Count > 0);
        }

        private void Submit()
        {
            if (_busy) return;
            if (!_root.Server.Online) { Say("Offline: accounts need the server."); return; }
            string email = AccountRules.NormaliseEmail(_email.text);
            if (AccountRules.EmailProblem(email) is string emailProblem) { Say(emailProblem); return; }
            if (_mode == Mode.Create)
            {
                if (AccountRules.PasswordProblem(_password.text) is string passwordProblem) { Say(passwordProblem); return; }
                if (_password.text != _repeat.text) { Say("The two passwords differ."); return; }
                Run(_root.Server.Register(email, _password.text, Done("Account created. Your hero is safe.", newHero: false)));
                return;
            }
            if (string.IsNullOrEmpty(_password.text)) { Say("Enter your password."); return; }
            string password = _password.text;
            if (GuestHasProgress())
            {
                _confirm.Show("Leave this guest hero?",
                    "Signing in switches this phone to your account. The guest hero you are playing now has no account and cannot be reached again.\n\n"
                    + ConfirmDialog.Tint("To keep it, create an account for it first.", Palette.Bad),
                    "SIGN IN", Palette.Danger, () => Run(_root.Server.SignIn(email, password, Done("Signed in. Welcome back.", newHero: false))));
                return;
            }
            Run(_root.Server.SignIn(email, password, Done("Signed in. Welcome back.", newHero: false)));
        }

        private void AskSignOut()
        {
            _confirm.Show("Sign out?", "This phone starts over with a new guest hero. Your account stays safe: sign in again with your email.",
                "SIGN OUT", Palette.Danger, () => Run(_root.Server.SignOut(Done("Signed out. A new hunt begins.", newHero: true))));
        }

        private void Run(System.Collections.IEnumerator call)
        {
            _busy = true;
            _submit.interactable = false;
            Say("...", Palette.Muted);
            StartCoroutine(call);
        }

        /// <summary>newHero: a brand-new guest follows (sign out), so the first-session guide runs again.</summary>
        private System.Action<string> Done(string success, bool newHero) => error =>
        {
            _busy = false;
            _submit.interactable = true;
            if (error != null) { Say(error); return; }
            MarkChosen();
            if (newHero) Tutorial.Reset();
            _root.Hud.Log(success);
            Close();
        };

        private void Say(string text, Color? color = null) => _message.text = ConfirmDialog.Tint(text, color ?? Palette.Bad);
    }
}
