using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// The hunting party (owner, 29 Sep 2026: "Hunting parties"; Rules.Parties): up to four heroes. Partymates hunting the
    /// same map walk and fight beside the hero (FieldFolk) and each adds a little XP and sorn to the hunt. This card shows
    /// an invite waiting (JOIN / DECLINE); alone, the way to the party board; in a party, its members, where each hunts, the
    /// bonus now, PARTY CHAT, BOARD and LEAVE, and the leader's SEND AWAY on a member. The party board (29 Sep 2026: "Party
    /// finder") lists the heroes of this map who look for a party: tap one to ask them, or LIST ME to be found. A chip under
    /// the minimap opens the card; heroes are also invited from the hero card (HeroActions) and FRIENDS.
    /// </summary>
    public sealed class PartyPanel : MonoBehaviour
    {
        private const float PollSeconds = 20f, BoardPollSeconds = 10f;
        private const int Rows = Parties.MaxMembers, BoardRows = 6;

        private GameRoot _root;
        private GameObject _canvas;
        private Text _body, _message;
        private GameObject _invite, _alone, _party, _board;
        private readonly Text[] _rows = new Text[Rows];
        private readonly Button[] _rowButtons = new Button[Rows];
        private readonly Text[] _boardRows = new Text[BoardRows];
        private readonly Button[] _boardButtons = new Button[BoardRows];
        private Text _bonus, _chatLabel, _lookLabel, _boardLookLabel;
        private Button _chip;
        private Text _chipLabel;
        private ConfirmDialog _confirm;
        private bool _busy, _fetching, _boardFetching, _showBoard;
        private float _polledAt = -100f, _boardAt = -100f;
        private PartyDto _shown;
        private PartyBoardDto _boardShown, _boardDto;
        /// <summary>Screenshots: -partyshow opens the card once the party is known; -partyboard opens the board.</summary>
        private bool _showOnce = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-partyshow") >= 0;
        private bool _boardOnce = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-partyboard") >= 0;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("PartyCanvas", 33).gameObject;
            Transform canvas = _canvas.transform;
            Image dim = Ui.Panel("Dim", canvas, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.6f));
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            Transform box = Ui.Framed("Box", canvas, 0.07f, 0.3f, 0.93f, 0.7f, Palette.PanelDark).transform;
            Ui.Title("Title", box, 0.05f, 0.87f, 0.95f, 0.98f, "HUNTING PARTY", 34, TextAnchor.MiddleCenter, Palette.Sorn);
            _body = Ui.Label("Body", box, 0.06f, 0.56f, 0.94f, 0.86f, "", 23, TextAnchor.MiddleCenter, Palette.Parchment);
            _body.supportRichText = true;

            _invite = Ui.Rect("Invite", box, 0f, 0f, 1f, 1f).gameObject;
            Ui.Button("Join", _invite.transform, 0.06f, 0.3f, 0.48f, 0.46f, "JOIN", 26, Palette.Safe, () => Answer(true), out _);
            Ui.Button("Decline", _invite.transform, 0.52f, 0.3f, 0.94f, 0.46f, "DECLINE", 26, Palette.Danger, () => Answer(false), out _);

            // Alone: the party board, and a listing on it.
            _alone = Ui.Rect("Alone", box, 0f, 0f, 1f, 1f).gameObject;
            Ui.Button("Board", _alone.transform, 0.06f, 0.3f, 0.48f, 0.46f, "PARTY BOARD", 24, Palette.Alloy, OpenBoard, out _);
            Ui.Button("Look", _alone.transform, 0.52f, 0.3f, 0.94f, 0.46f, "LIST ME", 24, Palette.Safe, ToggleLook, out _lookLabel);

            _party = Ui.Rect("Party", box, 0f, 0f, 1f, 1f).gameObject;
            for (int i = 0; i < Rows; i++)
            {
                int index = i;
                float y1 = 0.85f - i * 0.1f;
                _rowButtons[i] = Ui.Button("Member" + i, _party.transform, 0.05f, y1 - 0.09f, 0.95f, y1, "", 20, Palette.PanelDark, () => Pick(index), out _rows[i]);
                _rows[i].alignment = TextAnchor.MiddleLeft;
                _rows[i].supportRichText = true;
                _rows[i] = Ui.Raw(_rows[i]);
            }
            _bonus = Ui.Label("Bonus", _party.transform, 0.05f, 0.36f, 0.95f, 0.44f, "", 22, TextAnchor.MiddleCenter, Palette.Good);
            // The party's own chat (CHAT's PARTY tab), with the lines not read yet; the board to find more; LEAVE.
            Ui.Button("Chat", _party.transform, 0.05f, 0.22f, 0.35f, 0.34f, "PARTY CHAT", 20, Palette.Safe, () => { Close(); _root.Chat.Open(party: true); }, out _chatLabel);
            _chatLabel = Ui.Raw(_chatLabel);
            Ui.Button("Board", _party.transform, 0.37f, 0.22f, 0.63f, 0.34f, "BOARD", 20, Palette.Alloy, OpenBoard, out _);
            Ui.Button("Leave", _party.transform, 0.65f, 0.22f, 0.95f, 0.34f, "LEAVE", 20, Palette.Danger, AskLeave, out _);

            // The board: this map's heroes looking for a party (tap one to ask), LIST ME, BACK.
            _board = Ui.Rect("Board", box, 0f, 0f, 1f, 1f).gameObject;
            for (int i = 0; i < BoardRows; i++)
            {
                int index = i;
                float y1 = 0.86f - i * 0.085f;
                _boardButtons[i] = Ui.Button("Listed" + i, _board.transform, 0.05f, y1 - 0.077f, 0.95f, y1, "", 19, Palette.PanelDark, () => AskListed(index), out _boardRows[i]);
                _boardRows[i].alignment = TextAnchor.MiddleLeft;
                _boardRows[i].supportRichText = true;
                _boardRows[i] = Ui.Raw(_boardRows[i]);
            }
            Ui.Button("BoardLook", _board.transform, 0.06f, 0.22f, 0.48f, 0.33f, "LIST ME", 22, Palette.Safe, ToggleLook, out _boardLookLabel);
            Ui.Button("Back", _board.transform, 0.52f, 0.22f, 0.94f, 0.33f, "BACK", 22, Palette.ButtonIdle, () => { _showBoard = false; _message.text = ""; }, out _);

            _message = Ui.Label("Message", box, 0.05f, 0.15f, 0.95f, 0.21f, "", 20, TextAnchor.MiddleCenter, Palette.Muted);
            _message.supportRichText = true;
            Ui.Button("Close", box, 0.3f, 0.02f, 0.7f, 0.13f, "CLOSE", 24, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);

            // The chip under the minimap: the party's size and bonus, an invite waiting, a listing, or the way to the board.
            _chip = Ui.Button("PartyChip", root.Hud.Canvas, 0.02f, 0.628f, 0.22f, 0.66f, "", 18, Palette.Safe, Open, out _chipLabel);
            _chipLabel = Ui.Raw(_chipLabel);
            _chipLabel.supportRichText = true;
            _chip.gameObject.SetActive(false);
            _confirm = new GameObject("PartyConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
        }

        public void Open()
        {
            if (!_root.Server.Online) { _root.Hud.Log("Parties need the server."); return; }
            _message.text = "";
            _shown = null;
            _boardShown = null;
            _showBoard = false;
            _polledAt = -100f;
            _canvas.SetActive(true);
        }

        public void Close() => _canvas.SetActive(false);

        /// <summary>Asks a hero to join (from the hero card, FRIENDS or the board); the answer goes to <paramref name="report"/>.</summary>
        public void Invite(string heroId, System.Action<string> report)
        {
            if (!_root.Server.Online || _busy) return;
            _busy = true;
            StartCoroutine(_root.Server.PartyInvite(heroId, (party, error) =>
            {
                _busy = false;
                report(error ?? party?.message);
            }));
        }

        private void Say(string message, string error) => _message.text = error != null ? ConfirmDialog.Tint(error, Palette.Bad) : message ?? "";

        private void Answer(bool accept)
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(_root.Server.PartyAnswer(accept, (party, error) =>
            {
                _busy = false;
                Say(party?.message, error);
                if (error == null && accept) GameAudio.Instance?.Play("LaneLoot", 0.9f, 0.1f, 0f);
                if (error == null && !accept) Close();
            }));
        }

        private void AskLeave()
        {
            _confirm.Show("Leave the party?", "You hunt alone again, without the party's bonus.", "LEAVE", Palette.Danger, () =>
            {
                if (_busy) return;
                _busy = true;
                StartCoroutine(_root.Server.PartyLeave((party, error) =>
                {
                    _busy = false;
                    Say(party?.message, error);
                    if (error == null) Close();
                }));
            });
        }

        /// <summary>A member tapped: the leader may send them away; anyone else sees the hero card.</summary>
        private void Pick(int index)
        {
            PartyMemberDto[] members = _root.Server.Party?.members;
            if (members == null || index >= members.Length) return;
            PartyMemberDto m = members[index];
            if (m.id == _root.Server.AccountId) return;
            bool leading = _root.Server.PartyLeader == _root.Server.AccountId;
            if (!leading) { _root.HeroCard.Show(m.id, m.name, message => Say(message, null)); return; }
            _confirm.Show("Send " + m.name + " away?", "They leave the party at once.", "SEND AWAY", Palette.Danger, () =>
            {
                if (_busy) return;
                _busy = true;
                StartCoroutine(_root.Server.PartyKick(m.id, (party, error) =>
                {
                    _busy = false;
                    Say(party?.message, error);
                }));
            });
        }

        private void OpenBoard()
        {
            _showBoard = true;
            _boardShown = null;
            _boardAt = -100f;
            _message.text = "";
        }

        /// <summary>A listed hero tapped: asked to join the hero's party (one is made if the hero has none).</summary>
        private void AskListed(int index)
        {
            PartyBoardEntryDto[] heroes = _boardDto?.heroes;
            if (heroes == null || index >= heroes.Length) return;
            PartyBoardEntryDto h = heroes[index];
            Invite(h.id, message => Say(message, null));
        }

        private void ToggleLook()
        {
            if (_busy) return;
            _busy = true;
            bool look = !_root.Server.PartyLooking;
            StartCoroutine(_root.Server.PartyLook(look, (board, error) =>
            {
                _busy = false;
                if (board != null) _boardDto = board;
                _boardShown = null;
                Say(board?.message, error);
            }));
        }

        private void Update()
        {
            if (_root == null) return;
            ServerLinkView();
            bool online = _root.Server.Online;
            bool inviting = online && !string.IsNullOrEmpty(_root.Server.PartyInviteName);
            bool inParty = _root.Server.InParty;
            bool looking = _root.Server.PartyLooking;
            // In a party (or asked to join one) the party is fetched now and then: the chip's bonus, the card's rows.
            if ((inParty || _canvas.activeSelf) && !_fetching && Time.realtimeSinceStartup - _polledAt > (_canvas.activeSelf ? 5f : PollSeconds))
            {
                _fetching = true;
                _polledAt = Time.realtimeSinceStartup;
                StartCoroutine(_root.Server.FetchParty((_, error) =>
                {
                    _fetching = false;
                    if (error != null && _canvas.activeSelf) Say(null, error);
                }));
            }
            PartyDto party = inParty ? _root.Server.Party : null;
            if (_showOnce && party != null) { _showOnce = false; Open(); }
            if (_boardOnce && online && !_root.Server.WaitingForHero) { _boardOnce = false; Open(); OpenBoard(); }
            // The chip stays on the lane while online, so the board can be found without a party.
            bool chip = online && !_root.Server.WaitingForHero && !_root.Server.AtRiver && !_root.Town.IsOpen;
            if (_chip.gameObject.activeSelf != chip) _chip.gameObject.SetActive(chip);
            if (chip)
                _chipLabel.text = Loc.T(inviting && !inParty ? "PARTY INVITE" : party != null ? $"PARTY {party.members?.Length ?? 1}  +{party.bonusPercent}%"
                                      : inParty ? "PARTY" : looking ? "LOOKING FOR A PARTY" : "PARTY")
                                  + (inParty && _root.Chat.PartyUnread > 0 ? "  " + ConfirmDialog.Tint("(" + _root.Chat.PartyUnread + ")", Palette.Sorn) : "");
            if (!_canvas.activeSelf) return;

            bool board = _showBoard && !(inviting && !inParty);
            _invite.SetActive(inviting && !inParty && !board);
            _alone.SetActive(!inviting && !inParty && !board);
            _party.SetActive(inParty && !board);
            _board.SetActive(board);
            string lookText = looking ? "TAKE ME OFF" : "LIST ME";
            _lookLabel.text = Loc.T(lookText);
            _boardLookLabel.text = Loc.T(lookText);
            _boardLookLabel.transform.parent.gameObject.SetActive(!inParty);
            if (board)
            {
                _body.text = "";
                UpdateBoard();
                return;
            }
            if (inviting && !inParty)
            {
                _body.text = $"{_root.Server.PartyInviteName} asks you to hunt together.\n" +
                             ConfirmDialog.Tint($"Partymates hunting the same map walk beside you, and each adds {Parties.BonusBpPerMate / 100}% XP and sorn.", Palette.Muted);
                return;
            }
            if (!inParty)
            {
                _body.text = (looking ? $"You are on this map's party board: heroes hunting here can ask you to join.\n"
                                      : $"Hunt with up to {Parties.MaxMembers - 1} others: ask a friend or guildmate, or find heroes on this map's PARTY BOARD.\n")
                             + ConfirmDialog.Tint($"Each partymate hunting the same map adds {Parties.BonusBpPerMate / 100}% XP and sorn.", Palette.Muted);
                return;
            }
            _body.text = "";
            int unread = _root.Chat.PartyUnread;
            _chatLabel.text = Loc.T("PARTY CHAT") + (unread > 0 ? "  " + ConfirmDialog.Tint("(" + unread + ")", Palette.Sorn) : "");
            if (party == null || party == _shown) return;
            _shown = party;
            PartyMemberDto[] members = party.members ?? new PartyMemberDto[0];
            for (int i = 0; i < Rows; i++)
            {
                bool has = i < members.Length;
                _rowButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                PartyMemberDto m = members[i];
                string state = m.id == _root.Server.AccountId ? ConfirmDialog.Tint(Loc.T("you"), Palette.Sorn)
                    : m.together ? ConfirmDialog.Tint(Loc.T("hunting with you"), Palette.Good)
                    : m.online ? ConfirmDialog.Tint(Loc.T("at " + m.hunting), Palette.Muted)
                    : ConfirmDialog.Tint(Loc.T("away"), Palette.Muted);
                string crown = m.leader ? ConfirmDialog.Tint("★ ", Palette.Sorn) : "";
                _rows[i].text = $"{crown}<b>{m.name}</b>   <color=#C2BAAD>{Loc.T("Lv " + m.level)} {m.@class}</color>   {state}";
            }
            _bonus.text = party.bonusPercent > 0 ? $"Hunting together: +{party.bonusPercent}% XP and sorn" : "No partymate hunts your map now: no bonus.";
        }

        /// <summary>The board's rows, fetched every few seconds while it shows.</summary>
        private void UpdateBoard()
        {
            if (!_boardFetching && Time.realtimeSinceStartup - _boardAt > BoardPollSeconds)
            {
                _boardFetching = true;
                _boardAt = Time.realtimeSinceStartup;
                StartCoroutine(_root.Server.FetchPartyBoard((board, error) =>
                {
                    _boardFetching = false;
                    if (board != null) _boardDto = board;
                    else Say(null, error);
                }));
            }
            if (_boardDto == null || _boardDto == _boardShown) return;
            _boardShown = _boardDto;
            PartyBoardEntryDto[] heroes = _boardDto.heroes ?? new PartyBoardEntryDto[0];
            for (int i = 0; i < BoardRows; i++)
            {
                bool has = i < heroes.Length || (i == 0 && heroes.Length == 0);
                _boardButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                if (heroes.Length == 0)
                {
                    _boardRows[0].text = ConfirmDialog.Tint(Loc.T($"Nobody at {_boardDto.place} looks for a party now. LIST ME to be found."), Palette.Muted);
                    continue;
                }
                PartyBoardEntryDto h = heroes[i];
                string title = string.IsNullOrEmpty(h.title) ? "" : ConfirmDialog.Tint("‹" + Loc.T(h.title) + "› ", Palette.Sorn);
                _boardRows[i].text = $"{title}<b>{h.name}</b>   <color=#C2BAAD>{Loc.T("Lv " + h.level)} {h.@class}</color>   "
                                     + ConfirmDialog.Tint(Loc.T("ASK TO JOIN"), Palette.Good);
            }
        }

        /// <summary>The invite call on the HUD (a new invite: once).</summary>
        private string _calledFor = "";

        private void ServerLinkView()
        {
            string name = _root.Server.Online ? _root.Server.PartyInviteName : "";
            if (string.IsNullOrEmpty(name) || name == _calledFor || _root.Server.InParty) { if (string.IsNullOrEmpty(name)) _calledFor = ""; return; }
            _calledFor = name;
            _root.Hud.Log($"{name} asks you to hunt together: tap PARTY INVITE.");
            GameAudio.Instance?.Play("LaneLoot", 0.6f, 0.1f, 0f);
        }
    }
}
