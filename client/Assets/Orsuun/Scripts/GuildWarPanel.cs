using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// GUILD WAR (Rules.GuildWars, server GameService.GuildWar): the guild's rating and record, the next war night with
    /// SIGN UP for the leader, and during a war the score, the three lanes (the front, both war flags, FIGHT HERE and, for
    /// the leader or an officer, PLANT FLAG) and the ladder. A duel closes the screen for its replay and brings it back.
    /// </summary>
    public sealed class GuildWarPanel : MonoBehaviour
    {
        private const float RefreshSeconds = 5f;

        private sealed class Lane
        {
            public Image Back;
            public Text Name;
            public RectTransform Fill;
            public Image FillImage;
            public Text Front;
            public Button Fight;
            public Button Flag;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private Text _record;
        private Text _status;
        private Text _statusSmall;
        private Button _signup;
        private Text _signupLabel;
        private Text _score;
        private readonly Lane[] _lanes = new Lane[3];
        private Text _event;
        private readonly Text[] _ladder = new Text[8];
        private float _nextFetch;
        private bool _fetching;
        private bool _busy;
        private string _message = "";

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("GuildWarCanvas", 11).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "War");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "GUILD WAR", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _record = Ui.Label("Record", canvas, 0.05f, 0.9f, 0.95f, 0.93f, "", 24, TextAnchor.MiddleCenter, Palette.Muted);

            // The night: when it is and SIGN UP, or tonight's foe and the score.
            Ui.Framed("StatusBack", canvas, 0.04f, 0.745f, 0.96f, 0.895f, new Color(0.06f, 0.06f, 0.12f, 0.93f));
            _status = Ui.Title("Status", canvas, 0.07f, 0.83f, 0.93f, 0.885f, "", 32, TextAnchor.MiddleCenter, Palette.Parchment);
            _status.supportRichText = true;
            _score = Ui.Title("Score", canvas, 0.07f, 0.8f, 0.93f, 0.835f, "", 30, TextAnchor.MiddleCenter, Palette.Sorn);
            _statusSmall = Ui.Label("StatusSmall", canvas, 0.07f, 0.752f, 0.93f, 0.8f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);
            _statusSmall.supportRichText = true;
            _signup = Ui.Button("Signup", canvas, 0.3f, 0.755f, 0.7f, 0.798f, "", 26, Palette.Danger, ToggleSignup, out _signupLabel);

            // The three lanes: the front as a bar (our side on the right), the flags, and the actions.
            for (int i = 0; i < _lanes.Length; i++)
            {
                int lane = i;
                float y1 = 0.735f - i * 0.1f;
                var l = new Lane();
                l.Back = Ui.Framed("Lane" + i, canvas, 0.04f, y1 - 0.094f, 0.96f, y1, new Color(0.07f, 0.07f, 0.13f, 0.95f));
                Transform t = l.Back.transform;
                l.Name = Ui.Title("Name", t, 0.04f, 0.58f, 0.6f, 0.92f, "", 26, TextAnchor.MiddleLeft, Palette.Parchment);
                l.Name.supportRichText = true;
                l.Fill = Ui.Bar("Front", t, 0.04f, 0.14f, 0.6f, 0.5f, Palette.Sorn, out l.FillImage);
                l.Front = Ui.Title("FrontText", t, 0.04f, 0.14f, 0.6f, 0.5f, "", 18, TextAnchor.MiddleCenter, Palette.Parchment);
                l.Fight = Ui.Button("Fight", t, 0.63f, 0.5f, 0.97f, 0.92f, "FIGHT HERE", 22, Palette.Danger, () => Fight(lane), out _);
                l.Flag = Ui.Button("Flag", t, 0.63f, 0.08f, 0.97f, 0.48f, "PLANT FLAG", 18, Palette.Alloy, () => PlantFlag(lane), out _);
                _lanes[i] = l;
            }

            _event = Ui.Label("Event", canvas, 0.05f, 0.4f, 0.95f, 0.44f, "", 22, TextAnchor.MiddleCenter, Palette.Parchment);
            _event.supportRichText = true;
            Ui.Section("LadderHead", canvas, 0.12f, 0.358f, 0.88f, 0.394f, "LADDER", 28);
            Ui.Framed("LadderBack", canvas, 0.04f, 0.085f, 0.96f, 0.352f, new Color(0.06f, 0.06f, 0.12f, 0.9f));
            for (int i = 0; i < _ladder.Length; i++)
            {
                float y1 = 0.342f - i * 0.031f;
                _ladder[i] = Ui.Label("Ladder" + i, canvas, 0.07f, y1 - 0.03f, 0.93f, y1, "", 22, TextAnchor.MiddleLeft, Palette.Parchment);
                _ladder[i].supportRichText = true;
            }
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE GUILD", 28, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _message = _root.Server.Online ? "" : "Guild war needs the server.";
            _nextFetch = 0f;
            _canvas.SetActive(true);
        }

        public void Close() => _canvas.SetActive(false);

        private static void Place(Text text, float y0, float y1)
        {
            text.rectTransform.anchorMin = new Vector2(0.07f, y0);
            text.rectTransform.anchorMax = new Vector2(0.93f, y1);
        }

        private static string Clock(int seconds)
        {
            if (seconds >= 86_400) return $"{seconds / 86_400} d {seconds % 86_400 / 3600} h";
            if (seconds >= 3600) return $"{seconds / 3600} h {seconds % 3600 / 60:00} m";
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        private void ToggleSignup()
        {
            Net.ServerLink.GuildWarDto war = _root.Server.GuildWar;
            if (_busy || war == null) return;
            _busy = true;
            StartCoroutine(_root.Server.GuildWarCall("signup", new Net.ServerLink.GuildWarSignupRequest { join = !war.signedUp }, (message, error) =>
            {
                _busy = false;
                _message = error ?? message ?? "";
            }));
        }

        private void PlantFlag(int lane)
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(_root.Server.GuildWarCall("flag", new Net.ServerLink.GuildWarFlagRequest { lane = lane }, (message, error) =>
            {
                _busy = false;
                _message = error ?? message ?? "";
            }));
        }

        private void Fight(int lane)
        {
            if (_busy || _root.Replaying || _root.PushBusy) return;
            _message = "";
            _root.FightDuel(lane);
        }

        /// <summary>Shown when a duel's call failed (the replay never started).</summary>
        public void Say(string text) => _message = text;

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            if (_root.Server.Online && !_fetching && Time.realtimeSinceStartup >= _nextFetch)
            {
                _fetching = true;
                StartCoroutine(_root.Server.FetchGuildWar(error =>
                {
                    _fetching = false;
                    _nextFetch = Time.realtimeSinceStartup + RefreshSeconds;
                    if (error != null) _message = error;
                }));
            }

            Net.ServerLink.GuildWarDto war = _root.Server.GuildWar;
            if (war == null || war.lanes == null)
            {
                _status.text = _root.Server.Online ? "Sounding the horns..." : "";
                _record.text = _score.text = _statusSmall.text = "";
                _signup.gameObject.SetActive(false);
                foreach (Lane l in _lanes) l.Back.gameObject.SetActive(false);
                _event.text = ConfirmDialog.Tint(_message, Palette.Warn);
                return;
            }

            float age = Time.realtimeSinceStartup - _root.Server.GuildWarReceivedAt;
            _record.text = $"Rating {war.rating:N0}  ·  {war.wins} won  ·  {war.losses} lost  ·  {war.draws} drawn";
            string mine = _root.Server.InGuild ? "[" + _root.Server.Guild.tag + "]" : "";
            if (war.atWar)
            {
                int left = Mathf.Max(0, war.secondsLeft - (int)age);
                int cooldown = Mathf.Max(0, war.cooldownSeconds - (int)age);
                _status.text = $"{mine}  against  {ConfirmDialog.Tint("[" + war.foe.tag + "]", GuildPanel.ColorOf(war.foe.color))} {war.foe.name}";
                _score.text = $"{war.myScore}  :  {war.theirScore}";
                _statusSmall.text = $"Kills {war.myKills} : {war.theirKills}  ·  the horns fall silent in {Clock(left)}  ·  "
                                    + (war.fightsLeft <= 0 ? "your duels are spent" : cooldown > 0 ? $"next duel in {cooldown} s" : $"{war.fightsLeft} duels left");
                _signup.gameObject.SetActive(false);
                Place(_status, 0.845f, 0.89f);
                Place(_score, 0.808f, 0.846f);
                Place(_statusSmall, 0.752f, 0.808f);
                for (int i = 0; i < _lanes.Length; i++)
                {
                    Lane l = _lanes[i];
                    Net.ServerLink.GuildWarLaneDto d = war.lanes[i];
                    l.Back.gameObject.SetActive(true);
                    string flags = (d.myFlag ? "  " + ConfirmDialog.Tint("our flag", Palette.Good) : "") + (d.theirFlag ? "  " + ConfirmDialog.Tint("their flag", Palette.Bad) : "");
                    l.Name.text = d.name.ToUpperInvariant() + "<size=18>" + flags + "</size>";
                    // The front: the middle is even; the bar fills toward our side as we push.
                    l.Fill.anchorMax = new Vector2(Mathf.Clamp01((d.front + 5) / 10f), 1f);
                    l.FillImage.color = d.front > 0 ? Palette.Good : d.front < 0 ? Palette.Bad : Palette.Sorn;
                    l.Front.text = d.broken ? (d.front > 0 ? "BROKEN BY US" : "BROKEN AGAINST US") : d.front == 0 ? "EVEN" : d.front > 0 ? $"+{d.front} OUR WAY" : $"{d.front} THEIR WAY";
                    l.Fight.interactable = !_busy && war.fightsLeft > 0 && cooldown == 0 && !_root.Replaying && !_root.PushBusy;
                    l.Flag.gameObject.SetActive(war.canFlag);
                    l.Flag.interactable = !_busy && !d.myFlag;
                }
                _event.text = _message.Length > 0 ? ConfirmDialog.Tint(_message, Palette.Warn) : war.lastEvent;
            }
            else
            {
                int toNext = Mathf.Max(0, war.secondsToNext - (int)age);
                _status.text = $"Next war night: {war.nextNight}";
                _score.text = $"in {Clock(toNext)}";
                _statusSmall.text = (war.signedUp ? ConfirmDialog.Tint("Your guild is signed up.", Palette.Good) : "Your guild is not signed up.")
                                    + $"  ·  {war.signedGuilds} guild{(war.signedGuilds == 1 ? "" : "s")} so far"
                                    + (war.canSignUp ? "" : "\nThe leader signs the guild up.");
                _signup.gameObject.SetActive(war.canSignUp);
                _signupLabel.text = war.signedUp ? "WITHDRAW" : "SIGN UP";
                _signup.GetComponent<Image>().color = war.signedUp ? Palette.ButtonIdle : Palette.Danger;
                _signup.interactable = !_busy;
                // With SIGN UP at the bottom of the card the lines move up above it.
                Place(_status, 0.855f, 0.89f);
                Place(_score, 0.828f, 0.858f);
                Place(_statusSmall, war.canSignUp ? 0.797f : 0.752f, war.canSignUp ? 0.83f : 0.828f);
                foreach (Lane l in _lanes) l.Back.gameObject.SetActive(false);
                string last = string.IsNullOrEmpty(war.lastResult) ? "Wars are fought on Wednesday and Saturday nights at 21:00, for an hour." : "Last war: " + war.lastResult;
                _event.text = _message.Length > 0 ? ConfirmDialog.Tint(_message, Palette.Warn) : last;
            }

            for (int i = 0; i < _ladder.Length; i++)
            {
                bool has = war.ladder != null && i < war.ladder.Length;
                _ladder[i].gameObject.SetActive(has);
                if (!has) continue;
                Net.ServerLink.GuildWarLadderDto r = war.ladder[i];
                string line = $"{i + 1}.  {ConfirmDialog.Tint("[" + r.tag + "]", GuildPanel.ColorOf(r.color))} {r.name}   ·   {r.rating:N0}   ·   {r.wins}-{r.losses}-{r.draws}";
                _ladder[i].text = r.mine ? ConfirmDialog.Tint(line, Palette.Sorn) : line;
            }
        }
    }
}
