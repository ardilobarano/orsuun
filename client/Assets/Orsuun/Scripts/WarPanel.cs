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
            public Image Back;
            public RawImage Flag;
            public Text Name;
            public Text Label;
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
            public Button Keep;
            public Text KeepLabel;
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

            // The standings as three Banner columns (war mockup): the flag, the name and the points; yours is lit gold.
            for (int i = 0; i < 3; i++)
            {
                float x0 = 0.04f + i * 0.31f;
                var s = new Standing();
                s.Back = Ui.Framed("StandBack" + i, canvas, x0, 0.715f, x0 + 0.3f, 0.895f, new Color(0.06f, 0.06f, 0.12f, 0.93f));
                Transform card = s.Back.transform;
                s.Flag = BannerLook.FlagImage("StandFlag", card, 0.1f, 0.36f, 0.9f, 0.96f);
                s.Name = Ui.Title("StandName", card, 0.05f, 0.2f, 0.95f, 0.37f, "", 26, TextAnchor.MiddleCenter, Palette.Parchment);
                s.Label = Ui.Title("StandPoints", card, 0.05f, 0.04f, 0.95f, 0.21f, "", 26, TextAnchor.MiddleCenter, Palette.Sorn);
                s.Label.supportRichText = true;
                _standings[i] = s;
            }

            Ui.Framed("MineBack", canvas, 0.04f, 0.658f, 0.96f, 0.71f, new Color(0.07f, 0.07f, 0.14f, 0.95f));
            _myFlag = BannerLook.FlagImage("MyFlag", canvas, 0.05f, 0.662f, 0.1f, 0.706f);
            _mine = Ui.Label("Mine", canvas, 0.11f, 0.662f, 0.95f, 0.706f, "", 22, TextAnchor.MiddleLeft, Palette.Parchment);

            // The three fortresses as tall tiles: the painting under a gold frame, the holder's flag, the wall under
            // siege, the damage each Banner has done, and ATTACK or DEFEND.
            Ui.Section("FortTitle", canvas, 0.08f, 0.617f, 0.92f, 0.653f, "FORTRESSES", 28);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                float x0 = 0.04f + i * 0.31f;
                Transform card = Ui.Framed("Fort" + i, canvas, x0, 0.15f, x0 + 0.3f, 0.61f, new Color(0.06f, 0.06f, 0.12f, 0.93f)).transform;
                Ui.Picture("Art", card, 0.05f, 0.57f, 0.95f, 0.975f, "Thumbs/Fortress" + Fortresses.All[i].Name);
                var f = new Fort();
                f.Flag = BannerLook.FlagImage("Flag", card, 0.07f, 0.7f, 0.25f, 0.96f);
                f.Title = Ui.Title("Name", card, 0.04f, 0.5f, 0.96f, 0.57f, "", 28, TextAnchor.MiddleCenter, Palette.Parchment);
                f.Info = Ui.Label("Info", card, 0.06f, 0.37f, 0.94f, 0.5f, "", 17, TextAnchor.UpperCenter, Palette.Muted);
                f.Wall = Ui.Bar("Wall", card, 0.05f, 0.3f, 0.95f, 0.36f, new Color(0.72f, 0.56f, 0.3f), out _);
                f.WallText = Ui.Title("WallText", card, 0.05f, 0.302f, 0.95f, 0.358f, "", 17, TextAnchor.MiddleCenter, Palette.Parchment);
                f.Siege = Ui.Label("Siege", card, 0.08f, 0.15f, 0.92f, 0.295f, "", 18, TextAnchor.MiddleCenter, Palette.Parchment);
                f.Siege.supportRichText = true;
                f.Button = Ui.Button("Act", card, 0.05f, 0.025f, 0.52f, 0.145f, "", 22, Palette.Danger, () => Act(index), out f.ButtonLabel);
                f.Keep = Ui.Button("Keep", card, 0.54f, 0.025f, 0.95f, 0.145f, "KEEP", 22, Palette.Alloy, () => OpenKeep(index), out f.KeepLabel);
                _forts[i] = f;
            }

            _message = Ui.Label("Message", canvas, 0.05f, 0.085f, 0.95f, 0.145f, "", 24, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Button("Pits", canvas, 0.05f, 0.015f, 0.48f, 0.075f, "THE PITS", 30, Palette.Danger, () => { _canvas.SetActive(false); _root.Pits.Open(); }, out _);
            Ui.Button("Close", canvas, 0.52f, 0.015f, 0.95f, 0.075f, "BACK TO THE HUNT", 26, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);
            BuildKeep(canvas);
            _confirm = new GameObject("KeepConfirm").AddComponent<ConfirmDialog>();
            _confirm.Init();
            _canvas.SetActive(false);
        }

        // ---- The keep of a fortress (Rules.FortressKeeps): the holding guild, the week's bids, and the Sunday siege ----

        private static readonly long[] BidSteps = { 50_000, 250_000, 1_000_000 };
        private GameObject _keep;
        private Text _keepTitle;
        private Text _keepHolder;
        private Text _keepState;
        private Text _keepBids;
        private Text _keepMine;
        private Text _keepMessage;
        private readonly Button[] _bidButtons = new Button[BidSteps.Length];
        private Button _keepFight;
        private Text _keepFightLabel;
        private int _keepIndex = -1;
        private bool _keepBusy;
        private ConfirmDialog _confirm;

        private void BuildKeep(Transform canvas)
        {
            _keep = Ui.Panel("Keep", canvas, 0f, 0f, 1f, 1f, new Color(0f, 0f, 0.02f, 0.82f)).gameObject;
            Transform k = _keep.transform;
            Ui.Framed("Box", k, 0.04f, 0.13f, 0.96f, 0.87f, new Color(0.07f, 0.07f, 0.12f, 0.98f));
            _keepTitle = Ui.Title("Title", k, 0.06f, 0.8f, 0.94f, 0.855f, "", 36, TextAnchor.MiddleCenter, Palette.Sorn);
            _keepHolder = Ui.Label("Holder", k, 0.07f, 0.755f, 0.93f, 0.8f, "", 26, TextAnchor.MiddleCenter, Palette.Parchment);
            _keepHolder.supportRichText = true;
            _keepState = Ui.Label("State", k, 0.07f, 0.7f, 0.93f, 0.755f, "", 22, TextAnchor.MiddleCenter, Palette.Muted);
            Ui.Section("BidsHead", k, 0.15f, 0.655f, 0.85f, 0.692f, "BIDS", 26);
            _keepBids = Ui.Label("Bids", k, 0.08f, 0.45f, 0.92f, 0.65f, "", 24, TextAnchor.UpperLeft, Palette.Parchment);
            _keepBids.supportRichText = true;
            _keepMine = Ui.Label("Mine", k, 0.07f, 0.4f, 0.93f, 0.445f, "", 24, TextAnchor.MiddleCenter, Palette.Sorn);
            for (int i = 0; i < BidSteps.Length; i++)
            {
                long step = BidSteps[i];
                float x0 = 0.07f + i * 0.29f;
                _bidButtons[i] = Ui.Button("Bid" + i, k, x0, 0.325f, x0 + 0.27f, 0.39f, $"BID +{step:N0}", 22, Palette.ButtonForge, () => AskBid(step), out _);
            }
            _keepFight = Ui.Button("Fight", k, 0.15f, 0.24f, 0.85f, 0.315f, "", 28, Palette.Danger, FightKeep, out _keepFightLabel);
            _keepMessage = Ui.Label("Message", k, 0.07f, 0.19f, 0.93f, 0.235f, "", 22, TextAnchor.MiddleCenter, Palette.Warn);
            Ui.Button("CloseKeep", k, 0.3f, 0.14f, 0.7f, 0.185f, "CLOSE", 24, Palette.ButtonIdle, () => _keep.SetActive(false), out _);
            _keep.SetActive(false);
        }

        /// <summary>Opens a fortress's keep dialog (screenshots: -keep n).</summary>
        public void ShowKeep(int index) => OpenKeep(index);

        private void OpenKeep(int index)
        {
            _keepIndex = index;
            _keepMessage.text = _root.Server.Online ? "" : "The keeps need the server.";
            _keep.SetActive(true);
        }

        private Net.ServerLink.KeepDto KeepShown()
        {
            Net.ServerLink.WarDto war = _root.Server.War;
            return war?.keeps != null && _keepIndex >= 0 && _keepIndex < war.keeps.Length ? war.keeps[_keepIndex] : null;
        }

        private void AskBid(long amount)
        {
            Net.ServerLink.KeepDto keep = KeepShown();
            if (keep == null || _keepBusy) return;
            long total = keep.myBid + amount;
            _confirm.Show($"Bid on the keep of {keep.name}?",
                $"{amount:N0} sorn leave the guild treasury now; your bid becomes {total:N0}.\n\n"
                + ConfirmDialog.Tint("On Sunday at 20:00 the four highest bids storm the keep and are spent; the rest go back.", Palette.Muted),
                "BID", Palette.ButtonForge, () =>
                {
                    _keepBusy = true;
                    StartCoroutine(_root.Server.KeepBid(keep.fortressId, amount, (message, error) =>
                    {
                        _keepBusy = false;
                        _keepMessage.text = error ?? message ?? "";
                    }));
                });
        }

        private void FightKeep()
        {
            Net.ServerLink.KeepDto keep = KeepShown();
            if (keep == null || _keepBusy) return;
            _keep.SetActive(false);
            _canvas.SetActive(false);
            _root.FightKeep(keep.fortressId);
        }

        private static string Clock(int seconds)
        {
            if (seconds >= 86_400) return $"{seconds / 86_400} d {seconds % 86_400 / 3600} h";
            if (seconds >= 3600) return $"{seconds / 3600} h {seconds % 3600 / 60:00} m";
            return $"{seconds / 60}:{seconds % 60:00}";
        }

        private void UpdateKeep(float age)
        {
            Net.ServerLink.KeepDto keep = KeepShown();
            if (keep == null) return;
            _keepTitle.text = "THE KEEP OF " + keep.name.ToUpperInvariant();
            _keepHolder.text = string.IsNullOrEmpty(keep.holderTag) ? "No guild holds the keep."
                : $"{ConfirmDialog.Tint("[" + keep.holderTag + "]", GuildPanel.ColorOf(keep.holderColor))} {keep.holderName} holds the keep: their flag flies here.";
            int toSiege = Mathf.Max(0, keep.secondsToSiege - (int)age);
            int left = Mathf.Max(0, keep.secondsLeft - (int)age);
            _keepState.text = keep.state == 0 ? $"Bids are open. The keep is stormed on Sunday at 20:00, in {Clock(toSiege)}."
                : keep.state == 1 ? $"The keep siege is on: {Clock(left)} left. The best contender must beat {keep.wall:N0} plus the {keep.mended:N0} its holders mended."
                : keep.lastEvent + "  Bids open again on Monday at 20:00.";
            var sb = new System.Text.StringBuilder();
            if (keep.bids == null || keep.bids.Length == 0) sb.Append(ConfirmDialog.Tint("No bids yet this week.", Palette.Muted));
            else
                foreach (Net.ServerLink.KeepBidDto b in keep.bids)
                {
                    string line = $"{ConfirmDialog.Tint("[" + b.tag + "]", GuildPanel.ColorOf(b.color))} {b.name}   ·   {b.amount:N0} sorn"
                                  + (keep.state >= 1 ? $"   ·   {b.damage:N0} damage" : "");
                    sb.Append(b.mine ? ConfirmDialog.Tint(line, Palette.Sorn) : line).Append('\n');
                }
            _keepBids.text = sb.ToString().TrimEnd();
            _keepMine.text = keep.holding ? "Your guild holds this keep and defends it on Sunday."
                : keep.myBid > 0 ? $"Your guild bids {keep.myBid:N0} sorn." + (keep.contending ? "  It storms the keep." : "")
                : keep.canBid ? $"Bids start at {Rules.FortressKeeps.MinBid:N0} sorn from the treasury." : "";
            foreach (Button b in _bidButtons)
            {
                b.gameObject.SetActive(keep.canBid);
                b.interactable = !_keepBusy;
            }
            _keepFight.gameObject.SetActive(keep.canFight);
            _keepFightLabel.text = keep.holding ? "HOLD THE KEEP" : "STORM THE KEEP";
            _keepFight.GetComponent<Image>().color = keep.holding ? Palette.Safe : Palette.Danger;
            _keepFight.interactable = !_keepBusy && !_root.Replaying && !_root.PushBusy;
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
            // Offline at opening, online since: the note from Open no longer holds.
            if (_message.text.StartsWith("Offline")) _message.text = "";
            for (int i = 0; i < _standings.Length; i++)
            {
                bool has = i < war.standings.Length;
                _standings[i].Back.gameObject.SetActive(has);
                if (!has) continue;
                Net.ServerLink.BannerStandingDto s = war.standings[i];
                Banner b = BannerLook.Parse(s.banner);
                BannerLook.Show(_standings[i].Flag, b);
                _standings[i].Name.text = s.name.ToUpperInvariant();
                _standings[i].Name.color = Palette.Parchment;
                _standings[i].Label.text = $"{s.points:N0} pts\n<size=18>{s.fortresses} fortress{(s.fortresses == 1 ? "" : "es")}{(b == mine ? "  ·  yours" : "")}</size>";
                // Each column is lacquered in its Banner's colour; yours stands out brighter.
                Color lacquer = BannerLook.Color(b) * (b == mine ? 0.62f : 0.34f);
                lacquer.a = 0.95f;
                _standings[i].Back.color = lacquer;
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
                f.Info.resizeTextMinSize = 10;
                float wall = d.wallMax > 0 ? d.wall / (float)d.wallMax : 0f;
                f.Wall.anchorMax = new Vector2(wall, 1f);
                f.WallText.text = $"{d.phase.ToUpperInvariant()} WALL  {d.wall:N0} / {d.wallMax:N0}";
                f.Siege.text = ConfirmDialog.Tint($"Ember {d.siegeEmber:N0}", BannerLook.Color(Banner.Ember)) + "\n"
                               + ConfirmDialog.Tint($"Sky {d.siegeSky:N0}", BannerLook.Color(Banner.Sky)) + "\n"
                               + ConfirmDialog.Tint($"Gold {d.siegeGold:N0}", BannerLook.Color(Banner.Gold));
                bool defend = mine != Banner.None && mine == holder;
                f.ButtonLabel.text = cooldown > 0 ? $"REGROUP {cooldown / 60}:{cooldown % 60:00}" : defend ? "DEFEND" : "ATTACK";
                f.Button.GetComponent<Image>().color = defend ? Palette.Safe : Palette.Danger;
                f.Button.interactable = cooldown == 0 && _root.Server.Online && !_root.Replaying && !_root.PushBusy;
                Net.ServerLink.KeepDto keep = war.keeps != null && i < war.keeps.Length ? war.keeps[i] : null;
                f.KeepLabel.text = keep != null && keep.canFight ? (keep.holding ? "HOLD KEEP" : "STORM KEEP") : "KEEP";
                f.Keep.GetComponent<Image>().color = keep != null && keep.canFight ? Palette.ButtonForge : Palette.Alloy;
            }
            if (_keep.activeSelf) UpdateKeep(age);
            if (!string.IsNullOrEmpty(war.message) && _message.text.Length == 0) _message.text = war.message;
        }
    }
}
