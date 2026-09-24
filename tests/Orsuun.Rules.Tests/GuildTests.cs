using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Guild levels, skills, donations, names and the guild shop.</summary>
public class GuildTests
{
    [Fact]
    public void Levels_follow_the_xp_table()
    {
        Assert.Equal(1, Guilds.Level(0));
        Assert.Equal(1, Guilds.Level(99));
        Assert.Equal(2, Guilds.Level(100));
        Assert.Equal(10, Guilds.Level(7_000));
        Assert.Equal(10, Guilds.Level(1_000_000));
        Assert.Equal(100, Guilds.NextLevelXp(0));
        Assert.Equal(-1, Guilds.NextLevelXp(7_000));
    }

    [Fact]
    public void Muster_raises_the_member_cap_to_forty()
    {
        Assert.Equal(20, Guilds.MaxMembers(0));
        Assert.Equal(30, Guilds.MaxMembers(2));
        Assert.Equal(40, Guilds.MaxMembers(99));
    }

    [Fact]
    public void Skills_need_the_guild_level_and_the_treasury()
    {
        Assert.Null(Guilds.SkillProblem(GuildSkill.Plunder, 0, 1, 200_000));
        Assert.NotNull(Guilds.SkillProblem(GuildSkill.Plunder, 0, 1, 199_999));
        // Plunder 3 needs guild level 4 and 600k.
        Assert.NotNull(Guilds.SkillProblem(GuildSkill.Plunder, 2, 3, 10_000_000));
        Assert.Null(Guilds.SkillProblem(GuildSkill.Plunder, 2, 4, 600_000));
        Assert.NotNull(Guilds.SkillProblem(GuildSkill.Plunder, Guilds.MaxPlunder, 10, 10_000_000));
        Assert.NotNull(Guilds.SkillProblem(GuildSkill.Muster, Guilds.MaxMuster, 10, 10_000_000));
    }

    [Fact]
    public void Donations_pay_one_tally_per_five_thousand_sorn()
    {
        Assert.Equal(0, Guilds.TalliesFor(4_999));
        Assert.Equal(40, Guilds.TalliesFor(Guilds.DailyDonationCap));
    }

    [Theory]
    [InlineData("Canal Wardens", true)]
    [InlineData("Essex Riders", true)]
    [InlineData("Salt-Oath 7", true)]
    [InlineData("Khan's Own", true)]
    [InlineData("ab", false)]
    [InlineData("A name far too long to show", false)]
    [InlineData("Two  Spaces", false)]
    [InlineData("Emoji ☺", false)]
    [InlineData("Sex Riders", false)]
    [InlineData("F u c k Riders", false)]
    public void Guild_names_are_checked(string name, bool ok) => Assert.Equal(ok, Guilds.NameProblem(name) == null);

    [Theory]
    [InlineData("SALT", true)]
    [InlineData("K7", true)]
    [InlineData("salt", false)]
    [InlineData("S", false)]
    [InlineData("SALTS", false)]
    [InlineData("ANAL", false)]
    public void Guild_tags_are_checked(string tag, bool ok) => Assert.Equal(ok, Guilds.TagProblem(tag) == null);

    [Fact]
    public void Officers_remove_members_leaders_remove_anyone_but_themselves()
    {
        Assert.True(Guilds.CanKick(GuildRank.Leader, GuildRank.Officer));
        Assert.False(Guilds.CanKick(GuildRank.Leader, GuildRank.Leader));
        Assert.True(Guilds.CanKick(GuildRank.Officer, GuildRank.Member));
        Assert.False(Guilds.CanKick(GuildRank.Officer, GuildRank.Officer));
        Assert.False(Guilds.CanKick(GuildRank.Member, GuildRank.Member));
    }

    [Fact]
    public void Shop_spends_tallies()
    {
        var inv = new Inventory();
        int tallies = 35;
        Guilds.Buy(Guilds.ShopItem(1)!, ref tallies, inv);
        Assert.Equal(5, tallies);
        Assert.Equal(1, inv.AnvilWards);
        Assert.Throws<InvalidOperationException>(() => Guilds.Buy(Guilds.ShopItem(1)!, ref tallies, inv));
        Assert.Equal(5, tallies);
    }
}
