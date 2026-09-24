#nullable enable
using System;
using System.Linq;

namespace Orsuun.Rules
{
    public enum GuildRank
    {
        Member = 0,
        Officer = 1,
        Leader = 2,
    }

    /// <summary>Guild skills, bought from the treasury by the leader or an officer.</summary>
    public enum GuildSkill
    {
        /// <summary>+1% hunting sorn per level for every member.</summary>
        Plunder = 0,
        /// <summary>+5 member places per level.</summary>
        Muster = 1,
    }

    public sealed class GuildShopItem
    {
        public GuildShopItem(int id, string name, int tallies, string detail, Action<Inventory> grant)
        {
            Id = id;
            Name = name;
            Tallies = tallies;
            Detail = detail;
            Grant = grant;
        }

        public int Id { get; }
        public string Name { get; }
        public int Tallies { get; }
        public string Detail { get; }
        public Action<Inventory> Grant { get; }
    }

    /// <summary>
    /// Guilds (owner, 24 Sep 2026: "create guild as well"; GDD: members of any Banner, a daily donation to the
    /// treasury, Guild Tallies for the guild shop, guild skills toward B_afk, guild flags on fortresses, 50 Tallies for
    /// the guild of a Commander's rank 1). Donations are sorn: they fill the treasury, raise the guild's level and pay
    /// the donor Guild Tallies. Guild skills are bought from the treasury by the leader or an officer.
    /// </summary>
    public static class Guilds
    {
        public const long CreateCost = 100_000;
        public const int BaseMembers = 20;
        public const int MusterStep = 5;
        public const int MaxPlunder = 5;
        public const int MaxMuster = 4;
        /// <summary>What one member may donate in a bounty day (resets at 20:00 with the bounties).</summary>
        public const long DailyDonationCap = 200_000;
        public const long SornPerTally = 5_000;
        /// <summary>Guild XP is donated sorn in thousands, plus siege and Commander feats.</summary>
        public const long SornPerXp = 1_000;
        public const int MaxOfficers = 4;
        /// <summary>A guild's members hunt with this much more sorn for each fortress flying the guild's flag.</summary>
        public const int FlagBonusPercent = 2;
        /// <summary>GDD: the guild of a Commander's rank 1 gets 50 Guild Tallies (paid to that fighter) and XP.</summary>
        public const int CommanderTopTallies = 50;
        public const long CommanderTopXp = 50;
        /// <summary>Siege damage done by a member counts toward the guild's XP.</summary>
        public const long SiegeDamagePerXp = 10_000;

        /// <summary>XP needed for levels 1..10.</summary>
        private static readonly long[] LevelXp = { 0, 100, 300, 600, 1_000, 1_600, 2_400, 3_500, 5_000, 7_000 };

        public static int Level(long xp)
        {
            int level = 1;
            for (int i = 1; i < LevelXp.Length; i++) if (xp >= LevelXp[i]) level = i + 1;
            return level;
        }

        /// <summary>XP at which the next level comes, or -1 at the top.</summary>
        public static long NextLevelXp(long xp)
        {
            int level = Level(xp);
            return level >= LevelXp.Length ? -1 : LevelXp[level];
        }

        public static int MaxMembers(int muster) => BaseMembers + MusterStep * Math.Max(0, Math.Min(MaxMuster, muster));

        /// <summary>Treasury cost of the next level of a skill; each level also needs guild level 2x (1 for the first).</summary>
        public static long SkillCost(GuildSkill skill, int nextLevel) => (skill == GuildSkill.Plunder ? 200_000L : 300_000L) * nextLevel;

        public static int SkillMax(GuildSkill skill) => skill == GuildSkill.Plunder ? MaxPlunder : MaxMuster;

        public static int SkillGuildLevel(int nextLevel) => Math.Max(1, (nextLevel - 1) * 2);

        /// <summary>Why the skill cannot be raised now, or null.</summary>
        public static string? SkillProblem(GuildSkill skill, int current, int guildLevel, long treasury)
        {
            int next = current + 1;
            if (next > SkillMax(skill)) return "That skill is at its peak.";
            if (guildLevel < SkillGuildLevel(next)) return $"The guild must reach level {SkillGuildLevel(next)} first.";
            if (treasury < SkillCost(skill, next)) return "Not enough sorn in the treasury.";
            return null;
        }

