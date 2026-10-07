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
        /// <summary>The campaign's farm spot and every zone in Content.Zones.</summary>
        private static readonly int ZoneRows = 1 + Content.Zones.Length;
        private const int BossRows = 3;

        private sealed class Row
        {
            public Image Back;
            public RawImage Picture;
            public Text Name;
            public Text Label;
            public Button Button;
            public Image ButtonImage;
            public Text ButtonLabel;
            public int Id;
        }

        private GameRoot _root;
        private GameObject _canvas;
        private Text _message;
        private Row[] _zoneRows;
        private readonly Row[] _dungeonRows = new Row[Dungeons.All.Length];
        private readonly Row[] _bossRows = new Row[BossRows];
        private Row _riverRow, _townRow, _towerRow;

        public bool IsOpen => _canvas.activeSelf;

        public void Init(GameRoot root)
        {
            _root = root;
            _canvas = Ui.Canvas("ZoneCanvas", 9).gameObject;
            Transform canvas = _canvas.transform;
            transform.SetParent(canvas, false);

            Ui.Backdrop(canvas, "Zones");
            Ui.Title("Title", canvas, 0.05f, 0.935f, 0.95f, 0.98f, "WHERE TO HUNT", 40, TextAnchor.MiddleCenter, Palette.Sorn, carved: true);
            Ui.Label("ZonesTitle", canvas, 0.05f, 0.9f, 0.95f, 0.93f, "Hunting Grounds pay sorn and levels, Fields pay materials, Commander Grounds pay skins", 20, TextAnchor.MiddleCenter, Palette.Muted);

            // Zone cards (zones mockup): the painting, the name, what it is, and HUNT HERE; the list scrolls. The dungeon
            // leads it: ENTER spends one of the day's two keys.
            Ui.Scroll("ZoneList", canvas, 0.03f, 0.462f, 0.97f, 0.895f, out RectTransform content);
            // The town square (TownPanel): the townsfolk and the rugs; the hunt goes on meanwhile.
            RectTransform townCard = new GameObject("Town", typeof(RectTransform)).GetComponent<RectTransform>();
            townCard.SetParent(content, false);
            townCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 124f;
            _townRow = MakeCard(townCard, "Town", 1.33f, Palette.ButtonForge, GoToTown);
            // Old Nergui's river (Rules.Fishing): no hunting there, fishing instead.
            RectTransform riverCard = new GameObject("River", typeof(RectTransform)).GetComponent<RectTransform>();
            riverCard.SetParent(content, false);
            riverCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 124f;
            _riverRow = MakeCard(riverCard, "River", 1.33f, Palette.Alloy, GoFishing);
            // The Endless Tower (Rules.Tower): its own screen, with the week's ladder.
            RectTransform towerCard = new GameObject("Tower", typeof(RectTransform)).GetComponent<RectTransform>();
            towerCard.SetParent(content, false);
            towerCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 124f;
            _towerRow = MakeCard(towerCard, "Tower", 1.33f, Palette.Danger, OpenTower);
            for (int d = 0; d < Dungeons.All.Length; d++)
            {
                RectTransform dungeonCard = new GameObject("Dungeon" + d, typeof(RectTransform)).GetComponent<RectTransform>();
                dungeonCard.SetParent(content, false);
                dungeonCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 124f;
                int id = Dungeons.All[d].Id;
                _dungeonRows[d] = MakeCard(dungeonCard, "Dungeon", 1.45f, Palette.Danger, () => EnterDungeon(id));
            }
            _zoneRows = new Row[ZoneRows];
            for (int i = 0; i < ZoneRows; i++)
            {
                RectTransform card = new GameObject("Zone" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                card.SetParent(content, false);
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 124f;
                int index = i;
                _zoneRows[i] = MakeCard(card, "Zone", 1.45f, Palette.Safe, () => _root.Park(_zoneRows[index].Id));
            }

            Ui.Section("BossTitle", canvas, 0.1f, 0.418f, 0.9f, 0.455f, "COMMANDERS", 28);
            for (int i = 0; i < BossRows; i++)
            {
                float y1 = 0.41f - i * 0.083f;
                RectTransform card = Ui.Rect("Boss" + i, canvas, 0.03f, y1 - 0.078f, 0.97f, y1);
                int index = i;
                _bossRows[i] = MakeCard(card, "Boss", 1f, Palette.Danger, () => { _canvas.SetActive(false); _root.FightBoss(_bossRows[index].Id); });
                _bossRows[i].ButtonLabel.text = "FIGHT";
            }

            Ui.Button("BossesUp", canvas, 0.04f, 0.085f, 0.30f, 0.135f, "DEV: bosses up", 20, Palette.DevGrey, DevBossesUp, out _);
            _message = Ui.Label("Message", canvas, 0.32f, 0.085f, 0.96f, 0.135f, "", 22, TextAnchor.MiddleLeft, Palette.Muted);
            Ui.Button("Close", canvas, 0.25f, 0.015f, 0.75f, 0.075f, "BACK TO THE HUNT", 30, Palette.ButtonIdle, () => _canvas.SetActive(false), out _);
            _canvas.SetActive(false);
        }

        /// <summary>A card filling <paramref name="card"/>: a framed picture (width = pictureAspect x its height), the name
        /// and a line under it, and the action button on the right.</summary>
        private static Row MakeCard(RectTransform card, string name, float pictureAspect, Color color, System.Action onClick)
        {
            var row = new Row();
            row.Back = Ui.Framed(name + "Back", card, 0f, 0f, 1f, 1f, new Color(0.06f, 0.06f, 0.12f, 0.93f));
            // The picture keeps its shape: a fitter sizes its box from the card's height.
            RectTransform box = Ui.Rect(name + "PictureBox", card, 0.012f, 0.07f, 0.5f, 0.93f);
            var fit = box.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fit.aspectRatio = pictureAspect;
            box.pivot = new Vector2(0f, 0.5f);
            row.Picture = Ui.Picture("Picture", box, 0f, 0f, 1f, 1f, null);
            float textX = pictureAspect > 1.2f ? 0.27f : 0.2f;
            row.Name = Ui.Title(name + "Name", card, textX, 0.5f, 0.7f, 0.92f, "", 30, TextAnchor.MiddleLeft, Palette.Parchment);
            row.Label = Ui.Label(name + "Label", card, textX, 0.06f, 0.7f, 0.52f, "", 20, TextAnchor.UpperLeft, Palette.Muted);
            row.Label.supportRichText = true;
            row.Button = Ui.Button(name + "Btn", card, 0.715f, 0.16f, 0.985f, 0.84f, "", 24, color, onClick, out row.ButtonLabel);
            row.ButtonImage = row.Button.GetComponent<Image>();
            return row;
        }

        private void GoToTown()
        {
            _canvas.SetActive(false);
            _root.Town.Open();
        }

        private void UpdateTown()
        {
            Ui.SetPicture(_townRow.Picture, "Thumbs/Town");
            Banner banner = _root.Server.Banner;
            _townRow.Name.text = banner == Banner.None ? "The Town Square" : Banners.Def(banner).StartingTown;
            _townRow.Label.text = "The Forge, the Caravan, the elder, the Pits and the rugs  ·  the hunt goes on";
            _townRow.ButtonLabel.text = "GO TO TOWN";
        }

        private void GoFishing()
        {
            if (!_root.Unlocked(Feature.Fishing)) { _message.text = Unlocks.Locked(Feature.Fishing); return; }
            _canvas.SetActive(false);
            _root.River.Go();
        }

        private void UpdateRiver()
        {
            bool open = _root.Unlocked(Feature.Fishing);
            Ui.SetPicture(_riverRow.Picture, "Thumbs/River");
            _riverRow.Name.text = "Old Nergui's River";
            _riverRow.Label.text = !_root.Server.Online ? "Fishing needs the server."
                : !open ? $"Opens at level {Unlocks.Level(Feature.Fishing)}"
                : "Fishing  ·  no hunting here  ·  fish to eat for boosts, mussels with pearls";
            _riverRow.ButtonLabel.text = open ? "GO FISHING" : "LV " + Unlocks.Level(Feature.Fishing);
            _riverRow.ButtonImage.color = open && _root.Server.Online ? Palette.Alloy : Palette.ButtonIdle;
        }

        private void EnterDungeon(int id)
        {
            if (_root.Replaying || _root.PushBusy) return;
            if (_root.Server.DungeonRunAtSmith != 0) { _canvas.SetActive(false); _root.ContinueDungeon(); return; }
            // In a party the dungeon can be opened for it (Rules.PartyDungeons), or entered alone.
            if (_root.Server.InParty)
            {
                _confirm ??= NewConfirm();
                _confirm.Show("Take your party?", "Partymates can join for three minutes, each with a key of their own, and fight beside you. " +
                    "Those who clear it together share an extra Warden's chest.", "PARTY RUN", Palette.Safe,
                    () => { _canvas.SetActive(false); _root.EnterDungeon(id, openForParty: true); }, "ALONE",
                    () => { _canvas.SetActive(false); _root.EnterDungeon(id); });
                return;
            }
            _canvas.SetActive(false);
            _root.EnterDungeon(id);
        }

        private ConfirmDialog _confirm;

        private static ConfirmDialog NewConfirm()
        {
            var dialog = new GameObject("ZoneConfirm").AddComponent<ConfirmDialog>();
            dialog.Init();
            return dialog;
        }

        /// <summary>What a dungeon's card says it holds (Rules.Dungeons): its floors, its pause and its Warden's prize.</summary>
        private static string Holds(DungeonDef d) => d.Id switch
        {
            1 => "9 floors  ·  the Chained Smith on floor 6",
            2 => "2 levels, 6 floors  ·  the Silkmother's Khan's Alloy",
            _ => $"{d.Floors} floors  ·  a rune lock on floor {d.SmithFloor}  ·  Master's Needles",
        };

        /// <summary>The dungeon cards: keys left (shared), locked until their stage, or CONTINUE where a run waits.</summary>
        private void UpdateDungeon(PlayerSession session)
        {
            bool online = _root.Server.Online;
            bool levelOpen = _root.Unlocked(Feature.Dungeons);
            int keys = _root.Server.DungeonRunsLeft;
            int waiting = online && _root.Server.DungeonRunAtSmith != 0 ? _root.Server.DungeonPausedId : 0;
            for (int d = 0; d < Dungeons.All.Length; d++)
            {
                DungeonDef dungeon = Dungeons.All[d];
                Row row = _dungeonRows[d];
                Ui.SetPicture(row.Picture, "Thumbs/Dungeon" + new string(System.Array.FindAll(dungeon.Name.Replace("The ", "").ToCharArray(), char.IsLetter)));
                row.Name.text = dungeon.Name;
                bool here = waiting == dungeon.Id;
                bool unlocked = session.HighestStageCleared >= dungeon.UnlockStage && (levelOpen || here);
                row.Label.text = !online ? "Dungeons need the server."
                    : !levelOpen && !here ? $"Opens at level {Unlocks.Level(Feature.Dungeons)}"
                    : !unlocked ? $"Clear {Content.StageName(dungeon.UnlockStage)} to open"
                    : here ? (dungeon.Pause == DungeonPause.RuneLock ? $"The rune lock waits on floor {dungeon.SmithFloor}." : $"The Chained Smith is waiting on floor {dungeon.SmithFloor}.")
                    : $"Dungeon  ·  {Holds(dungeon)}  ·  keys today {keys}/{Dungeons.FreeRunsPerDay}";
                row.Name.color = unlocked ? Palette.Parchment : Palette.Muted;
                row.Picture.color = unlocked ? Color.white : new Color(0.45f, 0.45f, 0.5f);
                row.ButtonLabel.text = here ? "CONTINUE" : !levelOpen ? "LV " + Unlocks.Level(Feature.Dungeons) : !unlocked ? "LOCKED" : waiting != 0 ? "RUN WAITING" : keys <= 0 ? "NO KEYS" : "ENTER";
                row.ButtonImage.color = here ? Palette.Alloy : unlocked && keys > 0 && waiting == 0 ? Palette.Danger : Palette.ButtonIdle;
                row.Button.interactable = online && unlocked && (here || (waiting == 0 && keys > 0)) && !_root.Replaying && !_root.PushBusy;
            }
        }

        public void Open()
        {
            _message.text = "";
            _canvas.SetActive(true);
            // The tower's card shows the week's best: asked again when it is a minute old.
            if (_root.Server.Online && _root.Unlocked(Feature.Tower) && Time.realtimeSinceStartup - _root.Server.TowerAt > 60f)
                StartCoroutine(_root.Server.FetchTower((_, _) => { }));
        }

        private void OpenTower()
        {
            if (!_root.Unlocked(Feature.Tower)) { _message.text = Unlocks.Locked(Feature.Tower); return; }
            _canvas.SetActive(false);
            _root.TowerScreen.Open();
        }

        /// <summary>The Endless Tower's card: the week's best floor and the climbs left, or the level it opens at.</summary>
        private void UpdateTower()
        {
            bool online = _root.Server.Online, open = _root.Unlocked(Feature.Tower);
            Net.ServerLink.TowerDto tower = online ? _root.Server.Tower : null;
            Ui.SetPicture(_towerRow.Picture, "Thumbs/Tower");
            _towerRow.Name.text = Tower.Name;
            _towerRow.Label.text = !online ? "The tower needs the server."
                : !open ? $"Opens at level {Unlocks.Level(Feature.Tower)}"
                : tower == null ? "Climb as high as you can: every floor harder, a weekly ladder"
                : (tower.best > 0 ? $"Best this week: floor {tower.best}" : "No climb yet this week") + $"  ·  climbs today {tower.climbsLeft}/{tower.climbsPerDay}";
            _towerRow.Name.color = open ? Palette.Parchment : Palette.Muted;
            _towerRow.Picture.color = open ? Color.white : new Color(0.45f, 0.45f, 0.5f);
            _towerRow.ButtonLabel.text = open ? "OPEN" : "LV " + Unlocks.Level(Feature.Tower);
            _towerRow.ButtonImage.color = open && online ? Palette.Danger : Palette.ButtonIdle;
            _towerRow.Button.interactable = online && !_root.Replaying && !_root.PushBusy;
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

            UpdateDungeon(session);
            UpdateTower();
            UpdateTown();
            UpdateRiver();

            // Zones: campaign farm spot first, then every zone in content order.
            var entries = new List<(int Id, string Name, string Text, bool Unlocked)>();
            int campaign = session.HighestStageCleared > 0 ? session.HighestStageCleared : 1;
            entries.Add((campaign, Content.StageName(campaign), $"Campaign  ·  stages 1-{Content.TotalStages}  ·  balanced drops", true));
            foreach (ZoneDef z in Content.Zones)
            {
                bool unlocked = Content.IsUnlocked(z.Id, session.HighestStageCleared);
                string kind = z.Type == ZoneType.HuntingGround ? "Hunting Ground" : z.Type == ZoneType.KorstoneField ? "Korstone Field" : "Commander Ground";
                string flags = (z.Pvp ? "  ·  PvP" : "") + (z.OfflineAllowed ? "" : "  ·  no offline");
                string text = unlocked ? $"{kind} T{z.Tier}  ·  Lv {z.LevelMin}-{z.LevelMax}{flags}" : $"Clear campaign stage {z.UnlockStage} to open";
                entries.Add((z.Id, z.Name, text, unlocked));
            }
            for (int i = 0; i < ZoneRows; i++)
            {
                bool has = i < entries.Count;
                Row row = _zoneRows[i];
                row.Back.transform.parent.gameObject.SetActive(has);
                if (!has) continue;
                (int id, string name, string text, bool unlocked) = entries[i];
                row.Id = id;
                Ui.SetPicture(row.Picture, "Thumbs/" + ZoneThumb(id));
                row.Picture.color = unlocked ? Color.white : new Color(0.45f, 0.45f, 0.5f);
                bool parked = session.ParkedStage == id;
                row.Name.text = name;
                row.Name.color = parked ? Palette.Sorn : unlocked ? Palette.Parchment : Palette.Muted;
                row.Label.text = text;
                row.Button.interactable = unlocked && !parked && !_root.Replaying && !_root.PushBusy;
                row.ButtonLabel.text = parked ? "HUNTING NOW" : unlocked ? "HUNT HERE" : "LOCKED";
                row.ButtonImage.color = parked ? new Color(0.55f, 0.42f, 0.12f) : unlocked ? Palette.Safe : Palette.ButtonIdle;
            }

            // Commanders: from the server when online, otherwise always up in local mode.
            bool campOpen = Content.IsUnlocked(Content.GorakWarCamp, session.HighestStageCleared);
            bool commanders = _root.Unlocked(Feature.Commanders);
            float age = Time.realtimeSinceStartup - _root.Server.BossesReceivedAt;
            for (int i = 0; i < BossRows; i++)
            {
                Row row = _bossRows[i];
                BossDef boss = Content.Bosses[i];
                row.Id = boss.Id;
                Ui.SetPicture(row.Picture, "Thumbs/Commander" + new string(System.Array.FindAll(boss.Name.Replace("Warlord ", "").Replace("The ", "").ToCharArray(), char.IsLetter)));
                string status;
                string pool = "";
                bool canFight = campOpen && commanders;
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

                row.Name.text = boss.Name;
                row.Name.color = campOpen && commanders ? Palette.Parchment : Palette.Muted;
                row.ButtonLabel.text = commanders ? "FIGHT" : "LV " + Unlocks.Level(Feature.Commanders);
                row.Label.text = !commanders ? $"Opens at level {Unlocks.Level(Feature.Commanders)}" : campOpen ? $"{ConfirmDialog.Tint(status, canFight ? Palette.Good : Palette.Muted)}  ·  {Mechanic(boss.Mechanic)}{pool}" : "Clear campaign stage 5 to open";
                row.Button.interactable = canFight && !_root.Replaying && !_root.PushBusy;
            }
        }

        /// <summary>Resources/Thumbs picture for a campaign stage (below 100) or a zone id.</summary>
        private static string ZoneThumb(int id) => id switch
        {
            // Campaign maps: the Oathfields, then Gorak Pass, the Salt Sea and Whitefang Range on their grounds' pictures.
            < 100 => Content.MapOfStage(id).Id switch { 2 => "ZoneWarCamp", 3 => "ZoneSaltFlats", 4 => "ZoneFrostPasture", _ => "ZoneCampaign" },
            Content.EmberSteppe => "ZoneEmberSteppe",
            Content.SaltFlats => "ZoneSaltFlats",
            Content.FrostPasture => "ZoneFrostPasture",
            Content.GorakWarCamp => "ZoneWarCamp",
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
