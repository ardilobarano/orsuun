#nullable enable
using System;

namespace Orsuun.Rules
{
    public enum ForgeMethod
    {
        /// <summary>Sorn only. Failure: -1 level up to +3, Oathbreak from the +4 attempt.</summary>
        ForgeAlone,
        /// <summary>Failure costs one level instead of the item.</summary>
        ScrollOfMercy,
        /// <summary>+10 points of success, failure costs one level.</summary>
        KhansAlloy,
        /// <summary>Hollow Spire floor 6: +10 points of success, same failure rule as ForgeAlone.</summary>
        ChainedSmith,
        /// <summary>Failure keeps the level.</summary>
        AnvilWard,
    }

    public enum ForgeOutcome
    {
        Success,
        LevelLost,
        LevelKept,
        Oathbreak,
    }

    public readonly struct ForgeResult
    {
        public ForgeResult(ForgeOutcome outcome, int chanceBp, int levelBefore, int levelAfter)
        {
            Outcome = outcome;
            ChanceBp = chanceBp;
            LevelBefore = levelBefore;
            LevelAfter = levelAfter;
        }

        public ForgeOutcome Outcome { get; }
        /// <summary>The chance that was actually rolled against, for the audit log.</summary>
        public int ChanceBp { get; }
        public int LevelBefore { get; }
        public int LevelAfter { get; }
    }

    /// <summary>The published Forge numbers from GDD section 6.</summary>
    public static class ForgeRules
    {
        /// <summary>Success chance for the attempt TO level index+1, in basis points.</summary>
        private static readonly int[] SuccessBp = { 9500, 9000, 8500, 8000, 7000, 6000, 5000, 4000, 3000 };

        /// <summary>Base stat gain for reaching level index+1, in percent of the +0 stats.</summary>
        private static readonly int[] StatGainPercent = { 4, 4, 4, 6, 6, 6, 10, 12, 16 };

        /// <summary>Map materials consumed by the attempt TO level index+1.</summary>
        private static readonly int[] Materials = { 0, 0, 0, 1, 2, 2, 3, 3, 4 };

        public static int MaterialsNeeded(int targetLevel)
        {
            if (targetLevel < 1 || targetLevel > ItemState.MaxUpgradeLevel)
                throw new ArgumentOutOfRangeException(nameof(targetLevel));
            return Materials[targetLevel - 1];
        }

        public const int MethodBonusBp = 1000;
        public const int FirstOathbreakTarget = 4;
        public const int PatienceFromTarget = 7;
        public const int PatienceStepBp = 100;
        public const int PatienceMaxBp = 1000;

        public static int BaseSuccessBp(int targetLevel)
        {
            if (targetLevel < 1 || targetLevel > ItemState.MaxUpgradeLevel)
                throw new ArgumentOutOfRangeException(nameof(targetLevel));
            return SuccessBp[targetLevel - 1];
        }

        /// <summary>Base stats at the given upgrade level, in percent of +0. A +9 item returns 168.</summary>
        public static int StatPercent(int upgradeLevel)
        {
            if (upgradeLevel < 0 || upgradeLevel > ItemState.MaxUpgradeLevel)
                throw new ArgumentOutOfRangeException(nameof(upgradeLevel));
            int total = 100;
            for (int i = 0; i < upgradeLevel; i++) total += StatGainPercent[i];
            return total;
        }

        /// <summary>Sorn cost of one attempt: 1000 x item level x 1.6^currentLevel, in exact integer math.</summary>
        public static long Cost(int itemLevel, int currentLevel)
        {
            if (itemLevel < 1) throw new ArgumentOutOfRangeException(nameof(itemLevel));
            if (currentLevel < 0 || currentLevel >= ItemState.MaxUpgradeLevel)
                throw new ArgumentOutOfRangeException(nameof(currentLevel));
            long pow16 = 1, pow10 = 1;
            for (int i = 0; i < currentLevel; i++)
            {
                pow16 *= 16;
                pow10 *= 10;
            }
            return 1000L * itemLevel * pow16 / pow10;
        }

        public static int MethodBonus(ForgeMethod method) =>
            method == ForgeMethod.KhansAlloy || method == ForgeMethod.ChainedSmith ? MethodBonusBp : 0;
    }

    public sealed class ForgeService
    {
        private readonly bool _patienceEnabled;

        public ForgeService(bool patienceEnabled = true)
        {
            _patienceEnabled = patienceEnabled;
        }

        /// <summary>The chance the next attempt on this item will be rolled against.</summary>
        public int ChanceBp(ItemState item, ForgeMethod method)
        {
            int target = item.UpgradeLevel + 1;
            int chance = ForgeRules.BaseSuccessBp(target) + ForgeRules.MethodBonus(method);
            if (_patienceEnabled) chance += item.PatienceBp;
            return Math.Min(chance, RandomExtensions.FullBp);
        }

        /// <summary>
        /// Runs one upgrade attempt and mutates the item. Paying sorn and consuming materials and the
        /// scroll is the caller's job (inventory transaction); this method only decides the outcome.
        /// </summary>
        public ForgeResult Attempt(ItemState item, ForgeMethod method, IRandom rng)
        {
            if (item.Destroyed) throw new InvalidOperationException("Item was destroyed by an Oathbreak.");
            if (item.UpgradeLevel >= ItemState.MaxUpgradeLevel) throw new InvalidOperationException("Item is already +9.");

            int before = item.UpgradeLevel;
            int target = before + 1;
            int chance = ChanceBp(item, method);

            if (rng.RollBp(chance))
            {
                item.UpgradeLevel = target;
                item.PatienceBp = 0;
                return new ForgeResult(ForgeOutcome.Success, chance, before, target);
            }

            if (_patienceEnabled && target >= ForgeRules.PatienceFromTarget)
                item.PatienceBp = Math.Min(item.PatienceBp + ForgeRules.PatienceStepBp, ForgeRules.PatienceMaxBp);

            switch (method)
            {
                case ForgeMethod.AnvilWard:
                    return new ForgeResult(ForgeOutcome.LevelKept, chance, before, before);

                case ForgeMethod.ScrollOfMercy:
                case ForgeMethod.KhansAlloy:
                    return LoseLevel(item, chance, before);

                default:
                    if (target < ForgeRules.FirstOathbreakTarget) return LoseLevel(item, chance, before);
                    item.Destroyed = true;
                    return new ForgeResult(ForgeOutcome.Oathbreak, chance, before, before);
            }
        }

        private static ForgeResult LoseLevel(ItemState item, int chance, int before)
        {
            // Patience survives the drop: per the GDD it resets on success only.
            int after = Math.Max(0, before - 1);
            item.UpgradeLevel = after;
            return new ForgeResult(ForgeOutcome.LevelLost, chance, before, after);
        }
    }
}
