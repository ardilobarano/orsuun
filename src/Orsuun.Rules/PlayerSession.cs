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
        public static HeroStats FromEquipment(IEnumerable<ItemState> equipped, int level)
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

            return new HeroStats
            {
                Attack = attack,
                Defense = defense,
                MaxHp = maxHp,
                CritChanceBp = critBp,
                CritMultiplierPercent = critMult,
                BeastDamagePercent = beast,
                EvasionBp = Math.Min(evasionBp, 5000),
                AttackIntervalTicks = Math.Max(6, 12 * 100 / (100 + haste)),
                CommanderDamageTakenPercent = Math.Max(40, 100 - warding),
            };
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
            Lane = new LaneSim(stage ?? Content.Stage(1), Hero, SkillDef.VanguardWrath(), Inventory, rng);
        }

        public Inventory Inventory { get; }
        public LaneSim Lane { get; private set; }
        public int WeaponsBroken { get; private set; }
        /// <summary>Highest stage cleared by a push; the next one is the push target.</summary>
        public int HighestStageCleared { get; private set; }
        /// <summary>Campaign stage number or zone id the farm lane is parked in.</summary>
        public int ParkedStage => Lane.Stage.StageNumber;
        public int PushTarget => Math.Min(Content.TotalStages, HighestStageCleared + 1);
        public int Level => Inventory.Level;

        /// <summary>The equipped weapon. The Forge and the Turnstone act on it; an Oathbreak replaces it.</summary>
        public ItemState Weapon => _equipped[(int)EquipSlot.Weapon]!;
        public ItemState? Equipped(EquipSlot slot) => _equipped[(int)slot];
        public IEnumerable<ItemState> Equipment { get { foreach (ItemState? i in _equipped) if (i != null) yield return i; } }
        public HeroStats Hero => HeroFactory.FromEquipment(Equipment, Level);
        public EtchingPool Pool => EtchingPool.For(EquipSlot.Weapon);

        public long ForgeCost => Weapon.UpgradeLevel >= ItemState.MaxUpgradeLevel ? 0 : ForgeRules.Cost(Weapon.ItemLevel, Weapon.UpgradeLevel);
        public int ForgeMaterials => Weapon.UpgradeLevel >= ItemState.MaxUpgradeLevel ? 0 : ForgeRules.MaterialsNeeded(Weapon.UpgradeLevel + 1);
        public int ForgeChanceBp(ForgeMethod method) => _forge.ChanceBp(Weapon, method);

        /// <summary>Null when the attempt may run, otherwise the reason to show the player.</summary>
        public string? ForgeBlocker(ForgeMethod method)
        {
            if (Weapon.UpgradeLevel >= ItemState.MaxUpgradeLevel) return "Already +9";
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

            ForgeResult result = _forge.Attempt(Weapon, method, _rng);
            if (result.Outcome == ForgeOutcome.Oathbreak)
            {
                WeaponsBroken++;
                _equipped[(int)EquipSlot.Weapon] = NewWeapon();
            }

            Lane.SetHero(Hero);
            return result;
        }

        public string? TurnBlocker()
        {
            int cost = Weapon.LockedEtchingIndex >= 0 ? 2 : 1;
            return Inventory.Turnstones >= cost ? null : "Not enough Turnstones";
        }

        public void Turn()
        {
            string? blocker = TurnBlocker();
            if (blocker != null) throw new InvalidOperationException(blocker);

            Inventory.Turnstones -= _etchings.Turn(Weapon, Pool, _rng);
            Lane.SetHero(Hero);
        }

        public string? SocketBlocker(ItemState item, int socketIndex, ShardType type, int rank) =>
            _sockets.InsertBlocker(item, socketIndex, type, rank, Inventory);

        /// <summary>Sets a shard on an equipped item; 30% of the time it dies in the socket.</summary>
        public bool SetShard(ItemState item, int socketIndex, ShardType type, int rank)
        {
            bool ok = _sockets.TryInsert(item, socketIndex, type, rank, Inventory, _rng);
            Lane.SetHero(Hero);
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
            Lane.SetHero(Hero);
        }

        /// <summary>Moves the live lane to an unlocked campaign stage or zone.</summary>
        public void Park(int parkId)
        {
            if (!Content.IsUnlocked(parkId, HighestStageCleared)) throw new InvalidOperationException("Not unlocked yet.");
            Lane = new LaneSim(Content.Stage(parkId), Hero, SkillDef.VanguardWrath(), Inventory, _rng);
            Lane.AutoCast[1] = true;
        }

        /// <summary>Local Commander fight: damage, rank among simulated rivals and the chest. The server does the same.</summary>
        public BossRunResult FightBoss(BossDef boss, out ulong seed, out int rank, out string chest)
        {
            seed = ((ulong)_rng.NextInt(int.MaxValue) << 31) ^ (ulong)_rng.NextInt(int.MaxValue);
            BossRunResult result = BossRun.Simulate(boss, Hero, Inventory, seed);
            rank = BossRun.Rank(result.Damage, boss, _rng);
            chest = HuntYield.LootCommander(boss, rank, Inventory, _rng);
            Lane.SetHero(Hero);
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
            WeaponsBroken = weaponsBroken;
            HighestStageCleared = highestStageCleared;
            if (parkedStage != ParkedStage && Content.IsUnlocked(parkedStage, HighestStageCleared)) Park(parkedStage);
            Lane.SetHero(Hero);
        }

        /// <summary>Grey-box shortcut: a fresh Rare weapon arrives with 5 etchings so the Turnstone is usable at once.</summary>
        private ItemState NewWeapon()
        {
            var weapon = new ItemState(StarterItemLevel, Rarity.Rare);
            while (weapon.Etchings.Count < ItemState.MaxEtchings)
            {
                NeedleKind needle = weapon.Etchings.Count == ItemState.MaxEtchings - 1 ? NeedleKind.MastersNeedle : NeedleKind.EtchingNeedle;
                _etchings.TryAdd(weapon, Pool, needle, _rng);
            }
            return weapon;
        }
    }
}
