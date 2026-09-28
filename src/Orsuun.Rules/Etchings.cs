#nullable enable
using System;
using System.Collections.Generic;

namespace Orsuun.Rules
{
    public sealed class EtchingEntry
    {
        public EtchingEntry(int id, string name, int[] tierValues)
        {
            if (tierValues.Length != EtchingRules.TierCount) throw new ArgumentException("An entry needs 5 tier values.", nameof(tierValues));
            Id = id;
            Name = name;
            TierValues = tierValues;
        }

        public int Id { get; }
        public string Name { get; }
        public int[] TierValues { get; }
    }

    /// <summary>The etchings one equipment slot can roll. Entries are drawn with equal weight.</summary>
    public sealed class EtchingPool
    {
        public EtchingPool(IReadOnlyList<EtchingEntry> entries)
        {
            if (entries.Count < ItemState.MaxEtchings) throw new ArgumentException("A pool needs at least 5 entries.", nameof(entries));
            Entries = entries;
        }

        public IReadOnlyList<EtchingEntry> Entries { get; }

        /// <summary>The 16-entry weapon pool from GDD section 5.</summary>
        public static EtchingPool Weapon()
        {
            int[] chance10 = { 2, 4, 6, 8, 10 };
            int[] stat = { 2, 4, 6, 9, 12 };
            int[] family = { 4, 8, 12, 16, 20 };
            int[] chance8 = { 1, 2, 4, 6, 8 };

            var names = new (string Name, int[] Values)[]
            {
                ("Strong against Oathsworn", chance10),
                ("Critical hit chance", chance10),
                ("Piercing hit chance", chance10),
                ("STR", stat), ("DEX", stat), ("INT", stat), ("VIT", stat),
                ("Strong against Hollowed", family),
                ("Strong against Fiends", family),
                ("Strong against Marauders", family),
                ("Strong against Beasts", family),
                ("Strong against Cultists", family),
                ("Poison chance", chance8),
                ("Stun chance", chance8),
                ("Casting speed", family),
                ("Attack value", new[] { 10, 20, 30, 40, 50 }),
            };

            return Build(names);
        }

        /// <summary>The armor-side pool from GDD section 5, in ArmorEtchingIds order.</summary>
        public static EtchingPool Armor()
        {
            int[] pct15 = { 3, 6, 9, 12, 15 };
            int[] pct20 = { 4, 8, 12, 16, 20 };
            int[] stat = { 2, 4, 6, 9, 12 };
            var names = new (string Name, int[] Values)[]
            {
                ("Max HP", new[] { 200, 500, 900, 1400, 2000 }),
                ("Defense", new[] { 5, 10, 16, 24, 35 }),
                ("Resistance to Vanguard", pct15),
                ("Resistance to Kestrel", pct15),
                ("Resistance to Wraithsworn", pct15),
                ("Resistance to Drumcaller", pct15),
                ("HP regeneration", pct20),
                ("Block chance", pct15),
                ("Reflect", pct15),
                ("Sorn drop", pct20),
                ("Item drop", pct20),
                ("STR", stat), ("DEX", stat), ("INT", stat), ("VIT", stat),
                ("Evasion", pct15),
            };
            return Build(names);
        }

        private static readonly EtchingPool WeaponPool = Weapon();
        private static readonly EtchingPool ArmorPool = Armor();

        public static EtchingPool For(EquipSlot slot) => slot == EquipSlot.Weapon ? WeaponPool : ArmorPool;

        private static EtchingPool Build((string Name, int[] Values)[] names)
        {
            var entries = new List<EtchingEntry>(names.Length);
            for (int i = 0; i < names.Length; i++) entries.Add(new EtchingEntry(i, names[i].Name, names[i].Values));
            return new EtchingPool(entries);
        }
    }

    /// <summary>Armor, helmet, shield, shoes and accessories share this 16-entry pool for now.</summary>
    public static class ArmorEtchingIds
    {
        public const int MaxHp = 0;
        public const int Defense = 1;
        public const int ResistVanguard = 2;
        public const int ResistKestrel = 3;
        public const int ResistWraithsworn = 4;
        public const int ResistDrumcaller = 5;
        public const int HpRegen = 6;
        public const int Block = 7;
        public const int Reflect = 8;
        public const int SornDrop = 9;
        public const int ItemDrop = 10;
        public const int Str = 11;
        public const int Dex = 12;
        public const int Int = 13;
        public const int Vit = 14;
        public const int Evasion = 15;
    }

