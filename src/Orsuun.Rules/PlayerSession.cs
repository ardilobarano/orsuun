#nullable enable
using System;
using System.Collections.Generic;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    public sealed class Inventory
    {
        public long Sorn { get; set; }
        public int Potions { get; set; }
        public int Materials { get; set; }
        public int ScrollsOfMercy { get; set; }
        public int KhansAlloys { get; set; }
        public int AnvilWards { get; set; }
        public int Turnstones { get; set; }
        public int EtchingNeedles { get; set; }
        public int SummoningMarkers { get; set; }
        public long Xp { get; set; }
        /// <summary>Korshards held, by rank index (Trooper .. Guard of the Khan). Sockets come in the next step.</summary>
        public int[] Korshards { get; } = new int[5];
        /// <summary>Cosmetics owned; only Commanders drop them.</summary>
        public List<string> Skins { get; } = new List<string>();
        /// <summary>Gear that dropped and has not been placed anywhere yet.</summary>
        public List<ItemState> Loot { get; } = new List<ItemState>();

        public int Level => Content.LevelFor(Xp);

        public void CopyCurrenciesFrom(Inventory other)
        {
            Sorn = other.Sorn; Potions = other.Potions; Materials = other.Materials; ScrollsOfMercy = other.ScrollsOfMercy;
            KhansAlloys = other.KhansAlloys; AnvilWards = other.AnvilWards; Turnstones = other.Turnstones;
            EtchingNeedles = other.EtchingNeedles; SummoningMarkers = other.SummoningMarkers; Xp = other.Xp;
            Array.Copy(other.Korshards, Korshards, Korshards.Length);
            Skins.Clear();
            Skins.AddRange(other.Skins);
        }
    }

    public static class HeroFactory
    {
        public static HeroStats FromWeapon(ItemState weapon) => FromEquipment(new[] { weapon }, 1);

        /// <summary>
        /// Stat model: the weapon sets attack, body pieces set defense and HP, accessories add a little of both.
        /// Every piece scales with its upgrade level and rarity; etchings add on top; each character level adds
        /// +2 attack and +40 HP so Hunting Grounds pay off in power, not only in sorn.
        /// </summary>
        public static HeroStats FromEquipment(IEnumerable<ItemState> equipped, int level, HeroClass cls = HeroClass.Vanguard)
        {
            long attack = 20 + 2L * (level - 1), defense = 0, maxHp = 2000 + 40L * (level - 1);
            int critBp = 500, critMult = 200, beast = 0, evasionBp = 0, haste = 0, warding = 0;

            foreach (ItemState item in equipped)
            {
                if (item.Destroyed) continue;

                foreach (Socket socket in item.Sockets)
                {
                    if (socket.Type == null) continue;
                    int v = SocketRules.Value(socket.Type.Value, socket.Rank);
                    switch (socket.Type.Value)
                    {
                        case ShardType.BeastSlayer: beast += v; break;
                        case ShardType.Piercer: attack += v; break;
                        case ShardType.Deathdealer: critMult += v; break;
                        case ShardType.Bulwark: defense += v; break;
                        case ShardType.Vigor: maxHp += v; break;
                        case ShardType.Evasion: evasionBp += v; break;
                        case ShardType.Haste: haste += v; break;
                        case ShardType.Warding: warding += v; break;
                    }
                }
                long scale = ForgeRules.StatPercent(item.UpgradeLevel) * RarityPercent(item.Rarity);
                switch (item.Slot)
                {
                    case EquipSlot.Weapon: attack += (20 + item.ItemLevel * 4) * scale / 10000; break;
                    case EquipSlot.Armor: defense += (6 + item.ItemLevel) * scale / 10000; maxHp += (200 + item.ItemLevel * 20) * scale / 10000; break;
                    case EquipSlot.Helmet: case EquipSlot.Shield: case EquipSlot.Shoes: defense += (3 + item.ItemLevel / 2) * scale / 10000; maxHp += (80 + item.ItemLevel * 8) * scale / 10000; break;
                    default: attack += (2 + item.ItemLevel / 3) * scale / 10000; maxHp += (50 + item.ItemLevel * 5) * scale / 10000; break;
                }

                foreach (Etching e in item.Etchings)
                {
                    if (item.Slot == EquipSlot.Weapon)
                    {
                        switch (e.EntryId)
                        {
                            case WeaponEtchingIds.AttackValue: attack += e.Value; break;
                            case WeaponEtchingIds.Str: attack += e.Value * 2; break;
                            case WeaponEtchingIds.Vit: maxHp += e.Value * 40; break;
                            case WeaponEtchingIds.Critical: critBp += e.Value * 100; break;
                        }
                    }
                    else
                    {
                        switch (e.EntryId)
                        {
                            case ArmorEtchingIds.MaxHp: maxHp += e.Value; break;
                            case ArmorEtchingIds.Defense: defense += e.Value; break;
                            case ArmorEtchingIds.Str: attack += e.Value * 2; break;
                            case ArmorEtchingIds.Vit: maxHp += e.Value * 40; break;
                        }
                    }
                }
            }

            ClassShape shape = ClassShape.For(cls);
            attack = attack * shape.AttackPercent / 100;
            defense = defense * shape.DefensePercent / 100;
            maxHp = maxHp * shape.HpPercent / 100;
            critBp += shape.CritBonusBp;
            int interval = shape.AttackIntervalTicks, weakPoint = shape.WeakPointPercent;

            return new HeroStats
            {
                Class = cls,
                WeakPointPercent = weakPoint,
                Attack = attack,
                Defense = defense,
                MaxHp = maxHp,
                CritChanceBp = critBp,
                CritMultiplierPercent = critMult,
                BeastDamagePercent = beast,
                EvasionBp = Math.Min(evasionBp, 5000),
                AttackIntervalTicks = Math.Max(6, interval * 100 / (100 + haste)),
                CommanderDamageTakenPercent = Math.Max(40, 100 - warding),
            };
        }

        /// <summary>How a class bends the shared stat model (percent of attack, defense and HP, crit, swing speed, weak point).</summary>
        private readonly struct ClassShape
        {
            public ClassShape(int attack, int defense, int hp, int critBonusBp, int interval, int weakPoint)
            {
                AttackPercent = attack;
                DefensePercent = defense;
                HpPercent = hp;
                CritBonusBp = critBonusBp;
                AttackIntervalTicks = interval;
                WeakPointPercent = weakPoint;
            }

            public int AttackPercent { get; }
            public int DefensePercent { get; }
            public int HpPercent { get; }
            public int CritBonusBp { get; }
            public int AttackIntervalTicks { get; }
            public int WeakPointPercent { get; }

            public static ClassShape For(HeroClass cls)
            {
                switch (cls)
                {
                    // Light and quick: faster blows and more crits, less weight per hit, a thinner hide; the
                    // assassin's Heartseeker finds the seam in the stone.
                    case HeroClass.Kestrel: return new ClassShape(90, 80, 85, 700, 10, 900);
                    // Glass cannon: hits hard but slowly between casts, paper defense; Void Lance splits stone.
                    case HeroClass.Wraithsworn: return new ClassShape(105, 60, 80, 0, 13, 700);
                    // Storm rhythm: sturdier and critier, a little lighter per blow.
                    case HeroClass.Drumcaller: return new ClassShape(95, 110, 105, 500, 12, 700);
                    default: return new ClassShape(100, 100, 100, 0, 12, LaneSim.AimedWeakPointPercent);
                }
            }
        }

        private static int RarityPercent(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return 105;
                case Rarity.Rare: return 110;
                case Rarity.Epic: return 118;
                case Rarity.Legendary: return 125;
                default: return 100;
            }
        }
    }

    /// <summary>One finished online farm loop, as the heartbeat reports it (ActivePlay.Verify replays it).</summary>
    public sealed class LoopReport
    {
        public LoopReport(int loop, int ticks, int potions, bool[] autoCast, List<CastInput> casts)
        {
            Loop = loop;
            Ticks = ticks;
            Potions = potions;
            AutoCast = autoCast;
            Casts = casts;
        }

        public int Loop { get; }
        public int Ticks { get; }
        public int Potions { get; }
        public bool[] AutoCast { get; }
        public List<CastInput> Casts { get; }
    }

    /// <summary>
    /// One player's state: inventory, equipment, stage progress and the live lane. It owns the payments the
    /// rule services leave to the caller, so the same checks run on the server.
    /// </summary>
    public sealed class PlayerSession
    {
        public const int StarterItemLevel = 10;

        private readonly IRandom _rng;
        private readonly ForgeService _forge = new ForgeService();
        private readonly EtchingService _etchings = new EtchingService();
        private readonly SocketService _sockets = new SocketService();
        private readonly ItemState?[] _equipped = new ItemState?[8];

        public PlayerSession(IRandom rng, StageConfig? stage = null)
        {
            _rng = rng;
            Inventory = new Inventory { Sorn = 20_000, Potions = 30, ScrollsOfMercy = 2, Turnstones = 5 };
            _equipped[(int)EquipSlot.Weapon] = NewWeapon();
            Lane = new LaneSim(stage ?? Content.Stage(1), Hero, SkillDef.For(Class), Inventory, rng);
        }

        public Inventory Inventory { get; }
        public LaneSim Lane { get; private set; }
        public int WeaponsBroken { get; private set; }
        /// <summary>Every item lost to an Oathbreak, weapons included.</summary>
        public int ItemsBroken { get; private set; }
        /// <summary>Highest stage cleared by a push; the next one is the push target.</summary>
        public int HighestStageCleared { get; private set; }
        /// <summary>Campaign stage number or zone id the farm lane is parked in.</summary>
        public int ParkedStage => Lane.Stage.StageNumber;
        public int PushTarget => Math.Min(Content.TotalStages, HighestStageCleared + 1);
        public int Level => Inventory.Level;

        /// <summary>The equipped weapon.</summary>
        public ItemState Weapon => _equipped[(int)EquipSlot.Weapon]!;

        /// <summary>
        /// Every item follows the weapon's rules (owner decision, 23 Sep 2026): the Forge and the Turnstone act on the
        /// item on the anvil, whichever slot it is. Since 24 Sep 2026 (owner) that can be any owned piece, worn or in
        /// the bag. An Oathbreak on a worn piece replaces it with a starter for that slot; a bag piece is simply gone.
        /// </summary>
        public ItemState OnAnvil => _anvilItem != null && Owns(_anvilItem) ? _anvilItem : Weapon;
        /// <summary>Slot type of the piece on the anvil.</summary>
        public EquipSlot AnvilSlot => OnAnvil.Slot;
        /// <summary>True when the piece on the anvil is worn (its Oathbreak leaves a starter behind).</summary>
        public bool AnvilWorn => _equipped[(int)OnAnvil.Slot] == OnAnvil;

        private ItemState? _anvilItem;

        /// <summary>Puts the equipped item in <paramref name="slot"/> on the anvil.</summary>
        public void PutOnAnvil(EquipSlot slot)
        {
            _anvilItem = _equipped[(int)slot] ?? throw new InvalidOperationException("Nothing is equipped in that slot.");
        }

        /// <summary>Puts any owned piece on the anvil, worn or from the loot bag.</summary>
        public void PutOnAnvil(ItemState item)
        {
            if (!Owns(item)) throw new InvalidOperationException("You do not own that item.");
            _anvilItem = item;
        }

        private bool Owns(ItemState item) => !item.Destroyed && (_equipped[(int)item.Slot] == item || Inventory.Loot.Contains(item));
        public ItemState? Equipped(EquipSlot slot) => _equipped[(int)slot];
        public IEnumerable<ItemState> Equipment { get { foreach (ItemState? i in _equipped) if (i != null) yield return i; } }
        public HeroStats Hero => HeroFactory.FromEquipment(Equipment, Level, Class);

        /// <summary>The class being played. Changing it rebuilds the farm lane with that class's kit.</summary>
        public HeroClass Class { get; private set; } = HeroClass.Vanguard;

        public void SetClass(HeroClass cls)
        {
            if (cls == Class) return;
            Class = cls;
            NewFarmLane(ParkedStage);
        }
        /// <summary>Etching pool of the item on the anvil (weapons and armour roll from different pools).</summary>
        public EtchingPool Pool => EtchingPool.For(OnAnvil.Slot);

        public long ForgeCost => OnAnvil.UpgradeLevel >= ItemState.MaxUpgradeLevel ? 0 : ForgeRules.Cost(OnAnvil.ItemLevel, OnAnvil.UpgradeLevel);
        public int ForgeMaterials => OnAnvil.UpgradeLevel >= ItemState.MaxUpgradeLevel ? 0 : ForgeRules.MaterialsNeeded(OnAnvil.UpgradeLevel + 1);
        public int ForgeChanceBp(ForgeMethod method) => _forge.ChanceBp(OnAnvil, method);

        /// <summary>Null when the attempt may run, otherwise the reason to show the player.</summary>
        public string? ForgeBlocker(ForgeMethod method)
        {
            if (OnAnvil.UpgradeLevel >= ItemState.MaxUpgradeLevel) return "Already +9";
            if (Inventory.Sorn < ForgeCost) return "Not enough sorn";
            if (Inventory.Materials < ForgeMaterials) return "Not enough " + Lane.Stage.MaterialName;
            switch (method)
            {
                case ForgeMethod.ScrollOfMercy: return Inventory.ScrollsOfMercy > 0 ? null : "No Scroll of Mercy";
                case ForgeMethod.KhansAlloy: return Inventory.KhansAlloys > 0 ? null : "No Khan's Alloy";
                case ForgeMethod.AnvilWard: return Inventory.AnvilWards > 0 ? null : "No Anvil Ward";
                case ForgeMethod.ChainedSmith: return "Only in the Hollow Spire";
                default: return null;
            }
        }

        public ForgeResult Forge(ForgeMethod method)
        {
            string? blocker = ForgeBlocker(method);
            if (blocker != null) throw new InvalidOperationException(blocker);

            Inventory.Sorn -= ForgeCost;
            Inventory.Materials -= ForgeMaterials;
            switch (method)
            {
                case ForgeMethod.ScrollOfMercy: Inventory.ScrollsOfMercy--; break;
                case ForgeMethod.KhansAlloy: Inventory.KhansAlloys--; break;
                case ForgeMethod.AnvilWard: Inventory.AnvilWards--; break;
            }

            ItemState item = OnAnvil;
            ForgeResult result = _forge.Attempt(item, method, _rng);
            if (result.Outcome == ForgeOutcome.Oathbreak)
            {
                ItemsBroken++;
                if (item.Slot == EquipSlot.Weapon) WeaponsBroken++;
                if (_equipped[(int)item.Slot] == item) _equipped[(int)item.Slot] = NewStarter(item.Slot);
                else Inventory.Loot.Remove(item);
                _anvilItem = _equipped[(int)item.Slot];
            }

            RefreshHero();
            return result;
        }

        public string? TurnBlocker()
        {
            if (OnAnvil.Etchings.Count == 0) return "No etchings to turn yet";
            int cost = OnAnvil.LockedEtchingIndex >= 0 ? 2 : 1;
            return Inventory.Turnstones >= cost ? null : "Not enough Turnstones";
        }

        public void Turn()
        {
            string? blocker = TurnBlocker();
            if (blocker != null) throw new InvalidOperationException(blocker);

            Inventory.Turnstones -= _etchings.Turn(OnAnvil, Pool, _rng);
            RefreshHero();
        }

        /// <summary>Bulk Turn with an optional stop rule. Returns turns made; stopped says whether the rule hit.</summary>
        public int TurnBulk(int maxTurns, int? stopEntryId, int minTier, out bool stopped) =>
            TurnBulk(maxTurns, stopEntryId.HasValue ? new[] { new TurnTarget(stopEntryId.Value, minTier) } : Array.Empty<TurnTarget>(), out stopped);

        /// <summary>Bulk Turn toward a goal of up to five etchings with tiers (the turning helper).</summary>
        public int TurnBulk(int maxTurns, IReadOnlyList<TurnTarget> targets, out bool stopped)
        {
            string? blocker = TurnBlocker() ?? EtchingService.TargetProblem(OnAnvil, Pool, targets);
            if (blocker != null) throw new InvalidOperationException(blocker);

            _etchings.TurnUntil(OnAnvil, Pool, Inventory, _rng, maxTurns, targets, out int turns, out stopped);
            RefreshHero();
            return turns;
        }

        /// <summary>Re-reads the farm lane's configuration with the current bell applied (call when a bell changes).</summary>
        public void ApplyBell(Bell bell)
        {
            _bell = bell;
            NewFarmLane(ParkedStage);
        }

        public string? SocketBlocker(ItemState item, int socketIndex, ShardType type, int rank) =>
            _sockets.InsertBlocker(item, socketIndex, type, rank, Inventory);

        /// <summary>Sets a shard on an equipped item; 30% of the time it dies in the socket.</summary>
        public bool SetShard(ItemState item, int socketIndex, ShardType type, int rank)
        {
            bool ok = _sockets.TryInsert(item, socketIndex, type, rank, Inventory, _rng);
            RefreshHero();
            return ok;
        }

        public string? ClearSocketBlocker(ItemState item, int socketIndex) => _sockets.ClearBlocker(item, socketIndex, Inventory);

        public void ClearSocket(ItemState item, int socketIndex)
        {
            _sockets.Clear(item, socketIndex, Inventory);
        }

        /// <summary>Equips a piece from the loot list; the previous piece in that slot goes back to loot.</summary>
        public void Equip(ItemState item)
        {
            if (!Inventory.Loot.Remove(item)) throw new InvalidOperationException("Item is not in the loot list.");
            ItemState? previous = _equipped[(int)item.Slot];
            _equipped[(int)item.Slot] = item;
            if (previous != null) Inventory.Loot.Add(previous);
            RefreshHero();
        }

        /// <summary>Moves the live lane to an unlocked campaign stage or zone.</summary>
        public void Park(int parkId)
        {
            if (!Content.IsUnlocked(parkId, HighestStageCleared)) throw new InvalidOperationException("Not unlocked yet.");
            NewFarmLane(parkId);
        }

        private Bell _bell = Bell.None;

        /// <summary>
        /// The farm lane for a stage with the bell in force, keeping the player's auto-cast switches. Online (a lane
        /// seed is set) it restarts the current loop from the seed, so the server can replay it.
        /// </summary>
        private void NewFarmLane(int parkId)
        {
            StageConfig stage = EveningBells.Apply(Content.Stage(parkId), _bell);
            if (_laneSeed.HasValue)
            {
                StartLoop(stage, LaneLoop);
                return;
            }
            bool[] auto = (bool[])Lane.AutoCast.Clone();
            Lane = new LaneSim(stage, Hero, SkillDef.For(Class), Inventory, _rng);
            Array.Copy(auto, Lane.AutoCast, Math.Min(auto.Length, Lane.AutoCast.Length));
        }

        // ---- Active play (online): the farm lane runs one seeded loop at a time, see Rules.Combat.ActivePlay ----

        /// <summary>Loop reports kept for the next heartbeat; older ones are dropped past this.</summary>
        public const int MaxQueuedReports = 20;

        private ulong? _laneSeed;
        private bool[] _loopAuto = new bool[0];
        private int _loopPotions;
        private string _loopHero = "";
        private bool _loopDirty;
        private List<CastInput> _loopCasts = new List<CastInput>();
        private readonly List<LoopReport> _reports = new List<LoopReport>();

        /// <summary>Loop number the farm lane is playing (online only).</summary>
        public int LaneLoop { get; private set; }
        public bool LaneSeeded => _laneSeed.HasValue;
        public IReadOnlyList<LoopReport> PendingReports => _reports;

        /// <summary>
        /// The server's lane seed and the next loop it expects. A new seed (first login, a park) restarts the lane on
        /// that loop; the same seed again changes nothing, the client keeps counting its own loops.
        /// </summary>
        public void SetLaneSeed(ulong seed, int loop)
        {
            if (_laneSeed == seed) return;
            _laneSeed = seed;
            _reports.Clear();
            StartLoop(Lane.Stage, loop);
        }

        private void StartLoop(StageConfig stage, int loop)
        {
            bool[] auto = (bool[])Lane.AutoCast.Clone();
            HeroStats hero = Hero;
            Lane = ActivePlay.NewLoop(stage, hero, SkillDef.For(Class), Inventory, _laneSeed!.Value, loop);
            Array.Copy(auto, Lane.AutoCast, Math.Min(auto.Length, Lane.AutoCast.Length));
            LaneLoop = loop;
            _loopAuto = (bool[])Lane.AutoCast.Clone();
            _loopPotions = Inventory.Potions;
            _loopHero = Fingerprint(hero);
            _loopDirty = false;
            _loopCasts = new List<CastInput>();
        }

        /// <summary>A tapped skill: aimed, and recorded with its tick for the loop report.</summary>
        public bool Cast(int skill)
        {
            if (!Lane.TryCast(skill)) return false;
            if (_laneSeed.HasValue) _loopCasts.Add(new CastInput(Lane.CurrentTick, skill));
            return true;
        }

        /// <summary>Flips a skill's auto-cast. Mid-loop the replay cannot follow it, so that loop is not reported.</summary>
        public void ToggleAutoCast(int skill)
        {
            Lane.AutoCast[skill] = !Lane.AutoCast[skill];
            _loopDirty = true;
        }

        /// <summary>
        /// Call after every farm-lane tick. Online, when the lane finishes an encounter cycle the loop is queued for the
        /// next heartbeat (unless something the replay cannot see changed mid-loop) and the next loop starts. Returns
        /// true when Lane was replaced.
        /// </summary>
        public bool CloseLoopIfDone()
        {
            if (!_laneSeed.HasValue || Lane.Cycles == 0) return false;
            if (!_loopDirty && Lane.CurrentTick <= ActivePlay.MaxLoopTicks)
            {
                _reports.Add(new LoopReport(LaneLoop, Lane.CurrentTick, _loopPotions, _loopAuto, _loopCasts));
                if (_reports.Count > MaxQueuedReports) _reports.RemoveAt(0);
            }
            StartLoop(Lane.Stage, LaneLoop + 1);
            return true;
        }

        /// <summary>Hands the queued loop reports to the heartbeat. Put them back with RequeueReports if it fails.</summary>
        public List<LoopReport> TakeReports()
        {
            var taken = new List<LoopReport>(_reports);
            _reports.Clear();
            return taken;
        }

        public void RequeueReports(List<LoopReport> reports)
        {
            _reports.InsertRange(0, reports);
            while (_reports.Count > MaxQueuedReports) _reports.RemoveAt(0);
        }

        /// <summary>Pushes the current hero into the lane; a real change mid-loop means the replay would differ.</summary>
        private void RefreshHero()
        {
            HeroStats hero = Hero;
            if (_laneSeed.HasValue && Fingerprint(hero) != _loopHero) _loopDirty = true;
            Lane.SetHero(hero);
        }

        private static string Fingerprint(HeroStats h) =>
            h.Class + "/" + h.WeakPointPercent + "/" + h.MaxHp + "/" + h.Attack + "/" + h.Defense + "/" + h.AttackIntervalTicks + "/" + h.CritChanceBp + "/" + h.CritMultiplierPercent
            + "/" + h.BeastDamagePercent + "/" + h.EvasionBp + "/" + h.CommanderDamageTakenPercent;

        /// <summary>Local Commander fight: damage, rank among simulated rivals and the chest. The server does the same.</summary>
        public BossRunResult FightBoss(BossDef boss, out ulong seed, out int rank, out string chest)
        {
            seed = ((ulong)_rng.NextInt(int.MaxValue) << 31) ^ (ulong)_rng.NextInt(int.MaxValue);
            BossRunResult result = BossRun.Simulate(boss, Hero, Inventory, seed);
            rank = BossRun.Rank(result.Damage, boss, _rng);
            chest = HuntYield.LootCommander(boss, rank, Inventory, _rng);
            RefreshHero();
            return result;
        }

        /// <summary>Local push: decides the next stage with a seed drawn here. The server does the same with its own seed.</summary>
        public StageRunResult Push(out ulong seed)
        {
            seed = ((ulong)_rng.NextInt(int.MaxValue) << 31) ^ (ulong)_rng.NextInt(int.MaxValue);
            StageRunResult result = StageRun.Simulate(Content.Stage(PushTarget), Hero, Inventory, seed);
            if (result.Cleared) HighestStageCleared = Math.Max(HighestStageCleared, PushTarget);
            return result;
        }

        /// <summary>
        /// Adopts authoritative state from the server. The local lane keeps its own loot between heartbeats
        /// purely for display; whatever the server says replaces it.
        /// </summary>
        public void ApplyRemote(Inventory inventory, IEnumerable<ItemState> equipped, int weaponsBroken, int highestStageCleared, int parkedStage)
        {
            Inventory.CopyCurrenciesFrom(inventory);
            Inventory.Loot.Clear();
            Inventory.Loot.AddRange(inventory.Loot);
            Array.Clear(_equipped, 0, _equipped.Length);
            foreach (ItemState item in equipped) _equipped[(int)item.Slot] = item;
            // Every item object is new: the caller re-anchors the anvil (ServerLink matches it by server id).
            _anvilItem = null;
            WeaponsBroken = weaponsBroken;
            HighestStageCleared = highestStageCleared;
            if (parkedStage != ParkedStage && Content.IsUnlocked(parkedStage, HighestStageCleared)) Park(parkedStage);
            RefreshHero();
        }

        private ItemState NewWeapon() => NewStarter(EquipSlot.Weapon);

        /// <summary>Grey-box shortcut: a fresh Rare piece arrives with 5 etchings so the Turnstone is usable at once.</summary>
        private ItemState NewStarter(EquipSlot slot)
        {
            var item = new ItemState(StarterItemLevel, Rarity.Rare, slot);
            EtchingPool pool = EtchingPool.For(slot);
            while (item.Etchings.Count < ItemState.MaxEtchings)
            {
                NeedleKind needle = item.Etchings.Count == ItemState.MaxEtchings - 1 ? NeedleKind.MastersNeedle : NeedleKind.EtchingNeedle;
                _etchings.TryAdd(item, pool, needle, _rng);
            }
            return item;
        }
    }
}
