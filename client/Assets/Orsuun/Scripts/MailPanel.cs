using System;
using System.Globalization;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// MAILBOX (owner, 27 Sep 2026: "Mailbox"; Rules.Mail): the hero's letters, newest first. The Salt Exchange pays its
    /// sales and sends back what did not sell by letter; the Pits write their season's rewards. A letter opens to its words
    /// and what it holds: TAKE puts that in the bag (a piece waits while the bag is full), THROW AWAY clears an empty one;
    /// TAKE ALL and THROW AWAY TAKEN work on every letter. Opened from MENU and the HUD's call.
    /// </summary>
    public sealed class MailPanel : MonoBehaviour
    {
        private GameRoot _root;
        private GameObject _canvas;
        private GameObject _listView, _letterView;
        private RectTransform _listContent;
        private Text _empty;
        private Text _letterTitle, _letterFrom, _letterBody, _holds;
        private RawImage _holdsIcon;
        private GameObject _holdsBox;
        private Button _take, _throw;
        private Text _message;
        private LetterDto _open;
        private bool _busy;
        private string _listKey = "";

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("MailCanvas", 16).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);
            Ui.Backdrop(canvas, "Exchange");
            Ui.Title("Title", canvas, 0.05f, 0.925f, 0.95f, 0.975f, "MAILBOX", 48, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);

            // Every letter.
            _listView = Ui.Rect("List", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform list = _listView.transform;
            Ui.Scroll("Letters", list, 0.03f, 0.19f, 0.97f, 0.91f, out _listContent);
            _empty = Ui.Label("Empty", list, 0.08f, 0.5f, 0.92f, 0.62f,
                "No letters. The Salt Exchange writes when something of yours sells or comes back, and the Pits when a season ends.",
                24, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("TakeAll", list, 0.04f, 0.1f, 0.49f, 0.165f, "TAKE ALL", 28, Palette.ButtonForge, () => Take(0), out _);
            Ui.Button("ThrowAll", list, 0.51f, 0.1f, 0.96f, 0.165f, "THROW AWAY TAKEN", 22, Palette.ButtonIdle, () => Throw(0), out _);
            Ui.Button("Close", list, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE HUNT", 28, Palette.ButtonIdle, Close, out _);

            // One letter.
            _letterView = Ui.Rect("Letter", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform letter = _letterView.transform;
            Ui.Framed("Paper", letter, 0.04f, 0.3f, 0.96f, 0.9f, new Color(0.07f, 0.06f, 0.05f, 0.94f)).raycastTarget = false;
            _letterTitle = Ui.Title("LetterTitle", letter, 0.08f, 0.82f, 0.92f, 0.88f, "", 30, TextAnchor.MiddleLeft, Palette.Sorn);
            _letterFrom = Ui.Label("From", letter, 0.08f, 0.785f, 0.92f, 0.82f, "", 20, TextAnchor.MiddleLeft, Palette.Muted);
            _letterBody = Ui.Label("Body", letter, 0.08f, 0.56f, 0.92f, 0.775f, "", 26, TextAnchor.UpperLeft, Palette.Parchment);
            _holdsBox = Ui.Framed("Holds", letter, 0.08f, 0.34f, 0.92f, 0.54f, new Color(0.12f, 0.09f, 0.04f, 0.9f)).gameObject;
            RectTransform iconBox = Ui.Rect("IconBox", _holdsBox.transform, 0.04f, 0.12f, 0.28f, 0.88f);
            _holdsIcon = Ui.Icon("Icon", iconBox, 0f, 0f, 1f, 1f, "Sorn");
            _holds = Ui.Label("What", _holdsBox.transform, 0.32f, 0.08f, 0.96f, 0.92f, "", 26, TextAnchor.MiddleLeft, Palette.Parchment);
            _holds.supportRichText = true;
            _take = Ui.Button("Take", letter, 0.08f, 0.2f, 0.52f, 0.27f, "TAKE", 30, Palette.ButtonForge, () => Take(_open?.id ?? 0), out _);
            _throw = Ui.Button("Throw", letter, 0.54f, 0.2f, 0.92f, 0.27f, "THROW AWAY", 26, Palette.Danger, () => Throw(_open?.id ?? 0), out _);
            Ui.Button("Back", letter, 0.04f, 0.015f, 0.47f, 0.075f, "ALL LETTERS", 26, Palette.ButtonIdle, ShowList, out _);
            Ui.Button("Hunt", letter, 0.53f, 0.015f, 0.96f, 0.075f, "BACK TO THE HUNT", 22, Palette.ButtonIdle, Close, out _);

            _message = Ui.Label("Message", canvas, 0.05f, 0.075f, 0.95f, 0.1f, "", 22, TextAnchor.MiddleCenter, Palette.Parchment);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _canvas.SetActive(true);
            _message.text = _root.Server.Online ? "" : "Offline: letters need the server.";
            _listKey = "";
            ShowList();
            if (_root.Server.Online && !_busy)
            {
                _busy = true;
                StartCoroutine(_root.Server.FetchMail(error =>
                {
                    _busy = false;
                    if (error != null) _message.text = error;
                }));
            }
        }

        public void Close() => _canvas.SetActive(false);

        /// <summary>Screenshots (-mailletter): the newest letter open.</summary>
        public void OpenFirst()
        {
            LetterDto[] letters = _root.Server.Mail?.letters;
            if (letters != null && letters.Length > 0) ShowLetter(letters[0]);
        }

        private void ShowList()
        {
            _open = null;
            _listView.SetActive(true);
            _letterView.SetActive(false);
        }

        private void ShowLetter(LetterDto letter)
        {
            _open = letter;
            _listView.SetActive(false);
            _letterView.SetActive(true);
            _letterTitle.text = letter.title;
            _letterFrom.text = $"From {letter.from}   ·   {When(letter.utc)}";
            _letterBody.text = letter.body;
            bool holds = Holds(letter);
            _holdsBox.SetActive(holds || letter.taken && HadSomething(letter));
            if (_holdsBox.activeSelf)
            {
                Ui.SetIcon(_holdsIcon, IconOf(letter));
                _holds.text = What(letter) + (letter.taken ? "\n" + ConfirmDialog.Tint("TAKEN", Palette.Muted) : "");
            }
            _take.gameObject.SetActive(holds);
            _throw.gameObject.SetActive(!holds);
        }

        private void Take(long letterId)
        {
            if (_busy || !_root.Server.Online) return;
            _busy = true;
            StartCoroutine(_root.Server.TakeMail(letterId, error =>
            {
                _busy = false;
                _message.text = error ?? _root.Server.Mail?.message ?? "";
                if (error == null) GameAudio.Instance?.Play("LaneLoot", 0.8f, 0.1f, 0f);
                Reopen();
            }));
        }

        private void Throw(long letterId)
        {
            if (_busy || !_root.Server.Online) return;
            _busy = true;
            StartCoroutine(_root.Server.DeleteMail(letterId, error =>
            {
                _busy = false;
                _message.text = error ?? _root.Server.Mail?.message ?? "";
                if (error == null && letterId > 0) ShowList();
                else Reopen();
            }));
        }

        /// <summary>After a take, the open letter shows as the server now has it.</summary>
        private void Reopen()
        {
            _listKey = "";
            if (_open == null || _root.Server.Mail?.letters == null) return;
            foreach (LetterDto l in _root.Server.Mail.letters)
                if (l.id == _open.id) { ShowLetter(l); return; }
            ShowList();
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf || !_listView.activeSelf) return;
            MailDto mail = _root.Server.Mail;
            LetterDto[] letters = mail?.letters ?? new LetterDto[0];
            _empty.enabled = mail != null && letters.Length == 0;
            string key = "";
            foreach (LetterDto l in letters) key += l.id + (l.taken ? "t" : "") + ";";
            if (key == _listKey) return;
            _listKey = key;
            foreach (Transform child in _listContent) Destroy(child.gameObject);
            foreach (LetterDto letter in letters)
            {
                LetterDto l = letter;
                string fresh = !l.read ? ConfirmDialog.Tint("   NEW", Palette.Warn) : "";
                string holds = Holds(l) ? ConfirmDialog.Tint("Holds: ", Palette.Sorn) + What(l)
                    : l.taken ? ConfirmDialog.Tint("Taken", Palette.Muted) : ConfirmDialog.Tint("A notice", Palette.Muted);
                Ui.ListRow("Letter", _listContent, 26, () => ShowLetter(l)).text =
                    $"<b><color=#{(Holds(l) ? "FFD66B" : "F4E8CE")}>{l.title}</color></b>{fresh}\n"
                    + $"<size=20><color=#B9B3A8>{l.from}  ·  {When(l.utc)}</color></size>   <size=22>{holds}</size>";
            }
        }

        private static bool HasPiece(LetterDto l) => l.item != null && !string.IsNullOrEmpty(l.item.id);

        private static bool HadSomething(LetterDto l) => l.sorn > 0 || l.goodId >= 0 || l.bookId >= 0 || l.kind == "returned";

        /// <summary>Still holding something to take.</summary>
        private static bool Holds(LetterDto l) => !l.taken && (l.sorn > 0 || l.goodId >= 0 || l.bookId >= 0 || HasPiece(l));

        private static string What(LetterDto l)
        {
            if (l.sorn > 0) return l.sorn.ToString("N0", CultureInfo.InvariantCulture) + " sorn";
            if (l.goodId >= 0) return $"{l.goodCount} × {TradeGoods.Name(l.goodId)}";
            if (l.bookId >= 0) return $"{l.bookCount} × {Books.Name(l.bookId)}";
            if (HasPiece(l)) return $"{l.item.name} +{l.item.upgradeLevel}";
            return l.title.StartsWith("Not sold: ") ? l.title.Substring(10) : "";
        }

        private static string IconOf(LetterDto l)
        {
            if (l.goodId >= 0) return GearPanel.GoodIcon(l.goodId);
            if (l.bookId >= 0) return "Book" + Books.ClassOf(l.bookId);
            if (HasPiece(l)) return Ui.ItemIcon(ToState(l.item));
            return "Sorn";
        }

        /// <summary>A letter's time on this phone: the hour today, the day and hour before.</summary>
        private static string When(string utc)
        {
            if (!DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime at)) return "";
            DateTime local = at.ToLocalTime();
            return local.Date == DateTime.Now.Date ? local.ToString("HH:mm", CultureInfo.InvariantCulture) : local.ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
