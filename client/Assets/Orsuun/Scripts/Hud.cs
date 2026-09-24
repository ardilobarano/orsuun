using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>Resources on top, hero HP above the panel, skills and actions in thumb reach at the bottom.</summary>
    public sealed class Hud : MonoBehaviour
    {
        private GameRoot _root;
        private Text _level;
        /// <summary>Currency slots on the top bar: icon (Resources/Icons) and count.</summary>
        private static readonly string[] CurrencyIcons = { "Sorn", "Draught", "WolfSinew", "ScrollOfMercy", "KhansAlloy", "Turnstone", "Korshard" };
        private Text[] _currencies;
        private Text _stage;
        private Text _link;
        private Text _banner;
        private Text _weapon;
        private Text _log;
        private Text _guildLabel;
        private Text _ticker;
        private Text _guildTag;
        private Text _bountyLabel;
        private RawImage _flag;
        private int _lastLevel;
        private Text _stageLabel;
        private Text _pushLabel;
        private Button _pushButton;
        private RectTransform _hpFill;
        private Text _hpText;
        private Button[] _skillButtons;
        private Text[] _skillLabels;
        private Image[] _autoImages;
        private Text[] _autoLabels;
        private float _logAge;
        private Net.ServerLink.SettlementDto _shownSettlement;

        private Transform _canvas;

        public void Init(GameRoot root)
        {
            _root = root;
            Transform canvas = Ui.Canvas("HudCanvas", 0).transform;
            _canvas = canvas;
            transform.SetParent(canvas, false);

            Ui.Panel("TopBar", canvas, 0f, 0.945f, 1f, 1f, Palette.PanelDark);
            Ui.Trim("TopTrim", canvas, 0f, 0.943f, 1f, 0.945f);
            _level = Ui.Title("Level", canvas, 0.01f, 0.948f, 0.11f, 0.997f, "", 30, TextAnchor.MiddleCenter, Palette.Parchment);
            _currencies = new Text[CurrencyIcons.Length];
            for (int i = 0; i < CurrencyIcons.Length; i++)
            {
                // Sorn gets a wider slot: it runs to seven figures.
                float x0 = i == 0 ? 0.115f : 0.285f + (i - 1) * 0.118f;
                float x1 = i == 0 ? 0.28f : x0 + 0.113f;
                // Icon fits its parent, so it gets its own box.
                RectTransform box = Ui.Rect("IconBox" + CurrencyIcons[i], canvas, x0, 0.951f, x0 + 0.045f, 0.994f);
                Ui.Icon("Icon", box, 0f, 0f, 1f, 1f, CurrencyIcons[i]);
                _currencies[i] = Ui.Label("Count" + CurrencyIcons[i], canvas, x0 + 0.047f, 0.948f, x1, 0.997f, "", 28, TextAnchor.MiddleLeft, Palette.Sorn);
            }
            _stage = Ui.Label("Stage", canvas, 0.03f, 0.905f, 0.97f, 0.943f, "", 30, TextAnchor.MiddleLeft, Palette.Parchment);
            _link = Ui.Label("Link", canvas, 0.03f, 0.875f, 0.97f, 0.905f, "", 22, TextAnchor.MiddleLeft, Palette.Warn);
            _banner = Ui.Label("Banner", canvas, 0.05f, 0.80f, 0.95f, 0.87f, "", 56, TextAnchor.MiddleCenter, Palette.Warn);

            Ui.Trim("HpRim", canvas, 0.037f, 0.4565f, 0.963f, 0.4835f);
            Ui.Panel("HpBack", canvas, 0.04f, 0.458f, 0.96f, 0.482f, new Color(0.08f, 0.03f, 0.04f));
            _hpFill = Ui.Panel("HpFill", canvas, 0.04f, 0.458f, 0.96f, 0.482f, new Color(0.78f, 0.16f, 0.14f)).rectTransform;
            _hpText = Ui.Label("HpText", canvas, 0.04f, 0.458f, 0.96f, 0.482f, "", 24, TextAnchor.MiddleCenter, Palette.Parchment);

            Ui.Panel("BottomPanel", canvas, 0f, 0f, 1f, GameRoot.LaneViewportBottom, Palette.PanelDark);
            Ui.Trim("BottomTrim", canvas, 0f, GameRoot.LaneViewportBottom - 0.002f, 1f, GameRoot.LaneViewportBottom);
            _weapon = Ui.Title("Weapon", canvas, 0.04f, 0.405f, 0.96f, 0.445f, "", 32, TextAnchor.MiddleLeft, Palette.Parchment);
            _log = Ui.Label("Log", canvas, 0.04f, 0.365f, 0.96f, 0.405f, "", 26, TextAnchor.MiddleLeft, Palette.Sorn);

            int count = root.Session.Lane.Skills.Length;
            _skillButtons = new Button[count];
            _skillLabels = new Text[count];
            _autoImages = new Image[count];
            _autoLabels = new Text[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                float x0 = 0.04f + i * 0.31f;
                _skillButtons[i] = Ui.Button("Skill" + i, canvas, x0, 0.215f, x0 + 0.30f, 0.355f, root.Session.Lane.Skills[i].Name, 32,
                    Palette.ButtonIdle, () => { if (!_root.Replaying) _root.Session.Cast(index); }, out _skillLabels[i]);

                Button auto = Ui.Button("Auto" + i, canvas, x0, 0.165f, x0 + 0.30f, 0.208f, "", 24,
                    Palette.ButtonIdle, () => _root.Session.ToggleAutoCast(index), out _autoLabels[i]);
                _autoImages[i] = auto.GetComponent<Image>();
            }

            Ui.Button("Forge", canvas, 0.04f, 0.09f, 0.27f, 0.155f, "FORGE", 30, Palette.ButtonForge, () => root.Forge.Open(), out _);
            Ui.Button("Gear", canvas, 0.28f, 0.09f, 0.50f, 0.155f, "GEAR", 30, Palette.ButtonIdle, () => root.Gear.Open(), out _);
            Ui.Button("Shards", canvas, 0.51f, 0.09f, 0.73f, 0.155f, "SHARDS", 30, Palette.Alloy, () => root.Sockets.Open(), out _);
            _pushButton = Ui.Button("Push", canvas, 0.74f, 0.09f, 0.96f, 0.155f, "", 22, Palette.Danger, root.Push, out _pushLabel);

            // Bottom row (24 Sep 2026): the War of Banners, the bounty board, the guild and the Salt Exchange joined; SOUND
            // and SPEED moved into the MENU.
            Ui.Button("Zones", canvas, 0.04f, 0.02f, 0.22f, 0.08f, "", 18, Palette.ButtonIdle, () => root.Zones.Open(), out _stageLabel);
            Ui.Button("War", canvas, 0.23f, 0.02f, 0.36f, 0.08f, "WAR", 22, Palette.Danger, () => root.War.Open(), out _);
            Ui.Button("Bounties", canvas, 0.37f, 0.02f, 0.53f, 0.08f, "BOUNTIES", 18, Palette.Alloy, () => root.Bounties.Open(), out _bountyLabel);
            Ui.Button("Guild", canvas, 0.54f, 0.02f, 0.67f, 0.08f, "GUILD", 20, Palette.Safe, () => root.Guild.Open(), out _guildLabel);
            Ui.Button("Trade", canvas, 0.68f, 0.02f, 0.81f, 0.08f, "TRADE", 20, Palette.ButtonForge, () => root.Market.Open(), out _);
            Ui.Button("Menu", canvas, 0.82f, 0.02f, 0.96f, 0.08f, "MENU", 20, Palette.DevGrey, () => root.Menu.Open(), out _);

            // The newest world chat line over the bottom of the lane; tap it for CHAT.
            Image strip = Ui.Panel("ChatStrip", canvas, 0.03f, 0.488f, 0.97f, 0.522f, new Color(0f, 0f, 0f, 0.45f));
            strip.gameObject.AddComponent<Button>().onClick.AddListener(() => root.Chat.Open());
            Ui.Label("ChatIcon", strip.transform, 0.01f, 0f, 0.1f, 1f, "CHAT", 18, TextAnchor.MiddleCenter, Palette.Sorn);
            _ticker = Ui.Label("ChatLine", strip.transform, 0.11f, 0f, 0.99f, 1f, "", 20, TextAnchor.MiddleLeft, Palette.Parchment);
            _ticker.supportRichText = true;
            _ticker.horizontalOverflow = HorizontalWrapMode.Overflow;
            _ticker.resizeTextForBestFit = false;
            strip.gameObject.AddComponent<RectMask2D>();

            // The Banner's flag in the corner of the lane; tap it for the War of Banners.
            _flag = BannerLook.FlagImage("BannerFlag", canvas, 0.905f, 0.79f, 0.985f, 0.9f);
            var flagButton = _flag.gameObject.AddComponent<Button>();
            _flag.raycastTarget = true;
            flagButton.onClick.AddListener(() => root.War.Open());
            // The guild tag under the flag, in the guild's colour.
            _guildTag = Ui.Title("GuildTag", canvas, 0.88f, 0.762f, 1f, 0.79f, "", 22, TextAnchor.MiddleCenter, Palette.Parchment);
        }

        /// <summary>The screen area (canvas anchors) covering the named HUD elements, for the tutorial's highlight.</summary>
        public Rect Area(params string[] names)
        {
            Vector2 min = Vector2.one, max = Vector2.zero;
            foreach (string n in names)
            {
                var rect = (RectTransform)_canvas.Find(n);
                if (rect == null) continue;
                min = Vector2.Min(min, rect.anchorMin);
                max = Vector2.Max(max, rect.anchorMax);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public void Handle(LaneEvent e)
        {
            if (e.Kind != LaneEventKind.Loot) return;
            Log(e.Text);
        }

        public void Log(string text)
        {
            _log.text = text;
            _logAge = 0f;
        }

        private void Update()
        {
            if (_root == null) return;
            PlayerSession session = _root.Session;
            LaneSim lane = _root.ActiveLane;
            Inventory inv = session.Inventory;

            _level.text = "Lv " + inv.Level;
            _currencies[0].text = inv.Sorn.ToString("N0");
            _currencies[1].text = inv.Potions.ToString();
            _currencies[2].text = inv.Materials.ToString();
            _currencies[3].text = inv.ScrollsOfMercy.ToString();
            _currencies[4].text = inv.KhansAlloys.ToString();
            _currencies[5].text = inv.Turnstones.ToString();
            _currencies[6].text = (inv.Korshards[0] + inv.Korshards[1] + inv.Korshards[2] + inv.Korshards[3] + inv.Korshards[4]).ToString();

            string encounter = lane.IsBossEncounter ? lane.Stage.BossName.ToUpperInvariant()
                : lane.IsKorstoneEncounter ? (lane.IsElderNext ? "ELDER KORSTONE" : "KORSTONE")
                : lane.Stage.FinalEncounter == FinalEncounter.None ? "Pack" : $"Pack {lane.EncounterIndex + 1}/{lane.Stage.PacksBeforeKorstone}";
            _stage.text = $"{Content.StageName(lane.Stage.StageNumber)}  ·  {encounter}  ·  Korstones {lane.KorstonesDestroyed}  ·  Deaths {lane.Deaths}";
            _stage.color = lane.IsKorstoneEncounter ? Palette.Warn : Palette.Parchment;
            Bell bell = _root.LocalBell;
            string bellText;
            if (bell != Bell.None) bellText = "  ·  " + EveningBells.Name(bell).ToUpperInvariant();
            else if (_root.Server.Online && _root.Server.Bell != null) bellText = $"  ·  next bell in {_root.Server.Bell.minutesUntilNext / 60}h {_root.Server.Bell.minutesUntilNext % 60:00}m";
            else { EveningBells.Next(System.DateTime.Now, out int mins); bellText = $"  ·  next bell in {mins / 60}h {mins % 60:00}m"; }
            _link.text = _root.Server.Status + bellText;
            _link.color = bell != Bell.None ? Palette.Sorn : _root.Server.Online ? Palette.Good : Palette.Warn;
            _banner.text = _root.ReplayBanner;
            _banner.color = _root.ReplayBanner.StartsWith("CLEARED") ? Palette.Good : _root.ReplayBanner.StartsWith("FAILED") ? Palette.Bad : Palette.Warn;

            Net.ServerLink.SettlementDto settled = _root.Server.LastSettlement;
            if (settled != null && settled != _shownSettlement && settled.offline)
            {
                _shownSettlement = settled;
                Log($"Welcome back: {settled.countedSeconds / 3600f:0.0} h away, {settled.korstones} Korstones, +{settled.sornEarned:N0} sorn");
            }
            else if (settled != null && settled != _shownSettlement && settled.loopsVerified > 0)
            {
                // Active play paid: the server replayed this interval's loops and credited their pace.
                _shownSettlement = settled;
                Log($"Hunting pace {settled.activeBp / 100}%  ·  {settled.loopsVerified} loop{(settled.loopsVerified == 1 ? "" : "s")} verified");
            }

            float hp = Mathf.Clamp01(lane.HeroHp / (float)lane.HeroMaxHp);
            _hpFill.anchorMax = new Vector2(0.04f + 0.92f * hp, _hpFill.anchorMax.y);
            _hpText.text = lane.Phase == LanePhase.Dead ? "DEFEATED — respawning" : $"{lane.HeroHp} / {lane.HeroMaxHp}";

            HeroStats stats = session.Hero;
            _weapon.text = $"{session.Weapon.DisplayName} +{session.Weapon.UpgradeLevel}   ·   Atk {stats.Attack}   Def {stats.Defense}   Crit {stats.CritChanceBp / 100}%";
            _weapon.color = ForgePanel.LevelColor(session.Weapon.UpgradeLevel);

            _logAge += Time.deltaTime;
            Color logColor = Palette.Sorn;
            logColor.a = Mathf.Clamp01(4f - _logAge);
            _log.color = logColor;

            for (int i = 0; i < _skillButtons.Length; i++)
            {
                int ticksLeft = lane.CooldownTicksLeft(i);
                _skillButtons[i].interactable = !_root.Replaying && ticksLeft == 0 && lane.Phase == LanePhase.Fighting;
                _skillLabels[i].text = ticksLeft == 0
                    ? lane.Skills[i].Name
                    : $"{lane.Skills[i].Name}\n{ticksLeft / (float)LaneSim.TicksPerSecond:0.0}s";
                _autoLabels[i].text = lane.AutoCast[i] ? "AUTO: ON" : "AUTO: OFF";
                _autoImages[i].color = lane.AutoCast[i] ? Palette.Safe : Palette.ButtonIdle;
            }

            bool allCleared = session.HighestStageCleared >= Content.TotalStages;
            _pushLabel.text = allCleared ? "ALL CLEARED" : $"PUSH\n{Content.StageName(session.PushTarget)}";
            _pushButton.interactable = !_root.Replaying && !_root.PushBusy && !allCleared;
            _stageLabel.text = $"ZONES\n<size=16>{Content.StageName(session.ParkedStage)}</size>";
            Net.ServerLink.GuildBriefDto guild = _root.Server.Guild;
            bool inGuild = _root.Server.InGuild;
            _guildLabel.text = inGuild ? $"GUILD\n<size=16>[{guild.tag}]</size>" : "GUILD";
            _guildTag.text = inGuild ? "[" + guild.tag + "]" : "";
            _ticker.text = _root.Chat.Ticker.Length > 0 ? _root.Chat.Ticker : ConfirmDialog.Tint(_root.Server.Online ? "Tap to talk with the steppe." : "Chat needs the server.", Palette.Muted);
            if (inGuild) _guildTag.color = GuildPanel.ColorOf(guild.color);
            bool claim = _root.Bounties.AnyClaimable;
            _bountyLabel.text = claim ? "BOUNTIES\n<size=16><color=#8CF08C>ready!</color></size>" : "BOUNTIES";
            BannerLook.Show(_flag, _root.Server.Banner);
            if (_lastLevel > 0 && inv.Level > _lastLevel)
            {
                GameAudio.Instance?.Play("LaneLevelUp", 0.9f, 1f, 0f);
                Log($"Level up!  Level {inv.Level}");
            }
            _lastLevel = inv.Level;
        }
    }
}
