using Orsuun.Rules;
using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// Upgrade glow (owner decision, 23 Sep 2026): every equipped item glows from +7 upward, not only the weapon.
    /// Maps an upgrade level to the EmberGlow shader's _Glow value. Target look: docs/concept/glow-1-sheet.jpg.
    /// </summary>
    public static class UpgradeGlow
    {
        public const int FirstGlowLevel = 7;
        public static readonly int GlowId = Shader.PropertyToID("_Glow");

        public static float ForLevel(int upgradeLevel) =>
            upgradeLevel < FirstGlowLevel ? 0f : upgradeLevel == 7 ? 0.35f : upgradeLevel == 8 ? 0.65f : 1f;

        /// <summary>
        /// The grey-box hero is one body, so it shows the average glow of every equipped non-weapon item:
        /// a full +9 set blazes, a single +9 ring barely warms it. Real models give each slot its own renderer.
        /// </summary>
        public static float Armor(PlayerSession session)
        {
            float sum = 0f; int count = 0;
            foreach (ItemState item in session.Equipment)
            {
                if (item.Slot == EquipSlot.Weapon) continue;
                sum += ForLevel(item.UpgradeLevel);
                count++;
            }
            return count == 0 ? 0f : sum / count;
        }

        public static float Weapon(PlayerSession session) => ForLevel(session.Weapon.UpgradeLevel);
    }
}
