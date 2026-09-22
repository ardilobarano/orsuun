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
        private Net.ServerLink.SettlementDto _shownSettlement;
        private Text _weapon;
        private Text _log;
        private Text _speedLabel;
        private RectTransform _hpFill;
        private Text _hpText;
        private Button[] _skillButtons;
        private Text[] _skillLabels;
        private Image[] _autoImages;
        private Text[] _autoLabels;
        private float _logAge;

        public void Init(GameRoot root)
        {
            _root = root;
            LaneSim lane = root.Session.Lane;
            Transform canvas = Ui.Canvas("HudCanvas", 0).transform;
            transform.SetParent(canvas, false);

            Ui.Panel("TopBar", canvas, 0f, 0.945f, 1f, 1f, Palette.PanelDark);
            _resources = Ui.Label("Resources", canvas, 0.02f, 0.948f, 0.98f, 0.997f, "", 30, TextAnchor.MiddleCenter, Palette.Sorn);
            _stage = Ui.Label("Stage", canvas, 0.03f, 0.905f, 0.97f, 0.943f, "", 30, TextAnchor.MiddleLeft, Color.white);
            _link = Ui.Label("Link", canvas, 0.03f, 0.875f, 0.97f, 0.905f, "", 22, TextAnchor.MiddleLeft, Palette.Warn);

            Ui.Panel("HpBack", canvas, 0.04f, 0.458f, 0.96f, 0.482f, Color.black);
            _hpFill = Ui.Panel("HpFill", canvas, 0.04f, 0.458f, 0.96f, 0.482f, Palette.Bad).rectTransform;
            _hpText = Ui.Label("HpText", canvas, 0.04f, 0.458f, 0.96f, 0.482f, "", 24, TextAnchor.MiddleCenter, Color.white);

            Ui.Panel("BottomPanel", canvas, 0f, 0f, 1f, GameRoot.LaneViewportBottom, Palette.PanelDark);
            _weapon = Ui.Label("Weapon", canvas, 0.04f, 0.395f, 0.96f, 0.445f, "", 34, TextAnchor.MiddleLeft, Color.white);
            _log = Ui.Label("Log", canvas, 0.04f, 0.355f, 0.96f, 0.395f, "", 26, TextAnchor.MiddleLeft, Palette.Sorn);

            int count = lane.Skills.Length;
            _skillButtons = new Button[count];
            _skillLabels = new Text[count];
            _autoImages = new Image[count];
            _autoLabels = new Text[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                float x0 = 0.04f + i * 0.31f;
                _skillButtons[i] = Ui.Button("Skill" + i, canvas, x0, 0.20f, x0 + 0.30f, 0.345f, lane.Skills[i].Name, 32,
                    Palette.ButtonIdle, () => lane.TryCast(index), out _skillLabels[i]);

                Button auto = Ui.Button("Auto" + i, canvas, x0, 0.15f, x0 + 0.30f, 0.193f, "", 24,
                    Palette.ButtonIdle, () => lane.AutoCast[index] = !lane.AutoCast[index], out _autoLabels[i]);
                _autoImages[i] = auto.GetComponent<Image>();
            }

            Ui.Button("Forge", canvas, 0.04f, 0.03f, 0.50f, 0.13f, "FORGE", 44, Palette.ButtonForge, () => root.Forge.Open(), out _);
            Ui.Button("Speed", canvas, 0.52f, 0.03f, 0.73f, 0.13f, "", 32, Palette.ButtonIdle, CycleSpeed, out _speedLabel);
            Ui.Button("Dev", canvas, 0.75f, 0.03f, 0.96f, 0.13f, "DEV\n+sorn +scrolls", 24, new Color(0.2f, 0.2f, 0.2f), DevGrant, out _);
        }

        public void Handle(LaneEvent e)
        {
            if (e.Kind != LaneEventKind.Loot) return;
            _log.text = e.Text;
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
        }

        private void Update()
        {
            if (_root == null) return;
            PlayerSession session = _root.Session;
            LaneSim lane = session.Lane;
            Inventory inv = session.Inventory;

            _resources.text = $"{inv.Sorn:N0} sorn   Draughts {inv.Potions}   Sinew {inv.Materials}   Mercy {inv.ScrollsOfMercy}   Alloy {inv.KhansAlloys}   Turn {inv.Turnstones}";

            string encounter = lane.IsKorstoneEncounter ? "KORSTONE" : $"Pack {lane.EncounterIndex + 1}/5";
            _stage.text = $"The Oathfields  ·  {encounter}  ·  Korstones broken {lane.KorstonesDestroyed}  ·  Deaths {lane.Deaths}";
            _stage.color = lane.IsKorstoneEncounter ? Palette.Warn : Color.white;
            _link.text = _root.Server.Status;
            _link.color = _root.Server.Online ? Palette.Good : Palette.Warn;

            Net.ServerLink.SettlementDto settled = _root.Server.LastSettlement;
            if (settled != null && settled != _shownSettlement && settled.offline)
            {
                _shownSettlement = settled;
                _log.text = $"Welcome back: {settled.countedSeconds / 3600f:0.0} h away, {settled.korstones} Korstones, +{settled.sornEarned:N0} sorn";
                _logAge = 0f;
            }

            float hp = Mathf.Clamp01(lane.HeroHp / (float)lane.HeroMaxHp);
            _hpFill.anchorMax = new Vector2(0.04f + 0.92f * hp, _hpFill.anchorMax.y);
            _hpText.text = lane.Phase == LanePhase.Dead ? "DEFEATED — respawning" : $"{lane.HeroHp} / {lane.HeroMaxHp}";

            HeroStats stats = HeroFactory.FromWeapon(session.Weapon);
            _weapon.text = $"{ForgePanel.WeaponName} +{session.Weapon.UpgradeLevel}   ·   Attack {stats.Attack}   ·   Crit {stats.CritChanceBp / 100}%";
            _weapon.color = ForgePanel.LevelColor(session.Weapon.UpgradeLevel);

            _logAge += Time.deltaTime;
            Color logColor = Palette.Sorn;
            logColor.a = Mathf.Clamp01(4f - _logAge);
            _log.color = logColor;

            for (int i = 0; i < _skillButtons.Length; i++)
            {
                int ticksLeft = lane.CooldownTicksLeft(i);
                _skillButtons[i].interactable = ticksLeft == 0 && lane.Phase == LanePhase.Fighting;
                _skillLabels[i].text = ticksLeft == 0
                    ? lane.Skills[i].Name
                    : $"{lane.Skills[i].Name}\n{ticksLeft / (float)LaneSim.TicksPerSecond:0.0}s";
                _autoLabels[i].text = lane.AutoCast[i] ? "AUTO: ON" : "AUTO: OFF";
                _autoImages[i].color = lane.AutoCast[i] ? Palette.Safe : Palette.ButtonIdle;
            }

            _speedLabel.text = $"SPEED\nx{_root.SpeedMultiplier}";
        }
    }
}
