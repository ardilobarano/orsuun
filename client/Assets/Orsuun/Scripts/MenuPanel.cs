using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// MENU (where the DEV button was): the mailbox, replay the tutorial, hunt speed, sound on or off, read the privacy policy, delete
    /// the account (store requirement: an account made in the app can be deleted in the app), and the playtest grant.
    /// </summary>
    public sealed class MenuPanel : MonoBehaviour
    {
        /// <summary>Playtest builds keep the grant; the dev endpoints only exist on a server in Development mode.</summary>
        private static readonly bool ShowDevGrant = true;

        private GameRoot _root;
        private GameObject _canvas;
        private Text _status;
        private Text _soundLabel;
        private Text _speedLabel;
        private Text _accountLabel;
        private Text _mailLabel;
        private Text _languageLabel;
        private Text _achievementsLabel;
        private GameObject _languages;
        private Text _saverLabel;
        private ConfirmDialog _confirm;
        private bool _deleting;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("MenuCanvas", 12).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Gate");
            Ui.Title("Title", canvas, 0.05f, 0.905f, 0.95f, 0.965f, "MENU", 60, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Trim("Rule", canvas, 0.25f, 0.9f, 0.75f, 0.903f);

            Ui.Button("Friends", canvas, 0.51f, 0.83f, 0.85f, 0.89f, "FRIENDS", 30, Palette.Safe, () =>
            {
                Close();
                _root.Friends.Open();
            }, out _);
            Ui.Button("Characters", canvas, 0.15f, 0.83f, 0.49f, 0.89f, "CHARACTERS", 30, Palette.Alloy, () =>
            {
                if (!_root.Server.Online) return;
                Close();
                _root.Server.ChangeCharacter();
            }, out _);
            Ui.Button("HowToPlay", canvas, 0.15f, 0.755f, 0.49f, 0.815f, "HOW TO PLAY", 28, Palette.ButtonIdle, HowToPlay, out _);
            Ui.Button("Messages", canvas, 0.51f, 0.755f, 0.85f, 0.815f, "MESSAGES", 30, Palette.Safe, () =>
            {
                Close();
                _root.Messages.Open();
            }, out _);
            Ui.Button("Account", canvas, 0.15f, 0.68f, 0.49f, 0.74f, "", 26, Palette.Safe, OpenAccount, out _accountLabel);
            Ui.Button("Mailbox", canvas, 0.51f, 0.68f, 0.85f, 0.74f, "MAILBOX", 30, Palette.Safe, () =>
            {
                Close();
                _root.Mail.Open();
            }, out _mailLabel);
            Ui.Button("Speed", canvas, 0.15f, 0.605f, 0.49f, 0.665f, "", 26, Palette.ButtonIdle, CycleSpeed, out _speedLabel);
            // The language button names the language in use in its own words and opens the list (never translated).
            Ui.Button("Language", canvas, 0.51f, 0.605f, 0.85f, 0.665f, "", 26, Palette.Alloy, () => _languages.SetActive(true), out _languageLabel);
            Ui.Raw(_languageLabel);
            Ui.Button("Sound", canvas, 0.15f, 0.53f, 0.49f, 0.59f, "", 28, Palette.ButtonIdle, () => GameAudio.Instance?.ToggleMute(), out _soundLabel);
            Ui.Button("Daily", canvas, 0.51f, 0.53f, 0.85f, 0.59f, "DAILY GIFTS", 28, Palette.ButtonForge, () =>
            {
                Close();
                _root.Daily.Open();
            }, out _);
            Ui.Button("Achievements", canvas, 0.15f, 0.455f, 0.49f, 0.515f, "ACHIEVEMENTS", 24, Palette.ButtonForge, () =>
            {
                Close();
                _root.Achievements.Open();
            }, out _achievementsLabel);
            Ui.Button("Privacy", canvas, 0.51f, 0.455f, 0.85f, 0.515f, "PRIVACY POLICY", 24, Palette.ButtonIdle,
                () => Application.OpenURL(_root.Server.BaseUrl + "/privacy"), out _);
            Ui.Button("Saver", canvas, 0.15f, 0.38f, 0.49f, 0.44f, "", 22, Palette.ButtonIdle, () => _root.GetComponent<Performance>()?.SetSaver(!Performance.Saver), out _saverLabel);
            Ui.Button("Delete", canvas, 0.51f, 0.38f, 0.85f, 0.44f, "DELETE ACCOUNT", 24, Palette.Danger, AskDelete, out _);
            if (ShowDevGrant)
            {
                Ui.Button("DevGrant", canvas, 0.15f, 0.24f, 0.49f, 0.295f, "DEV: GRANT", 24, Palette.DevGrey, DevGrant, out _);
                // The playtest server's stand-in sign-in: walks the whole Google / Apple round trip (the in-app sheet on
                // iOS, the browser and the orsuun:// link on Android) before real Google / Apple keys exist.
                Ui.Button("DevSignIn", canvas, 0.51f, 0.24f, 0.85f, 0.295f, "DEV: TEST SIGN-IN", 22, Palette.DevGrey, DevSignIn, out _);
            }
            _status = Ui.Label("Status", canvas, 0.08f, 0.2f, 0.92f, 0.237f, "", 24, TextAnchor.MiddleCenter, Palette.Parchment);
            Ui.Button("Leaderboards", canvas, 0.15f, 0.305f, 0.49f, 0.365f, "LEADERBOARDS", 24, Palette.Alloy, () =>
            {
                Close();
                _root.Leaderboards.Open();
            }, out _);
            Ui.Button("Invite", canvas, 0.51f, 0.305f, 0.85f, 0.365f, "INVITE A FRIEND", 22, Palette.Alloy, () =>
            {
                Close();
                _root.Invites.Open();
            }, out _);
            Ui.Button("Close", canvas, 0.25f, 0.13f, 0.75f, 0.195f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, Close, out _);
            Ui.Label("Version", canvas, 0.05f, 0.09f, 0.95f, 0.125f, "Orsuun: War of Banners  ·  v" + Application.version, 22,
                TextAnchor.MiddleCenter, Palette.Muted);

            // The language list: every language in its own words, over the menu.
            _languages = Ui.Rect("Languages", canvas, 0f, 0f, 1f, 1f).gameObject;
            Ui.Panel("Shade", _languages.transform, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, 0.7f));
            Ui.Framed("Box", _languages.transform, 0.1f, 0.3f, 0.9f, 0.72f, new Color(0.07f, 0.06f, 0.05f, 0.96f));
            for (int i = 0; i < Loc.Names.Length; i++)
            {
                var lang = (Loc.Lang)i;
                float x0 = i % 2 == 0 ? 0.14f : 0.51f, y1 = 0.69f - i / 2 * 0.1f;
                Ui.Button("Lang" + i, _languages.transform, x0, y1 - 0.08f, x0 + 0.35f, y1, Loc.Names[i], 26, Palette.Alloy, () =>
                {
                    Loc.Set(lang);
                    _languages.SetActive(false);
                }, out Text name);
                Ui.Raw(name);
            }
            Ui.Button("LangClose", _languages.transform, 0.3f, 0.32f, 0.7f, 0.38f, "CLOSE", 24, Palette.ButtonIdle, () => _languages.SetActive(false), out _);
            _languages.SetActive(false);

            _canvas.SetActive(false);
            _confirm = new GameObject("MenuConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
        }

        public void Open()
        {
            _status.text = "";
            _canvas.SetActive(true);
        }

        public void Close()
        {
            if (!_deleting) _canvas.SetActive(false);
        }

        private void HowToPlay()
        {
            Close();
            _root.Tutorial.Begin();
        }

        private void AskDelete()
        {
            if (_deleting) return;
            if (!_root.Server.Online)
            {
                _status.text = "Offline: nothing of yours is stored on the server right now.";
                return;
            }
            _confirm.Show("Delete your account?",
                "Your hero, every item and all progress are removed from the server for good.\n\n"
                + ConfirmDialog.Tint("This cannot be undone.", Palette.Bad),
                "DELETE", Palette.Danger, () => StartCoroutine(Delete()));
        }

        private System.Collections.IEnumerator Delete()
        {
            _deleting = true;
            _status.text = "Deleting...";
            string failure = null;
            yield return _root.Server.DeleteAccount(error => failure = error);
            _deleting = false;
            if (failure != null)
            {
                _status.text = ConfirmDialog.Tint(failure, Palette.Bad);
                yield break;
            }
            Tutorial.Reset();
            _status.text = "Account deleted. A new hunt begins.";
            _root.Hud.Log("Account deleted. A new hunt begins.");
        }

        private void OpenAccount()
        {
            Close();
            _root.Account.Open();
        }

        /// <summary>Hunt speed x1, x3, x8 (the lane runs faster on screen; the server still pays by the clock).</summary>
        private void CycleSpeed()
        {
            _root.SpeedMultiplier = _root.SpeedMultiplier == 1 ? 3 : _root.SpeedMultiplier == 3 ? 8 : 1;
        }

        private void Update()
        {
            if (!_canvas.activeSelf) return;
            _soundLabel.text = GameAudio.Instance != null && GameAudio.Instance.Muted ? "SOUND: OFF" : "SOUND: ON";
            _speedLabel.text = $"HUNT SPEED: x{_root.SpeedMultiplier}";
            _languageLabel.text = Loc.Name;
            _saverLabel.text = Performance.Saver ? "BATTERY SAVER: ON" : "BATTERY SAVER: OFF";
            _accountLabel.text = _root.Server.Registered ? "ACCOUNT\n<size=16>" + _root.Server.Email + "</size>" : "SIGN UP / SIGN IN";
            int unread = _root.Server.Online ? _root.Server.MailUnread : 0;
            _mailLabel.text = unread > 0 ? $"MAILBOX ({unread})" : "MAILBOX";
            int ready = _root.Server.Online ? _root.Server.AchievementsReady : 0;
            _achievementsLabel.text = ready > 0 ? $"ACHIEVEMENTS ({ready})" : "ACHIEVEMENTS";
        }

        private void DevSignIn()
        {
            if (!_root.Server.Online) { _status.text = "Offline."; return; }
            if (System.Array.IndexOf(_root.Server.Providers, "dev") < 0) { _status.text = "This server has no test sign-in."; return; }
            _status.text = "Pick Tester 1 on the page that opens.";
            StartCoroutine(_root.Server.BeginExternal("dev", error => _status.text = error ?? _status.text));
        }

        /// <summary>Playtest shortcut so testers can reach the high Forge levels within one sitting.</summary>
        private void DevGrant()
        {
            if (_root.Server.Online)
            {
                StartCoroutine(_root.Server.DevGrant());
                _status.text = "Granted (playtest server).";
                return;
            }
            Inventory inv = _root.Session.Inventory;
            inv.Sorn += 500_000;
            inv.Materials += 10;
            inv.ScrollsOfMercy += 5;
            inv.KhansAlloys += 1;
            inv.Turnstones += 20;
            for (int r = 0; r < inv.Korshards.Length; r++) inv.Korshards[r] += 3;
            _status.text = "Granted (local).";
        }
    }
}
