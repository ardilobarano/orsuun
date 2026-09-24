using System.Collections.Generic;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Orsuun.Client
{
    /// <summary>
    /// Where to farm: the three zone types with PARK buttons, and the Commanders of the unlocked
    /// Commander Ground with their clocks and FIGHT buttons.
    /// </summary>
    public sealed class ZonePanel : MonoBehaviour
    {
        private const int ZoneRows = 9;
        private const int BossRows = 3;

        private sealed class Row
        {
            public Image Back;
            public RawImage Picture;
            public Text Label;
            public Button Button;
            public Text ButtonLabel;
            public int Id;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private Text _message;
        private readonly Row[] _zoneRows = new Row[ZoneRows];
        private readonly Row[] _bossRows = new Row[BossRows];

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("ZoneCanvas", 9).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Zones");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "WHERE TO HUNT", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Label("ZonesTitle", canvas, 0.05f, 0.895f, 0.95f, 0.93f, "ZONES  ·  Hunting Grounds pay sorn and levels, Fields pay materials, Commander Grounds pay skins", 20, TextAnchor.MiddleLeft, Palette.Muted);

            for (int i = 0; i < ZoneRows; i++)
            {
                float y1 = 0.89f - i * 0.052f;
                _zoneRows[i] = MakeRow(canvas, "Zone" + i, y1 - 0.048f, y1, "PARK", Palette.Safe, i, row => _root.Park(_zoneRows[row].Id));
            }

            Ui.Section("BossTitle", canvas, 0.1f, 0.378f, 0.9f, 0.41f, "COMMANDERS", 24);
            for (int i = 0; i < BossRows; i++)
            {
                float y1 = 0.37f - i * 0.075f;
                _bossRows[i] = MakeRow(canvas, "Boss" + i, y1 - 0.07f, y1, "FIGHT", Palette.Danger, i, row => { _canvas.SetActive(false); _root.FightBoss(_bossRows[row].Id); });
            }

            Ui.Button("BossesUp", canvas, 0.04f, 0.085f, 0.30f, 0.135f, "DEV: bosses up", 20, Palette.DevGrey, DevBossesUp, out _);
            _message = Ui.Label("Message", canvas, 0.32f, 0.085f, 0.96f, 0.135f, "", 22, TextAnchor.MiddleLeft, Palette.Muted);
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);
            _canvas.SetActive(false);
        }

        private Row MakeRow(Transform canvas, string name, float y0, float y1, string action, Color color, int index, System.Action<int> onClick)
        {
            var row = new Row();
            row.Back = Ui.Framed(name + "Back", canvas, 0.04f, y0, 0.74f, y1, Palette.PanelDark);
            // A painted thumbnail of the zone, or the Commander's portrait, in a gold slot frame at the row's left.
            float picture = (y1 - y0) * 1920f / 1080f * (action == "FIGHT" ? 1f : 1.3f);
            row.Picture = Ui.Picture(name + "Picture", canvas, 0.045f, y0 + 0.003f, 0.045f + picture, y1 - 0.003f, null);
            row.Label = Ui.Label(name + "Label", canvas, 0.06f + picture, y0, 0.73f, y1, "", 22, TextAnchor.MiddleLeft, Palette.Parchment);
            row.Button = Ui.Button(name + "Btn", canvas, 0.76f, y0, 0.96f, y1, action, 22, color, () => onClick(index), out row.ButtonLabel);
            return row;
        }

        public void Open()
        {
            _message.text = "";
            _canvas.SetActive(true);
        }

        private void DevBossesUp()
        {
            if (_root.Server.Online) StartCoroutine(_root.Server.DevBossesUp());
            else _message.text = "Local mode: Commanders are always up.";
        }

        private void Update()
        {
            if (_root == null || !_canvas.activeSelf) return;
            PlayerSession session = _root.Session;

            // Zones: campaign farm spot first, then every zone in content order.
            var entries = new List<(int Id, string Text, bool Unlocked)>();
            entries.Add((session.HighestStageCleared > 0 ? session.HighestStageCleared : 1, $"Campaign  ·  {Content.StageName(session.HighestStageCleared > 0 ? session.HighestStageCleared : 1)}  ·  balanced drops", true));
            foreach (ZoneDef z in Content.Zones)
            {
                bool unlocked = Content.IsUnlocked(z.Id, session.HighestStageCleared);
                string kind = z.Type == ZoneType.HuntingGround ? "Hunting Ground" : z.Type == ZoneType.KorstoneField ? "Korstone Field" : "Commander Ground";
                string flags = (z.Pvp ? "  PvP" : "") + (z.OfflineAllowed ? "" : "  no offline");
                string text = unlocked ? $"{z.Name}  ·  {kind} T{z.Tier}  ·  Lv {z.LevelMin}-{z.LevelMax}{flags}" : $"{z.Name}  ·  clear campaign stage {z.UnlockStage}";
                entries.Add((z.Id, text, unlocked));
            }
            for (int i = 0; i < ZoneRows; i++)
            {
                bool has = i < entries.Count;
                Row row = _zoneRows[i];
                row.Back.gameObject.SetActive(has);
                row.Label.gameObject.SetActive(has);
                row.Button.gameObject.SetActive(has && entries[i].Unlocked);
                if (!has) continue;
                row.Id = entries[i].Id;
                Ui.SetPicture(row.Picture, "Thumbs/" + ZoneThumb(entries[i].Id));
                bool parked = session.ParkedStage == entries[i].Id;
                row.Label.text = (parked ? "▶ " : "") + entries[i].Text;
                row.Label.color = entries[i].Unlocked ? Palette.Parchment : Palette.Muted;
                row.Button.interactable = !parked && !_root.Replaying && !_root.PushBusy;
                row.ButtonLabel.text = parked ? "HERE" : "PARK";
            }

            // Commanders: from the server when online, otherwise always up in local mode.
            bool campOpen = Content.IsUnlocked(121, session.HighestStageCleared);
            float age = Time.realtimeSinceStartup - _root.Server.BossesReceivedAt;
            for (int i = 0; i < BossRows; i++)
            {
                Row row = _bossRows[i];
                BossDef boss = Content.Bosses[i];
                row.Id = boss.Id;
                Ui.SetPicture(row.Picture, "Thumbs/Commander" + new string(System.Array.FindAll(boss.Name.Replace("Warlord ", "").Replace("The ", "").ToCharArray(), char.IsLetter)));
                string status;
                string pool = "";
                bool canFight = campOpen;
                if (_root.Server.Online)
                {
                    Net.ServerLink.BossStatusDto s = null;
                    foreach (Net.ServerLink.BossStatusDto b in _root.Server.Bosses) if (b.bossId == boss.Id) s = b;
                    if (s == null) { status = "…"; canFight = false; }
                    else
                    {
                        long left = System.Math.Max(0, s.secondsLeft - (long)age);
                        status = s.slain ? $"next in {left / 60}:{left % 60:00}"
                            : s.up ? (s.foughtThisSpawn ? $"fought · up {left / 60}:{left % 60:00}" : $"UP {left / 60}:{left % 60:00}") : $"next in {left / 60}:{left % 60:00}";
                        canFight &= s.up && !s.foughtThisSpawn && !s.slain;
                        // The shared pool: what the server has left of it, the best fighter so far, or who slew it.
                        if (s.slain) pool = "\n" + ConfirmDialog.Tint($"SLAIN by {s.slainBy}", BannerLook.Color(BannerLook.Parse(s.slainBanner)));
                        else if (s.hpMax > 0)
                        {
                            pool = $"\nServer pool {s.hpLeft:N0} / {s.hpMax:N0} HP";
                            if (s.top != null && s.top.Length > 0)
                                pool += "  ·  best: " + ConfirmDialog.Tint($"{s.top[0].name} {s.top[0].damage:N0}", BannerLook.Color(BannerLook.Parse(s.top[0].banner)));
                        }
                    }
                }
                else status = "up (local)";

                row.Label.text = campOpen ? $"{boss.Name}  ·  {status}\n{Mechanic(boss.Mechanic)}{pool}" : $"{boss.Name}  ·  clear campaign stage 5";
                row.Label.color = campOpen ? Palette.Parchment : Palette.Muted;
                row.Button.interactable = canFight && !_root.Replaying && !_root.PushBusy;
            }
        }

        /// <summary>Resources/Thumbs picture for a campaign stage (below 100) or a zone id.</summary>
        private static string ZoneThumb(int id) => id switch
        {
            < 100 => "ZoneCampaign",
            101 => "ZoneEmberSteppe",
            102 => "ZoneSaltFlats",
            103 => "ZoneFrostPasture",
            121 => "ZoneWarCamp",
            _ => "ZoneKorstoneField",
        };

        private static string Mechanic(BossMechanic m) => m switch
        {
            BossMechanic.CaptainShield => "shielded by 4 captains, enrages at 30%",
            BossMechanic.MirrorImages => "splits into reflecting images at 70% and 40%",
            BossMechanic.PackCaller => "calls a pack every 15 s",
            _ => "",
        };
    }
}
