using System.Collections.Generic;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// FRIENDS (owner, 25 Sep 2026: "adding friends and friend list"): this hero's friends, online first, with the heroes
    /// asking and the ones asked (Rules.Friends). Add a hero by name here, or tap a name in CHAT. Tap a friend to ask them to
    /// trade, invite them to your guild (leader or officer) or take them off; tap one asking to take or turn down the request.
    /// </summary>
    public sealed class FriendsPanel : MonoBehaviour
    {
        private const float RefreshSeconds = 10f;

        private enum Kind { Header, Friend, Asking, Asked }

        private sealed class Entry
        {
            public Kind Kind;
            public FriendDto Hero;
            public string Text;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private ConfirmDialog _confirm;
        private Text _count;
        private InputField _name;
        private Button _add;
        private RectTransform _content;
        private readonly List<Text> _rows = new List<Text>();
        private readonly List<Entry> _entries = new List<Entry>();
        private Text _message;
        private bool _busy;
        private bool _fetching;
        private float _nextFetch;
        private FriendsDto _shown;

        // The tapped hero's actions.
        private GameObject _actions;
        private Text _actionsTitle;
        private GameObject _friendActions;
        private Button _invite;
        private GameObject _askingActions;
        private GameObject _askedActions;
        private Entry _picked;

        public bool IsOpen => _canvas.activeSelf;

        /// <summary>Friend requests already seen here: the HUD calls only when more arrive.</summary>
        public int SeenAsks { get; set; }

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("FriendsCanvas", 15).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Chat");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "FRIENDS", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _count = Ui.Label("Count", canvas, 0.05f, 0.895f, 0.95f, 0.93f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);
            _name = Ui.Input("Name", canvas, 0.04f, 0.835f, 0.7f, 0.885f, "A hero's name", 28, Characters.NameMax);
            _name.lineType = InputField.LineType.SingleLine;
            _add = Ui.Button("Add", canvas, 0.72f, 0.835f, 0.96f, 0.885f, "ADD", 28, Palette.Safe, AddByName, out _);
            Ui.Scroll("List", canvas, 0.04f, 0.2f, 0.96f, 0.825f, out _content);
            _message = Ui.Label("Message", canvas, 0.05f, 0.13f, 0.95f, 0.19f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);
            _message.supportRichText = true;
            Ui.Label("Hint", canvas, 0.05f, 0.085f, 0.95f, 0.125f, "Tap a name here or in CHAT to trade, invite to your guild or add a friend.", 19,
                TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, Close, out _);

            _actions = Ui.Rect("Actions", canvas, 0f, 0f, 1f, 1f).gameObject;
            Image dim = Ui.Panel("Dim", _actions.transform, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => _actions.SetActive(false));
            Transform box = Ui.Framed("Box", _actions.transform, 0.1f, 0.38f, 0.9f, 0.62f, Palette.PanelDark).transform;
            _actionsTitle = Ui.Title("Title", box, 0.05f, 0.74f, 0.95f, 0.96f, "", 30, TextAnchor.MiddleCenter, Palette.Sorn);
            _actionsTitle.supportRichText = true;

            _friendActions = Ui.Rect("Friend", box, 0f, 0f, 1f, 1f).gameObject;
            Ui.Button("Trade", _friendActions.transform, 0.06f, 0.46f, 0.48f, 0.7f, "TRADE", 26, Palette.Alloy, TradePicked, out _);
            _invite = Ui.Button("Invite", _friendActions.transform, 0.52f, 0.46f, 0.94f, 0.7f, "GUILD INVITE", 24, Palette.Safe, InvitePicked, out _);
            Ui.Button("Remove", _friendActions.transform, 0.06f, 0.2f, 0.48f, 0.42f, "TAKE OFF", 24, Palette.Danger, AskRemovePicked, out _);
            Ui.Button("Cancel", _friendActions.transform, 0.52f, 0.2f, 0.94f, 0.42f, "CLOSE", 24, Palette.ButtonIdle, () => _actions.SetActive(false), out _);

            _askingActions = Ui.Rect("Asking", box, 0f, 0f, 1f, 1f).gameObject;
            Ui.Button("Take", _askingActions.transform, 0.06f, 0.46f, 0.48f, 0.7f, "BE FRIENDS", 24, Palette.Safe, () => AnswerPicked(true), out _);
            Ui.Button("Decline", _askingActions.transform, 0.52f, 0.46f, 0.94f, 0.7f, "TURN DOWN", 24, Palette.Danger, () => AnswerPicked(false), out _);
            Ui.Button("Cancel", _askingActions.transform, 0.3f, 0.2f, 0.7f, 0.42f, "CLOSE", 24, Palette.ButtonIdle, () => _actions.SetActive(false), out _);

            _askedActions = Ui.Rect("Asked", box, 0f, 0f, 1f, 1f).gameObject;
            Ui.Button("TakeBack", _askedActions.transform, 0.06f, 0.46f, 0.94f, 0.7f, "TAKE BACK THE REQUEST", 24, Palette.Danger, RemovePicked, out _);
            Ui.Button("Cancel", _askedActions.transform, 0.3f, 0.2f, 0.7f, 0.42f, "CLOSE", 24, Palette.ButtonIdle, () => _actions.SetActive(false), out _);
            _actions.SetActive(false);

            _canvas.SetActive(false);
            _confirm = new GameObject("FriendsConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
        }

        public void Open()
        {
            _message.text = "";
            _nextFetch = 0f;
            _shown = null;
            _actions.SetActive(false);
            _canvas.SetActive(true);
        }

        public void Close()
        {
            _actions.SetActive(false);
            _canvas.SetActive(false);
        }

        private void Say(string message, string error) =>
            _message.text = error != null ? ConfirmDialog.Tint(error, Palette.Bad) : message ?? "";

        /// <summary>Runs one friend call at a time and shows its answer.</summary>
        private void Run(System.Collections.IEnumerator call)
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(call);
        }

        private System.Action<string, string> Answered(System.Action onDone = null) => (message, error) =>
        {
            _busy = false;
            Say(message, error);
            if (error == null) onDone?.Invoke();
        };

        private void AddByName()
        {
            string name = (_name.text ?? "").Trim();
            if (name.Length == 0 || !_root.Server.Online) return;
            Run(_root.Server.AddFriend(null, name, Answered(() => _name.text = "")));
        }

        private void Pick(int index)
        {
            if (index >= _entries.Count || _entries[index].Kind == Kind.Header) return;
            _picked = _entries[index];
            FriendDto hero = _picked.Hero;
            _actionsTitle.text = hero.name + (string.IsNullOrEmpty(hero.guildTag) ? "" : $"  <size=22>[{hero.guildTag}]</size>");
            _friendActions.SetActive(_picked.Kind == Kind.Friend);
            _askingActions.SetActive(_picked.Kind == Kind.Asking);
            _askedActions.SetActive(_picked.Kind == Kind.Asked);
            // Guild invites are the leader's and officers' (the server says whether this hero may).
            _invite.interactable = _root.Server.Friends != null && _root.Server.Friends.canInvite && string.IsNullOrEmpty(hero.guildTag);
            _actions.SetActive(true);
        }

        private void TradePicked()
        {
            FriendDto hero = _picked?.Hero;
            _actions.SetActive(false);
            if (hero == null || _busy) return;
            _busy = true;
            StartCoroutine(_root.Server.TradeInvite(hero.name, error =>
            {
                _busy = false;
                if (error != null) { Say(null, error); return; }
                Close();
                _root.Trade.Open();
            }, hero.accountId));
        }

        private void InvitePicked()
        {
            FriendDto hero = _picked?.Hero;
            _actions.SetActive(false);
            if (hero == null) return;
            Run(_root.Server.InviteToGuild(hero.accountId, null, Answered()));
        }

        private void AnswerPicked(bool accept)
        {
            FriendDto hero = _picked?.Hero;
            _actions.SetActive(false);
            if (hero == null) return;
            Run(_root.Server.AnswerFriend(hero.accountId, accept, Answered(accept ? () => GameAudio.Instance?.Play("LaneLoot", 0.9f, 0.1f, 0f) : (System.Action)null)));
        }

        private void AskRemovePicked()
        {
            FriendDto hero = _picked?.Hero;
            _actions.SetActive(false);
            if (hero == null) return;
            _confirm.Show("Take " + hero.name + " off?", "They leave your friend list, and you leave theirs.", "TAKE OFF", Palette.Danger,
                () => Run(_root.Server.RemoveFriend(hero.accountId, Answered())));
        }

        private void RemovePicked()
        {
            FriendDto hero = _picked?.Hero;
            _actions.SetActive(false);
            if (hero == null) return;
            Run(_root.Server.RemoveFriend(hero.accountId, Answered()));
        }

        /// <summary>One hero as rich text: the Banner's mark, name, level and class, guild tag, and when last seen.</summary>
        private static string Line(FriendDto f, string tail)
        {
            string mark = ColorUtility.ToHtmlStringRGB(BannerLook.Color(BannerLook.Parse(f.banner)));
            string tag = string.IsNullOrEmpty(f.guildTag) ? "" : $"  [{f.guildTag}]";
            string seen = Friends.Online(f.minutesAway) ? "<color=#8CF08C>online</color>" : $"<color=#8C857A>{Friends.Seen(f.minutesAway)}</color>";
            return $"<color=#{mark}>■</color>  <b>{f.name}</b>{tag}   <color=#C2BAAD>Lv {f.level} {f.@class}</color>   {seen}{tail}";
        }

        private void Rebuild(FriendsDto dto)
        {
            _entries.Clear();
            void Header(string text) => _entries.Add(new Entry { Kind = Kind.Header, Text = $"<color=#E8C170><b>{text}</b></color>" });
            FriendDto[] asking = dto.asking ?? new FriendDto[0];
            FriendDto[] friends = dto.friends ?? new FriendDto[0];
            FriendDto[] asked = dto.asked ?? new FriendDto[0];
            if (asking.Length > 0)
            {
                Header($"ASKING YOU ({asking.Length})");
                foreach (FriendDto f in asking) _entries.Add(new Entry { Kind = Kind.Asking, Hero = f, Text = Line(f, "   <color=#FFD66B>tap to answer</color>") });
            }
            Header($"FRIENDS ({friends.Length})");
            if (friends.Length == 0)
                _entries.Add(new Entry { Kind = Kind.Header, Text = ConfirmDialog.Tint("No friends yet. Add a hero by name above, or tap a name in CHAT.", Palette.Muted) });
            foreach (FriendDto f in friends) _entries.Add(new Entry { Kind = Kind.Friend, Hero = f, Text = Line(f, "") });
            if (asked.Length > 0)
            {
                Header($"ASKED ({asked.Length})");
                foreach (FriendDto f in asked) _entries.Add(new Entry { Kind = Kind.Asked, Hero = f, Text = Line(f, "   <color=#8C857A>waiting</color>") });
            }

            while (_rows.Count < _entries.Count)
            {
                int index = _rows.Count;
                _rows.Add(Ui.ListRow("Row" + index, _content, 26, () => Pick(index)));
            }
            for (int i = 0; i < _rows.Count; i++)
            {
                bool has = i < _entries.Count;
                _rows[i].gameObject.SetActive(has);
                if (has) _rows[i].text = _entries[i].Text;
            }
            int online = 0;
            foreach (FriendDto f in friends) if (Friends.Online(f.minutesAway)) online++;
            _count.text = $"{friends.Length} / {dto.max} friends  ·  {online} online";
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            SeenAsks = _root.Server.FriendAsks;
            if (_root.Server.Online && !_fetching && !_busy && Time.realtimeSinceStartup >= _nextFetch)
            {
                _fetching = true;
                StartCoroutine(_root.Server.FetchFriends(error =>
                {
                    _fetching = false;
                    _nextFetch = Time.realtimeSinceStartup + RefreshSeconds;
                    if (error != null) Say(null, error);
                }));
            }
            _add.interactable = !_busy && _root.Server.Online;
            FriendsDto dto = _root.Server.Friends;
            if (dto == null)
            {
                _count.text = _root.Server.Online ? "Gathering your friends..." : "Offline: friends need the server.";
                return;
            }
            if (dto != _shown)
            {
                _shown = dto;
                Rebuild(dto);
            }
        }
    }
}
