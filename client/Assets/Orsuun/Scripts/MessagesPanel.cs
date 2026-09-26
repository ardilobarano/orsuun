using System;
using System.Collections.Generic;
using System.Globalization;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// MESSAGES (owner, 26 Sep 2026: "go for private messages", "they need to stay after days and days", "make it a
    /// screen"; Rules.Whispers): hero to hero, kept with no expiry. The list shows every conversation, newest first, with
    /// the last line and an unread count; a name typed starts a new one. A conversation pages back through older lines,
    /// polls for new ones while it is open, and marks what arrives as read. Their lines can be reported (tap one).
    /// Opened from MENU, the HUD's call, a name in CHAT and the FRIENDS list.
    /// </summary>
    public sealed class MessagesPanel : MonoBehaviour
    {
        private const float PollSeconds = 3f;
        private const float ListSeconds = 10f;

        private GameRoot _root;
        private GameObject _canvas;
        private ConfirmDialog _confirm;
        private GameObject _listView, _threadView;
        private InputField _newName;
        private RectTransform _listContent;
        private Text _listEmpty;
        private Text _title, _who, _notice;
        private RectTransform _lines;
        private ScrollRect _linesScroll;
        private InputField _text;
        private Button _send;
        private Text _message;

        // The conversation open: by id once known, by name for a first message.
        private string _otherId;
        private string _otherName;
        private readonly List<WhisperLineDto> _held = new List<WhisperLineDto>();
        private long _latest;
        private bool _hasOlder;
        private bool _blocked;
        private bool _busy;
        private float _nextPoll;
        private float _nextList;
        private bool _rebuild;
        private bool _nameTried;
        private bool _toBottom;
        private string _listKey = "";

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("MessagesCanvas", 16).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);
            Ui.Backdrop(canvas, "Chat");
            _title = Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "MESSAGES", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);

            // The list: a name to write to, then every conversation.
            _listView = Ui.Rect("List", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform list = _listView.transform;
            _newName = Ui.Input("NewName", list, 0.04f, 0.855f, 0.68f, 0.905f, "A hero's name to write to", 26, Characters.NameMax);
            Ui.Button("Write", list, 0.7f, 0.855f, 0.96f, 0.905f, "WRITE", 26, Palette.Safe, WriteByName, out _);
            Ui.Scroll("Conversations", list, 0.03f, 0.1f, 0.97f, 0.84f, out _listContent);
            _listEmpty = Ui.Label("Empty", list, 0.08f, 0.45f, 0.92f, 0.6f,
                "No messages yet. Write to a hero by name above, or tap a name in CHAT or on your FRIENDS list.", 24, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Close", list, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE HUNT", 28, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);

            // One conversation.
            _threadView = Ui.Rect("Thread", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform thread = _threadView.transform;
            Ui.Framed("WhoBack", thread, 0.03f, 0.86f, 0.97f, 0.925f, new Color(0.05f, 0.05f, 0.1f, 0.92f)).raycastTarget = false;
            _who = Ui.Label("Who", thread, 0.06f, 0.865f, 0.94f, 0.92f, "", 24, TextAnchor.MiddleCenter, Palette.Parchment);
            _who.supportRichText = true;
            _linesScroll = Ui.Scroll("Lines", thread, 0.03f, 0.2f, 0.97f, 0.855f, out _lines);
            _notice = Ui.Label("Notice", thread, 0.05f, 0.165f, 0.95f, 0.195f, "", 20, TextAnchor.MiddleCenter, Palette.Warn);
            _text = Ui.Input("Text", thread, 0.03f, 0.1f, 0.74f, 0.16f, "Write a message", 26, Chat.MaxLength);
            _send = Ui.Button("Send", thread, 0.76f, 0.1f, 0.97f, 0.16f, "SEND", 28, Palette.ButtonForge, Send, out _);
            Ui.Button("Back", thread, 0.04f, 0.015f, 0.47f, 0.075f, "ALL MESSAGES", 24, Palette.ButtonIdle, ShowList, out _);
            Ui.Button("Hunt", thread, 0.53f, 0.015f, 0.96f, 0.075f, "BACK TO THE HUNT", 22, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);

            _message = Ui.Label("Message", canvas, 0.05f, 0.075f, 0.95f, 0.1f, "", 20, TextAnchor.MiddleCenter, Palette.Muted);
            _confirm = new GameObject("MessagesConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
            _canvas.SetActive(false);
        }

        /// <summary>The list of conversations.</summary>
        public void Open()
        {
            _canvas.SetActive(true);
            _message.text = _root.Server.Online ? "" : "Offline: messages need the server.";
            ShowList();
        }

        /// <summary>A conversation with a hero (from chat or the friend list): by id, or by name before the first message.</summary>
        public void OpenWith(string accountId, string name)
        {
            _canvas.SetActive(true);
            _message.text = _root.Server.Online ? "" : "Offline: messages need the server.";
            ShowThread(string.IsNullOrEmpty(accountId) || accountId == NoId ? null : accountId, name);
        }

        private void ShowList()
        {
            _listView.SetActive(true);
            _threadView.SetActive(false);
            _otherId = null;
            _nextList = 0f;
        }

        private void ShowThread(string accountId, string name)
        {
            _listView.SetActive(false);
            _threadView.SetActive(true);
            _otherId = accountId;
            _otherName = name ?? "";
            _held.Clear();
            _latest = 0;
            _hasOlder = false;
            _blocked = false;
            _nameTried = false;
            _text.text = "";
            _notice.text = "";
            _who.text = ConfirmDialog.Tint(_otherName, Palette.Sorn);
            _rebuild = true;
            _toBottom = true;
            _nextPoll = 0f;
        }

        private void WriteByName()
        {
            string name = _newName.text.Trim();
            if (name.Length == 0) { _message.text = "Type a hero's name first."; return; }
            _newName.text = "";
            ShowThread(null, name);
        }

        private void Send()
        {
            string text = _text.text.Trim();
            if (_busy || text.Length == 0 || !_root.Server.Online) return;
            _busy = true;
            StartCoroutine(_root.Server.SendWhisper(_otherId, _otherId == null ? _otherName : "", text, _latest, (thread, error) =>
            {
                _busy = false;
                if (error != null) { _notice.text = error; return; }
                _text.text = "";
                _notice.text = "";
                Take(thread, append: true);
                _toBottom = true;
                GameAudio.Instance?.Play("LaneLoot", 0.5f, 0.1f, 0f);
            }));
        }

        private void LoadOlder()
        {
            if (_busy || _otherId == null || _held.Count == 0) return;
            _busy = true;
            StartCoroutine(_root.Server.FetchWhisperThread(_otherId, 0, _held[0].id, (thread, error) =>
            {
                _busy = false;
                if (error != null) { _notice.text = error; return; }
                if (thread.lines != null) _held.InsertRange(0, thread.lines);
                _hasOlder = thread.hasOlder;
                _rebuild = true;
            }));
        }

        /// <summary>Folds a server page into the lines held: appended (new lines) or the whole newest page.</summary>
        private void Take(WhisperThreadDto thread, bool append)
        {
            _otherId = thread.accountId;
            _otherName = thread.name;
            _blocked = thread.blocked;
            if (!append) _held.Clear();
            int before = _held.Count;
            if (thread.lines != null)
                foreach (WhisperLineDto line in thread.lines)
                    if (_held.Count == 0 || line.id > _held[_held.Count - 1].id) _held.Add(line);
            if (!append) _hasOlder = thread.hasOlder;
            _latest = Math.Max(_latest, thread.latest);
            string seen = Friends.Online(thread.minutesAway) ? ConfirmDialog.Tint("online", Palette.Good) : "seen " + Friends.Seen(thread.minutesAway);
            _who.text = $"{ConfirmDialog.Tint(thread.name, Palette.Sorn)}   ·   {thread.@class} level {thread.level}   ·   {seen}";
            _notice.text = _blocked ? "You blocked them. Unblock them in CHAT to write again." : _notice.text;
            if (!string.IsNullOrEmpty(thread.message)) _message.text = thread.message;
            if (_held.Count != before || !append) _rebuild = true;
            int theirs = 0;
            if (thread.lines != null) foreach (WhisperLineDto l in thread.lines) if (!l.mine) theirs++;
            _root.Server.WhispersRead(theirs);
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf || !_root.Server.Online) return;
            float now = Time.realtimeSinceStartup;
            if (_listView.activeSelf)
            {
                if (!_busy && now >= _nextList)
                {
                    _nextList = now + ListSeconds;
                    _busy = true;
                    StartCoroutine(_root.Server.FetchWhispers(error =>
                    {
                        _busy = false;
                        if (error != null) _message.text = error;
                    }));
                }
                DrawList();
                return;
            }
            // A conversation: poll for new lines (the first fetch takes the newest page; by name until the id is known).
            _send.interactable = !_busy && !_blocked;
            if ((_otherId != null || !_nameTried) && !_busy && now >= _nextPoll)
            {
                _nextPoll = now + PollSeconds;
                _busy = true;
                bool first = _held.Count == 0;
                bool byName = _otherId == null;
                _nameTried |= byName;
                StartCoroutine(_root.Server.FetchWhisperThread(_otherId, first ? 0 : _latest, 0, (thread, error) =>
                {
                    _busy = false;
                    if (error != null) { _notice.text = error; return; }
                    int had = _held.Count;
                    Take(thread, append: !first);
                    if (_held.Count > had) _toBottom = true;
                }, byName ? _otherName : null));
            }
            if (_rebuild) DrawThread();
            if (_toBottom)
            {
                Canvas.ForceUpdateCanvases();
                _linesScroll.verticalNormalizedPosition = 0f;
                _toBottom = false;
            }
        }

        private void DrawList()
        {
            WhispersDto view = _root.Server.Whispers;
            WhisperConversationDto[] rows = view?.conversations ?? new WhisperConversationDto[0];
            string key = "";
            foreach (WhisperConversationDto c in rows) key += c.accountId + c.lastUtc + c.unread + c.minutesAway / 5 + ";";
            _listEmpty.enabled = view != null && rows.Length == 0;
            if (key == _listKey) return;
            _listKey = key;
            foreach (Transform child in _listContent) Destroy(child.gameObject);
            foreach (WhisperConversationDto c in rows)
            {
                WhisperConversationDto conversation = c;
                string online = Friends.Online(c.minutesAway) ? ConfirmDialog.Tint("  ●", Palette.Good) : "";
                string unread = c.unread > 0 ? ConfirmDialog.Tint($"   {c.unread} NEW", Palette.Warn) : "";
                string last = (c.lastMine ? "You: " : "") + c.lastText;
                if (last.Length > 70) last = last.Substring(0, 70) + "...";
                Ui.ListRow("Conversation", _listContent, 28, () => ShowThread(conversation.accountId, conversation.name)).text =
                    $"<b><color=#FFD66B>{c.name}</color></b>{online}  <size=20><color=#B9B3A8>{c.@class} {c.level}  ·  {When(c.lastUtc)}</color></size>{unread}\n"
                    + $"<size=24><color=#{(c.unread > 0 ? "F4E8CE" : "B9B3A8")}>{Escape(last)}</color></size>";
            }
        }

        private void DrawThread()
        {
            _rebuild = false;
            foreach (Transform child in _lines) Destroy(child.gameObject);
            if (_hasOlder) Ui.ListRow("Older", _lines, 20, LoadOlder).text = "<color=#9FC7FF>LOAD OLDER MESSAGES</color>";
            if (_held.Count == 0)
                Ui.ListRow("None", _lines, 21, () => { }).text = "<color=#B9B3A8>No messages yet. Say hello.</color>";
            foreach (WhisperLineDto line in _held)
            {
                WhisperLineDto l = line;
                Text row = Ui.ListRow(l.mine ? "Mine" : "Theirs", _lines, 27, () => { if (!l.mine) AskReport(l); });
                row.alignment = l.mine ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
                row.text = $"<size=18><color=#8F8878>{When(l.utc)}</color></size>\n<color=#{(l.mine ? "FFD66B" : "F4E8CE")}>{Escape(l.text)}</color>";
            }
        }

        private void AskReport(WhisperLineDto line)
        {
            _confirm.Show("Report this message?", $"\"{Escape(line.text)}\"\n\nA moderator will read it. To stop their messages, block them in CHAT.", "REPORT", Palette.Danger,
                () => StartCoroutine(_root.Server.ReportWhisper(line.id, (thread, error) => _message.text = error ?? thread?.message ?? "")));
        }

        /// <summary>A message's time on this phone: the hour today, the day and hour before.</summary>
        private static string When(string utc)
        {
            if (!DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime at)) return "";
            DateTime local = at.ToLocalTime();
            return local.Date == DateTime.Now.Date ? local.ToString("HH:mm", CultureInfo.InvariantCulture) : local.ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>Players' words go into rich text: their angle brackets must not become tags.</summary>
        private static string Escape(string text) => (text ?? "").Replace("<", "‹").Replace(">", "›");
    }
}
