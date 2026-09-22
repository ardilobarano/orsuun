#nullable enable
using System;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    public enum Bell
    {
        None,
        /// <summary>21:00 - Korstone chests pay double.</summary>
        KorstoneBell,
        /// <summary>21:30 - double XP.</summary>
        LevelBell,
        /// <summary>22:00 - gear rarity cap one tier higher.</summary>
        LootBell,
        /// <summary>22:30 - double materials.</summary>
        MaterialBell,
        /// <summary>23:00 - +25% damage.</summary>
        WarBell,
    }

    /// <summary>
    /// Server bells, GDD section 12: five 30-minute windows from 21:00 to 23:30 server time that concentrate
    /// the community in the evening block. Times are server-local (Europe/Istanbul for the first servers).
    /// Bells apply to present players only: live lanes, pushes and Commander fights, never offline settlement.
    /// </summary>
    public static class EveningBells
    {
        public const int StartHour = 21;
        public const int WindowMinutes = 30;

        private static readonly Bell[] Schedule = { Bell.KorstoneBell, Bell.LevelBell, Bell.LootBell, Bell.MaterialBell, Bell.WarBell };

        public static Bell Active(DateTime serverLocalTime)
        {
            int minutes = (serverLocalTime.Hour - StartHour) * 60 + serverLocalTime.Minute;
            if (minutes < 0) return Bell.None;
            int slot = minutes / WindowMinutes;
            return slot < Schedule.Length ? Schedule[slot] : Bell.None;
        }

        /// <summary>The next bell and the minutes until it rings (0 while one is active for the one after it).</summary>
        public static Bell Next(DateTime serverLocalTime, out int minutesUntil)
        {
            int nowMin = serverLocalTime.Hour * 60 + serverLocalTime.Minute;
            for (int i = 0; i < Schedule.Length; i++)
            {
                int start = StartHour * 60 + i * WindowMinutes;
                if (start > nowMin)
                {
                    minutesUntil = start - nowMin;
                    return Schedule[i];
                }
            }
            minutesUntil = 24 * 60 - nowMin + StartHour * 60;
            return Schedule[0];
        }

        public static string Name(Bell bell)
        {
            switch (bell)
            {
                case Bell.KorstoneBell: return "Korstone Bell: double Korstone chests";
                case Bell.LevelBell: return "Level Bell: double XP";
                case Bell.LootBell: return "Loot Bell: rarer gear drops";
                case Bell.MaterialBell: return "Material Bell: double materials";
                case Bell.WarBell: return "War Bell: +25% damage";
                default: return "";
            }
        }

        /// <summary>Applies the active bell's multipliers to a lane configuration.</summary>
        public static StageConfig Apply(StageConfig config, Bell bell)
        {
            switch (bell)
            {
                case Bell.KorstoneBell: config.KorstoneChestPercent = 200; break;
                case Bell.LevelBell: config.XpPercent = 200; break;
                case Bell.LootBell: config.GearRarityCap = (Rarity)Math.Min((int)Rarity.Legendary, (int)config.GearRarityCap + 1); break;
                case Bell.MaterialBell: config.MaterialPercent = 200; break;
                case Bell.WarBell: config.DamagePercent = 125; break;
            }
            return config;
        }
    }
}