    public static class WeaponEtchingIds
    {
        public const int StrongAgainstOathsworn = 0;
        public const int Critical = 1;
        public const int Piercing = 2;
        public const int Str = 3;
        public const int Dex = 4;
        public const int Int = 5;
        public const int Vit = 6;
        public const int AttackValue = 15;
    }

    /// <summary>One line of a turning goal: this etching at this tier or better.</summary>
    public readonly struct TurnTarget
    {
        public TurnTarget(int entryId, int minTier)
        {
            EntryId = entryId;
            MinTier = minTier;
        }

        public int EntryId { get; }
        public int MinTier { get; }
    }

    public enum NeedleKind
    {
        /// <summary>Adds etchings 1 to 4.</summary>
        EtchingNeedle,
        /// <summary>Adds the fifth etching.</summary>
        MastersNeedle,
        /// <summary>Adds a sixth etching to an Epic or Legendary piece whose five are all T3 or better (10%).</summary>
        GrandmasterNeedle,
    }

    public static class EtchingRules
    {
        public const int TierCount = 5;

        /// <summary>Cumulative tier thresholds for T1..T5 weights of 40 / 30 / 17 / 9 / 4 percent.</summary>
        private static readonly int[] TierCumulativeBp = { 4000, 7000, 8700, 9600, 10000 };

        /// <summary>Chance to add the etching in slot index (0-based), in basis points.</summary>
        private static readonly int[] AddChanceBp = { 10000, 8000, 6000, 4000, 3000, 1000 };

        public static int AddChance(int slotIndex) => AddChanceBp[slotIndex];

        public static int MaxTier(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return 3;
                case Rarity.Uncommon: return 4;
                default: return 5;
            }
        }

        /// <summary>The chance a fresh etching rolls tier minTier or better (a roll above the rarity cap takes the cap).</summary>
        public static double TierAtLeastChance(Rarity rarity, int minTier)
        {
            if (minTier <= 1) return 1.0;
            if (minTier > MaxTier(rarity)) return 0.0;
            return (RandomExtensions.FullBp - TierCumulativeBp[minTier - 2]) / (double)RandomExtensions.FullBp;
        }

