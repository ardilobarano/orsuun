using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// The hunting party (owner, 29 Sep 2026: "Hunting parties"; Rules.Parties): up to four friends or guildmates. Partymates
    /// hunting the same map walk and fight beside the hero (FieldFolk) and each adds a little XP and sorn to the hunt. This
    /// card shows an invite waiting (JOIN / DECLINE), or the party: its members, where each hunts, the bonus now, LEAVE, and
    /// the leader's SEND AWAY on a member. A chip under the minimap opens it while the hero is in a party; heroes are
    /// invited from the hero card (HeroActions) and FRIENDS.
    /// </summary>
    public sealed class PartyPanel : MonoBehaviour
    {
        private const float PollSeconds = 20f;
        private const int Rows = Parties.MaxMembers;

        private GameRoot _root;
        private GameObject _canvas;
        private Text _body, _message;
        private GameObject _invite, _party;
        private readonly Text[] _rows = new Text[Rows];
        private readonly Button[] _rowButtons = new Button[Rows];
        private Text _bonus;
        private Button _chip;
        private Text _chipLabel;
        private ConfirmDialog _confirm;
        private bool _busy, _fetching;
        private float _polledAt = -100f;
        private PartyDto _shown;
        /// <summary>Screenshots: -partyshow opens the card once the party is known.</summary>
        private bool _showOnce = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-partyshow") >= 0;

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
            _bonus = Ui.Label("Bonus", _party.transform, 0.05f, 0.34f, 0.95f, 0.44f, "", 22, TextAnchor.MiddleCenter, Palette.Good);
            Ui.Button("Leave", _party.transform, 0.06f, 0.2f, 0.94f, 0.32f, "LEAVE THE PARTY", 24, Palette.Danger, AskLeave, out _);

            _message = Ui.Label("Message", box, 0.05f, 0.15f, 0.95f, 0.21f, "", 20, TextAnchor.MiddleCenter, Palette.Muted);
            _message.supportRichText = true;
            Ui.Button("Close", box, 0.3f, 0.02f, 0.7f, 0.13f, "CLOSE", 24, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);

            // The chip under the minimap: the party's size and bonus, or an invite waiting.
            _chip = Ui.Button("PartyChip", root.Hud.Canvas, 0.02f, 0.628f, 0.22f, 0.66f, "", 18, Palette.Safe, Open, out _chipLabel);
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
            _polledAt = -100f;
            _canvas.SetActive(true);
        }

        public void Close() => _canvas.SetActive(false);

        /// <summary>Asks a hero to join (from the hero card or FRIENDS); the answer goes to <paramref name="report"/>.</summary>
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

        private void Update()
        {
            if (_root == null) return;
            ServerLinkView();
            bool inviting = _root.Server.Online && !string.IsNullOrEmpty(_root.Server.PartyInviteName);
            bool inParty = _root.Server.InParty;
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
            bool chip = (inParty || inviting) && !_root.Server.WaitingForHero && !_root.Server.AtRiver && !_root.Town.IsOpen;
            if (_chip.gameObject.activeSelf != chip) _chip.gameObject.SetActive(chip);
            if (chip)
                _chipLabel.text = inviting && !inParty ? "PARTY INVITE"
                    : party == null ? "PARTY" : $"PARTY {party.members?.Length ?? 1}  +{party.bonusPercent}%";
            if (!_canvas.activeSelf) return;

            _invite.SetActive(inviting && !inParty);
            _party.SetActive(inParty);
            if (inviting && !inParty)
            {
                _body.text = $"{_root.Server.PartyInviteName} asks you to hunt together.\n" +
                             ConfirmDialog.Tint($"Partymates hunting the same map walk beside you, and each adds {Parties.BonusBpPerMate / 100}% XP and sorn.", Palette.Muted);
                return;
            }
            if (!inParty)
            {
                _body.text = $"Hunt with up to {Parties.MaxMembers - 1} friends or guildmates: tap a hero on the map, or a friend in FRIENDS, and INVITE TO PARTY.\n"
                             + ConfirmDialog.Tint($"Each partymate hunting the same map adds {Parties.BonusBpPerMate / 100}% XP and sorn.", Palette.Muted);
                return;
            }
            _body.text = "";
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
