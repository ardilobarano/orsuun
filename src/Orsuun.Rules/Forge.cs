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

        /// <summary>A piece's base stats in percent of +0: its forge level, and a point for each Temper step past +9.</summary>
        public static int StatPercent(ItemState item) => StatPercent(Math.Min(item.UpgradeLevel, ItemState.MaxUpgradeLevel)) + item.Temper;

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

        /// <summary>Extra chance on every attempt (a lucky forge hour, WorldEvents.ForgeLuckBp), in basis points.</summary>
        public int LuckBp { get; set; }

        /// <summary>The chance the next attempt on this item will be rolled against.</summary>
        public int ChanceBp(ItemState item, ForgeMethod method)
        {
            int target = item.UpgradeLevel + 1;
            int chance = ForgeRules.BaseSuccessBp(target) + ForgeRules.MethodBonus(method);
            if (_patienceEnabled) chance += item.PatienceBp;
            chance += LuckBp;
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

    /// <summary>What one Temper attempt did.</summary>
    public readonly struct TemperResult
    {
        public TemperResult(bool success, int before, int after)
        {
            Success = success;
            Before = before;
            After = after;
        }

        public bool Success { get; }
        public int Before { get; }
        public int After { get; }
    }

    /// <summary>
    /// Temper (owner, 28 Sep 2026: picked "Temper after +9"; GDD section 12: "Ten whetstone steps after +9, each +1% base
    /// stats, 60% success, failure drops one Temper step, never the item. A post-+9 ladder with no Oathbreak, so it does
    /// not dilute the Forge's fear"). Each attempt costs sorn and hunt materials; the cost climbs with the step. Assumptions
    /// (not stated by the owner): the cost (the +6 attempt's sorn times the step, and four materials).
    /// </summary>
    public static class Tempering
    {
        public const int MaxSteps = 10;
        public const int ChanceBp = 6000;
        public const int Materials = 4;

        /// <summary>The attempt from <paramref name="step"/> to the next: the +6 attempt's sorn, times the step to reach.</summary>
        public static long Cost(int itemLevel, int step) => ForgeRules.Cost(itemLevel, 6) * (step + 1);

        /// <summary>Why this piece cannot be tempered now, or null.</summary>
        public static string? Blocker(ItemState item, Inventory inventory)
        {
            if (item.Destroyed) return "That piece is gone.";
            if (item.UpgradeLevel < ItemState.MaxUpgradeLevel) return "Only a +9 piece takes a temper.";
            if (item.Temper >= MaxSteps) return "Fully tempered.";
            if (inventory.Sorn < Cost(item.ItemLevel, item.Temper)) return "Not enough sorn";
            if (inventory.Materials < Materials) return "Not enough hunt materials";
            return null;
        }

        /// <summary>One attempt: 60% a step up; else a step down (never below none), and never the piece.</summary>
        public static TemperResult Attempt(ItemState item, IRandom rng)
        {
            int before = item.Temper;
            bool success = rng.RollBp(ChanceBp);
            item.Temper = success ? Math.Min(MaxSteps, before + 1) : Math.Max(0, before - 1);
            return new TemperResult(success, before, item.Temper);
        }
    }
}
