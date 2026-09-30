using System;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// A modal "are you sure" box on its own layer above every screen: a title, a few lines of detail (rich text, so
    /// the risk can be shown in red), a confirm button in the action's colour and a cancel button. Tapping outside the
    /// box cancels.
    /// </summary>
    public sealed class ConfirmDialog : MonoBehaviour
    {
        private GameObject _canvas;
        private Text _title;
        private Text _body;
        private Text _confirmLabel;
        private Image _confirmImage;
        private Action _onConfirm, _onCancel;
        private Text _cancelLabel;

        public bool IsOpen => _canvas.activeSelf;

        public void Init()
        {
            _canvas = Ui.Canvas("ConfirmCanvas", 15).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Image dim = Ui.Panel("Dim", canvas, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.72f));
            var outside = dim.gameObject.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(Cancel);

            Image box = Ui.Framed("Box", canvas, 0.07f, 0.33f, 0.93f, 0.67f, Palette.PanelDark);
            Transform b = box.transform;
            Ui.Trim("TopRule", b, 0f, 0.985f, 1f, 1f);
            _title = Ui.Title("Title", b, 0.05f, 0.80f, 0.95f, 0.96f, "", 52, TextAnchor.MiddleCenter, Palette.Sorn);
            _body = Ui.Label("Body", b, 0.07f, 0.30f, 0.93f, 0.79f, "", 34, TextAnchor.MiddleCenter, Palette.Parchment);
            _body.supportRichText = true;
            Button confirm = Ui.Button("Confirm", b, 0.05f, 0.05f, 0.62f, 0.25f, "", 36, Palette.Danger, Confirm, out _confirmLabel);
            _confirmImage = confirm.GetComponent<Image>();
            Ui.Button("Cancel", b, 0.65f, 0.05f, 0.95f, 0.25f, "CANCEL", 32, Palette.ButtonIdle, CancelPressed, out _cancelLabel);

            _canvas.SetActive(false);
        }

        /// <summary>The box; <paramref name="cancelLabel"/> and <paramref name="onCancel"/> make the second button a choice of its
        /// own (a tap outside the box still only closes it).</summary>
        public void Show(string title, string body, string confirmLabel, Color confirmColor, Action onConfirm, string cancelLabel = "CANCEL", Action onCancel = null)
        {
            _title.text = title;
            _body.text = body;
            _confirmLabel.text = confirmLabel;
            _confirmImage.color = confirmColor;
            _onConfirm = onConfirm;
            _cancelLabel.text = cancelLabel;
            _onCancel = onCancel;
            _canvas.SetActive(true);
        }

        private void CancelPressed()
        {
            Action action = _onCancel;
            Cancel();
            action?.Invoke();
        }

        private void Confirm()
        {
            Action action = _onConfirm;
            _onConfirm = null;
            _canvas.SetActive(false);
            action?.Invoke();
        }

        private void Cancel()
        {
            _onConfirm = null;
            _onCancel = null;
            _canvas.SetActive(false);
        }

        /// <summary>Rich-text colour tag for a palette colour.</summary>
        public static string Tint(string text, Color color) => "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
    }
}