        /// <summary>Guild Tallies for a donation.</summary>
        public static int TalliesFor(long sorn) => (int)(sorn / SornPerTally);

        /// <summary>The eight guild colours to choose from, "#RRGGBB".</summary>
        public static readonly string[] Colors =
        {
            "#C0392B", "#2F6FD0", "#E0A81C", "#2E9E5B", "#8E44AD", "#D35400", "#16A5A5", "#B0B0B0",
        };

        // Crude but cheap: guild names are shown to every player, so the obvious slurs and obscenities are refused.
        // Proper moderation (reports, a review queue) comes before a public launch. Strong words are refused anywhere,
        // even spaced out; short ones only as a whole word, so Canal Wardens and Essex Riders pass.
        private static readonly string[] Strong =
        {
            "fuck", "shit", "cunt", "nigg", "hitler", "nazi", "porn", "pussy", "whore", "slut", "bitch", "penis", "vagina",
        };
        private static readonly string[] Short = { "sex", "anal", "anus", "dick", "cock", "fag", "rape", "kys", "tits", "cum" };

        public static bool Clean(string text)
        {
            string lower = text.ToLowerInvariant();
            string letters = new string(lower.Where(char.IsLetter).ToArray());
            if (Strong.Any(letters.Contains)) return false;
            string[] words = lower.Split(new[] { ' ', '-', '\'' }, StringSplitOptions.RemoveEmptyEntries);
            return !words.Any(w => Short.Any(b => w == b || w == b + "s" || w == b + "y"));
        }

        public static string? NameProblem(string? name)
        {
            string n = (name ?? "").Trim();
            if (n.Length < 3 || n.Length > 20) return "A guild name has 3 to 20 characters.";
            if (!n.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '\'' || c == '-')) return "Letters, digits, spaces, ' and - only.";
            if (n.Contains("  ")) return "One space between words.";
            if (!Clean(n)) return "Choose another name.";
            return null;
        }

        public static string? TagProblem(string? tag)
        {
            string t = (tag ?? "").Trim();
            if (t.Length < 2 || t.Length > 4) return "A tag has 2 to 4 letters or digits.";
            if (!t.All(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return "Tags are capital letters and digits.";
            if (!Clean(t)) return "Choose another tag.";
            return null;
        }

        public static string NormaliseName(string name) => string.Join(" ", name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        public static string NormaliseTag(string tag) => tag.Trim().ToUpperInvariant();

        /// <summary>The guild shop: Guild Tallies buy what the Forge wants most.</summary>
        public static readonly GuildShopItem[] Shop =
        {
            new GuildShopItem(1, "Anvil Ward", 30, "A failed Forge keeps its level", i => i.AnvilWards++),
            new GuildShopItem(2, "Khan's Alloy", 40, "+10% chance on a Forge", i => i.KhansAlloys++),
            new GuildShopItem(3, "Trooper Korshard", 8, "A rank 1 shard for a socket", i => i.Korshards[0]++),
        };

        public static GuildShopItem? ShopItem(int id) => Shop.FirstOrDefault(s => s.Id == id);

        /// <summary>Spends Tallies on a guild shop item; throws with the reason when it cannot.</summary>
        public static void Buy(GuildShopItem item, ref int tallies, Inventory inventory)
        {
            if (tallies < item.Tallies) throw new InvalidOperationException("Not enough Guild Tallies.");
            tallies -= item.Tallies;
            item.Grant(inventory);
        }

        /// <summary>What a rank may do to another member's rank. Leaders do anything; officers only remove members.</summary>
        public static bool CanKick(GuildRank actor, GuildRank target) => actor == GuildRank.Leader ? target != GuildRank.Leader : actor == GuildRank.Officer && target == GuildRank.Member;
        public static bool CanManage(GuildRank actor) => actor >= GuildRank.Officer;
    }
}
