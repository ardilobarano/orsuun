using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// MENU (where the DEV button was): replay the tutorial, hunt speed, sound on or off, read the privacy policy, delete
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
            Ui.Title("Title", canvas, 0.05f, 0.84f, 0.95f, 0.91f, "MENU", 60, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Trim("Rule", canvas, 0.25f, 0.835f, 0.75f, 0.838f);

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
            Ui.Button("Account", canvas, 0.15f, 0.68f, 0.85f, 0.74f, "", 30, Palette.Safe, OpenAccount, out _accountLabel);
            Ui.Button("Speed", canvas, 0.15f, 0.605f, 0.85f, 0.665f, "", 32, Palette.ButtonIdle, CycleSpeed, out _speedLabel);
            Ui.Button("Sound", canvas, 0.15f, 0.53f, 0.85f, 0.59f, "", 32, Palette.ButtonIdle, () => GameAudio.Instance?.ToggleMute(), out _soundLabel);
            Ui.Button("Privacy", canvas, 0.15f, 0.455f, 0.85f, 0.515f, "PRIVACY POLICY", 32, Palette.ButtonIdle,
                () => Application.OpenURL(_root.Server.BaseUrl + "/privacy"), out _);
            Ui.Button("Delete", canvas, 0.15f, 0.38f, 0.85f, 0.44f, "DELETE ACCOUNT", 32, Palette.Danger, AskDelete, out _);
            if (ShowDevGrant)
            {
                Ui.Button("DevGrant", canvas, 0.15f, 0.305f, 0.49f, 0.365f, "DEV: GRANT", 24, Palette.DevGrey, DevGrant, out _);
                // The playtest server's stand-in sign-in: walks the whole Google / Apple round trip (the in-app sheet on
                // iOS, the browser and the orsuun:// link on Android) before real Google / Apple keys exist.
                Ui.Button("DevSignIn", canvas, 0.51f, 0.305f, 0.85f, 0.365f, "DEV: TEST SIGN-IN", 22, Palette.DevGrey, DevSignIn, out _);
            }
            _status = Ui.Label("Status", canvas, 0.08f, 0.255f, 0.92f, 0.3f, "", 28, TextAnchor.MiddleCenter, Palette.Parchment);
            Ui.Button("Close", canvas, 0.25f, 0.18f, 0.75f, 0.245f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, Close, out _);
            Ui.Label("Version", canvas, 0.05f, 0.10f, 0.95f, 0.14f, "Orsuun: War of Banners  ·  v" + Application.version, 22,
                TextAnchor.MiddleCenter, Palette.Muted);

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
            _accountLabel.text = _root.Server.Registered ? "ACCOUNT\n<size=18>" + _root.Server.Email + "</size>" : "SIGN UP / SIGN IN";
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
