using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// DAILY GIFTS (owner, 27 Sep 2026: "Daily login rewards"; Rules.DailyLogin): the seven days of the login calendar,
    /// the ones taken this round marked, today's lit, and TAKE TODAY'S GIFT; the next comes with the evening bell. The
    /// calendar is the account's (its heroes share it); the gift goes to the hero playing. GameRoot opens it once a
    /// session when a gift waits; MENU has DAILY GIFTS.
    /// </summary>
    public sealed class DailyPanel : MonoBehaviour
    {
        private static readonly string[] Icons = { "Sorn", "Turnstone", "Draught", "Sorn", "ScrollOfMercy", "Turnstone", "ShardRider" };

        private sealed class Card
        {
            public Image Back;
            public Text Day;
            public Text Gift;
            public Text State;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private readonly Card[] _cards = new Card[DailyLogin.Days];
        private Button _claim;
        private Text _claimLabel;
        private Text _status;
        private bool _busy;
        private float _fetchedAt;
        private Net.ServerLink.DailyDto _seen;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("DailyCanvas", 12).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Bounties");
            Ui.Title("Title", canvas, 0.05f, 0.9f, 0.95f, 0.95f, "DAILY GIFTS", 48, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Label("Blurb", canvas, 0.06f, 0.83f, 0.94f, 0.89f,
                "A gift for each day you come back: the seventh is a Korshard, then the calendar starts again. Your heroes share it.",
                22, TextAnchor.MiddleCenter, Palette.Parchment);

            for (int i = 0; i < _cards.Length; i++)
            {
                bool top = i < 4;
                float x0 = top ? 0.04f + i * 0.235f : 0.1575f + (i - 4) * 0.235f;
                float y0 = top ? 0.6f : 0.37f;
                var c = new Card();
                c.Back = Ui.Framed("Day" + i, canvas, x0, y0, x0 + 0.215f, y0 + 0.21f, new Color(0.07f, 0.07f, 0.13f, 0.95f));
                Transform t = c.Back.transform;
                c.Day = Ui.Title("Day", t, 0.05f, 0.8f, 0.95f, 0.96f, "DAY " + (i + 1), 22, TextAnchor.MiddleCenter, Palette.Parchment);
                RectTransform iconBox = Ui.Rect("IconBox", t, 0.22f, 0.36f, 0.78f, 0.78f);
                Ui.Icon("Icon", iconBox, 0f, 0f, 1f, 1f, Icons[i]);
                c.Gift = Ui.Label("Gift", t, 0.05f, 0.16f, 0.95f, 0.36f, "", 16, TextAnchor.MiddleCenter, Palette.Parchment);
                c.State = Ui.Title("State", t, 0.05f, 0.02f, 0.95f, 0.16f, "", 20, TextAnchor.MiddleCenter, Palette.Good);
                _cards[i] = c;
            }

            _status = Ui.Label("Status", canvas, 0.06f, 0.29f, 0.94f, 0.34f, "", 24, TextAnchor.MiddleCenter, Palette.Parchment);
            _status.supportRichText = true;
            _claim = Ui.Button("Claim", canvas, 0.15f, 0.19f, 0.85f, 0.27f, "TAKE TODAY'S GIFT", 30, Palette.ButtonForge, Claim, out _claimLabel);
            Ui.Button("Close", canvas, 0.25f, 0.08f, 0.75f, 0.15f, "BACK TO THE HUNT", 28, Palette.ButtonIdle, Close, out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _status.text = "";
            _canvas.SetActive(true);
        }

        public void Close() => _canvas.SetActive(false);

        private void Claim()
        {
            var daily = _root.Server.Daily;
            if (_busy || daily == null || !daily.claimable || !_root.Server.Online) return;
            _busy = true;
            string gift = daily.day >= 1 && daily.day <= daily.gifts.Length ? daily.gifts[daily.day - 1] : "";
            StartCoroutine(_root.Server.ClaimDaily(error =>
            {
                _busy = false;
                _status.text = error ?? ConfirmDialog.Tint("Taken: " + gift + ".", Palette.Good);
                if (error == null) GameAudio.Instance?.Play("LaneLevelUp", 0.8f, 1f, 0f);
            }));
        }

        private static string Wait(long seconds) =>
            seconds >= 3600 ? $"{seconds / 3600}h {seconds % 3600 / 60}m" : $"{Mathf.Max(1, (int)(seconds / 60))}m";

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            var daily = _root.Server.Daily;
            if (daily != _seen)
            {
                _seen = daily;
                _fetchedAt = Time.realtimeSinceStartup;
            }
            if (daily == null || daily.gifts == null)
            {
                _claim.interactable = false;
                if (_status.text.Length == 0) _status.text = _root.Server.Online ? "..." : "The daily gifts need the server.";
                return;
            }
            // The days taken this round: before today's when it waits, up to today's once it is taken.
            int taken = daily.claimable ? daily.day - 1 : daily.day;
            for (int i = 0; i < _cards.Length; i++)
            {
                Card c = _cards[i];
                int day = i + 1;
                bool today = daily.claimable && day == daily.day;
                c.Gift.text = i < daily.gifts.Length ? daily.gifts[i] : "";
                c.Back.color = today ? new Color(0.62f, 0.42f, 0.1f, 0.97f) : day <= taken ? new Color(0.05f, 0.08f, 0.06f, 0.95f) : new Color(0.07f, 0.07f, 0.13f, 0.95f);
                c.State.text = day <= taken ? "TAKEN" : today ? "TODAY" : "";
                c.State.color = today ? Palette.Parchment : Palette.Good;
                c.Day.color = today ? Palette.Parchment : Palette.Muted;
            }
            _claim.interactable = daily.claimable && !_busy;
            _claimLabel.text = daily.claimable ? "TAKE TODAY'S GIFT" : "TAKEN FOR TODAY";
            if (!daily.claimable && (_status.text.Length == 0 || _status.text.StartsWith("Next gift")))
            {
                long left = daily.secondsToNext - (long)(Time.realtimeSinceStartup - _fetchedAt);
                _status.text = "Next gift in " + Wait(System.Math.Max(0, left)) + ", with the evening bell.";
            }
        }
    }
}
