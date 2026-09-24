using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>The "next goal" line: one goal at a time, reminders first, met chain steps stay behind.</summary>
public class GoalTests
{
    private static PlayerSession Fresh() => new PlayerSession(new XorShiftRandom(7));

    private static void SetCleared(PlayerSession s, int stages) =>
        s.ApplyRemote(s.Inventory, s.Equipment.ToList(), 0, stages, 1);

    [Fact]
    public void A_new_hero_starts_at_the_forge_then_pushes()
    {
        PlayerSession s = Fresh();
        Goal first = Goals.Next(s, default, 0)!;
        Assert.Equal("forge-1", first.Id);
        Assert.Equal(GoalScreen.Forge, first.Screen);
        Assert.Equal(0, first.Step);

        s.Weapon.UpgradeLevel = 1;
        Goal push = Goals.Next(s, default, 0)!;
        Assert.Equal("push-1", push.Id);
        Assert.Equal(GoalScreen.Push, push.Screen);
        // The clear names what it opens.
        Assert.Contains("The Oathfields 1", push.Text);
        Assert.Contains("Ember Steppe", push.Text);
    }

    [Fact]
    public void A_met_step_stays_met_when_its_condition_lapses()
    {
        PlayerSession s = Fresh();
        s.Weapon.UpgradeLevel = 1;
        Goal push = Goals.Next(s, default, 0)!;
        // The +1 is lost on the next attempt; the caller remembers the push step was reached.
        s.Weapon.UpgradeLevel = 0;
        Assert.Equal("forge-1", Goals.Next(s, default, 0)!.Id);
        Assert.Equal("push-1", Goals.Next(s, default, push.Step)!.Id);
    }

    [Fact]
    public void Reminders_come_before_the_chain()
    {
        PlayerSession s = Fresh();
        s.Inventory.Loot.Add(new ItemState(12, Rarity.Common, EquipSlot.Shoes));
        s.Inventory.Loot.Add(new ItemState(14, Rarity.Common, EquipSlot.Helmet));
        s.Inventory.Loot.Add(new ItemState(20, Rarity.Rare, EquipSlot.Helmet));
        s.Inventory.Loot.Add(new ItemState(30, Rarity.Rare, EquipSlot.Weapon));   // the weapon slot is taken

        Goal wear = Goals.Next(s, default, 0)!;
        Assert.Equal("wear-Helmet", wear.Id);
        Assert.Equal(-1, wear.Step);
        Assert.Contains("Rare", wear.Text);

        // Online, a finished bounty goes first; offline it is never asked for.
        Assert.Equal("bounty", Goals.Next(s, new GoalWorld { Online = true, BountyReady = true }, 0)!.Id);
        Assert.Equal("wear-Helmet", Goals.Next(s, new GoalWorld { Online = false, BountyReady = true }, 0)!.Id);
    }

    [Fact]
    public void The_guild_step_is_online_only_and_the_chain_ends()
    {
        PlayerSession s = Fresh();
        int guild = -1;
        for (int i = 0; i < Goals.ChainLength; i++)
        {
            Goal? g = Goals.Next(s, new GoalWorld { Online = true }, i);
            if (g?.Id == "guild") { guild = g.Step; break; }
        }
        Assert.True(guild > 0);
        Assert.Equal("guild", Goals.Next(s, new GoalWorld { Online = true }, guild)!.Id);
        Assert.NotEqual("guild", Goals.Next(s, new GoalWorld { Online = false }, guild)!.Id);
        Assert.NotEqual("guild", Goals.Next(s, new GoalWorld { Online = true, InGuild = true }, guild)!.Id);
        Assert.Null(Goals.Next(s, default, Goals.ChainLength));
    }

    [Fact]
    public void Progress_counts_toward_the_target()
    {
        PlayerSession s = Fresh();
        s.Weapon.UpgradeLevel = 1;
        SetCleared(s, 1);
        Goal wear = Goals.Next(s, default, 0)!;
        Assert.Equal("wear-3", wear.Id);
        Assert.Equal(1, wear.Current);
        Assert.Equal(3, wear.Target);

        SetCleared(s, 3);
        s.Inventory.Loot.Add(new ItemState(12, Rarity.Common, EquipSlot.Shoes));
        s.Inventory.Loot.Add(new ItemState(12, Rarity.Common, EquipSlot.Helmet));
        foreach (ItemState item in s.Inventory.Loot.ToList()) s.Equip(item);
        Goal forge = Goals.Next(s, default, 0)!;
        Assert.Equal("forge-3", forge.Id);
        Assert.Equal(1, forge.Current);
    }
}
