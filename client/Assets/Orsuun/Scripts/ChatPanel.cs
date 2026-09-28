using System;
using System.Collections.Generic;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// CHAT: the world channel, the guild channel (whose system lines are the guild log) and the Bazaar Call (the trade
    /// channel, Rules.Chat.Trade: heroes of level 20 call there once every 30 seconds, and LINK puts one of their pieces
    /// on the line; tapping a linked piece shows its card with TRADE and WHISPER). Polls the world channel all the time
    /// for the lane's ticker, and the open channel quickly while the screen is up. Tap someone's line to add them as a
    /// friend, ask them to trade, invite them to your guild (leader or officer; owner, 25 Sep 2026: "sending trade invite,
    /// guild invite from chat also add adding friends"), report the line or block them.
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
        private readonly Channel _trade = new Channel(Chat.Trade);
        private Channel _shown;
        private ScrollRect _scroll;
        private RectTransform _content;
        private readonly List<Text> _rows = new List<Text>();
        private Button _worldTab;
        private Button _guildTab;
        private Button _tradeTab;
        private Button _linkButton;
        private Text _linkLabel;
        private ItemState _link;
        private float _tradeReadyAt;
        private GameObject _picker;
        private RectTransform _pickerContent;
        private readonly List<Text> _pickerRows = new List<Text>();
        private readonly List<ItemState> _pickable = new List<ItemState>();
        private GameObject _card;
        private Text _cardTitle, _cardBody, _cardFrom;
        private RawImage _cardIcon;
        private GameObject _cardTrade, _cardWhisper;
        private Text _cardMoreLabel;
        private InputField _input;
        private Button _send;
        private Text _sendLabel;
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
        /// <summary>Screenshots: -bazaar opens the Bazaar Call; -bazaarcard also the card of its newest linked piece.</summary>
        private bool _bazaarForShot = Array.IndexOf(Environment.GetCommandLineArgs(), "-bazaar") >= 0;
        private bool _cardForShot = Array.IndexOf(Environment.GetCommandLineArgs(), "-bazaarcard") >= 0;

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
            _worldTab = Ui.Button("WorldTab", canvas, 0.04f, 0.875f, 0.34f, 0.925f, "WORLD", 26, Palette.ButtonIdle, () => Show(_world), out _);
            _guildTab = Ui.Button("GuildTab", canvas, 0.35f, 0.875f, 0.65f, 0.925f, "GUILD", 26, Palette.ButtonIdle, () => Show(_guild), out _);
            _tradeTab = Ui.Button("TradeTab", canvas, 0.66f, 0.875f, 0.96f, 0.925f, "BAZAAR", 26, Palette.ButtonIdle, () => Show(_trade), out _);
            _scroll = Ui.Scroll("Lines", canvas, 0.04f, 0.2f, 0.96f, 0.865f, out _content);
            _input = Ui.Input("Input", canvas, 0.04f, 0.135f, 0.74f, 0.19f, "Say something", 28, Chat.MaxLength);
            _input.lineType = InputField.LineType.SingleLine;
            // The Bazaar Call's LINK sits left of the field, which then gives it room.
            _linkButton = Ui.Button("Link", canvas, 0.04f, 0.135f, 0.21f, 0.19f, "LINK", 22, Palette.Alloy, OpenPicker, out _linkLabel);
            _linkLabel.supportRichText = true;
            _linkButton.gameObject.SetActive(false);
            _send = Ui.Button("Send", canvas, 0.76f, 0.135f, 0.96f, 0.19f, "SEND", 28, Palette.Safe, Send, out _sendLabel);
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
            Transform box = Ui.Framed("Box", _actions.transform, 0.1f, 0.29f, 0.9f, 0.71f, Palette.PanelDark).transform;
            _actionsTitle = Ui.Title("Title", box, 0.05f, 0.86f, 0.95f, 0.98f, "", 30, TextAnchor.MiddleCenter, Palette.Sorn);
            Ui.Button("Friend", box, 0.06f, 0.69f, 0.48f, 0.82f, "ADD FRIEND", 24, Palette.Safe, AddFriendPicked, out _);
            Ui.Button("Message", box, 0.52f, 0.69f, 0.94f, 0.82f, "MESSAGE", 24, Palette.Safe, MessagePicked, out _);
            Ui.Button("Trade", box, 0.06f, 0.53f, 0.48f, 0.66f, "TRADE", 24, Palette.Alloy, TradePicked, out _);
            _inviteButton = Ui.Button("Invite", box, 0.52f, 0.53f, 0.94f, 0.66f, "GUILD INVITE", 22, Palette.ButtonForge, InvitePicked, out _);
            Ui.Button("Report", box, 0.06f, 0.37f, 0.48f, 0.5f, "REPORT LINE", 22, Palette.Danger, ReportPicked, out _);
            Ui.Button("Block", box, 0.52f, 0.37f, 0.94f, 0.5f, "BLOCK", 24, Palette.ButtonIdle, AskBlockPicked, out _);
            // A name that breaks the rules (27 Sep 2026): the hero's own, or the guild the hero is in.
            Ui.Button("ReportName", box, 0.06f, 0.21f, 0.48f, 0.34f, "REPORT NAME", 22, Palette.Danger, () => ReportNamePicked("hero"), out _);
            Ui.Button("ReportGuild", box, 0.52f, 0.21f, 0.94f, 0.34f, "REPORT GUILD", 22, Palette.Danger, () => ReportNamePicked("guild"), out _);
            Ui.Button("Inspect", box, 0.06f, 0.04f, 0.48f, 0.16f, "INSPECT", 22, Palette.Alloy, InspectPicked, out _);
            Ui.Button("Cancel", box, 0.52f, 0.04f, 0.94f, 0.16f, "CLOSE", 22, Palette.ButtonIdle, () => _actions.SetActive(false), out _);
            _actions.SetActive(false);
            BuildPicker(canvas);
            BuildCard(canvas);

            _canvas.SetActive(false);
            _confirm = new GameObject("ChatConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
            _shown = _world;
        }

        private void BuildPicker(Transform canvas)
        {
            _picker = Ui.Rect("Picker", canvas, 0f, 0f, 1f, 1f).gameObject;
            Image dim = Ui.Panel("Dim", _picker.transform, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => _picker.SetActive(false));
            Transform box = Ui.Framed("Box", _picker.transform, 0.05f, 0.14f, 0.95f, 0.86f, Palette.PanelDark).transform;
            Ui.Title("Title", box, 0.05f, 0.92f, 0.95f, 0.985f, "LINK A PIECE", 32, TextAnchor.MiddleCenter, Palette.Sorn);
            Ui.Scroll("Pieces", box, 0.04f, 0.12f, 0.96f, 0.91f, out _pickerContent);
            Ui.Button("None", box, 0.05f, 0.02f, 0.48f, 0.1f, "NO LINK", 22, Palette.ButtonIdle, () => { SetLink(null); _picker.SetActive(false); }, out _);
            Ui.Button("Close", box, 0.52f, 0.02f, 0.95f, 0.1f, "CLOSE", 22, Palette.ButtonIdle, () => _picker.SetActive(false), out _);
            _picker.SetActive(false);
        }

        /// <summary>A linked piece's card: its name, what it is and its etchings, who called it, and TRADE or WHISPER.</summary>
        private void BuildCard(Transform canvas)
        {
            _card = Ui.Rect("Card", canvas, 0f, 0f, 1f, 1f).gameObject;
            Image dim = Ui.Panel("Dim", _card.transform, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => _card.SetActive(false));
            Transform box = Ui.Framed("Box", _card.transform, 0.06f, 0.34f, 0.94f, 0.76f, Palette.PanelDark).transform;
            RectTransform iconBox = Ui.Rect("IconBox", box, 0.04f, 0.74f, 0.24f, 0.96f);
            _cardIcon = Ui.Icon("Icon", iconBox, 0f, 0f, 1f, 1f, "Weapon");
            _cardTitle = Ui.Title("Title", box, 0.27f, 0.84f, 0.96f, 0.96f, "", 26, TextAnchor.MiddleLeft, Palette.Parchment);
            _cardTitle.supportRichText = true;
            _cardFrom = Ui.Label("From", box, 0.27f, 0.75f, 0.96f, 0.84f, "", 20, TextAnchor.MiddleLeft, Palette.Muted);
            Ui.Raw(_cardFrom);
            _cardBody = Ui.Label("Body", box, 0.05f, 0.2f, 0.95f, 0.72f, "", 21, TextAnchor.UpperLeft, Palette.Parchment);
            _cardBody.supportRichText = true;
            _cardTrade = Ui.Button("Trade", box, 0.04f, 0.04f, 0.34f, 0.16f, "TRADE", 22, Palette.Alloy, () => { _card.SetActive(false); TradePicked(); }, out _).gameObject;
            _cardWhisper = Ui.Button("Whisper", box, 0.36f, 0.04f, 0.66f, 0.16f, "WHISPER", 22, Palette.Safe, () => { _card.SetActive(false); MessagePicked(); }, out _).gameObject;
            // MORE: the line's other actions (friend, report, block...); one's own call just closes.
            Ui.Button("More", box, 0.68f, 0.04f, 0.96f, 0.16f, "MORE", 22, Palette.ButtonIdle, () =>
            {
                _card.SetActive(false);
                if (_picked != null && !_picked.mine) ShowActions(_picked);
            }, out _cardMoreLabel);
            _card.SetActive(false);
        }

        /// <summary>LINK: the hero's worn pieces, then the bag's (those the server knows).</summary>
        private void OpenPicker()
        {
            _pickable.Clear();
            PlayerSession s = _root.Session;
            foreach (EquipSlot slot in (EquipSlot[])Enum.GetValues(typeof(EquipSlot)))
                if (s.Equipped(slot) is ItemState worn && _root.Server.IdOf(worn) != null) _pickable.Add(worn);
            foreach (ItemState piece in s.Inventory.Loot)
                if (_root.Server.IdOf(piece) != null) _pickable.Add(piece);
            while (_pickerRows.Count < _pickable.Count)
            {
                int index = _pickerRows.Count;
                Text row = Ui.ListRow("Piece" + index, _pickerContent, 24, () =>
                {
                    if (index < _pickable.Count) SetLink(_pickable[index]);
                    _picker.SetActive(false);
                });
                row.supportRichText = true;
                _pickerRows.Add(row);
            }
            for (int i = 0; i < _pickerRows.Count; i++)
            {
                bool has = i < _pickable.Count;
                _pickerRows[i].gameObject.SetActive(has);
                if (has) _pickerRows[i].text = LinkName(_pickable[i]) + ConfirmDialog.Tint($"  ·  item level {_pickable[i].ItemLevel}", Palette.Muted);
            }
            _picker.SetActive(true);
        }

        private void SetLink(ItemState piece)
        {
            _link = piece;
            _message.text = piece == null ? "" : "Linked: " + LinkName(piece);
        }

        /// <summary>A piece's name as a link shows it: +level, name, in its rarity's colour.</summary>
        private static string LinkName(ItemState piece) =>
            ConfirmDialog.Tint($"[{Loc.T(MarketPanel.Title(piece))}]", GearPanel.RarityColor(piece.Rarity));

        private static bool HasLink(ChatLineDto line) => line.item != null && !string.IsNullOrEmpty(line.item.id);

        private void ShowCard(ChatLineDto line)
        {
            ItemState piece = ToState(line.item);
            _picked = line;
            Ui.SetIcon(_cardIcon, Ui.ItemIcon(piece));
            _cardTitle.text = LinkName(piece);
            _cardFrom.text = Loc.T(line.mine ? "Your call" : "Called by") + (line.mine ? "" : " " + line.name);
            _cardBody.text = MarketPanel.Summary(piece) + "\n\n" + MarketPanel.Etchings(piece);
            _cardTrade.SetActive(!line.mine);
            _cardWhisper.SetActive(!line.mine);
            _cardMoreLabel.text = line.mine ? "CLOSE" : "MORE";
            _card.SetActive(true);
        }

        public void Open(bool guild = false)
        {
            _message.text = "";
            _actions.SetActive(false);
            _picker.SetActive(false);
            _card.SetActive(false);
            _canvas.SetActive(true);
            Show(guild && _root.Server.InGuild ? _guild : _world);
        }

        public void Close()
        {
            _actions.SetActive(false);
            _picker.SetActive(false);
            _card.SetActive(false);
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
            // LINK only on the Bazaar Call; the field takes its room elsewhere.
            bool trade = channel == _trade;
            if (trade) _root.Tips.Offer(TipCard.Tip.Bazaar);
            _linkButton.gameObject.SetActive(trade);
            var field = (RectTransform)_input.transform;
            field.anchorMin = new Vector2(trade ? 0.225f : 0.04f, field.anchorMin.y);
            if (!trade) SetLink(null);
            if (trade && _root.Session.Level < Chat.TradeLevel) _message.text = $"The Bazaar Call opens at level {Chat.TradeLevel}.";
            else if (!trade && Ui.Src(_message).StartsWith("The Bazaar Call")) _message.text = "";
        }

        private bool _stickToBottom;
        private bool _offlineShown;

        private void Send()
        {
            string text = Chat.Normalise(_input.text);
            bool trade = _shown == _trade;
            string link = trade && _link != null ? _root.Server.IdOf(_link) : null;
            if (_busy || (text.Length == 0 && link == null) || !_root.Server.Online) return;
            if (trade && _root.Session.Level < Chat.TradeLevel) { _message.text = $"The Bazaar Call opens at level {Chat.TradeLevel}."; return; }
            if (trade && Time.realtimeSinceStartup < _tradeReadyAt) return;
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
                if (trade)
                {
                    SetLink(null);
                    _tradeReadyAt = Time.realtimeSinceStartup + Chat.TradeCooldownSeconds;
                }
                channel.Take(dto);
                _blockedCount = dto.blocked;
                _stickToBottom = true;
            }, link));
        }

        private void Pick(int index)
        {
            if (index >= _shown.Lines.Count) return;
            ChatLineDto line = _shown.Lines[index];
            // A linked piece opens its card (one's own too, to see what was called).
            if (!line.system && HasLink(line)) { ShowCard(line); return; }
            if (line.system || line.mine) return;
            ShowActions(line);
        }

        private void ShowActions(ChatLineDto line)
        {
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

        /// <summary>A private message to the hero picked (MESSAGES).</summary>
        private void MessagePicked()
        {
            ChatLineDto line = _picked;
            _actions.SetActive(false);
            if (line == null) return;
            Close();
            _root.Messages.OpenWith(line.accountId, line.name);
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

        private void InspectPicked()
        {
            ChatLineDto line = _picked;
            _actions.SetActive(false);
            if (line != null) _root.Inspect.Open(line.accountId);
        }

        private void ReportNamePicked(string kind)
        {
            ChatLineDto line = _picked;
            _actions.SetActive(false);
            if (line == null) return;
            StartCoroutine(_root.Server.ReportName(kind, line.accountId, (message, error) =>
                _message.text = error != null ? ConfirmDialog.Tint(error, Palette.Bad) : message));
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
                    _trade.Lines.RemoveAll(l => l.accountId == line.accountId);
                    _world.Dirty = _guild.Dirty = _trade.Dirty = true;
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
                    _trade.Clear();
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
            // A worn title (Rules.Achievements) before the name, in the reader's language (the row itself stays raw).
            string title = string.IsNullOrEmpty(line.title) ? "" : $"<color=#FFD66B>‹{Loc.T(line.title)}›</color> ";
            // A Bazaar Call's piece after the words, in its rarity's colour (tap the line for its card).
            string link = HasLink(line) ? " " + LinkName(ToState(line.item)) : line.linkGone ? $" <color=#8C857A>[{Loc.T("sold or gone")}]</color>" : "";
            return time + $"<color=#{mark}>■</color> {title}{name}: {text}{link}";
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
                _trade.Clear();
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
                if (IsOpen && _shown == _trade) Poll(_trade, PollOpen);
            }
            if (_world.Lines.Count > 0) Ticker = Format(_world.Lines[_world.Lines.Count - 1], clock: false);

            if (!IsOpen) return;
            if (_shown == _guild && !_root.Server.InGuild) _shown = _world;
            _worldTab.GetComponent<Image>().color = _shown == _world ? Palette.Safe : Palette.ButtonIdle;
            _guildTab.GetComponent<Image>().color = _shown == _guild ? Palette.Safe : Palette.ButtonIdle;
            _tradeTab.GetComponent<Image>().color = _shown == _trade ? Palette.Safe : Palette.ButtonIdle;
            _guildTab.interactable = _root.Server.InGuild;
            float wait = _shown == _trade ? _tradeReadyAt - Time.realtimeSinceStartup : 0f;
            _send.interactable = !_busy && _root.Server.Online && wait <= 0f;
            _sendLabel.text = wait > 0f ? Mathf.CeilToInt(wait) + "s" : "SEND";
            if (_shown == _trade) _linkLabel.text = _link != null ? ConfirmDialog.Tint("LINKED", GearPanel.RarityColor(_link.Rarity)) : "LINK";
            _blocked.text = _blockedCount > 0 ? $"{_blockedCount} blocked · unblock" : "";
            if (!_root.Server.Online && !_offlineShown) { _message.text = "Offline: chat needs the server."; _offlineShown = true; }
            else if (_root.Server.Online && _offlineShown) { _message.text = ""; _offlineShown = false; }

            if (_bazaarForShot || _cardForShot)
            {
                _bazaarForShot = false;
                Show(_trade);
            }
            if (_cardForShot && _shown == _trade && _trade.Lines.Count > 0)
            {
                int linked = _trade.Lines.FindLastIndex(HasLink);
                if (linked >= 0) { _cardForShot = false; ShowCard(_trade.Lines[linked]); }
            }
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
                // Players' lines stay as they wrote them; the steppe's own (system) lines take the language.
                if (has && _rows[i] is LocText row) row.Raw = !_shown.Lines[i].system;
                if (has) _rows[i].text = Format(_shown.Lines[i], clock: true);
            }
            if (_shown.Lines.Count == 0)
            {
                if (_rows.Count == 0) _rows.Add(Ui.ListRow("Line0", _content, 26, () => { }));
                _rows[0].gameObject.SetActive(true);
                if (_rows[0] is LocText quiet) quiet.Raw = false;
                _rows[0].text = ConfirmDialog.Tint(_shown == _guild ? "The guild is quiet. Say hello." : _shown == _trade ? "The bazaar is quiet. Call your wares."
                    : "The steppe is quiet. Say hello.", Palette.Muted);
            }
            if (atBottom)
            {
                Canvas.ForceUpdateCanvases();
                _scroll.verticalNormalizedPosition = 0f;
            }
        }
    }
}
