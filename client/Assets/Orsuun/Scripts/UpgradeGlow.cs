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

        /// <summary>Glow for each EquipSlot index (0 for an empty slot), written into <paramref name="buffer"/>.</summary>
        public static float[] PerSlot(PlayerSession session, float[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = session.Equipped((EquipSlot)i) is ItemState item ? ForLevel(item.UpgradeLevel) : 0f;
            return buffer;
        }

        public static float Weapon(PlayerSession session) => ForLevel(session.Weapon.UpgradeLevel);
    }
}
