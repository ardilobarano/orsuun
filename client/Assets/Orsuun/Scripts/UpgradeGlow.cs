using Orsuun.Rules;
using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// Upgrade glow (owner, 23 Sep 2026): only the weapon and the body armour are visible on the character and glow,
    /// each by its own level from +7 up (classic MMO upgrade shine). Helmet, shield, jewellery and shoes are stats only:
    /// they can be forged like the weapon but have no look and no glow. Maps a level to the EmberGlow shader's _Glow.
    /// </summary>
    public static class UpgradeGlow
    {
        public const int FirstGlowLevel = 7;
        public static readonly int GlowId = Shader.PropertyToID("_Glow");

        public static float ForLevel(int upgradeLevel) =>
            upgradeLevel < FirstGlowLevel ? 0f : upgradeLevel == 7 ? 0.35f : upgradeLevel == 8 ? 0.65f : 1f;

        /// <summary>True for the slots that show on the character and glow: the weapon and the body armour.</summary>
        public static bool IsVisible(EquipSlot slot) => slot == EquipSlot.Weapon || slot == EquipSlot.Armor;

        /// <summary>Glow for each EquipSlot index, written into <paramref name="buffer"/>: 0 for empty and stat-only slots.</summary>
        public static float[] PerSlot(PlayerSession session, float[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = IsVisible((EquipSlot)i) && session.Equipped((EquipSlot)i) is ItemState item ? ForLevel(item.UpgradeLevel) : 0f;
            return buffer;
        }

    }
}
