using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// MENU (where the DEV button was): replay the tutorial, sound on or off, read the privacy policy, delete the account
    /// (store requirement: an account made in the app can be deleted in the app), and the playtest resource grant.
    /// </summary>
    public sealed class MenuPanel : MonoBehaviour
    {
        /// <summary>Playtest builds keep the grant; the dev endpoints only exist on a server in Development mode.</summary>
        private static readonly bool ShowDevGrant = true;

        private GameRoot _root;
        private GameObject _canvas;
        private Text _status;
        private Text _soundLabel;
        private ConfirmDialog _confirm;
        private bool _deleting;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("MenuCanvas", 12).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Panel("Dim", canvas, 0f, 0f, 1f, 1f, Palette.Dim);
            Ui.Title("Title", canvas, 0.05f, 0.80f, 0.95f, 0.87f, "MENU", 60, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Trim("Rule", canvas, 0.25f, 0.795f, 0.75f, 0.798f);

            Ui.Button("HowToPlay", canvas, 0.15f, 0.7f, 0.85f, 0.765f, "HOW TO PLAY", 34, Palette.ButtonIdle, HowToPlay, out _);
            Ui.Button("Sound", canvas, 0.15f, 0.62f, 0.85f, 0.685f, "", 34, Palette.ButtonIdle, () => GameAudio.Instance?.ToggleMute(), out _soundLabel);
            Ui.Button("Privacy", canvas, 0.15f, 0.54f, 0.85f, 0.605f, "PRIVACY POLICY", 34, Palette.ButtonIdle,
                () => Application.OpenURL(_root.Server.BaseUrl + "/privacy"), out _);
            Ui.Button("Delete", canvas, 0.15f, 0.46f, 0.85f, 0.525f, "DELETE ACCOUNT", 34, Palette.Danger, AskDelete, out _);
            if (ShowDevGrant)
                Ui.Button("DevGrant", canvas, 0.15f, 0.38f, 0.85f, 0.445f, "DEV: GRANT RESOURCES", 28, Palette.DevGrey, DevGrant, out _);
            _status = Ui.Label("Status", canvas, 0.08f, 0.28f, 0.92f, 0.37f, "", 30, TextAnchor.MiddleCenter, Palette.Parchment);
            Ui.Button("Close", canvas, 0.25f, 0.18f, 0.75f, 0.25f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, Close, out _);
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

        private void Update()
        {
            if (_canvas.activeSelf) _soundLabel.text = GameAudio.Instance != null && GameAudio.Instance.Muted ? "SOUND: OFF" : "SOUND: ON";
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
