#nullable enable
using System;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    /// <summary>
    /// Elite camps (owner, 29 Sep 2026: "Elite camps"): now and then one camp of a big map flies a golden banner, and its
    /// monsters are an elite pack the hero meets on the trail. Rolled on the server's clock: every hunting place
    /// (Parties.Place) has one elite camp for UpMinutes of every WindowMinutes; which camp and when is a hash of the place
    /// and the window, so every hero there sees the same one. While it flies, one pack in Camps of a hero's online hunt
    /// there is elite: its monsters' loot is paid ExtraLootTimes more (sorn, XP, draughts, materials, drops), and it leaves a
    /// piece of gear GearBp of the time. Offline hunting meets none. The pay is settled with the hunt (GameService.Settle);
    /// in the lane the packs met near the banner fight tougher (HpPercent, AttackPercent; LaneSim.ElitePacks).
    /// </summary>
    public static class EliteCamps
    {
        public const int WindowMinutes = 30, UpMinutes = 10, Camps = 6, ExtraLootTimes = 2, GearBp = 3300;
        /// <summary>An elite pack's monsters in the lane (owner, 29 Sep 2026: "Truly tougher elite packs"): health and attack
        /// as percents of the stage's (LaneSim.ElitePacks). The server replays a loop with the elite packs its report names,
        /// only while a banner flew at the hero's place: they can only slow a loop, so naming them earns nothing.</summary>
        public const int HpPercent = 250, AttackPercent = 160;

        /// <summary>The encounter indices a loop report may name as elite packs: its packs (not the Korstone or boss), each once.</summary>
        public static int[] ValidPacks(int[]? named, StageConfig stage)
        {
            if (named == null || named.Length == 0) return Array.Empty<int>();
            int packs = stage.PacksBeforeKorstone;
            var valid = new System.Collections.Generic.SortedSet<int>();
            foreach (int i in named) if (i >= 0 && i < packs) valid.Add(i);
            int[] result = new int[valid.Count];
            valid.CopyTo(result);
            return result;
        }
        private static readonly DateTime Epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>The window's elite camp (0..Camps-1) and when its banner flies, for the window holding <paramref name="utc"/>.</summary>
        public static (int Camp, DateTime From, DateTime Until) Roll(int place, DateTime utc)
        {
            long window = (long)Math.Floor((utc - Epoch).TotalMinutes / WindowMinutes);
            ulong h = Mix(unchecked((ulong)place * 0x9E3779B97F4A7C15UL ^ (ulong)window * 0xC2B2AE3D27D4EB4FUL));
            int camp = (int)(h % Camps);
            int offset = (int)((h >> 16) % (ulong)(WindowMinutes - UpMinutes + 1));
            DateTime from = Epoch.AddMinutes(window * WindowMinutes + offset);
            return (camp, from, from.AddMinutes(UpMinutes));
        }

        /// <summary>The camp flying the golden banner at a place now, and its seconds left; false when none flies (or the
        /// place has no camps: a dungeon floor).</summary>
        public static bool Up(int place, DateTime utc, out int camp, out long secondsLeft)
        {
            camp = -1;
            secondsLeft = 0;
            if (place < 0) return false;
            (int c, DateTime from, DateTime until) = Roll(place, utc);
            if (utc < from || utc >= until) return false;
            camp = c;
            secondsLeft = (long)Math.Ceiling((until - utc).TotalSeconds);
            return true;
        }

        /// <summary>Seconds of [from, to) during which a banner flew at the place (a settlement's stretch may cross windows).</summary>
        public static long SecondsUp(int place, DateTime from, DateTime to)
        {
            if (place < 0 || to <= from) return 0;
            long total = 0;
            // Each window the stretch touches (a long gap is capped by the caller; at most a day is looked at).
            DateTime cursor = from;
            for (int guard = 0; cursor < to && guard < 48 * 24 * 60 / WindowMinutes; guard++)
            {
                (int _, DateTime up, DateTime down) = Roll(place, cursor);
                DateTime a = up > from ? up : from, b = down < to ? down : to;
                if (b > a) total += (long)(b - a).TotalSeconds;
                long window = (long)Math.Floor((cursor - Epoch).TotalMinutes / WindowMinutes);
                cursor = Epoch.AddMinutes((window + 1) * WindowMinutes);
            }
            return total;
        }

        /// <summary>How many of a settlement's packs were elite: one in Camps of those hunted while a banner flew (a share
        /// of them by the seconds), the part short of a whole one by chance.</summary>
        public static long ElitePacks(long packs, long countedSeconds, long upSeconds, IRandom rng)
        {
            if (packs <= 0 || countedSeconds <= 0 || upSeconds <= 0) return 0;
            long up = Math.Min(upSeconds, countedSeconds);
            // In thousandths of a pack: packs * (up / counted) / Camps.
            long milli = packs * 1000 * up / countedSeconds / Camps;
            return milli / 1000 + (rng.NextInt(1000) < milli % 1000 ? 1 : 0);
        }

        /// <summary>Pays the elite packs: each monster's loot ExtraLootTimes more, and a piece of gear GearBp of the time.
        /// Returns the line the hunt's log shows ("" for none).</summary>
        public static string Loot(StageConfig stage, Inventory inventory, IRandom rng, long elitePacks)
        {
            if (elitePacks <= 0) return "";
            long sornBefore = inventory.Sorn;
            int gear = 0;
            long monsters = (stage.PackSizeMin + stage.PackSizeMax) / 2;
            for (long p = 0; p < elitePacks; p++)
            {
                for (long m = 0; m < monsters * ExtraLootTimes; m++) HuntYield.LootMob(stage, inventory, rng, out _);
                if (rng.RollBp(GearBp))
                {
                    HuntYield.DropGear(stage, inventory, rng, stage.GearRarityCap, minimum: Rarity.Rare);
                    gear++;
                }
            }
            string text = (elitePacks == 1 ? "An elite pack" : elitePacks + " elite packs") + ": +" + (inventory.Sorn - sornBefore) + " sorn";
            return gear == 0 ? text : text + ", +" + gear + " piece" + (gear == 1 ? "" : "s") + " of gear";
        }

        private static ulong Mix(ulong z)
        {
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            return z ^ (z >> 31);
        }
    }
}
