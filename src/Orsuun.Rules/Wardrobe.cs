#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Orsuun.Rules
{
    public enum WardrobeKind { Skin = 0, Mount = 1, Companion = 2 }

    /// <summary>What a wardrobe piece adds: a skin HP, a mount attack, a companion hunting XP or sorn.</summary>
    public enum WardrobePerk { Hp = 0, Attack = 1, Xp = 2, Sorn = 3 }

    public sealed class WardrobeDef
    {
        public WardrobeDef(string id, string name, WardrobeKind kind, int tier, WardrobePerk perk, int perkPercent, bool sold, string look, string blurb)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Tier = tier;
            Perk = perk;
            PerkPercent = perkPercent;
            Sold = sold;
            Look = look;
            Blurb = blurb;
        }

        public string Id { get; }
        public string Name { get; }
        public WardrobeKind Kind { get; }
        /// <summary>1 (plain) to 4 (legendary): sets the price and how rarely it drops.</summary>
        public int Tier { get; }
        public WardrobePerk Perk { get; }
        public int PerkPercent { get; }
        /// <summary>On sale at the Caravan; the rest (the Commanders' trophies, the Campaign Trail's pieces) are won.</summary>
        public bool Sold { get; }
        /// <summary>The client's look key (model and palette).</summary>
        public string Look { get; }
        public string Blurb { get; }

        public string PerkText => "+" + PerkPercent + "% " + Wardrobe.PerkName(Perk);
    }

    /// <summary>A piece an account holds, until a moment in Unix seconds.</summary>
    public sealed class OwnedPiece
    {
        public OwnedPiece(string id, long expiresUnix)
        {
            Id = id;
            ExpiresUnix = expiresUnix;
        }

        public string Id { get; }
        public long ExpiresUnix { get; set; }
        public bool Active(long nowUnix) => ExpiresUnix > nowUnix;
    }

    /// <summary>
    /// The wardrobe (owner, 25 Sep 2026): skins, mounts and companions held for 1, 3, 5, 7 or 14 days. Small stats in the
    /// Metin2 way: a skin adds HP, a mount attack, a companion hunting XP or sorn. The Caravan sells every duration for
    /// Amber (bought with real money only); bosses from Gorak Pass on drop short ones (1-3 days, rarely 5-7). Getting a
    /// piece already held adds its time. One of each kind is worn. Duels and the Pits measure gear only, so the wardrobe
    /// does not reach PvP.
    /// </summary>
    public static class Wardrobe
    {
        public static readonly int[] Days = { 1, 3, 5, 7, 14 };

        /// <summary>Amber by tier (rows) and duration (columns, as Days).</summary>
        private static readonly int[][] PriceByTier =
        {
            new[] { 30, 80, 120, 150, 260 },
            new[] { 60, 150, 220, 280, 480 },
            new[] { 80, 200, 290, 350, 600 },
            new[] { 110, 280, 400, 480, 820 },
        };

        public static readonly WardrobeDef[] All =
        {
            new WardrobeDef("salt-nomad-garb", "Salt Nomad Garb", WardrobeKind.Skin, 1, WardrobePerk.Hp, 2, true, "SaltNomad",
                "Sand-bleached wraps of the salt roads."),
            new WardrobeDef("frost-hunter-furs", "Frost Hunter Furs", WardrobeKind.Skin, 2, WardrobePerk.Hp, 3, true, "FrostHunter",
                "White furs over the lamellar, for Whitefang winters."),
            new WardrobeDef("ember-khan-guise", "Ember Khan Guise", WardrobeKind.Skin, 3, WardrobePerk.Hp, 4, true, "EmberKhan",
                "Crimson and gold, cut for a khagan's feast."),
            new WardrobeDef("grave-warden-shroud", "Grave Warden Shroud", WardrobeKind.Skin, 4, WardrobePerk.Hp, 6, true, "GraveWarden",
                "Black and violet, sewn from a barrow's banners."),
            new WardrobeDef("tul-goraks-warmask", "Tul-Gorak's Warmask", WardrobeKind.Skin, 3, WardrobePerk.Hp, 4, false, "TulGorak",
                "Taken from the Warlord's camp. Only he drops it."),
            new WardrobeDef("mirage-veil", "Mirage Veil", WardrobeKind.Skin, 3, WardrobePerk.Hp, 4, false, "MirageVeil",
                "Shimmers like the Salt Sea at noon. Only the Mirage Queen drops it."),
            new WardrobeDef("greyjaw-pelt-cloak", "Greyjaw Pelt Cloak", WardrobeKind.Skin, 3, WardrobePerk.Hp, 4, false, "GreyjawPelt",
                "Old Greyjaw's grey pelt. Only he drops it."),
            new WardrobeDef("amber-road-regalia", "Amber Road Regalia", WardrobeKind.Skin, 4, WardrobePerk.Hp, 5, false, "AmberRoad",
                "A caravan master's teal and bronze, set with steppe amber. The Campaign Trail's first season."),
            new WardrobeDef("white-steppe-regalia", "White Steppe Regalia", WardrobeKind.Skin, 4, WardrobePerk.Hp, 5, false, "WhiteSteppe",
                "Snow-leopard fur over pale silver lamellar and sky-blue silk. The Campaign Trail's second season."),

            new WardrobeDef("steppe-pony", "Steppe Pony", WardrobeKind.Mount, 1, WardrobePerk.Attack, 2, true, "HorsePony",
                "Small, shaggy and never tired."),
            new WardrobeDef("ember-warhorse", "Ember Warhorse", WardrobeKind.Mount, 2, WardrobePerk.Attack, 4, true, "HorseEmber",
                "A heavy charger in crimson lamellar barding."),
            new WardrobeDef("gold-banner-charger", "Gold Banner Charger", WardrobeKind.Mount, 3, WardrobePerk.Attack, 5, true, "HorseGold",
                "Barded in saffron and brass, as the caravan cities ride."),
            new WardrobeDef("hollow-steed", "Hollow Steed", WardrobeKind.Mount, 4, WardrobePerk.Attack, 6, true, "HorseHollow",
                "One of the herds that went Hollow, tamed again."),
            new WardrobeDef("amber-road-courser", "Amber Road Courser", WardrobeKind.Mount, 4, WardrobePerk.Attack, 5, false, "HorseAmber",
                "Barded in honey amber and bronze for the salt roads. The Campaign Trail's first season."),
            new WardrobeDef("white-steppe-courser", "White Steppe Courser", WardrobeKind.Mount, 4, WardrobePerk.Attack, 5, false, "HorseWhite",
                "A grey-white charger in frost-blue scale barding, furred for the winter steppe. The Campaign Trail's second season."),

            new WardrobeDef("ember-fox", "Ember Fox", WardrobeKind.Companion, 1, WardrobePerk.Sorn, 3, true, "Fox",
                "Finds the coins the dead forgot."),
            new WardrobeDef("sky-falcon", "Sky Falcon", WardrobeKind.Companion, 2, WardrobePerk.Xp, 5, true, "Falcon",
                "A hooded hunting falcon, taught on the steppe."),
            new WardrobeDef("grave-wolf-pup", "Grave Wolf Pup", WardrobeKind.Companion, 3, WardrobePerk.Sorn, 6, true, "WolfPup",
                "A Hollow wolf's whelp that chose the living."),
            new WardrobeDef("khagan-eagle", "Khagan's Eagle", WardrobeKind.Companion, 4, WardrobePerk.Xp, 8, true, "Eagle",
                "A golden eagle fit for the Khagan league."),
        };

        public static WardrobeDef? Find(string? id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (WardrobeDef def in All)
                if (def.Id == id) return def;
            return null;
        }

        public static WardrobeDef? FindByName(string name)
        {
            foreach (WardrobeDef def in All)
                if (def.Name == name) return def;
            return null;
        }

        public static string PerkName(WardrobePerk perk) => perk switch
        {
            WardrobePerk.Hp => "HP",
            WardrobePerk.Attack => "Attack",
            WardrobePerk.Xp => "hunting XP",
            _ => "hunting sorn",
        };

        public static string KindName(WardrobeKind kind) => kind switch
        {
            WardrobeKind.Skin => "Skin",
            WardrobeKind.Mount => "Mount",
            _ => "Companion",
        };

        /// <summary>Amber for a piece and a duration; -1 when the Caravan does not sell it for that long.</summary>
        public static int Price(WardrobeDef def, int days)
        {
            int column = Array.IndexOf(Days, days);
            if (!def.Sold || column < 0) return -1;
            return PriceByTier[Math.Max(1, Math.Min(4, def.Tier)) - 1][column];
        }

        // ---- drops ----

        /// <summary>Map bosses (and the Spire's floors fought as bosses) from this stage level on can drop a piece.</summary>
        public const int MinDropLevel = 20;
        /// <summary>
        /// Chance per boss kill at level 20 and above. A hero parked on a map boss kills about 50 an hour, so about one
        /// piece every three hours of boss farming.
        /// </summary>
        public const int BossDropBp = 60;
        /// <summary>The Spire Warden's chest.</summary>
        public const int WardenDropBp = 1000;

        /// <summary>A Commander's chest by damage rank: its own trophy piece.</summary>
        public static int CommanderDropBp(int rank) => rank == 1 ? 1000 : rank <= 5 ? 400 : 0;

        private static readonly int[] TierWeights = { 45, 32, 17, 6 };

        /// <summary>1 day 60%, 3 days 30%, 5 days 7%, 7 days 3%.</summary>
        public static int RollDropDays(IRandom rng)
        {
            int r = rng.NextInt(100);
            return r < 60 ? 1 : r < 90 ? 3 : r < 97 ? 5 : 7;
        }

        /// <summary>A piece from the Caravan's stock, plainer ones more often.</summary>
        public static WardrobeDef RollDropPiece(IRandom rng)
        {
            int total = 0;
            foreach (WardrobeDef def in All) if (def.Sold) total += TierWeights[def.Tier - 1];
            int r = rng.NextInt(total);
            foreach (WardrobeDef def in All)
            {
                if (!def.Sold) continue;
                r -= TierWeights[def.Tier - 1];
                if (r < 0) return def;
            }
            return All[0];
        }

        /// <summary>
        /// Rolls a drop at <paramref name="chanceBp"/>: a random piece (or <paramref name="piece"/>) for a rolled duration,
        /// added to the inventory's pending drops. Returns the loot line, or null.
        /// </summary>
        public static string? RollDrop(Inventory inventory, int chanceBp, IRandom rng, WardrobeDef? piece = null)
        {
            if (chanceBp <= 0 || !rng.RollBp(chanceBp)) return null;
            WardrobeDef def = piece ?? RollDropPiece(rng);
            int days = RollDropDays(rng);
            inventory.WardrobeDrops.Add(def.Id + ":" + days.ToString(CultureInfo.InvariantCulture));
            return KindName(def.Kind).ToUpperInvariant() + ": " + def.Name + " (" + days + (days == 1 ? " day)" : " days)");
        }

        // ---- holding ----

        public static List<OwnedPiece> Parse(string? text)
        {
            var list = new List<OwnedPiece>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (string part in text!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = part.LastIndexOf(':');
                if (colon <= 0) continue;
                if (long.TryParse(part.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long expires))
                    list.Add(new OwnedPiece(part.Substring(0, colon), expires));
            }
            return list;
        }

        public static string Format(IEnumerable<OwnedPiece> owned) =>
            string.Join(";", owned.Select(p => p.Id + ":" + p.ExpiresUnix.ToString(CultureInfo.InvariantCulture)));

        /// <summary>Adds <paramref name="days"/> to a held piece (from now if it has run out), or holds a new one.</summary>
        public static OwnedPiece Grant(List<OwnedPiece> owned, string id, int days, long nowUnix)
        {
            OwnedPiece? piece = owned.FirstOrDefault(p => p.Id == id);
            if (piece == null)
            {
                piece = new OwnedPiece(id, nowUnix);
                owned.Add(piece);
            }
            piece.ExpiresUnix = Math.Max(piece.ExpiresUnix, nowUnix) + days * 86400L;
            return piece;
        }

        /// <summary>Drops pieces that ran out more than a week ago (the list stays short).</summary>
        public static void Prune(List<OwnedPiece> owned, long nowUnix) => owned.RemoveAll(p => p.ExpiresUnix < nowUnix - 7 * 86400L);

        // ---- stats ----

        public static int Bonus(IEnumerable<WardrobeDef>? worn, WardrobePerk perk)
        {
            int sum = 0;
            if (worn == null) return 0;
            foreach (WardrobeDef def in worn) if (def.Perk == perk) sum += def.PerkPercent;
            return sum;
        }

        /// <summary>The worn pieces among these ids that are held and have time left.</summary>
        public static List<WardrobeDef> Worn(IEnumerable<string?> wornIds, List<OwnedPiece> owned, long nowUnix)
        {
            var list = new List<WardrobeDef>();
            foreach (string? id in wornIds)
            {
                WardrobeDef? def = Find(id);
                if (def != null && owned.Any(p => p.Id == def.Id && p.Active(nowUnix))) list.Add(def);
            }
            return list;
        }
    }

    public sealed class AmberPack
    {
        public AmberPack(int id, int amber, int bonus, int priceCents, string storeId)
        {
            Id = id;
            Amber = amber;
            Bonus = bonus;
            PriceCents = priceCents;
            StoreId = storeId;
        }

        public int Id { get; }
        public int Amber { get; }
        public int Bonus { get; }
        public int PriceCents { get; }
        /// <summary>The App Store / Google Play product id (consumable).</summary>
        public string StoreId { get; }
        public string PriceText => "$" + (PriceCents / 100) + "." + (PriceCents % 100).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Amber (owner, 25 Sep 2026: the game's Dragon Coins, bought with real money only; world bible: steppe amber carried on
    /// the salt roads). Not tradable. The first pack an account buys pays its Amber twice.
    /// </summary>
    public static class Amber
    {
        public static readonly AmberPack[] Packs =
        {
            new AmberPack(1, 60, 0, 99, "orsuun.amber.60"),
            new AmberPack(2, 300, 30, 499, "orsuun.amber.300"),
            new AmberPack(3, 650, 80, 999, "orsuun.amber.650"),
            new AmberPack(4, 1400, 200, 1999, "orsuun.amber.1400"),
            new AmberPack(5, 3800, 600, 4999, "orsuun.amber.3800"),
            new AmberPack(6, 8000, 1500, 9999, "orsuun.amber.8000"),
        };

        public static AmberPack? Pack(int id)
        {
            foreach (AmberPack p in Packs)
                if (p.Id == id) return p;
            return null;
        }

        /// <summary>Amber a purchase pays: the pack and its bonus, the pack's Amber doubled on an account's first purchase.</summary>
        public static int Paid(AmberPack pack, bool firstPurchase) => pack.Amber * (firstPurchase ? 2 : 1) + pack.Bonus;
    }
}
