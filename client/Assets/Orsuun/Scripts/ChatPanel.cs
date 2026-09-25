using System;
using System.Collections.Generic;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// CHAT: the world channel and the guild channel (whose system lines are the guild log). Polls the world channel
    /// all the time for the lane's ticker, and the open channel quickly while the screen is up. Tap someone's line to add
    /// them as a friend, ask them to trade, invite them to your guild (leader or officer; owner, 25 Sep 2026: "sending
    /// trade invite, guild invite from chat also add adding friends"), report the line or block them.
    /// </summary>
    public sealed class ChatPanel : MonoBehaviour
    {
        private const int MaxLines = 100;
        private const float WorldPollClosed = 6f;
        private const float PollOpen = 2.5f;

        private sealed class Channel
        {
            public readonly string Name;
            public readonly List<ChatLineDto> Lines = new List<ChatLineDto>();
            public long Latest;
            public float NextPoll;
            public bool Fetching;
            public bool Dirty = true;
            public Channel(string name) => Name = name;

            public void Clear()
            {
                Lines.Clear();
                Latest = 0;
                NextPoll = 0f;
                Dirty = true;
            }

            public void Take(ChatDto dto)
            {
                if (dto?.lines == null) return;
                foreach (ChatLineDto line in dto.lines)
                {
                    if (line.id <= Latest && Lines.Exists(l => l.id == line.id)) continue;
                    Lines.Add(line);
                    Dirty = true;
                }
                Lines.Sort((a, b) => a.id.CompareTo(b.id));
                if (Lines.Count > MaxLines) Lines.RemoveRange(0, Lines.Count - MaxLines);
                Latest = Math.Max(Latest, dto.latestId);
            }
        }

        private GameRoot _root;
        private GameObject _canvas;
        private readonly Channel _world = new Channel("world");
        private readonly Channel _guild = new Channel("guild");
        private Channel _shown;
        private ScrollRect _scroll;
        private RectTransform _content;
        private readonly List<Text> _rows = new List<Text>();
        private Button _worldTab;
        private Button _guildTab;
        private InputField _input;
        private Button _send;
        private Text _message;
        private Text _blocked;
        private int _blockedCount;
        private bool _busy;
        private int _generation = -1;
        private string _guildTag = "";
        private ConfirmDialog _confirm;
        private GameObject _actions;
        private Text _actionsTitle;
        private ChatLineDto _picked;
        private Button _inviteButton;
        private bool _pickForShot = Array.IndexOf(Environment.GetCommandLineArgs(), "-chatpick") >= 0;

        public bool IsOpen => _canvas.activeSelf;

        /// <summary>The newest world line for the lane's ticker ("" before the first one arrives).</summary>
        public string Ticker { get; private set; } = "";

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("ChatCanvas", 14).gameObject;
            Transform canvas = _canvas.transform;
            // This component stays off its canvas: it keeps polling world chat for the lane's ticker while hidden.

            Ui.Backdrop(canvas, "Chat");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "CHAT", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _worldTab = Ui.Button("WorldTab", canvas, 0.04f, 0.875f, 0.49f, 0.925f, "WORLD", 28, Palette.ButtonIdle, () => Show(_world), out _);
            _guildTab = Ui.Button("GuildTab", canvas, 0.51f, 0.875f, 0.96f, 0.925f, "GUILD", 28, Palette.ButtonIdle, () => Show(_guild), out _);
            _scroll = Ui.Scroll("Lines", canvas, 0.04f, 0.2f, 0.96f, 0.865f, out _content);
            _input = Ui.Input("Input", canvas, 0.04f, 0.135f, 0.74f, 0.19f, "Say something", 28, Chat.MaxLength);
            _input.lineType = InputField.LineType.SingleLine;
            _send = Ui.Button("Send", canvas, 0.76f, 0.135f, 0.96f, 0.19f, "SEND", 28, Palette.Safe, Send, out _);
            _message = Ui.Label("Message", canvas, 0.05f, 0.085f, 0.7f, 0.13f, "", 22, TextAnchor.MiddleLeft, Palette.Muted);
            _message.supportRichText = true;
            _blocked = Ui.Label("Blocked", canvas, 0.7f, 0.085f, 0.96f, 0.13f, "", 20, TextAnchor.MiddleRight, Palette.Muted);
            Button unblock = _blocked.gameObject.AddComponent<Button>();
            _blocked.raycastTarget = true;
            unblock.onClick.AddListener(AskUnblockAll);
            Ui.Button("Friends", canvas, 0.04f, 0.015f, 0.3f, 0.075f, "FRIENDS", 26, Palette.Safe, () => { Close(); _root.Friends.Open(); }, out _);
            Ui.Button("Close", canvas, 0.32f, 0.015f, 0.96f, 0.075f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, Close, out _);

            _actions = Ui.Rect("Actions", canvas, 0f, 0f, 1f, 1f).gameObject;
            Image dim = Ui.Panel("Dim", _actions.transform, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => _actions.SetActive(false));
            Transform box = Ui.Framed("Box", _actions.transform, 0.1f, 0.33f, 0.9f, 0.67f, Palette.PanelDark).transform;
            _actionsTitle = Ui.Title("Title", box, 0.05f, 0.82f, 0.95f, 0.97f, "", 30, TextAnchor.MiddleCenter, Palette.Sorn);
            Ui.Button("Friend", box, 0.06f, 0.63f, 0.48f, 0.79f, "ADD FRIEND", 24, Palette.Safe, AddFriendPicked, out _);
            Ui.Button("Trade", box, 0.52f, 0.63f, 0.94f, 0.79f, "TRADE", 24, Palette.Alloy, TradePicked, out _);
            _inviteButton = Ui.Button("Invite", box, 0.06f, 0.45f, 0.94f, 0.61f, "INVITE TO MY GUILD", 24, Palette.ButtonForge, InvitePicked, out _);
            Ui.Button("Report", box, 0.06f, 0.25f, 0.48f, 0.41f, "REPORT", 24, Palette.Danger, ReportPicked, out _);
            Ui.Button("Block", box, 0.52f, 0.25f, 0.94f, 0.41f, "BLOCK", 24, Palette.ButtonIdle, AskBlockPicked, out _);
            Ui.Button("Cancel", box, 0.3f, 0.05f, 0.7f, 0.19f, "CLOSE", 22, Palette.ButtonIdle, () => _actions.SetActive(false), out _);
            _actions.SetActive(false);

            _canvas.SetActive(false);
            _confirm = new GameObject("ChatConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
            _shown = _world;
        }

        public void Open(bool guild = false)
        {
            _message.text = "";
            _actions.SetActive(false);
            _canvas.SetActive(true);
            Show(guild && _root.Server.InGuild ? _guild : _world);
        }

        public void Close()
        {
            _actions.SetActive(false);
            _canvas.SetActive(false);
        }

        private void Show(Channel channel)
        {
            if (channel == _guild && !_root.Server.InGuild)
            {
                _message.text = "Join a guild to talk with it.";
                return;
            }
            _shown = channel;
            _shown.Dirty = true;
            _shown.NextPoll = 0f;
            _stickToBottom = true;
        }

        private bool _stickToBottom;
        private bool _offlineShown;

        private void Send()
        {
            string text = Chat.Normalise(_input.text);
            if (_busy || text.Length == 0 || !_root.Server.Online) return;
            _busy = true;
            Channel channel = _shown;
            StartCoroutine(_root.Server.Say(channel.Name, text, channel.Latest, (dto, error) =>
            {
                _busy = false;
                if (error != null)
                {
                    _message.text = ConfirmDialog.Tint(error, Palette.Bad);
                    return;
                }
                _input.text = "";
                _message.text = "";
                channel.Take(dto);
                _blockedCount = dto.blocked;
                _stickToBottom = true;
            }));
        }

        private void Pick(int index)
        {
            if (index >= _shown.Lines.Count) return;
            ChatLineDto line = _shown.Lines[index];
            if (line.system || line.mine) return;
            _picked = line;
            _actionsTitle.text = line.name;
            // Guild invites are the leader's and officers' (a member of a guild shows its tag before the name).
            string rank = _root.Server.Guild?.rank;
            bool manager = System.Enum.TryParse(rank, out GuildRank r) && Guilds.CanManage(r);
            _inviteButton.gameObject.SetActive(manager);
            _actions.SetActive(true);
        }

        private void AddFriendPicked()
        {
            ChatLineDto line = _picked;
            _actions.SetActive(false);
            if (line == null) return;
            StartCoroutine(_root.Server.AddFriend(line.accountId, null, (message, error) =>
                _message.text = error != null ? ConfirmDialog.Tint(error, Palette.Bad) : message));
        }

        private void TradePicked()
        {
            ChatLineDto line = _picked;
            _actions.SetActive(false);
            if (line == null) return;
            StartCoroutine(_root.Server.TradeInvite(null, error =>
            {
                if (error != null) { _message.text = ConfirmDialog.Tint(error, Palette.Bad); return; }
                Close();
                _root.Trade.Open();
            }, line.accountId));
        }

        private void InvitePicked()
        {
            ChatLineDto line = _picked;
            _actions.SetActive(false);
            if (line == null) return;
            StartCoroutine(_root.Server.InviteToGuild(line.accountId, null, (message, error) =>
                _message.text = error != null ? ConfirmDialog.Tint(error, Palette.Bad) : message));
        }

        private void ReportPicked()
        {
            ChatLineDto line = _picked;
            _actions.SetActive(false);
            if (line == null) return;
            StartCoroutine(_root.Server.ReportLine(line.id, _shown.Name, error =>
                _message.text = error != null ? ConfirmDialog.Tint(error, Palette.Bad) : "Reported. Thank you: three reports hide a line."));
        }

        private void AskBlockPicked()
        {
            ChatLineDto line = _picked;
            _actions.SetActive(false);
            if (line == null) return;
            _confirm.Show("Block " + line.name + "?", "You will no longer see their chat lines. Tap the blocked count below the chat to unblock everyone.",
                "BLOCK", Palette.Danger, () => StartCoroutine(_root.Server.Block(line.accountId, true, (dto, error) =>
                {
                    if (error != null) { _message.text = ConfirmDialog.Tint(error, Palette.Bad); return; }
                    _blockedCount = dto.blocked;
                    _world.Lines.RemoveAll(l => l.accountId == line.accountId);
                    _guild.Lines.RemoveAll(l => l.accountId == line.accountId);
                    _world.Dirty = _guild.Dirty = true;
                    _message.text = line.name + " is blocked.";
                })));
        }

        private void AskUnblockAll()
        {
            if (_blockedCount == 0) return;
            _confirm.Show("Unblock everyone?", $"You have blocked {_blockedCount} player{(_blockedCount == 1 ? "" : "s")}. Their lines come back.", "UNBLOCK",
                Palette.Safe, () => StartCoroutine(_root.Server.Block("00000000-0000-0000-0000-000000000000", false, (dto, error) =>
                {
                    if (error != null) { _message.text = ConfirmDialog.Tint(error, Palette.Bad); return; }
                    _blockedCount = dto.blocked;
                    _world.Clear();
                    _guild.Clear();
                })));
        }

        private void Poll(Channel channel, float every)
        {
            if (channel.Fetching || Time.realtimeSinceStartup < channel.NextPoll) return;
            channel.Fetching = true;
            StartCoroutine(_root.Server.FetchChat(channel.Name, channel.Latest, (dto, error) =>
            {
                channel.Fetching = false;
                channel.NextPoll = Time.realtimeSinceStartup + every;
                if (error != null) return;
                channel.Take(dto);
                _blockedCount = dto.blocked;
            }));
        }

        private static string Clock(string utc) =>
            DateTime.TryParse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime t)
                ? t.ToLocalTime().ToString("HH:mm") : "";

        /// <summary>One line as rich text: system lines in gold italics, players with their Banner's mark.</summary>
        private static string Format(ChatLineDto line, bool clock)
        {
            string time = clock ? $"<color=#8C857A>{Clock(line.utc)}</color>  " : "";
            string text = line.text.Replace("<", "‹").Replace(">", "›");
            if (line.system) return time + $"<color=#E8C170><i>{text}</i></color>";
            string mark = ColorUtility.ToHtmlStringRGB(BannerLook.Color(BannerLook.Parse(line.banner)));
            string name = line.mine ? $"<color=#9FE39F><b>{line.name}</b></color>" : $"<b>{line.name}</b>";
            return time + $"<color=#{mark}>■</color> {name}: {text}";
        }

        private void Update()
        {
            if (_root == null) return;
            // A new account on this device, or a new guild: what was cached belongs to someone else.
            string tag = _root.Server.InGuild ? _root.Server.Guild.tag : "";
            if (_generation != _root.Server.AccountGeneration)
            {
                _generation = _root.Server.AccountGeneration;
                _world.Clear();
                _guild.Clear();
                Ticker = "";
            }
            if (tag != _guildTag)
            {
                _guildTag = tag;
                _guild.Clear();
            }

            if (_root.Server.Online)
            {
                Poll(_world, IsOpen && _shown == _world ? PollOpen : WorldPollClosed);
                if (IsOpen && _shown == _guild && _root.Server.InGuild) Poll(_guild, PollOpen);
            }
            if (_world.Lines.Count > 0) Ticker = Format(_world.Lines[_world.Lines.Count - 1], clock: false);

            if (!IsOpen) return;
            if (_shown == _guild && !_root.Server.InGuild) _shown = _world;
            _worldTab.GetComponent<Image>().color = _shown == _world ? Palette.Safe : Palette.ButtonIdle;
            _guildTab.GetComponent<Image>().color = _shown == _guild ? Palette.Safe : Palette.ButtonIdle;
            _guildTab.interactable = _root.Server.InGuild;
            _send.interactable = !_busy && _root.Server.Online;
            _blocked.text = _blockedCount > 0 ? $"{_blockedCount} blocked · unblock" : "";
            if (!_root.Server.Online && !_offlineShown) { _message.text = "Offline: chat needs the server."; _offlineShown = true; }
            else if (_root.Server.Online && _offlineShown) { _message.text = ""; _offlineShown = false; }

            // Screenshots: -chatpick opens the actions of the newest line someone else wrote.
            if (_pickForShot && _shown.Lines.Count > 0)
            {
                int other = _shown.Lines.FindLastIndex(l => !l.system && !l.mine);
                if (other >= 0) { _pickForShot = false; Pick(other); }
            }
            if (!_shown.Dirty) return;
            _shown.Dirty = false;
            bool atBottom = _stickToBottom || _scroll.verticalNormalizedPosition < 0.02f;
            _stickToBottom = false;
            while (_rows.Count < _shown.Lines.Count)
            {
                int index = _rows.Count;
                _rows.Add(Ui.ListRow("Line" + index, _content, 26, () => Pick(index)));
            }
            for (int i = 0; i < _rows.Count; i++)
            {
                bool has = i < _shown.Lines.Count;
                _rows[i].gameObject.SetActive(has);
                if (has) _rows[i].text = Format(_shown.Lines[i], clock: true);
            }
            if (_shown.Lines.Count == 0)
            {
                if (_rows.Count == 0) _rows.Add(Ui.ListRow("Line0", _content, 26, () => { }));
                _rows[0].gameObject.SetActive(true);
                _rows[0].text = ConfirmDialog.Tint(_shown == _guild ? "The guild is quiet. Say hello." : "The steppe is quiet. Say hello.", Palette.Muted);
            }
            if (atBottom)
            {
                Canvas.ForceUpdateCanvases();
                _scroll.verticalNormalizedPosition = 0f;
            }
        }
    }
}
