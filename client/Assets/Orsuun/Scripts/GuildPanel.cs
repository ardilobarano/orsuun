using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// GUILD. Without a guild: search and JOIN open guilds, or found one (name, tag, colour; 100,000 sorn). In a guild:
    /// the emblem, level and treasury, the daily donation (Guild Tallies for the donor), the two guild skills, the
    /// guild shop and the member list; tap a member to promote, demote, hand over the lead or send them away. Guild
    /// invites (owner, 25 Sep 2026): the leader or an officer invites a hero by name under ASKING / INVITE (or from chat
    /// and the friend list); a hero without a guild answers its invites under INVITES, which let it in through shut gates.
    /// Everything is decided by the server; the panel refreshes while open.
    /// </summary>
    public sealed class GuildPanel : MonoBehaviour
    {
        private const float RefreshSeconds = 15f;
        private const int BrowseRows = 8;
        private const int MemberRows = 7;

        private sealed class BrowseRow
        {
            public GameObject Root;
            public Image Plate;
            public Text Tag;
            public Text Label;
            public Button Join;
            public Text JoinLabel;
        }

        private sealed class MemberRow
        {
            public Button Button;
            public Text Label;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private ConfirmDialog _confirm;
        private Text _message;
        private bool _busy;
        private bool _fetching;
        private float _nextFetch;
        private bool _offlineShown;

        // Browse / found a guild.
        private GameObject _browse;
        private InputField _search;
        private readonly BrowseRow[] _rows = new BrowseRow[BrowseRows];
        private Text _empty;
        private InputField _name;
        private InputField _tag;
        // The kit's framed buttons have no Outline: the chosen colour stands a little larger than the rest.
        private readonly Transform[] _swatches = new Transform[Guilds.Colors.Length];
        private int _color;
        private Button _create;

        // Home: the account's guild.
        private GameObject _home;
        private Image _plate;
        private Text _plateTag;
        private Text _title;
        private Text _info;
        private RectTransform _xpFill;
        private Text _xpText;
        private Text _event;
        private Text _donateText;
        private Button[] _donate;
        private Text _plunderText;
        private Text _musterText;
        private Button _plunderRaise;
        private Button _musterRaise;
        private Text _shopTitle;
        private Button[] _shop;
        private Text _membersTitle;
        private readonly MemberRow[] _members = new MemberRow[MemberRows];
        private int _page;
        private Button _gates;
        private Text _gatesLabel;
        private Button _requestsButton;
        private Text _requestsLabel;

        // Invites to this hero (no guild), answered under INVITES.
        private const int InviteRows = 6;
        private Button _invitesButton;
        private Text _invitesLabel;
        private GameObject _invites;
        private readonly Text[] _inviteNames = new Text[InviteRows];
        private readonly GameObject[] _inviteRows = new GameObject[InviteRows];
        private Text _invitesEmpty;
        private bool _invitesShownOnce;
        private bool _popupForShot = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-guildpopup") >= 0;

        // Invites sent by the leader or an officer (under ASKING / INVITE).
        private InputField _inviteName;
        private Text _invited;

        // Join requests (shut gates), answered by the leader or an officer.
        private const int RequestRows = 6;
        private GameObject _requests;
        private readonly Text[] _requestNames = new Text[RequestRows];
        private readonly GameObject[] _requestRows = new GameObject[RequestRows];
        private Text _requestsEmpty;

        // Member actions.
        private GameObject _manage;
        private Text _manageTitle;
        private Button _promote;
        private Text _promoteLabel;
        private Button _lead;
        private Button _kick;
        private GuildMemberDto _selected;

        public bool IsOpen => _canvas.activeSelf;

        private static bool HasGuild(GuildViewDto v) => v?.mine != null && !string.IsNullOrEmpty(v.mine.id);

        public static Color ColorOf(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Palette.Muted;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("GuildCanvas", 9).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Guild");
            BuildBrowse(canvas);
            BuildHome(canvas);
            _message = Ui.Label("Message", canvas, 0.05f, 0.08f, 0.95f, 0.125f, "", 24, TextAnchor.MiddleCenter, Palette.Muted);
            _message.supportRichText = true;
            BuildManage(canvas);
            BuildRequests(canvas);
            BuildInvites(canvas);
            _canvas.SetActive(false);
            _confirm = new GameObject("GuildConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
        }

        private void BuildBrowse(Transform canvas)
        {
            _browse = Ui.Rect("Browse", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform b = _browse.transform;
            Ui.Title("Title", b, 0.05f, 0.935f, 0.95f, 0.98f, "GUILDS", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Label("Lead", b, 0.05f, 0.895f, 0.95f, 0.935f, "Any Banner may join any guild. Donate each day, earn Guild Tallies, raise the guild together.",
                22, TextAnchor.MiddleCenter, Palette.Muted);
            _search = Ui.Input("Search", b, 0.04f, 0.835f, 0.54f, 0.885f, "Search by name or tag", 28, 20);
            Ui.Button("SearchGo", b, 0.56f, 0.835f, 0.75f, 0.885f, "SEARCH", 24, Palette.ButtonIdle, Refresh, out _);
            _invitesButton = Ui.Button("Invites", b, 0.77f, 0.835f, 0.96f, 0.885f, "INVITES", 22, Palette.Alloy, OpenInvites, out _invitesLabel);

            for (int i = 0; i < BrowseRows; i++)
            {
                int index = i;
                float y1 = 0.825f - i * 0.053f;
                float y0 = y1 - 0.047f;
                var r = new BrowseRow();
                r.Root = Ui.Framed("Row" + i, b, 0.04f, y0, 0.96f, y1, Palette.PanelDark).gameObject;
                Transform row = r.Root.transform;
                r.Plate = Ui.Panel("Plate", row, 0.01f, 0.1f, 0.15f, 0.9f, Palette.Muted);
                r.Tag = Ui.Raw(Ui.Title("Tag", r.Plate.transform, 0f, 0f, 1f, 1f, "", 26, TextAnchor.MiddleCenter, Palette.Parchment));
                r.Label = Ui.Raw(Ui.Label("Label", row, 0.17f, 0f, 0.77f, 1f, "", 24, TextAnchor.MiddleLeft, Palette.Parchment));
                r.Join = Ui.Button("Join", row, 0.79f, 0.08f, 0.99f, 0.92f, "JOIN", 24, Palette.Safe, () => Join(index), out r.JoinLabel);
                _rows[i] = r;
            }
            _empty = Ui.Label("Empty", b, 0.05f, 0.6f, 0.95f, 0.7f, "", 26, TextAnchor.MiddleCenter, Palette.Muted);

            Ui.Trim("CreateRule", b, 0.04f, 0.4f, 0.96f, 0.402f);
            Ui.Title("CreateTitle", b, 0.04f, 0.355f, 0.96f, 0.395f, $"FOUND A GUILD  ·  {Guilds.CreateCost:N0} sorn", 26, TextAnchor.MiddleLeft, Palette.Sorn);
            _name = Ui.Input("Name", b, 0.04f, 0.295f, 0.66f, 0.35f, "Guild name (3 to 20)", 28, 20);
            _tag = Ui.Input("Tag", b, 0.68f, 0.295f, 0.96f, 0.35f, "TAG", 28, 4);
            _tag.characterValidation = InputField.CharacterValidation.Alphanumeric;
            _tag.onValueChanged.AddListener(t =>
            {
                string upper = t.ToUpperInvariant();
                if (upper != t) _tag.text = upper;
            });
            for (int i = 0; i < Guilds.Colors.Length; i++)
            {
                int index = i;
                float x0 = 0.04f + i * 0.116f;
                Button swatch = Ui.Button("Swatch" + i, b, x0, 0.225f, x0 + 0.1f, 0.28f, "", 20, ColorOf(Guilds.Colors[i]), () => _color = index, out _);
                _swatches[i] = swatch.transform;
            }
            _create = Ui.Button("Create", b, 0.25f, 0.14f, 0.75f, 0.205f, "FOUND THE GUILD", 30, Palette.ButtonForge, AskCreate, out _);
            Ui.Button("Close", b, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, Close, out _);
        }

        private void BuildHome(Transform canvas)
        {
            _home = Ui.Rect("Home", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform h = _home.transform;
            _plate = Ui.Framed("Plate", h, 0.04f, 0.87f, 0.21f, 0.975f, Palette.Muted);
            // A guild's name and tag are its players' words: never translated.
            _plateTag = Ui.Raw(Ui.Title("Tag", _plate.transform, 0f, 0f, 1f, 1f, "", 44, TextAnchor.MiddleCenter, Palette.Parchment));
            _title = Ui.Raw(Ui.Title("Name", h, 0.23f, 0.93f, 0.96f, 0.975f, "", 42, TextAnchor.MiddleLeft, Palette.Parchment));
            _info = Ui.Label("Info", h, 0.23f, 0.87f, 0.96f, 0.93f, "", 22, TextAnchor.MiddleLeft, Palette.Muted);
            _xpFill = Ui.Bar("Xp", h, 0.04f, 0.834f, 0.96f, 0.866f, new Color(0.85f, 0.62f, 0.2f), out _);
            _xpText = Ui.Title("XpText", h, 0.04f, 0.838f, 0.96f, 0.862f, "", 20, TextAnchor.MiddleCenter, Palette.Parchment);
            _event = Ui.Label("Event", h, 0.04f, 0.8f, 0.96f, 0.835f, "", 22, TextAnchor.MiddleLeft, Palette.Parchment);

            Ui.Framed("DonateBack", h, 0.04f, 0.725f, 0.96f, 0.795f, Palette.PanelDark);
            _donateText = Ui.Label("DonateText", h, 0.06f, 0.725f, 0.5f, 0.795f, "", 22, TextAnchor.MiddleLeft, Palette.Parchment);
            long[] amounts = { 10_000, 50_000, -1 };
            string[] labels = { "GIVE 10K", "GIVE 50K", "GIVE MAX" };
            _donate = new Button[amounts.Length];
            for (int i = 0; i < amounts.Length; i++)
            {
                long amount = amounts[i];
                float x0 = 0.52f + i * 0.145f;
                _donate[i] = Ui.Button("Donate" + i, h, x0, 0.735f, x0 + 0.135f, 0.785f, labels[i], 20, Palette.ButtonForge, () => Donate(amount), out _);
            }

            Ui.Framed("PlunderCard", h, 0.04f, 0.6f, 0.49f, 0.715f, Palette.PanelDark);
            _plunderText = Ui.Label("PlunderText", h, 0.06f, 0.655f, 0.47f, 0.713f, "", 22, TextAnchor.MiddleLeft, Palette.Parchment);
            _plunderRaise = Ui.Button("PlunderRaise", h, 0.06f, 0.607f, 0.47f, 0.65f, "", 20, Palette.Safe, () => AskRaise(GuildSkill.Plunder), out _);
            Ui.Framed("MusterCard", h, 0.51f, 0.6f, 0.96f, 0.715f, Palette.PanelDark);
            _musterText = Ui.Label("MusterText", h, 0.53f, 0.655f, 0.94f, 0.713f, "", 22, TextAnchor.MiddleLeft, Palette.Parchment);
            _musterRaise = Ui.Button("MusterRaise", h, 0.53f, 0.607f, 0.94f, 0.65f, "", 20, Palette.Safe, () => AskRaise(GuildSkill.Muster), out _);

            _shopTitle = Ui.Title("ShopTitle", h, 0.04f, 0.555f, 0.96f, 0.59f, "", 24, TextAnchor.MiddleLeft, Palette.Sorn);
            _shop = new Button[Guilds.Shop.Length];
            for (int i = 0; i < Guilds.Shop.Length; i++)
            {
                GuildShopItem item = Guilds.Shop[i];
                float x0 = 0.04f + i * 0.31f;
                _shop[i] = Ui.Button("Shop" + i, h, x0, 0.475f, x0 + 0.3f, 0.55f, $"{item.Name}\n<size=16>{item.Detail}</size>\n{item.Tallies} TALLIES", 20,
                    Palette.ButtonIdle, () => Buy(item.Id), out _);
            }

            _membersTitle = Ui.Title("MembersTitle", h, 0.04f, 0.43f, 0.4f, 0.465f, "", 24, TextAnchor.MiddleLeft, Palette.Sorn);
            _requestsButton = Ui.Button("Requests", h, 0.41f, 0.43f, 0.62f, 0.467f, "", 18, Palette.Alloy, OpenRequests, out _requestsLabel);
            Ui.Button("Prev", h, 0.64f, 0.43f, 0.79f, 0.467f, "PREV", 20, Palette.ButtonIdle, () => _page = Mathf.Max(0, _page - 1), out _);
            Ui.Button("Next", h, 0.81f, 0.43f, 0.96f, 0.467f, "NEXT", 20, Palette.ButtonIdle, () => _page++, out _);
            for (int i = 0; i < MemberRows; i++)
            {
                int index = i;
                float y1 = 0.425f - i * 0.043f;
                var m = new MemberRow();
                m.Button = Ui.Button("Member" + i, h, 0.04f, y1 - 0.039f, 0.96f, y1, "", 20, Palette.PanelDark, () => Select(index), out m.Label);
                m.Label.alignment = TextAnchor.MiddleLeft;
                m.Label.font = Ui.Font;
                m.Label.fontStyle = FontStyle.Normal;
                m.Label.supportRichText = true;
                _members[i] = m;
            }

            Ui.Button("Leave", h, 0.02f, 0.015f, 0.14f, 0.075f, "LEAVE", 20, Palette.Danger, AskLeave, out _);
            _gates = Ui.Button("Gates", h, 0.15f, 0.015f, 0.29f, 0.075f, "", 16, Palette.ButtonIdle, ToggleGates, out _gatesLabel);
            Ui.Button("Chat", h, 0.3f, 0.015f, 0.44f, 0.075f, "GUILD CHAT", 16, Palette.Safe, () => _root.Chat.Open(guild: true), out _);
            Ui.Button("War", h, 0.45f, 0.015f, 0.59f, 0.075f, "GUILD WAR", 16, Palette.Danger, () => _root.GuildWar.Open(), out _);
            Ui.Button("Raid", h, 0.6f, 0.015f, 0.74f, 0.075f, "GUILD RAID", 16, Palette.ButtonForge, () =>
            {
                Close();
                _root.GuildRaid.Open();
            }, out _);
            Ui.Button("Close", h, 0.75f, 0.015f, 0.98f, 0.075f, "BACK TO THE HUNT", 18, Palette.ButtonIdle, Close, out _);
        }

        private void BuildRequests(Transform canvas)
        {
            _requests = Ui.Rect("Requests", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform m = _requests.transform;
            Image dim = Ui.Panel("Dim", m, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => _requests.SetActive(false));
            Transform box = Ui.Framed("Box", m, 0.06f, 0.16f, 0.94f, 0.84f, Palette.PanelDark).transform;
            Ui.Title("Title", box, 0.05f, 0.92f, 0.95f, 0.985f, "ASKING TO JOIN", 30, TextAnchor.MiddleCenter, Palette.Sorn);
            for (int i = 0; i < RequestRows; i++)
            {
                int index = i;
                float y1 = 0.91f - i * 0.083f;
                _requestRows[i] = Ui.Rect("Row" + i, box, 0.03f, y1 - 0.076f, 0.97f, y1).gameObject;
                Transform row = _requestRows[i].transform;
                _requestNames[i] = Ui.Label("Name", row, 0.02f, 0f, 0.56f, 1f, "", 22, TextAnchor.MiddleLeft, Palette.Parchment);
                _requestNames[i].supportRichText = true;
                Ui.Button("Accept", row, 0.58f, 0.08f, 0.78f, 0.92f, "LET IN", 20, Palette.Safe, () => Answer(index, true), out _);
                Ui.Button("Decline", row, 0.8f, 0.08f, 0.99f, 0.92f, "TURN AWAY", 18, Palette.Danger, () => Answer(index, false), out _);
            }
            _requestsEmpty = Ui.Label("Empty", box, 0.05f, 0.62f, 0.95f, 0.72f, "Nobody is asking to join.", 24, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Trim("InviteRule", box, 0.04f, 0.4f, 0.96f, 0.403f);
            Ui.Title("InviteTitle", box, 0.04f, 0.33f, 0.96f, 0.39f, "INVITE A HERO", 26, TextAnchor.MiddleLeft, Palette.Sorn);
            _inviteName = Ui.Input("InviteName", box, 0.03f, 0.245f, 0.68f, 0.32f, "The hero's name", 26, Characters.NameMax);
            _inviteName.lineType = InputField.LineType.SingleLine;
            Ui.Button("InviteGo", box, 0.7f, 0.245f, 0.97f, 0.32f, "INVITE", 24, Palette.Safe, InviteByName, out _);
            _invited = Ui.Label("Invited", box, 0.04f, 0.115f, 0.96f, 0.235f, "", 20, TextAnchor.UpperLeft, Palette.Muted);
            Ui.Button("Close", box, 0.3f, 0.02f, 0.7f, 0.1f, "CLOSE", 22, Palette.ButtonIdle, () => _requests.SetActive(false), out _);
            _requests.SetActive(false);
        }

        private void OpenRequests() => _requests.SetActive(true);

        private void InviteByName()
        {
            string name = (_inviteName.text ?? "").Trim();
            if (name.Length == 0 || _busy || !_root.Server.Online) return;
            _busy = true;
            StartCoroutine(_root.Server.InviteToGuild(null, name, (message, error) =>
            {
                _busy = false;
                Say(message, error);
                if (error == null) _inviteName.text = "";
            }));
        }

        private void BuildInvites(Transform canvas)
        {
            _invites = Ui.Rect("Invites", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform m = _invites.transform;
            Image dim = Ui.Panel("Dim", m, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => _invites.SetActive(false));
            Transform box = Ui.Framed("Box", m, 0.06f, 0.25f, 0.94f, 0.75f, Palette.PanelDark).transform;
            Ui.Title("Title", box, 0.05f, 0.88f, 0.95f, 0.98f, "GUILD INVITES", 30, TextAnchor.MiddleCenter, Palette.Sorn);
            for (int i = 0; i < InviteRows; i++)
            {
                int index = i;
                float y1 = 0.86f - i * 0.12f;
                _inviteRows[i] = Ui.Rect("Row" + i, box, 0.03f, y1 - 0.11f, 0.97f, y1).gameObject;
                Transform row = _inviteRows[i].transform;
                _inviteNames[i] = Ui.Label("Name", row, 0.02f, 0f, 0.6f, 1f, "", 20, TextAnchor.MiddleLeft, Palette.Parchment);
                _inviteNames[i].supportRichText = true;
                Ui.Button("Accept", row, 0.62f, 0.08f, 0.8f, 0.92f, "JOIN", 22, Palette.Safe, () => AnswerInvite(index, true), out _);
                Ui.Button("Decline", row, 0.82f, 0.08f, 0.99f, 0.92f, "NO", 22, Palette.Danger, () => AnswerInvite(index, false), out _);
            }
            _invitesEmpty = Ui.Label("Empty", box, 0.05f, 0.4f, 0.95f, 0.6f, "No guild has invited you. A guild's leader or officers can, from chat or their friend list.",
                22, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Close", box, 0.3f, 0.02f, 0.7f, 0.11f, "CLOSE", 22, Palette.ButtonIdle, () => _invites.SetActive(false), out _);
            _invites.SetActive(false);
        }

        private void OpenInvites() => _invites.SetActive(true);

        private void AnswerInvite(int index, bool accept)
        {
            GuildListItemDto[] invites = _root.Server.GuildView?.invites;
            if (invites == null || index >= invites.Length) return;
            Call("invite/answer", new GuildInviteAnswerRequest { requestId = NewRequestId(), guildId = invites[index].id, accept = accept }, () =>
            {
                if (accept)
                {
                    _invites.SetActive(false);
                    GameAudio.Instance?.Play("LaneLevelUp", 0.9f, 1f, 0f);
                }
            });
        }

        private void ShowInvites(GuildViewDto v)
        {
            GuildListItemDto[] invites = v?.invites ?? new GuildListItemDto[0];
            _invitesLabel.text = invites.Length > 0 ? $"INVITES ({invites.Length})" : "INVITES";
            _invitesButton.GetComponent<Image>().color = invites.Length > 0 ? Palette.ButtonForge : Palette.Alloy;
            // The first time the screen shows an invite, it opens by itself.
            if (invites.Length > 0 && !_invitesShownOnce)
            {
                _invitesShownOnce = true;
                _invites.SetActive(true);
            }
            if (!_invites.activeSelf) return;
            for (int i = 0; i < InviteRows; i++)
            {
                bool has = i < invites.Length;
                _inviteRows[i].SetActive(has);
                if (!has) continue;
                GuildListItemDto g = invites[i];
                string tint = ColorUtility.ToHtmlStringRGB(ColorOf(g.color));
                _inviteNames[i].text = $"<color=#{tint}><b>[{g.tag}]</b></color> {g.name}   Lv {g.level}   {g.members}/{g.maxMembers}\n<size=17><color=#8C857A>invited by {g.invitedBy}</color></size>";
            }
            _invitesEmpty.gameObject.SetActive(invites.Length == 0);
        }

        private void Answer(int index, bool accept)
        {
            GuildMemberDto[] asking = _root.Server.GuildView?.requests;
            if (asking == null || index >= asking.Length) return;
            Call("answer", new GuildAnswerRequest { requestId = NewRequestId(), accountId = asking[index].accountId, accept = accept });
        }

        private void ShowRequests(GuildViewDto v)
        {
            GuildMemberDto[] asking = v.requests ?? new GuildMemberDto[0];
            for (int i = 0; i < RequestRows; i++)
            {
                bool has = i < asking.Length;
                _requestRows[i].SetActive(has);
                if (!has) continue;
                GuildMemberDto a = asking[i];
                string mark = ColorUtility.ToHtmlStringRGB(BannerLook.Color(BannerLook.Parse(a.banner)));
                _requestNames[i].text = $"<color=#{mark}>■</color>  {a.name}   Lv {a.level}   ·   {Ago(a.lastSeenMinutes)}";
            }
            _requestsEmpty.gameObject.SetActive(asking.Length == 0);
            GuildMemberDto[] invited = v.invited ?? new GuildMemberDto[0];
            var names = new System.Collections.Generic.List<string>();
            foreach (GuildMemberDto a in invited) names.Add(a.name);
            _invited.text = invited.Length == 0 ? $"Invites wait {Guilds.InviteDays} days for an answer, and let the hero in even through shut gates."
                : $"Invited ({invited.Length}/{Guilds.MaxInvites}): " + string.Join(", ", names);
        }

        private void BuildManage(Transform canvas)
        {
            _manage = Ui.Rect("Manage", canvas, 0f, 0f, 1f, 1f).gameObject;
            Transform m = _manage.transform;
            Image dim = Ui.Panel("Dim", m, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => _manage.SetActive(false));
            Transform box = Ui.Framed("Box", m, 0.1f, 0.36f, 0.9f, 0.64f, Palette.PanelDark).transform;
            _manageTitle = Ui.Title("Title", box, 0.05f, 0.78f, 0.95f, 0.97f, "", 32, TextAnchor.MiddleCenter, Palette.Sorn);
            _promote = Ui.Button("Promote", box, 0.06f, 0.56f, 0.94f, 0.74f, "", 26, Palette.ButtonIdle, SetOfficer, out _promoteLabel);
            _lead = Ui.Button("Lead", box, 0.06f, 0.36f, 0.94f, 0.54f, "HAND OVER THE LEAD", 26, Palette.Alloy, AskLead, out _);
            _kick = Ui.Button("Kick", box, 0.06f, 0.16f, 0.94f, 0.34f, "SEND AWAY", 26, Palette.Danger, AskKick, out _);
            Ui.Button("Cancel", box, 0.3f, 0.02f, 0.7f, 0.14f, "CLOSE", 22, Palette.ButtonIdle, () => _manage.SetActive(false), out _);
            _manage.SetActive(false);
        }

        public void Open()
        {
            _message.text = "";
            _nextFetch = 0f;
            _page = 0;
            _manage.SetActive(false);
            _requests.SetActive(false);
            _invites.SetActive(false);
            _invitesShownOnce = false;
            _canvas.SetActive(true);
        }

        public void Close()
        {
            _manage.SetActive(false);
            _canvas.SetActive(false);
        }

        private void Refresh() => _nextFetch = 0f;

        private void Say(string message, string error)
        {
            _message.text = error != null ? ConfirmDialog.Tint(error, Palette.Bad) : message ?? "";
        }

        /// <summary>Runs a guild call once at a time and shows its answer.</summary>
        private void Call(string path, object request, System.Action onDone = null)
        {
            if (_busy || !_root.Server.Online) return;
            _busy = true;
            StartCoroutine(_root.Server.GuildCall(path, request, (message, error) =>
            {
                _busy = false;
                Say(message, error);
                if (error == null) onDone?.Invoke();
            }));
        }

        private void Join(int index)
        {
            GuildListItemDto g = _root.Server.GuildView?.browse != null && index < _root.Server.GuildView.browse.Length ? _root.Server.GuildView.browse[index] : null;
            if (g == null) return;
            Call("join", new GuildJoinRequest { requestId = NewRequestId(), guildId = g.id }, () => GameAudio.Instance?.Play("LaneLoot", 0.9f, 0.1f, 0f));
            // A shut guild answers later: look again soon.
            if (!g.open) _nextFetch = Time.realtimeSinceStartup + 3f;
        }

        private void AskCreate()
        {
            string name = Guilds.NormaliseName(_name.text ?? "");
            string tag = Guilds.NormaliseTag(_tag.text ?? "");
            string problem = Guilds.NameProblem(name) ?? Guilds.TagProblem(tag);
            if (problem != null) { Say(null, problem); return; }
            if (_root.Session.Inventory.Sorn < Guilds.CreateCost) { Say(null, $"A guild charter costs {Guilds.CreateCost:N0} sorn."); return; }
            _confirm.Show("Found " + name + "?",
                $"[{tag}] {name}\n\nThe charter costs {Guilds.CreateCost:N0} sorn. You will lead it; anyone may join until you shut the gates.",
                "FOUND IT", Palette.ButtonForge,
                () => Call("create", new GuildCreateRequest { requestId = NewRequestId(), name = name, tag = tag, color = Guilds.Colors[_color] },
                    () => GameAudio.Instance?.Play("LaneLevelUp", 0.9f, 1f, 0f)));
        }

        private void Donate(long amount)
        {
            GuildViewDto v = _root.Server.GuildView;
            if (v == null) return;
            long room = v.donationCap - v.donatedToday;
            long sorn = amount > 0 ? amount : System.Math.Min(room, _root.Session.Inventory.Sorn);
            sorn -= sorn % Guilds.SornPerXp;
            if (sorn <= 0) { Say(null, room <= 0 ? "You have given all you can today." : "Not enough sorn."); return; }
            Call("donate", new GuildDonateRequest { requestId = NewRequestId(), sorn = sorn }, () => GameAudio.Instance?.Play("LaneLoot", 0.9f, 0.1f, 0f));
        }

        private void AskRaise(GuildSkill skill)
        {
            GuildDto g = _root.Server.GuildView?.mine;
            if (g == null) return;
            int next = (skill == GuildSkill.Plunder ? g.plunder : g.muster) + 1;
            long cost = Guilds.SkillCost(skill, next);
            string what = skill == GuildSkill.Plunder ? $"every member hunts with +{next}% sorn" : $"room for {Guilds.MaxMembers(next)} members";
            _confirm.Show($"Raise {skill} to {next}?", $"{cost:N0} sorn from the treasury: {what}.", "RAISE", Palette.Safe,
                () => Call("skill", new GuildSkillRequest { requestId = NewRequestId(), skill = skill.ToString() }));
        }

        private void Buy(int itemId) => Call("shop", new GuildShopRequest { requestId = NewRequestId(), itemId = itemId });

        private void AskLeave()
        {
            GuildViewDto v = _root.Server.GuildView;
            if (!HasGuild(v)) return;
            bool last = v.mine.members <= 1;
            bool leader = _root.Server.Guild?.rank == nameof(GuildRank.Leader);
            string body = last ? ConfirmDialog.Tint("You are its last member: the guild disbands, its treasury is lost.", Palette.Bad)
                : leader ? "The lead passes to your highest officer (or the longest member)." : "Your Guild Tallies stay with you.";
            _confirm.Show("Leave " + v.mine.name + "?", body, "LEAVE", Palette.Danger,
                () => Call("leave", new GuildLeaveRequest { requestId = NewRequestId() }, () => _page = 0));
        }

        private void ToggleGates()
        {
            GuildDto g = _root.Server.GuildView?.mine;
            if (g == null) return;
            Call("settings", new GuildSettingsRequest { requestId = NewRequestId(), open = !g.open, color = g.color });
        }

        private void Select(int row)
        {
            GuildViewDto v = _root.Server.GuildView;
            int index = _page * MemberRows + row;
            if (!HasGuild(v) || v.members == null || index >= v.members.Length) return;
            GuildMemberDto member = v.members[index];
            if (!ParseEnum<GuildRank>(_root.Server.Guild?.rank, out GuildRank mine) || member.me) return;
            ParseEnum<GuildRank>(member.rank, out GuildRank theirs);
            if (!Guilds.CanKick(mine, theirs)) return;
            _selected = member;
            _manageTitle.text = $"{member.name}  ·  {member.rank}";
            bool leader = mine == GuildRank.Leader;
            _promote.gameObject.SetActive(leader);
            _promoteLabel.text = theirs == GuildRank.Officer ? "MAKE MEMBER" : "MAKE OFFICER";
            _lead.gameObject.SetActive(leader);
            _kick.gameObject.SetActive(true);
            _manage.SetActive(true);
        }

        private static bool ParseEnum<T>(string text, out T value) where T : struct => System.Enum.TryParse(text, out value);

        private void SetOfficer()
        {
            if (_selected == null) return;
            string rank = _selected.rank == nameof(GuildRank.Officer) ? nameof(GuildRank.Member) : nameof(GuildRank.Officer);
            _manage.SetActive(false);
            Call("rank", new GuildMemberRequest { requestId = NewRequestId(), accountId = _selected.accountId, rank = rank });
        }

        private void AskLead()
        {
            GuildMemberDto m = _selected;
            if (m == null) return;
            _manage.SetActive(false);
            _confirm.Show("Hand over the lead?", $"{m.name} will lead the guild and you become an officer.", "HAND OVER", Palette.Alloy,
                () => Call("rank", new GuildMemberRequest { requestId = NewRequestId(), accountId = m.accountId, rank = nameof(GuildRank.Leader) }));
        }

        private void AskKick()
        {
            GuildMemberDto m = _selected;
            if (m == null) return;
            _manage.SetActive(false);
            _confirm.Show("Send " + m.name + " away?", "They leave the guild at once and keep their Guild Tallies.", "SEND AWAY", Palette.Danger,
                () => Call("kick", new GuildMemberRequest { requestId = NewRequestId(), accountId = m.accountId }));
        }

        private static string Ago(int minutes) =>
            minutes < 5 ? "<color=#8CF08C>hunting now</color>" : minutes < 60 ? $"{minutes}m ago" : minutes < 60 * 24 ? $"{minutes / 60}h ago" : $"{minutes / 1440}d ago";

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            // The note follows the link: it clears itself once the server answers (the screen can open before login).
            if (!_root.Server.Online && !_offlineShown) { _message.text = "Offline: guilds need the server."; _offlineShown = true; }
            else if (_root.Server.Online && _offlineShown) { _message.text = ""; _offlineShown = false; }
            if (_root.Server.Online && !_fetching && !_busy && Time.realtimeSinceStartup >= _nextFetch)
            {
                _fetching = true;
                StartCoroutine(_root.Server.FetchGuild(_search.text, error =>
                {
                    _fetching = false;
                    _nextFetch = Time.realtimeSinceStartup + RefreshSeconds;
                    if (error != null) Say(null, error);
                }));
            }

            GuildViewDto v = _root.Server.GuildView;
            bool home = HasGuild(v) && _root.Server.InGuild;
            _home.SetActive(home);
            _browse.SetActive(!home);
            if (home) ShowHome(v); else ShowBrowse(v);
            if (home) _invites.SetActive(false); else ShowInvites(v);
            // Screenshots: -guildpopup opens ASKING / INVITE (in a guild) once the guild has loaded.
            if (_popupForShot && home && v.mine != null)
            {
                _popupForShot = false;
                OpenRequests();
            }
        }

        private void ShowBrowse(GuildViewDto v)
        {
            GuildListItemDto[] list = v?.browse ?? new GuildListItemDto[0];
            for (int i = 0; i < BrowseRows; i++)
            {
                BrowseRow r = _rows[i];
                bool has = i < list.Length;
                r.Root.SetActive(has);
                if (!has) continue;
                GuildListItemDto g = list[i];
                r.Plate.color = ColorOf(g.color);
                r.Tag.text = g.tag;
                r.Label.text = $"{g.name}   ·   {Loc.T("Lv " + g.level)}   ·   {g.members}/{g.maxMembers}";
                bool full = g.members >= g.maxMembers;
                // Shut gates take a request instead of a join.
                r.JoinLabel.text = full ? "FULL" : g.open ? "JOIN" : g.requested ? "ASKED" : "ASK";
                r.Join.GetComponent<Image>().color = g.open ? Palette.Safe : Palette.Alloy;
                r.Join.interactable = !full && !g.requested && !_busy && _root.Server.Online;
            }
            _empty.text = v == null ? (_root.Server.Online ? "Gathering word of the guilds..." : "")
                : list.Length == 0 ? (string.IsNullOrEmpty(_search.text) ? "No guilds yet. Found the first one below." : "No guild by that name.") : "";
            for (int i = 0; i < _swatches.Length; i++) _swatches[i].localScale = Vector3.one * (i == _color ? 1.15f : 0.85f);
            _create.interactable = !_busy && _root.Server.Online;
        }

        private void ShowHome(GuildViewDto v)
        {
            GuildDto g = v.mine;
            ParseEnum<GuildRank>(_root.Server.Guild?.rank, out GuildRank rank);
            bool manager = Guilds.CanManage(rank);
            _plate.color = ColorOf(g.color);
            _plateTag.text = g.tag;
            _title.text = g.name;
            string forts = g.fortresses != null && g.fortresses.Length > 0 ? "  ·  flag on " + string.Join(", ", g.fortresses) : "";
            _info.text = $"Level {g.level}  ·  {g.members}/{g.maxMembers} members  ·  treasury {g.treasury:N0} sorn\nYou are {rank.ToString().ToLowerInvariant()}  ·  guild bonus +{g.sornBonusPercent}% hunting sorn{forts}";
            float xp = g.nextLevelXp > 0 ? Mathf.Clamp01(g.xp / (float)g.nextLevelXp) : 1f;
            _xpFill.anchorMax = new Vector2(xp, 1f);
            _xpText.text = g.nextLevelXp > 0 ? $"GUILD XP  {g.xp:N0} / {g.nextLevelXp:N0}" : $"GUILD XP  {g.xp:N0}  ·  TOP LEVEL";
            _event.text = g.lastEvent;

            long room = v.donationCap - v.donatedToday;
            _donateText.text = $"Given today {v.donatedToday:N0} / {v.donationCap:N0}\n1 Guild Tally per {Guilds.SornPerTally:N0} sorn";
            foreach (Button b in _donate) b.interactable = !_busy && room >= Guilds.SornPerXp && _root.Session.Inventory.Sorn >= Guilds.SornPerXp;

            SkillCard(GuildSkill.Plunder, g.plunder, $"PLUNDER {g.plunder}/{Guilds.MaxPlunder}\n+{g.plunder}% hunting sorn for all", _plunderText, _plunderRaise, g, manager);
            SkillCard(GuildSkill.Muster, g.muster, $"MUSTER {g.muster}/{Guilds.MaxMuster}\nroom for {Guilds.MaxMembers(g.muster)} members", _musterText, _musterRaise, g, manager);

            int tallies = _root.Server.Tallies;
            _shopTitle.text = $"GUILD SHOP  ·  {tallies} Guild Tallies";
            for (int i = 0; i < _shop.Length; i++) _shop[i].interactable = !_busy && tallies >= Guilds.Shop[i].Tallies;

            GuildMemberDto[] members = v.members ?? new GuildMemberDto[0];
            int pages = Mathf.Max(1, (members.Length + MemberRows - 1) / MemberRows);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            _membersTitle.text = $"MEMBERS  ·  page {_page + 1}/{pages}";
            for (int i = 0; i < MemberRows; i++)
            {
                int index = _page * MemberRows + i;
                MemberRow row = _members[i];
                bool has = index < members.Length;
                row.Button.gameObject.SetActive(has);
                if (!has) continue;
                GuildMemberDto m = members[index];
                string banner = ColorUtility.ToHtmlStringRGB(BannerLook.Color(BannerLook.Parse(m.banner)));
                string rankTint = m.rank == nameof(GuildRank.Leader) ? "#FFD66B" : m.rank == nameof(GuildRank.Officer) ? "#C8A8FF" : "#C2BAAD";
                row.Label.text = $"<color=#{banner}>■</color>  {(m.me ? "<b>" + m.name + "</b>" : m.name)}   <color={rankTint}>{m.rank.ToUpperInvariant()}</color>"
                                 + $"   Lv {m.level}   ·   {m.donated:N0} given   ·   {Ago(m.lastSeenMinutes)}";
                ParseEnum<GuildRank>(m.rank, out GuildRank theirs);
                row.Button.interactable = !m.me && Guilds.CanKick(rank, theirs);
            }

            int asking = v.requests?.Length ?? 0;
            _requestsButton.gameObject.SetActive(manager);
            _requestsLabel.text = asking > 0 ? $"ASKING ({asking})" : "INVITE";
            _requestsButton.GetComponent<Image>().color = asking > 0 ? Palette.ButtonForge : Palette.Alloy;
            if (_requests.activeSelf) ShowRequests(v);
            _gates.gameObject.SetActive(manager);
            _gatesLabel.text = g.open ? "GATES OPEN\n<size=14>tap to shut</size>" : "GATES SHUT\n<size=14>tap to open</size>";
            _gates.GetComponent<Image>().color = g.open ? Palette.Safe : Palette.ButtonIdle;
        }

        private void SkillCard(GuildSkill skill, int level, string text, Text label, Button raise, GuildDto g, bool manager)
        {
            label.text = text;
            Text caption = raise.GetComponentInChildren<Text>();
            if (level >= Guilds.SkillMax(skill))
            {
                caption.text = "AT ITS PEAK";
                raise.interactable = false;
                return;
            }
            int next = level + 1;
            caption.text = $"RAISE  ·  {Guilds.SkillCost(skill, next):N0}" + (g.level < Guilds.SkillGuildLevel(next) ? $"  ·  Lv {Guilds.SkillGuildLevel(next)}" : "");
            raise.interactable = manager && !_busy && Guilds.SkillProblem(skill, level, g.level, g.treasury) == null;
        }
    }
}
