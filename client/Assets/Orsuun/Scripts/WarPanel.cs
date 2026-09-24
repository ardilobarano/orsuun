using Orsuun.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// WAR OF BANNERS: this season's points per Banner, last season's winner and the hunting bonus it gives, and the
    /// three fortresses (holder, phase, wall, siege damage per Banner) with ATTACK, or DEFEND for the holding Banner.
    /// Refreshes from the server while open.
    /// </summary>
    public sealed class WarPanel : MonoBehaviour
    {
        private const float RefreshSeconds = 10f;

        private sealed class Standing
        {
            public RawImage Flag;
            public Text Label;
            public RectTransform Fill;
        }

        private sealed class Fort
        {
            public RawImage Flag;
            public Text Title;
            public Text Info;
            public RectTransform Wall;
            public Text WallText;
            public Text Siege;
            public Button Button;
            public Text ButtonLabel;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private Text _season;
        private Text _mine;
        private RawImage _myFlag;
        private Text _message;
        private readonly Standing[] _standings = new Standing[3];
        private readonly Fort[] _forts = new Fort[3];
        private float _nextFetch;
        private bool _fetching;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("WarCanvas", 9).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "War");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "WAR OF BANNERS", 44, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            _season = Ui.Label("Season", canvas, 0.05f, 0.9f, 0.95f, 0.935f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);

            for (int i = 0; i < 3; i++)
            {
                float y1 = 0.895f - i * 0.058f;
                float y0 = y1 - 0.052f;
                Ui.Framed("StandBack" + i, canvas, 0.04f, y0, 0.96f, y1, Palette.PanelDark);
                var s = new Standing();
                s.Flag = BannerLook.FlagImage("StandFlag" + i, canvas, 0.045f, y0 + 0.002f, 0.1f, y1 - 0.002f);
                Ui.Panel("StandBar" + i, canvas, 0.11f, y0 + 0.008f, 0.95f, y1 - 0.008f, new Color(0f, 0f, 0f, 0.35f)).raycastTarget = false;
                s.Fill = Ui.Panel("StandFill" + i, canvas, 0.11f, y0 + 0.008f, 0.95f, y1 - 0.008f, Palette.Muted).rectTransform;
                s.Label = Ui.Label("StandLabel" + i, canvas, 0.13f, y0, 0.94f, y1, "", 26, TextAnchor.MiddleLeft, Palette.Parchment);
                _standings[i] = s;
            }

            _myFlag = BannerLook.FlagImage("MyFlag", canvas, 0.04f, 0.665f, 0.1f, 0.715f);
            _mine = Ui.Label("Mine", canvas, 0.11f, 0.665f, 0.96f, 0.715f, "", 24, TextAnchor.MiddleLeft, Palette.Parchment);

            Ui.Title("FortTitle", canvas, 0.04f, 0.625f, 0.96f, 0.66f, "FORTRESSES  ·  one siege fight every 10 minutes", 24, TextAnchor.MiddleLeft, Palette.Sorn);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                float y1 = 0.62f - i * 0.16f;
                float y0 = y1 - 0.15f;
                Transform card = Ui.Framed("Fort" + i, canvas, 0.04f, y0, 0.96f, y1, Palette.PanelDark).transform;
                var f = new Fort();
                f.Flag = BannerLook.FlagImage("Flag", card, 0.01f, 0.05f, 0.1f, 0.95f);
                f.Title = Ui.Title("Name", card, 0.12f, 0.72f, 0.7f, 0.97f, "", 30, TextAnchor.MiddleLeft, Palette.Parchment);
                f.Info = Ui.Label("Info", card, 0.12f, 0.52f, 0.97f, 0.72f, "", 20, TextAnchor.MiddleLeft, Palette.Muted);
                f.Wall = Ui.Bar("Wall", card, 0.12f, 0.31f, 0.72f, 0.52f, new Color(0.72f, 0.56f, 0.3f), out _);
                f.WallText = Ui.Label("WallText", card, 0.12f, 0.33f, 0.72f, 0.5f, "", 20, TextAnchor.MiddleCenter, Palette.Parchment);
                f.Siege = Ui.Label("Siege", card, 0.12f, 0.04f, 0.97f, 0.32f, "", 19, TextAnchor.MiddleLeft, Palette.Parchment);
                f.Button = Ui.Button("Act", card, 0.74f, 0.33f, 0.98f, 0.97f, "", 26, Palette.Danger, () => Act(index), out f.ButtonLabel);
                _forts[i] = f;
            }

            _message = Ui.Label("Message", canvas, 0.05f, 0.085f, 0.95f, 0.14f, "", 24, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);
            _canvas.SetActive(false);
        }

        public void Open()
        {
            _message.text = _root.Server.Online ? "" : "Offline: the War of Banners needs the server.";
            _nextFetch = 0f;
            _canvas.SetActive(true);
        }

        public void Say(string text) => _message.text = text;

        private void Act(int index)
        {
            Net.ServerLink.WarDto war = _root.Server.War;
            if (war?.fortresses == null || index >= war.fortresses.Length) return;
            if (_root.Server.Banner == Banner.None)
            {
                _message.text = "Swear to a Banner first.";
                _root.Oath.Open();
                return;
            }
            _canvas.SetActive(false);
            _root.FightSiege(war.fortresses[index].id);
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            if (_root.Server.Online && !_fetching && Time.realtimeSinceStartup >= _nextFetch)
            {
                _fetching = true;
                StartCoroutine(_root.Server.FetchWar(error =>
                {
                    _fetching = false;
                    _nextFetch = Time.realtimeSinceStartup + RefreshSeconds;
                    if (error != null) _message.text = error;
                }));
            }

            Net.ServerLink.WarDto war = _root.Server.War;
            Banner mine = _root.Server.Banner;
            if (war == null || war.standings == null)
            {
                _season.text = _root.Server.Online ? "Gathering word from the steppe..." : "";
                return;
            }

            _season.text = "Season " + war.season.TrimStart('W') + "  ·  points from Korstones, Commanders, pushes and sieges";
            long top = 1;
            foreach (Net.ServerLink.BannerStandingDto s in war.standings) top = System.Math.Max(top, s.points);
            for (int i = 0; i < _standings.Length; i++)
            {
                bool has = i < war.standings.Length;
                if (!has) continue;
                Net.ServerLink.BannerStandingDto s = war.standings[i];
                Banner b = BannerLook.Parse(s.banner);
                BannerLook.Show(_standings[i].Flag, b);
                Color c = BannerLook.Color(b);
                c.a = 0.6f;
                _standings[i].Fill.GetComponent<Image>().color = c;
                _standings[i].Fill.anchorMax = new Vector2(0.11f + 0.84f * (s.points / (float)top), _standings[i].Fill.anchorMax.y);
                _standings[i].Label.text = $"{s.name.ToUpperInvariant()}   {s.points:N0} pts   ·   {s.fortresses} fortress{(s.fortresses == 1 ? "" : "es")}" + (b == mine ? "   (yours)" : "");
            }

            Banner winner = BannerLook.Parse(war.lastWinner);
            BannerLook.Show(_myFlag, mine);
            _mine.text = (mine == Banner.None ? "You have sworn to no Banner yet." : $"You ride for the {BannerLook.Name(mine)}  ·  hunting bonus +{war.mySornBonusPercent}% sorn")
                         + "\n" + (winner == Banner.None ? "Last season: no winner." : $"Last season's winner: the {BannerLook.Name(winner)} (+{Banners.WinnerBonusPercent}% sorn this season)");

            float age = Time.realtimeSinceStartup - _root.Server.WarReceivedAt;
            int cooldown = Mathf.Max(0, war.siegeCooldownSeconds - (int)age);
            for (int i = 0; i < _forts.Length; i++)
            {
                Fort f = _forts[i];
                bool has = war.fortresses != null && i < war.fortresses.Length;
                f.Button.gameObject.SetActive(has);
                if (!has) continue;
                Net.ServerLink.FortressDto d = war.fortresses[i];
                Banner holder = BannerLook.Parse(d.holder);
                BannerLook.Show(f.Flag, holder);
                f.Title.text = d.name.ToUpperInvariant();
                f.Title.color = BannerLook.Color(holder);
                string flag = string.IsNullOrEmpty(d.flagGuild) ? "" : $"  ·  guild flag [{d.flagGuild}]";
                f.Info.text = $"{d.region}  ·  the {d.phase} is under siege{flag}\n{d.lastEvent}";
                float wall = d.wallMax > 0 ? d.wall / (float)d.wallMax : 0f;
                f.Wall.anchorMax = new Vector2(wall, 1f);
                f.WallText.text = $"{d.phase.ToUpperInvariant()} WALL  {d.wall:N0} / {d.wallMax:N0}";
                f.Siege.text = "Siege damage:  " + ConfirmDialog.Tint($"Ember {d.siegeEmber:N0}", BannerLook.Color(Banner.Ember)) + "   "
                               + ConfirmDialog.Tint($"Sky {d.siegeSky:N0}", BannerLook.Color(Banner.Sky)) + "   "
                               + ConfirmDialog.Tint($"Gold {d.siegeGold:N0}", BannerLook.Color(Banner.Gold));
                bool defend = mine != Banner.None && mine == holder;
                f.ButtonLabel.text = cooldown > 0 ? $"REGROUP\n{cooldown / 60}:{cooldown % 60:00}" : defend ? "DEFEND" : "ATTACK";
                f.Button.GetComponent<Image>().color = defend ? Palette.Safe : Palette.Danger;
                f.Button.interactable = cooldown == 0 && _root.Server.Online && !_root.Replaying && !_root.PushBusy;
            }
        }
    }
}
