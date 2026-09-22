#nullable enable
using System;
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
    }

    public static class HeroFactory
    {
        /// <summary>Grey-box stat model: the weapon is the hero. Upgrade level and etchings drive kill speed.</summary>
        public static HeroStats FromWeapon(ItemState weapon)
        {
            long weaponBase = 40 + weapon.ItemLevel * 4;
            long attack = weaponBase * ForgeRules.StatPercent(weapon.UpgradeLevel) / 100;
            long maxHp = 2000;
            int critBp = 500;

            foreach (Etching e in weapon.Etchings)
            {
                switch (e.EntryId)
                {
                    case WeaponEtchingIds.AttackValue: attack += e.Value; break;
                    case WeaponEtchingIds.Str: attack += e.Value * 2; break;
                    case WeaponEtchingIds.Vit: maxHp += e.Value * 40; break;
                    case WeaponEtchingIds.Critical: critBp += e.Value * 100; break;
                }
            }

            return new HeroStats { Attack = attack, MaxHp = maxHp, CritChanceBp = critBp };
        }
    }

    /// <summary>
    /// One player's grey-box state: inventory, weapon and lane. It owns the payments the rule services
    /// leave to the caller, so the same checks run on the server later.
    /// </summary>
    public sealed class PlayerSession
    {
        public const int StarterItemLevel = 10;

        private readonly IRandom _rng;
        private readonly ForgeService _forge = new ForgeService();
        private readonly EtchingService _etchings = new EtchingService();
        private readonly EtchingPool _pool = EtchingPool.Weapon();

        public PlayerSession(IRandom rng, StageConfig? stage = null)
        {
            _rng = rng;
            Inventory = new Inventory { Sorn = 20_000, Potions = 30, ScrollsOfMercy = 2, Turnstones = 5 };
            Weapon = NewWeapon();
            Lane = new LaneSim(stage ?? new StageConfig(), HeroFactory.FromWeapon(Weapon), SkillDef.VanguardWrath(), Inventory, rng);
        }

        public Inventory Inventory { get; }
        public ItemState Weapon { get; private set; }
        public LaneSim Lane { get; }
        public EtchingPool Pool => _pool;
        public int WeaponsBroken { get; private set; }

        public long ForgeCost => Weapon.UpgradeLevel >= ItemState.MaxUpgradeLevel ? 0 : ForgeRules.Cost(Weapon.ItemLevel, Weapon.UpgradeLevel);
        public int ForgeMaterials => Weapon.UpgradeLevel >= ItemState.MaxUpgradeLevel ? 0 : ForgeRules.MaterialsNeeded(Weapon.UpgradeLevel + 1);
        public int ForgeChanceBp(ForgeMethod method) => _forge.ChanceBp(Weapon, method);

        /// <summary>Null when the attempt may run, otherwise the reason to show the player.</summary>
        public string? ForgeBlocker(ForgeMethod method)
        {
            if (Weapon.UpgradeLevel >= ItemState.MaxUpgradeLevel) return "Already +9";
            if (Inventory.Sorn < ForgeCost) return "Not enough sorn";
            if (Inventory.Materials < ForgeMaterials) return "Not enough Wolf Sinew";
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
                Weapon = NewWeapon();
            }

            Lane.SetHero(HeroFactory.FromWeapon(Weapon));
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

            Inventory.Turnstones -= _etchings.Turn(Weapon, _pool, _rng);
            Lane.SetHero(HeroFactory.FromWeapon(Weapon));
        }

        /// <summary>Grey-box shortcut: a fresh Rare weapon arrives with 5 etchings so the Turnstone is usable at once.</summary>
        private ItemState NewWeapon()
        {
            var weapon = new ItemState(StarterItemLevel, Rarity.Rare);
            while (weapon.Etchings.Count < ItemState.MaxEtchings)
            {
                NeedleKind needle = weapon.Etchings.Count == ItemState.MaxEtchings - 1 ? NeedleKind.MastersNeedle : NeedleKind.EtchingNeedle;
                _etchings.TryAdd(weapon, _pool, needle, _rng);
            }
            return weapon;
        }
    }
}
