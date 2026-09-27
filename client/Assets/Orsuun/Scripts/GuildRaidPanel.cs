using System.Globalization;
using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;
using static Orsuun.Client.Net.ServerLink;

namespace Orsuun.Client
{
    /// <summary>
    /// GUILD RAID (owner, 27 Sep 2026: "Guild raid"; Rules.GuildRaids): the guild's boss of the week, its one pool of HP
    /// the members wear down, the hero's fights left today and the top ten. FIGHT closes the screen and the lane replays
    /// the server's fight (GameRoot.FightRaid). Opened from the guild screen.
    /// </summary>
    public sealed class GuildRaidPanel : MonoBehaviour
    {
        private GameRoot _root;
        private GameObject _canvas;
        private Text _boss, _where, _hp, _time, _mine, _top, _message;
        private RectTransform _fill;
        private Button _fight;
        private bool _busy;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("GuildRaidCanvas", 17).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);
            Ui.Backdrop(canvas, "War");
            Ui.Title("Title", canvas, 0.05f, 0.925f, 0.95f, 0.975f, "GUILD RAID", 46, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Framed("Card", canvas, 0.04f, 0.6f, 0.96f, 0.9f, new Color(0.07f, 0.06f, 0.05f, 0.92f)).raycastTarget = false;
            _boss = Ui.Title("Boss", canvas, 0.08f, 0.83f, 0.92f, 0.885f, "", 36, TextAnchor.MiddleCenter, Palette.Parchment);
            _where = Ui.Label("Where", canvas, 0.08f, 0.78f, 0.92f, 0.83f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);
            _fill = Ui.Bar("Pool", canvas, 0.08f, 0.72f, 0.92f, 0.77f, new Color(0.78f, 0.2f, 0.16f), out _);
            _hp = Ui.Label("Hp", canvas, 0.08f, 0.68f, 0.92f, 0.715f, "", 22, TextAnchor.MiddleCenter, Palette.Parchment);
            _time = Ui.Label("Time", canvas, 0.08f, 0.645f, 0.92f, 0.68f, "", 20, TextAnchor.MiddleCenter, Palette.Muted);
            _mine = Ui.Label("Mine", canvas, 0.08f, 0.605f, 0.92f, 0.645f, "", 22, TextAnchor.MiddleCenter, Palette.Sorn);
            Ui.Framed("Board", canvas, 0.04f, 0.26f, 0.96f, 0.58f, new Color(0.07f, 0.06f, 0.05f, 0.92f)).raycastTarget = false;
            Ui.Label("BoardTitle", canvas, 0.08f, 0.54f, 0.92f, 0.575f, "THE GUILD'S BLADES THIS WEEK", 22, TextAnchor.MiddleCenter, Palette.Sorn);
            _top = Ui.Label("Top", canvas, 0.08f, 0.28f, 0.92f, 0.535f, "", 22, TextAnchor.UpperLeft, Palette.Parchment);
            Ui.Raw(_top);   // heroes' and guilds' names
            Ui.Label("Rules", canvas, 0.06f, 0.2f, 0.94f, 0.255f,
                $"{GuildRaids.FightsPerDay} fights a day each. When the boss falls, everyone who fought is paid by his share: Guild Tallies at once, sorn by letter.",
                19, TextAnchor.MiddleCenter, Palette.Muted);
            _fight = Ui.Button("Fight", canvas, 0.2f, 0.11f, 0.8f, 0.18f, "FIGHT", 34, Palette.Danger, Fight, out _);
            _message = Ui.Label("Message", canvas, 0.05f, 0.08f, 0.95f, 0.105f, "", 22, TextAnchor.MiddleCenter, Palette.Parchment);
            Ui.Button("Back", canvas, 0.04f, 0.015f, 0.47f, 0.075f, "THE GUILD", 26, Palette.ButtonIdle, () =>
            {
                Close();
                _root.Guild.Open();
            }, out _);
            Ui.Button("Close", canvas, 0.53f, 0.015f, 0.96f, 0.075f, "BACK TO THE HUNT", 22, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _canvas.SetActive(true);
            _message.text = _root.Server.Online ? "" : "Offline: the raid needs the server.";
            if (_root.Server.Online && !_busy)
            {
                _busy = true;
                StartCoroutine(_root.Server.FetchRaid(error =>
                {
                    _busy = false;
                    if (error != null) _message.text = error;
                }));
            }
        }

        public void Close() => _canvas.SetActive(false);

        private void Fight()
        {
            GuildRaidDto raid = _root.Server.Raid;
            if (_busy || raid == null || raid.slain || raid.fightsLeft <= 0 || _root.Replaying || _root.PushBusy) return;
            Close();
            _root.FightRaid();
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            GuildRaidDto raid = _root.Server.Raid;
            if (raid == null) return;
            _boss.text = raid.boss;
            _where.text = raid.mapName + "  ·  " + Trick(raid.mechanic);
            float left = raid.hpMax > 0 ? (float)raid.hpLeft / raid.hpMax : 0f;
            _fill.anchorMax = new Vector2(Mathf.Clamp01(left), 1f);
            _hp.text = raid.slain ? $"Fallen to {raid.slainBy}'s blow" : $"{N(raid.hpLeft)} / {N(raid.hpMax)} HP";
            long s = raid.secondsLeft - (long)(Time.realtimeSinceStartup - _root.Server.RaidAt);
            _time.text = raid.slain ? "A new boss comes on Monday at 20:00." : $"Ends in {Span(s)}  ·  Monday 20:00";
            _mine.text = $"Your damage {N(raid.myDamage)}  ·  fights left today {raid.fightsLeft}";
            var sb = new System.Text.StringBuilder();
            int rank = 1;
            foreach (RaidHitDto h in raid.top ?? new RaidHitDto[0])
                sb.Append(rank++).Append(".  ").Append(h.name).Append("   <color=#FFD66B>").Append(N(h.damage)).Append("</color>\n");
            _top.text = sb.Length > 0 ? sb.ToString() : Loc.T("Nobody has fought it yet this week.");
            _fight.interactable = !_busy && !raid.slain && raid.fightsLeft > 0;
            if (_message.text.Length == 0 && !string.IsNullOrEmpty(raid.message)) _message.text = raid.message;
        }

        private static string Trick(string mechanic) =>
            mechanic == "PackCaller" ? "calls a pack every 15 seconds" : mechanic == "MirrorImages" ? "casts mirror images at 70% and 40%" : "";

        private static string N(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

        private static string Span(long seconds)
        {
            seconds = System.Math.Max(0, seconds);
            return seconds >= 86400 ? $"{seconds / 86400}d {seconds % 86400 / 3600}h" : $"{seconds / 3600}h {seconds % 3600 / 60:00}m";
        }
    }
}
