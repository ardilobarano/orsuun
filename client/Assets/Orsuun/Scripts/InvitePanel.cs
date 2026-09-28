using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// INVITE A FRIEND (owner, 28 Sep 2026; Rules.Invites): this hero's code to share, what it pays (sorn and a Scroll of
    /// Mercy for both when the friend's hero reaches level 10, never Amber), how many it brought in, and, for a hero still
    /// below level 10 with no code yet, a field for a friend's code. The pay comes by letter. Opened from MENU.
    /// </summary>
    public sealed class InvitePanel : MonoBehaviour
    {
        private GameRoot _root;
        private GameObject _canvas;
        private Text _code, _reward, _sorn, _scrolls, _count, _by, _message;
        private GameObject _enter;
        private InputField _field;
        private Button _copy, _share, _take;
        private InviteDto _invite;
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("InviteCanvas", 17).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);
            Ui.Backdrop(canvas, "Caravan");
            Ui.Title("Title", canvas, 0.05f, 0.9f, 0.95f, 0.96f, "INVITE A FRIEND", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);

            Ui.Framed("CodeBack", canvas, 0.08f, 0.62f, 0.92f, 0.875f, new Color(0.07f, 0.06f, 0.1f, 0.94f)).raycastTarget = false;
            Ui.Label("CodeHead", canvas, 0.1f, 0.82f, 0.9f, 0.86f, "YOUR CODE", 24, TextAnchor.MiddleCenter, Palette.Muted);
            _code = Ui.Title("Code", canvas, 0.1f, 0.73f, 0.9f, 0.82f, "", 72, TextAnchor.MiddleCenter, Palette.Parchment);
            Ui.Raw(_code);
            _copy = Ui.Button("Copy", canvas, 0.14f, 0.64f, 0.49f, 0.705f, "COPY", 26, Palette.ButtonIdle, Copy, out _);
            _share = Ui.Button("Share", canvas, 0.51f, 0.64f, 0.86f, 0.705f, "SHARE", 26, Palette.Alloy, Share, out _);

            Image rewardBack = Ui.Framed("RewardBack", canvas, 0.08f, 0.44f, 0.92f, 0.6f, new Color(0.08f, 0.06f, 0.04f, 0.94f));
            rewardBack.raycastTarget = false;
            _reward = Ui.Label("Reward", canvas, 0.1f, 0.55f, 0.9f, 0.59f, "", 23, TextAnchor.MiddleCenter, Palette.Parchment);
            RectTransform sornBox = Ui.Rect("SornBox", canvas, 0.2f, 0.5f, 0.28f, 0.545f);
            Ui.Icon("Sorn", sornBox, 0f, 0f, 1f, 1f, "Sorn");
            _sorn = Ui.Label("SornText", canvas, 0.31f, 0.5f, 0.9f, 0.545f, "", 26, TextAnchor.MiddleLeft, Palette.Sorn);
            RectTransform scrollBox = Ui.Rect("ScrollBox", canvas, 0.2f, 0.45f, 0.28f, 0.495f);
            Ui.Icon("Scroll", scrollBox, 0f, 0f, 1f, 1f, "ScrollOfMercy");
            _scrolls = Ui.Label("ScrollText", canvas, 0.31f, 0.45f, 0.9f, 0.495f, "", 26, TextAnchor.MiddleLeft, Palette.Sorn);
            _count = Ui.Label("Count", canvas, 0.08f, 0.395f, 0.92f, 0.435f, "", 22, TextAnchor.MiddleCenter, Palette.Parchment);

            _enter = Ui.Rect("Enter", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform enter = _enter.transform;
            Ui.Section("EnterHead", enter, 0.15f, 0.32f, 0.85f, 0.36f, "HAVE A FRIEND'S CODE?", 24);
            _field = Ui.Input("Field", enter, 0.12f, 0.245f, 0.6f, 0.305f, "Their code", 30, 8);
            _field.characterValidation = InputField.CharacterValidation.Alphanumeric;
            _take = Ui.Button("Take", enter, 0.62f, 0.245f, 0.88f, 0.305f, "USE CODE", 24, Palette.ButtonForge, Take, out _);
            _by = Ui.Label("By", canvas, 0.08f, 0.26f, 0.92f, 0.34f, "", 23, TextAnchor.MiddleCenter, Palette.Parchment);
            _by.supportRichText = true;
            Ui.Raw(_by);

            _message = Ui.Label("Message", canvas, 0.06f, 0.14f, 0.94f, 0.23f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Close", canvas, 0.25f, 0.03f, 0.75f, 0.095f, "BACK TO THE HUNT", 28, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _canvas.SetActive(true);
            _message.text = "";
            Fill();
            if (!_root.Server.Online) { _message.text = "Offline: invites need the server."; return; }
            _busy = true;
            StartCoroutine(_root.Server.FetchInvite((dto, error) => Answer(dto, error)));
        }

        public void Close() => _canvas.SetActive(false);

        private void Answer(InviteDto dto, string error)
        {
            _busy = false;
            if (dto != null) _invite = dto;
            _message.text = error ?? dto?.message ?? "";
            Fill();
        }

        private void Fill()
        {
            InviteDto v = _invite;
            _code.text = v == null ? "······" : v.code;
            _copy.interactable = _share.interactable = v != null && !string.IsNullOrEmpty(v.code);
            int level = v?.rewardLevel ?? Invites.RewardLevel;
            _reward.text = $"When a friend's hero reaches level {level}, you both get:";
            _sorn.text = v == null ? "" : v.sorn.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " sorn";
            _scrolls.text = v == null ? "" : v.scrolls == 1 ? "a Scroll of Mercy" : v.scrolls + " Scrolls of Mercy";
            _count.text = v == null ? "" : $"Friends brought in: {v.invited}/{v.maxInvited}  ·  paid: {v.rewarded}";
            bool canEnter = v != null && v.canEnter;
            _enter.SetActive(canEnter);
            _take.interactable = !_busy;
            _by.gameObject.SetActive(!canEnter && v != null);
            if (v == null) return;
            if (!string.IsNullOrEmpty(v.invitedBy))
                _by.text = v.mineRewarded ? $"You came on {v.invitedBy}'s invitation. Your share was sent by letter."
                    : $"You came on {v.invitedBy}'s invitation. Reach level {level} and you are both paid.";
            else _by.text = ConfirmDialog.Tint($"A friend's code is entered before level {level}.", Palette.Muted);
        }

        private void Copy()
        {
            if (_invite == null) return;
            GUIUtility.systemCopyBuffer = _invite.code;
            _message.text = "Copied.";
        }

        /// <summary>The phone's share sheet where there is one (NativeShare), else the clipboard.</summary>
        private void Share()
        {
            if (_invite == null) return;
            string text = Loc.T($"Join me in Orsuun: War of Banners. Enter my code {_invite.code} under MENU, INVITE A FRIEND, and we both get sorn and a Scroll of Mercy at level {_invite.rewardLevel}.");
            if (!NativeShare.Share(text))
            {
                GUIUtility.systemCopyBuffer = text;
                _message.text = "The invitation is copied: paste it to a friend.";
            }
        }

        private void Take()
        {
            string code = Invites.Clean(_field.text);
            if (_busy) return;
            if (!Invites.Valid(code)) { _message.text = "That code could not be read: it has six letters and numbers."; return; }
            _busy = true;
            _take.interactable = false;
            StartCoroutine(_root.Server.EnterInvite(code, (dto, error) =>
            {
                Answer(dto, error);
                if (dto != null) _field.text = "";
            }));
        }
    }
}
