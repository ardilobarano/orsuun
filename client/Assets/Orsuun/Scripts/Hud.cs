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
        private Text _resources;
        private Text _stage;
        private Text _link;
        private Text _banner;
        private Text _weapon;
        private Text _log;
        private Text _speedLabel;
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

        public void Init(GameRoot root)
        {
            _root = root;
            Transform canvas = Ui.Canvas("HudCanvas", 0).transform;
            transform.SetParent(canvas, false);

            Ui.Panel("TopBar", canvas, 0f, 0.945f, 1f, 1f, Palette.PanelDark);
            _resources = Ui.Label("Resources", canvas, 0.02f, 0.948f, 0.98f, 0.997f, "", 30, TextAnchor.MiddleCenter, Palette.Sorn);
            _stage = Ui.Label("Stage", canvas, 0.03f, 0.905f, 0.97f, 0.943f, "", 30, TextAnchor.MiddleLeft, Color.white);
            _link = Ui.Label("Link", canvas, 0.03f, 0.875f, 0.97f, 0.905f, "", 22, TextAnchor.MiddleLeft, Palette.Warn);
            _banner = Ui.Label("Banner", canvas, 0.05f, 0.80f, 0.95f, 0.87f, "", 56, TextAnchor.MiddleCenter, Palette.Warn);

            Ui.Panel("HpBack", canvas, 0.04f, 0.458f, 0.96f, 0.482f, Color.black);
            _hpFill = Ui.Panel("HpFill", canvas, 0.04f, 0.458f, 0.96f, 0.482f, Palette.Bad).rectTransform;
            _hpText = Ui.Label("HpText", canvas, 0.04f, 0.458f, 0.96f, 0.482f, "", 24, TextAnchor.MiddleCenter, Color.white);

            Ui.Panel("BottomPanel", canvas, 0f, 0f, 1f, GameRoot.LaneViewportBottom, Palette.PanelDark);
            _weapon = Ui.Label("Weapon", canvas, 0.04f, 0.405f, 0.96f, 0.445f, "", 32, TextAnchor.MiddleLeft, Color.white);
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
                    Palette.ButtonIdle, () => { if (!_root.Replaying) _root.ActiveLane.TryCast(index); }, out _skillLabels[i]);

                Button auto = Ui.Button("Auto" + i, canvas, x0, 0.165f, x0 + 0.30f, 0.208f, "", 24,
                    Palette.ButtonIdle, () => { LaneSim l = _root.Session.Lane; l.AutoCast[index] = !l.AutoCast[index]; }, out _autoLabels[i]);
                _autoImages[i] = auto.GetComponent<Image>();
            }

            Ui.Button("Forge", canvas, 0.04f, 0.09f, 0.27f, 0.155f, "FORGE", 30, Palette.ButtonForge, () => root.Forge.Open(), out _);
            Ui.Button("Gear", canvas, 0.28f, 0.09f, 0.50f, 0.155f, "GEAR", 30, Palette.ButtonIdle, () => root.Gear.Open(), out _);
            Ui.Button("Shards", canvas, 0.51f, 0.09f, 0.73f, 0.155f, "SHARDS", 30, Palette.Alloy, () => root.Sockets.Open(), out _);
            _pushButton = Ui.Button("Push", canvas, 0.74f, 0.09f, 0.96f, 0.155f, "", 22, Palette.Danger, root.Push, out _pushLabel);

            Ui.Button("Zones", canvas, 0.04f, 0.02f, 0.55f, 0.08f, "", 24, Palette.ButtonIdle, () => root.Zones.Open(), out _stageLabel);
            Ui.Button("Speed", canvas, 0.57f, 0.02f, 0.75f, 0.08f, "", 24, Palette.ButtonIdle, CycleSpeed, out _speedLabel);
            Ui.Button("Dev", canvas, 0.77f, 0.02f, 0.96f, 0.08f, "DEV", 24, new Color(0.2f, 0.2f, 0.2f), DevGrant, out _);
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

        private void CycleSpeed()
        {
            _root.SpeedMultiplier = _root.SpeedMultiplier == 1 ? 3 : _root.SpeedMultiplier == 3 ? 8 : 1;
        }

        /// <summary>Playtest shortcut so testers can reach the high Forge levels within one sitting.</summary>
        private void DevGrant()
        {
            if (_root.Server.Online)
            {
                StartCoroutine(_root.Server.DevGrant());
                return;
            }
            Inventory inv = _root.Session.Inventory;
            inv.Sorn += 500_000;
            inv.Materials += 10;
            inv.ScrollsOfMercy += 5;
            inv.KhansAlloys += 1;
            inv.Turnstones += 20;
            for (int r = 0; r < inv.Korshards.Length; r++) inv.Korshards[r] += 3;
        }

        private void Update()
        {
            if (_root == null) return;
            PlayerSession session = _root.Session;
            LaneSim lane = _root.ActiveLane;
            Inventory inv = session.Inventory;

            _resources.text = $"Lv {inv.Level}   {inv.Sorn:N0} sorn   Draughts {inv.Potions}   Sinew {inv.Materials}   Mercy {inv.ScrollsOfMercy}   Alloy {inv.KhansAlloys}   Turn {inv.Turnstones}   Shards {inv.Korshards[0] + inv.Korshards[1] + inv.Korshards[2] + inv.Korshards[3] + inv.Korshards[4]}";

            string encounter = lane.IsBossEncounter ? lane.Stage.BossName.ToUpperInvariant()
                : lane.IsKorstoneEncounter ? (lane.IsElderNext ? "ELDER KORSTONE" : "KORSTONE")
                : lane.Stage.FinalEncounter == FinalEncounter.None ? "Pack" : $"Pack {lane.EncounterIndex + 1}/{lane.Stage.PacksBeforeKorstone}";
            _stage.text = $"{Content.StageName(lane.Stage.StageNumber)}  ·  {encounter}  ·  Korstones {lane.KorstonesDestroyed}  ·  Deaths {lane.Deaths}";
            _stage.color = lane.IsKorstoneEncounter ? Palette.Warn : Color.white;
            _link.text = _root.Server.Status;
            _link.color = _root.Server.Online ? Palette.Good : Palette.Warn;
            _banner.text = _root.ReplayBanner;
            _banner.color = _root.ReplayBanner.StartsWith("CLEARED") ? Palette.Good : _root.ReplayBanner.StartsWith("FAILED") ? Palette.Bad : Palette.Warn;

            Net.ServerLink.SettlementDto settled = _root.Server.LastSettlement;
            if (settled != null && settled != _shownSettlement && settled.offline)
            {
                _shownSettlement = settled;
                Log($"Welcome back: {settled.countedSeconds / 3600f:0.0} h away, {settled.korstones} Korstones, +{settled.sornEarned:N0} sorn");
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
            _stageLabel.text = $"ZONES  ·  here: {Content.StageName(session.ParkedStage)}";
            _speedLabel.text = $"SPEED x{_root.SpeedMultiplier}";
        }
    }
}
