using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// SIGN UP / SIGN IN (owner, 24 Sep 2026). The first screen of a new install, straight after the title (owner,
    /// 25 Sep 2026: sign in or up, then the Banner, then the character screen), again after SIGN OUT, and from MENU or the
    /// character screen (ACCOUNT). CONTINUE WITH APPLE / GOOGLE links this account to that login, or switches this phone
    /// to the account already linked to it; CREATE ACCOUNT saves an email and password to the account; SIGN IN switches
    /// this phone to an account made elsewhere; PLAY AS GUEST carries on without one. Signed in, it shows how, links the
    /// other provider, and signs out. Forgot your password? (owner, 27 Sep 2026: "Password reset by email") emails a code
    /// that sets a new one; signed in with an email not yet proven, a row asks for the code the server emailed.
    /// </summary>
    public sealed class AccountPanel : MonoBehaviour
    {
        /// <summary>Set once the player has chosen (guest, sign up or sign in), so the screen does not come back by itself.</summary>
        public const string ChosenKey = "orsuun.accountChosen";

        private enum Mode { Choose, Create, SignIn, SignedIn, Reset }

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
        private Text _switchNote;
        private Text _doneLabel;
        private GameObject _forgotLink;
        private GameObject _reset;
        private InputField _resetEmail;
        private InputField _resetCode;
        private InputField _newPassword;
        private InputField _newRepeat;
        private Button _resetButton;
        private GameObject _verifyRow;
        private InputField _verifyCode;

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

        /// <summary>Whether this screen is the way in: a phone that has not chosen yet (a new install, or signed out) with a guest account.</summary>
        public static bool FirstScreen(Net.ServerLink server) => !Chosen && !server.Registered;

        /// <summary>After SIGN OUT the phone starts over at this screen.</summary>
        private static void ClearChosen()
        {
            PlayerPrefs.DeleteKey(ChosenKey);
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
            Ui.Label("Or", c, 0.15f, 0.47f, 0.85f, 0.505f, "or with an email", 22, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Create", c, 0.15f, 0.405f, 0.49f, 0.465f, "CREATE ACCOUNT", 24, Palette.ButtonForge, () => SetMode(Mode.Create), out _);
            Ui.Button("SignIn", c, 0.51f, 0.405f, 0.85f, 0.465f, "SIGN IN", 24, Palette.Safe, () => SetMode(Mode.SignIn), out _);
            Ui.Button("Guest", c, 0.15f, 0.32f, 0.85f, 0.38f, "PLAY AS GUEST", 28, Palette.ButtonIdle, PlayAsGuest, out _);
            _switchNote = Ui.Label("Switch", c, 0.1f, 0.24f, 0.9f, 0.31f, "", 20, TextAnchor.MiddleCenter, Palette.Muted);

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
            // Signing in, where the repeat field would be.
            _forgotLink = Ui.Button("Forgot", f, 0.25f, 0.44f, 0.75f, 0.485f, "Forgot your password?", 22, Palette.ButtonIdle, () =>
            {
                _resetEmail.text = _email.text;
                SetMode(Mode.Reset);
            }, out _).gameObject;

            _reset = Ui.Rect("Reset", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform r = _reset.transform;
            _resetEmail = Ui.Input("Email", r, 0.1f, 0.585f, 0.9f, 0.645f, "Email", 30, AccountRules.MaxEmail);
            _resetEmail.contentType = InputField.ContentType.EmailAddress;
            Ui.Button("SendCode", r, 0.15f, 0.515f, 0.85f, 0.57f, "EMAIL ME A CODE", 26, Palette.Safe, SendResetCode, out _);
            _resetCode = Ui.Input("Code", r, 0.1f, 0.445f, 0.9f, 0.5f, "The 6-digit code", 30, 6);
            _resetCode.contentType = InputField.ContentType.IntegerNumber;
            _newPassword = Ui.Input("NewPassword", r, 0.1f, 0.38f, 0.9f, 0.435f, "New password (8 or more)", 30, AccountRules.MaxPassword);
            _newPassword.contentType = InputField.ContentType.Password;
            _newRepeat = Ui.Input("NewRepeat", r, 0.1f, 0.315f, 0.9f, 0.37f, "New password again", 30, AccountRules.MaxPassword);
            _newRepeat.contentType = InputField.ContentType.Password;
            _resetButton = Ui.Button("SetPassword", r, 0.15f, 0.24f, 0.85f, 0.3f, "SET NEW PASSWORD", 30, Palette.ButtonForge, SubmitReset, out _);
            Ui.Button("BackToSignIn", r, 0.3f, 0.18f, 0.7f, 0.228f, "BACK", 24, Palette.DevGrey, () => SetMode(Mode.SignIn), out _);

            _signedIn = Ui.Rect("SignedIn", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform s = _signedIn.transform;
            _who = Ui.Title("Who", s, 0.08f, 0.58f, 0.92f, 0.66f, "", 30, TextAnchor.MiddleCenter, Palette.Sorn);
            _linkApple = Ui.Button("LinkApple", s, 0.15f, 0.505f, 0.85f, 0.56f, "ALSO LINK APPLE", 26, new Color(0.04f, 0.04f, 0.05f), () => External("apple"), out _);
            _linkGoogle = Ui.Button("LinkGoogle", s, 0.15f, 0.435f, 0.85f, 0.49f, "ALSO LINK GOOGLE", 26, new Color(0.97f, 0.97f, 0.97f), () => External("google"), out Text linkGoogleLabel);
            linkGoogleLabel.color = new Color(0.15f, 0.15f, 0.18f);
            linkGoogleLabel.GetComponent<Shadow>().enabled = false;
            Ui.Button("SignOut", s, 0.15f, 0.345f, 0.85f, 0.405f, "SIGN OUT", 30, Palette.Danger, AskSignOut, out _);
            Ui.Button("Done", s, 0.15f, 0.265f, 0.85f, 0.325f, "BACK TO THE HUNT", 28, Palette.ButtonIdle, Close, out _doneLabel);
            // An email not yet proven: the code the server sent at sign-up (or a new one) proves it.
            _verifyRow = Ui.Rect("Verify", s, 0f, 0f, 1f, 1f).gameObject;
            Transform v = _verifyRow.transform;
            _verifyCode = Ui.Input("Code", v, 0.1f, 0.19f, 0.44f, 0.245f, "Email code", 26, 6);
            _verifyCode.contentType = InputField.ContentType.IntegerNumber;
            Ui.Button("Check", v, 0.46f, 0.19f, 0.67f, 0.245f, "VERIFY", 24, Palette.ButtonForge, () => VerifyEmail(_verifyCode.text.Trim()), out _);
            Ui.Button("Resend", v, 0.69f, 0.19f, 0.9f, 0.245f, "NEW CODE", 22, Palette.ButtonIdle, () => VerifyEmail(null), out _);

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
            if (!_root.Server.Connected) { Say("Offline: accounts need the server."); return; }
            if (!Offered(provider))
            {
                Say(provider == "apple" ? "Sign in with Apple opens once the game's App Store account is set up. Use Google or an email for now."
                    : "Sign in with Google is not set up on this server yet. Use an email for now.", Palette.Muted);
                return;
            }
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
            // The first screen always shows both (owner, 25 Sep 2026: "login sign up screen with google apple etc"); one
            // not set up yet says so when tapped.
            _apple.gameObject.SetActive(true);
            _google.gameObject.SetActive(true);
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
            _reset.SetActive(mode == Mode.Reset);
            _repeat.gameObject.SetActive(mode == Mode.Create);
            _forgotLink.SetActive(mode == Mode.SignIn);
            _password.text = "";
            _repeat.text = "";
            _resetCode.text = _newPassword.text = _newRepeat.text = _verifyCode.text = "";
            switch (mode)
            {
                case Mode.Choose:
                    // At the character screen (a new install, or after SIGN OUT) this is the way in; in the game, a guest's
                    // way to keep the account.
                    bool gate = _root.Server.InLobby;
                    _heading.text = gate ? "Sign in or sign up" : "Keep your heroes safe";
                    _lead.text = gate ? "Your account keeps up to four heroes, your Banner and your Amber, on any phone."
                        : "A guest account lives only on this phone. With an account you can play your heroes on any phone, and get them back if this one is lost.";
                    _switchNote.text = gate ? "If that Apple or Google login already has an account, this phone switches to it. If not, it signs you up."
                        : "If that Apple or Google login already has an account, this phone switches to it. If not, it is linked to the account you play now.";
                    break;
                case Mode.Create:
                    _heading.text = "Create an account";
                    _lead.text = "Your email and a password are saved to this account: sign in with them on any phone. Nothing is lost.";
                    _submitLabel.text = "CREATE ACCOUNT";
                    _switchLabel.text = "I already have an account";
                    break;
                case Mode.SignIn:
                    _heading.text = "Sign in";
                    _lead.text = "This phone switches to the account saved with that email.";
                    _submitLabel.text = "SIGN IN";
                    _switchLabel.text = "I need a new account";
                    break;
                case Mode.SignedIn:
                    _heading.text = "Your account";
                    var ways = new System.Collections.Generic.List<string>();
                    if (Linked("apple")) ways.Add("Apple");
                    if (Linked("google")) ways.Add("Google");
                    if (!string.IsNullOrEmpty(_root.Server.Email)) ways.Add(_root.Server.Email);
                    bool unproven = !string.IsNullOrEmpty(_root.Server.Email) && !_root.Server.EmailVerified;
                    _lead.text = unproven ? "Verify your email with the code we sent to it, so a forgotten password can always be reset."
                        : "This account and its heroes are saved. Sign in with it on any phone.";
                    _who.text = "Saved with " + string.Join(" and ", ways);
                    _verifyRow.SetActive(unproven);
                    _doneLabel.text = _root.Server.InLobby ? "BACK TO THE HEROES" : "BACK TO THE HUNT";
                    break;
                case Mode.Reset:
                    _heading.text = "New password";
                    _lead.text = "We email a 6-digit code to your account's address. Enter it with a new password: this phone then signs in.";
                    break;
            }
        }

        private void BackFromForm()
        {
            if (_root.Server.Registered) Close();
            else SetMode(Mode.Choose);
        }

        /// <summary>Screenshots of the forms: "signin", "create" or "reset" (a following flag, or nothing, keeps the screen as it opened).</summary>
        public void ShotMode(string mode)
        {
            if (mode == "signin") SetMode(Mode.SignIn);
            else if (mode == "create") SetMode(Mode.Create);
            else if (mode == "reset") SetMode(Mode.Reset);
        }

        /// <summary>Screenshots of the way in (-firstrun guest).</summary>
        public void PlayAsGuestForShot() => PlayAsGuest();

        private void PlayAsGuest()
        {
            MarkChosen();
            Close();
        }

        /// <summary>Whether the guest on this phone has anything that signing in elsewhere would leave behind.</summary>
        private bool GuestHasProgress()
        {
            if (_root.Server.Registered) return false;
            if (_root.Server.InLobby) return _root.Server.Lobby?.characters != null && _root.Server.Lobby.characters.Length > 0;
            Inventory inv = _root.Session.Inventory;
            return inv.Level > 1 || _root.Session.HighestStageCleared > 0 || inv.Loot.Count > 0;
        }

        private void Submit()
        {
            if (_busy) return;
            if (!_root.Server.Connected) { Say("Offline: accounts need the server."); return; }
            string email = AccountRules.NormaliseEmail(_email.text);
            if (AccountRules.EmailProblem(email) is string emailProblem) { Say(emailProblem); return; }
            if (_mode == Mode.Create)
            {
                if (AccountRules.PasswordProblem(_password.text) is string passwordProblem) { Say(passwordProblem); return; }
                if (_password.text != _repeat.text) { Say("The two passwords differ."); return; }
                Run(_root.Server.Register(email, _password.text, Done("Account created. Your heroes are safe.", newHero: false)));
                return;
            }
            if (string.IsNullOrEmpty(_password.text)) { Say("Enter your password."); return; }
            string password = _password.text;
            if (GuestHasProgress())
            {
                _confirm.Show("Leave this guest account?",
                    "Signing in switches this phone to your account. The guest heroes on this phone have no account and cannot be reached again.\n\n"
                    + ConfirmDialog.Tint("To keep them, create an account for them first.", Palette.Bad),
                    "SIGN IN", Palette.Danger, () => Run(_root.Server.SignIn(email, password, Done("Signed in. Welcome back.", newHero: false))));
                return;
            }
            Run(_root.Server.SignIn(email, password, Done("Signed in. Welcome back.", newHero: false)));
        }

        private void SendResetCode()
        {
            if (_busy) return;
            if (!_root.Server.Connected) { Say("Offline: accounts need the server."); return; }
            string email = AccountRules.NormaliseEmail(_resetEmail.text);
            if (AccountRules.EmailProblem(email) is string emailProblem) { Say(emailProblem); return; }
            _busy = true;
            Say("...", Palette.Muted);
            StartCoroutine(_root.Server.ForgotPassword(email, (message, error) =>
            {
                _busy = false;
                Say(error ?? message, error != null ? Palette.Bad : Palette.Good);
            }));
        }

        private void SubmitReset()
        {
            if (_busy) return;
            if (!_root.Server.Connected) { Say("Offline: accounts need the server."); return; }
            string email = AccountRules.NormaliseEmail(_resetEmail.text);
            string code = _resetCode.text.Trim();
            if (AccountRules.EmailProblem(email) is string emailProblem) { Say(emailProblem); return; }
            if (code.Length != 6) { Say("Enter the 6-digit code from the email."); return; }
            if (AccountRules.PasswordProblem(_newPassword.text) is string passwordProblem) { Say(passwordProblem); return; }
            if (_newPassword.text != _newRepeat.text) { Say("The two passwords differ."); return; }
            string password = _newPassword.text;
            System.Action reset = () =>
            {
                _busy = true;
                _resetButton.interactable = false;
                Say("...", Palette.Muted);
                StartCoroutine(_root.Server.ResetPassword(email, code, password, error =>
                {
                    _resetButton.interactable = true;
                    Done("New password set. Welcome back.", newHero: false)(error);
                }));
            };
            if (GuestHasProgress())
            {
                _confirm.Show("Leave this guest account?",
                    "Signing in switches this phone to your account. The guest heroes on this phone have no account and cannot be reached again.\n\n"
                    + ConfirmDialog.Tint("To keep them, create an account for them first.", Palette.Bad),
                    "SIGN IN", Palette.Danger, reset);
                return;
            }
            reset();
        }

        private void VerifyEmail(string code)
        {
            if (_busy) return;
            if (!_root.Server.Connected) { Say("Offline: accounts need the server."); return; }
            if (code != null && code.Length != 6) { Say("Enter the 6-digit code from the email."); return; }
            _busy = true;
            Say("...", Palette.Muted);
            StartCoroutine(_root.Server.VerifyEmail(code, (message, error) =>
            {
                _busy = false;
                if (error == null && _root.Server.EmailVerified) SetMode(Mode.SignedIn);
                Say(error ?? message, error != null ? Palette.Bad : Palette.Good);
            }));
        }

        private void AskSignOut()
        {
            _confirm.Show("Sign out?", "This phone goes back to the sign-in screen. Your account stays safe: sign in again with it.",
                "SIGN OUT", Palette.Danger, () => Run(_root.Server.SignOut(Done("Signed out.", newHero: true))));
        }

        private void Run(System.Collections.IEnumerator call)
        {
            _busy = true;
            _submit.interactable = false;
            Say("...", Palette.Muted);
            StartCoroutine(call);
        }

        /// <summary>newHero: a brand-new guest follows (sign out): the sign-in screen and the first-session guide come again.</summary>
        private System.Action<string> Done(string success, bool newHero) => error =>
        {
            _busy = false;
            _submit.interactable = true;
            if (error != null) { Say(error); return; }
            if (newHero)
            {
                ClearChosen();
                Tutorial.Reset();
            }
            else MarkChosen();
            _root.Hud.Log(success);
            Close();
        };

        private void Say(string text, Color? color = null) => _message.text = ConfirmDialog.Tint(text, color ?? Palette.Bad);
    }
}
