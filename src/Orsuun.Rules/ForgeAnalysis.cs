#nullable enable
namespace Orsuun.Rules
{
    /// <summary>
    /// Closed-form balance math for designers and CI. Uses doubles, so it must never decide a gameplay outcome.
    /// </summary>
    public static class ForgeAnalysis
    {
        /// <summary>
        /// Expected attempts per step for a lose-one-level method, ignoring Forgemaster's Patience:
        /// E(k) = (1 + (1 - p(k)) * E(k-1)) / p(k), with E(0) = 1 / p(0). Index k is the step from +k to +k+1.
        /// </summary>
        public static double[] ExpectedAttemptsPerStep(ForgeMethod method)
        {
            var result = new double[ItemState.MaxUpgradeLevel];
            double previous = 0;
            for (int k = 0; k < ItemState.MaxUpgradeLevel; k++)
            {
                int bp = ForgeRules.BaseSuccessBp(k + 1) + ForgeRules.MethodBonus(method);
                double p = bp >= RandomExtensions.FullBp ? 1.0 : bp / (double)RandomExtensions.FullBp;
                result[k] = k == 0 ? 1.0 / p : (1.0 + (1.0 - p) * previous) / p;
                previous = result[k];
            }
            return result;
        }

        public static double ExpectedAttemptsTotal(ForgeMethod method)
        {
            double total = 0;
            foreach (double step in ExpectedAttemptsPerStep(method)) total += step;
            return total;
        }

        /// <summary>Chance to climb from one level to another with no failure at all (the raw gamble).</summary>
        public static double StraightRunChance(int fromLevel, int toLevel)
        {
            double chance = 1.0;
            for (int target = fromLevel + 1; target <= toLevel; target++)
                chance *= ForgeRules.BaseSuccessBp(target) / (double)RandomExtensions.FullBp;
            return chance;
        }
    }
}