        /// <summary>Rolls a tier from the weights; a roll above the rarity cap takes the cap.</summary>
        public static int RollTier(Rarity rarity, IRandom rng)
        {
            int roll = rng.NextInt(RandomExtensions.FullBp);
            int tier = 1;
            while (roll >= TierCumulativeBp[tier - 1]) tier++;
            return Math.Min(tier, MaxTier(rarity));
        }
    }

    /// <summary>
    /// The player's etching tools: an Etching Needle adds the next etching (1st to 4th, at the slot's add chance; a
    /// failure only costs the needle), a Master's Needle the fifth (the Carvers' Archive, 25 Sep 2026), and Pinning Wax holds one etching through turns (GDD: one lock per item,
    /// turns cost double while it holds). Unpinning is free but the wax is spent. The sixth (owner, 28 Sep 2026: picked
    /// "Sixth etching"; GDD section 12: "Sixth etching on Epic and Legendary items only, via a Grandmaster's Needle at 10%
    /// success; the item must already hold five T3+ etchings"): a failure costs only the needle.
    /// </summary>
    public static class EtchingActions
    {
        public const int SixthMinTier = 3;

        /// <summary>Why this piece cannot take a sixth etching, or null (the needle not counted).</summary>
        public static string? SixthBlocker(ItemState item)
        {
            if (item.Rarity < Rarity.Epic) return "Only Epic and Legendary pieces take a sixth etching.";
            foreach (Etching e in item.Etchings)
                if (e.Tier < SixthMinTier) return "A sixth etching needs all five at T3 or better.";
            return null;
        }

        public static string? EtchBlocker(ItemState item, Inventory inventory)
        {
            if (item.Destroyed) return "That piece is gone.";
            if (item.Kin) return "The Bannerkin's pieces take no etchings.";
            if (item.Etchings.Count >= ItemState.SixthEtching) return "This piece has all six etchings.";
            if (item.Etchings.Count == ItemState.MaxEtchings)
                return SixthBlocker(item) ?? (inventory.GrandmasterNeedles < 1 ? "The sixth etching needs a Grandmaster's Needle (the Pit shop or a Commander's own chest)." : null);
            if (item.Etchings.Count == ItemState.MaxEtchings - 1)
                return inventory.MastersNeedles < 1 ? "The fifth etching needs a Master's Needle (the Carvers' Archive or the Pit shop)." : null;
            if (inventory.EtchingNeedles < 1) return "No Etching Needles.";
            return null;
        }

        /// <summary>The chance the next Etching Needle takes on this piece, in basis points.</summary>
        public static int EtchChanceBp(ItemState item) =>
            item.Etchings.Count < ItemState.SixthEtching ? EtchingRules.AddChance(item.Etchings.Count) : 0;

        public static bool Etch(ItemState item, Inventory inventory, EtchingService etchings, IRandom rng)
        {
            string? blocker = EtchBlocker(item, inventory);
            if (blocker != null) throw new InvalidOperationException(blocker);
            // The sixth takes a Grandmaster's Needle, the fifth a Master's Needle; the first four an Etching Needle.
            bool sixth = item.Etchings.Count == ItemState.MaxEtchings;
            bool fifth = item.Etchings.Count == ItemState.MaxEtchings - 1;
            if (sixth) inventory.GrandmasterNeedles--;
            else if (fifth) inventory.MastersNeedles--;
            else inventory.EtchingNeedles--;
            NeedleKind needle = sixth ? NeedleKind.GrandmasterNeedle : fifth ? NeedleKind.MastersNeedle : NeedleKind.EtchingNeedle;
            return etchings.TryAdd(item, EtchingPool.For(item.Slot), needle, rng);
        }

        public static string? PinBlocker(ItemState item, int index, Inventory inventory)
        {
            if (item.Destroyed) return "That piece is gone.";
            if (index < 0 || index >= item.Etchings.Count) return "No etching there.";
            if (item.LockedEtchingIndex == index) return null;          // unpinning is free
            return inventory.PinningWax < 1 ? "No Pinning Wax." : null;
        }

        public static void Pin(ItemState item, int index, Inventory inventory)
        {
            string? blocker = PinBlocker(item, index, inventory);
            if (blocker != null) throw new InvalidOperationException(blocker);
            if (item.LockedEtchingIndex == index)
            {
                item.LockedEtchingIndex = -1;
                return;
            }
            inventory.PinningWax--;
            item.LockedEtchingIndex = index;
        }
    }

    public sealed class EtchingService
    {
        /// <summary>Tries to add the next etching. Failure only consumes the needle; gear is never at risk here.</summary>
        public bool TryAdd(ItemState item, EtchingPool pool, NeedleKind needle, IRandom rng)
        {
            EnsureUsable(item);
            int slot = item.Etchings.Count;
            if (slot >= ItemState.SixthEtching) throw new InvalidOperationException("Item already has 6 etchings.");

            bool fifth = slot == ItemState.MaxEtchings - 1, sixth = slot == ItemState.MaxEtchings;
            if (sixth != (needle == NeedleKind.GrandmasterNeedle))
                throw new InvalidOperationException(sixth ? "The sixth etching needs a Grandmaster's Needle." : "A Grandmaster's Needle only adds the sixth etching.");
            if (sixth && EtchingActions.SixthBlocker(item) is string no) throw new InvalidOperationException(no);
            if (!sixth && fifth != (needle == NeedleKind.MastersNeedle))
                throw new InvalidOperationException(fifth ? "The fifth etching needs a Master's Needle." : "A Master's Needle only adds the fifth etching.");

            if (!rng.RollBp(EtchingRules.AddChance(slot))) return false;

            var taken = new HashSet<int>();
            foreach (Etching e in item.Etchings) taken.Add(e.EntryId);
            item.Etchings.Add(RollOne(item.Rarity, DrawEntries(pool, taken, 1, rng)[0], rng));
            return true;
        }

        /// <summary>
        /// Turns the item: every etching is replaced by a new distinct one, except the one held by Pinning Wax.
        /// Returns the number of Turnstones the turn consumes (2 while an etching is locked).
        /// </summary>
        public int Turn(ItemState item, EtchingPool pool, IRandom rng)
        {
            EnsureUsable(item);
            if (item.Etchings.Count == 0) throw new InvalidOperationException("Nothing to turn: the item has no etchings.");

            int locked = item.LockedEtchingIndex;
            bool hasLock = locked >= 0 && locked < item.Etchings.Count;

            var taken = new HashSet<int>();
            if (hasLock) taken.Add(item.Etchings[locked].EntryId);

            int toRoll = item.Etchings.Count - (hasLock ? 1 : 0);
            List<EtchingEntry> drawn = DrawEntries(pool, taken, toRoll, rng);

            int next = 0;
            for (int i = 0; i < item.Etchings.Count; i++)
            {
                if (hasLock && i == locked) continue;
                item.Etchings[i] = RollOne(item.Rarity, drawn[next++], rng);
            }

            return hasLock ? 2 : 1;
        }

        public const int BulkTurnFree = 10;
        public const int BulkTurnMax = 50;

        /// <summary>A turning goal names at most one etching per etching slot.</summary>
        public const int MaxTargets = ItemState.MaxEtchings;

        /// <summary>
        /// Bulk Turn (GDD section 12): turns up to maxTurns times, stopping early when an etching with entry
        /// stopEntryId at tier >= minTier appears, or when Turnstones run out. Returns the Turnstones spent.
        /// </summary>
        public int TurnUntil(ItemState item, EtchingPool pool, Inventory inventory, IRandom rng, int maxTurns, int? stopEntryId, int minTier, out int turns, out bool stopped) =>
            TurnUntil(item, pool, inventory, rng, maxTurns, stopEntryId.HasValue ? new[] { new TurnTarget(stopEntryId.Value, minTier) } : Array.Empty<TurnTarget>(), out turns, out stopped);

        /// <summary>
        /// Bulk Turn toward a goal (owner, 24 Sep 2026: the turning helper, up to five etchings with tiers): turns up
        /// to maxTurns times, stopping early once every target is on the item at its tier or better, or when
        /// Turnstones run out. No targets means no stop rule. Returns the Turnstones spent.
        /// </summary>
        public int TurnUntil(ItemState item, EtchingPool pool, Inventory inventory, IRandom rng, int maxTurns, IReadOnlyList<TurnTarget> targets, out int turns, out bool stopped)
        {
            turns = 0;
            stopped = false;
            int spent = 0;
            int limit = Math.Max(1, Math.Min(BulkTurnMax, maxTurns));
            while (turns < limit)
            {
                int cost = item.LockedEtchingIndex >= 0 ? 2 : 1;
                if (inventory.Turnstones < cost) break;
                inventory.Turnstones -= Turn(item, pool, rng);
                spent += cost;
                turns++;
                if (targets.Count > 0 && MatchesAll(item, targets))
                {
                    stopped = true;
                    break;
                }
            }
            return spent;
        }

        public static bool Matches(ItemState item, int entryId, int minTier)
        {
            foreach (Etching e in item.Etchings)
                if (e.EntryId == entryId && e.Tier >= minTier) return true;
            return false;
        }

        /// <summary>Every target is on the item at its tier or better.</summary>
        public static bool MatchesAll(ItemState item, IReadOnlyList<TurnTarget> targets)
        {
            foreach (TurnTarget t in targets)
                if (!Matches(item, t.EntryId, t.MinTier)) return false;
            return true;
        }

        /// <summary>Why a goal can never be met on this item (null when it can): too many targets, a repeat, a tier
        /// above the rarity's cap, or a clash with the etching held by Pinning Wax.</summary>
        public static string? TargetProblem(ItemState item, EtchingPool pool, IReadOnlyList<TurnTarget> targets)
        {
            if (targets.Count > MaxTargets) return $"Pick at most {MaxTargets} etchings.";
            int cap = EtchingRules.MaxTier(item.Rarity);
            var seen = new HashSet<int>();
            foreach (TurnTarget t in targets)
            {
                if (t.EntryId < 0 || t.EntryId >= pool.Entries.Count) return "Unknown etching.";
                if (!seen.Add(t.EntryId)) return "Each etching can be picked once.";
                if (t.MinTier < 1 || t.MinTier > EtchingRules.TierCount) return "Tiers run from T1 to T5.";
                if (t.MinTier > cap) return $"A {item.Rarity} piece rolls up to T{cap}.";
            }
            if (targets.Count > item.Etchings.Count)
                return $"This piece has {item.Etchings.Count} etching{(item.Etchings.Count == 1 ? "" : "s")}: pick at most {item.Etchings.Count}.";
            int locked = item.LockedEtchingIndex;
            if (locked >= 0 && locked < item.Etchings.Count)
            {
                Etching held = item.Etchings[locked];
                bool heldIsTarget = false;
                foreach (TurnTarget t in targets)
                {
                    if (t.EntryId != held.EntryId) continue;
                    heldIsTarget = true;
                    if (held.Tier < t.MinTier) return "The etching held by Pinning Wax never changes, and it is below that tier.";
                }
                if (!heldIsTarget && targets.Count > item.Etchings.Count - 1)
                    return $"One etching is pinned: pick at most {item.Etchings.Count - 1} others, or include it.";
            }
            return null;
        }

        /// <summary>
        /// The chance that one turn meets the whole goal: the free targets must all be among the fresh draws
        /// (C(N-k, n-k) / C(N, n) for n distinct draws from N entries) and each must roll its tier or better.
        /// Zero when TargetProblem says the goal is out of reach.
        /// </summary>
        public static double TargetChance(ItemState item, EtchingPool pool, IReadOnlyList<TurnTarget> targets)
        {
            if (targets.Count == 0 || item.Etchings.Count == 0 || TargetProblem(item, pool, targets) != null) return 0.0;
            int locked = item.LockedEtchingIndex;
            bool hasLock = locked >= 0 && locked < item.Etchings.Count;
            int heldEntry = hasLock ? item.Etchings[locked].EntryId : -1;
            int draws = item.Etchings.Count - (hasLock ? 1 : 0);
            int entries = pool.Entries.Count - (hasLock ? 1 : 0);
            double chance = 1.0;
            int free = 0;
            foreach (TurnTarget t in targets)
            {
                if (t.EntryId == heldEntry) continue;       // TargetProblem already checked its tier
                free++;
                chance *= EtchingRules.TierAtLeastChance(item.Rarity, t.MinTier);
            }
            if (free > draws) return 0.0;
            return chance * Choose(entries - free, draws - free) / Choose(entries, draws);
        }

        private static double Choose(int n, int k)
        {
            if (k < 0 || k > n) return 0.0;
            double r = 1.0;
            for (int i = 1; i <= k; i++) r = r * (n - k + i) / i;
            return r;
        }

        private static Etching RollOne(Rarity rarity, EtchingEntry entry, IRandom rng)
        {
            int tier = EtchingRules.RollTier(rarity, rng);
            return new Etching(entry.Id, tier, entry.TierValues[tier - 1]);
        }

        /// <summary>Draws distinct entries with equal weight using a partial Fisher-Yates shuffle.</summary>
        private static List<EtchingEntry> DrawEntries(EtchingPool pool, HashSet<int> excludedIds, int count, IRandom rng)
        {
            var candidates = new List<EtchingEntry>(pool.Entries.Count);
            foreach (EtchingEntry entry in pool.Entries)
                if (!excludedIds.Contains(entry.Id)) candidates.Add(entry);

            if (count > candidates.Count) throw new InvalidOperationException("Pool is too small for this draw.");

            for (int i = 0; i < count; i++)
            {
                int j = i + rng.NextInt(candidates.Count - i);
                EtchingEntry tmp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = tmp;
            }

            return candidates.GetRange(0, count);
        }

        private static void EnsureUsable(ItemState item)
        {
            if (item.Destroyed) throw new InvalidOperationException("Item was destroyed by an Oathbreak.");
        }
    }
}
