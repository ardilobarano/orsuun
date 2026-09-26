using System.Collections.Generic;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;
using Xunit.Abstractions;

/// <summary>Every class must push at about the Vanguard's pace and earn about the same for aimed play (GDD ~130%).</summary>
public class ClassBalanceTests
{
    private readonly ITestOutputHelper _out;
    public ClassBalanceTests(ITestOutputHelper output) => _out = output;

    private static ItemState Starter() => new ItemState(10, Rarity.Rare);

    [Theory]
    [InlineData(HeroClass.Kestrel)]
    [InlineData(HeroClass.Wraithsworn)]
    [InlineData(HeroClass.Drumcaller)]
    public void Pushes_at_about_the_Vanguard_pace(HeroClass cls)
    {
        long vanguard = 0, other = 0;
        int vClears = 0, oClears = 0;
        for (int stage = 1; stage <= 5; stage++)
            for (ulong seed = 1; seed <= 6; seed++)
            {
                StageRunResult v = StageRun.Simulate(Content.Stage(stage), HeroFactory.FromEquipment(new[] { Starter() }, stage, HeroClass.Vanguard), new Inventory { Potions = 30 }, seed);
                StageRunResult o = StageRun.Simulate(Content.Stage(stage), HeroFactory.FromEquipment(new[] { Starter() }, stage, cls), new Inventory { Potions = 30 }, seed);
                vanguard += v.Ticks;
                other += o.Ticks;
                if (v.Cleared) vClears++;
                if (o.Cleared) oClears++;
            }
        double ratio = other / (double)vanguard;
        _out.WriteLine($"{cls} / Vanguard push time over stages 1-5: {ratio:0.00}, clears {oClears} vs {vClears}");
        Assert.InRange(ratio, 0.75, 1.25);
        Assert.True(oClears >= vClears - 3);
    }

    [Theory]
    [InlineData(HeroClass.Vanguard)]
    [InlineData(HeroClass.Kestrel)]
    [InlineData(HeroClass.Wraithsworn)]
    [InlineData(HeroClass.Drumcaller)]
    public void Aimed_play_pays_about_the_same_for_every_class(HeroClass cls)
    {
        SkillDef[] skills = SkillDef.For(cls);
        long sum = 0;
        int n = 0;
        foreach (int st in new[] { 1, 3, 5 })
            for (ulong seed = 1; seed <= 8; seed++)
            {
                HeroStats hero = HeroFactory.FromEquipment(new[] { Starter() }, st, cls);
                LaneSim lane = ActivePlay.NewLoop(Content.Stage(st), hero, skills, new Inventory { Potions = 30 }, seed, 0);
                var casts = new List<CastInput>();
                while (lane.CurrentTick < ActivePlay.MaxLoopTicks && lane.Cycles == 0)
                {
                    if (lane.Phase == LanePhase.Fighting)
                    {
                        if (lane.TryCast(0)) casts.Add(new CastInput(lane.CurrentTick, 0));
                        if ((lane.Enemies.Count >= 3 || lane.IsKorstoneEncounter) && lane.TryCast(1)) casts.Add(new CastInput(lane.CurrentTick, 1));
                        if (lane.IsKorstoneEncounter && lane.TryCast(2)) casts.Add(new CastInput(lane.CurrentTick, 2));
                    }
                    lane.Tick();
                    lane.DrainEvents();
                }
                LoopVerdict v = ActivePlay.Verify(Content.Stage(st), hero, skills, seed, 0, new[] { false, false, false }, casts, 30, lane.CurrentTick);
                Assert.True(v.Accepted, v.Reason);
                sum += (long)v.BaselineTicks * 10000 / v.Ticks;
                n++;
            }
        long average = sum / n;
        _out.WriteLine($"{cls} aimed play over {n} loops: {average / 100}% of auto-cast pace");
        Assert.InRange(average, 11800, 13600);
    }

    private static HeroStats Geared(int level, int upgrade, HeroClass cls)
    {
        var items = new List<ItemState>();
        for (int s = 0; s < 8; s++) items.Add(new ItemState(level, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = upgrade });
        return HeroFactory.FromEquipment(items, level, cls);
    }

    /// <summary>
    /// Late game (owner, 25 Sep 2026: balance the classes past the Oathfields): each class clears the Salt Sea's and
    /// Whitefang's bosses one forge level above the Vanguard's need, and none clears Whitefang two levels below it.
    /// </summary>
    [Theory]
    [InlineData(HeroClass.Vanguard)]
    [InlineData(HeroClass.Kestrel)]
    [InlineData(HeroClass.Wraithsworn)]
    [InlineData(HeroClass.Drumcaller)]
    public void Late_map_bosses_ask_every_class_for_about_the_same_forge_level(HeroClass cls)
    {
        // Sixty seeds (ten until 26 Sep 2026, when weapon drops began to draw their rolls), the same share to pass.
        int s30 = 0, s40 = 0, s40Low = 0;
        for (ulong seed = 1; seed <= 60; seed++)
        {
            if (StageRun.Simulate(Content.Stage(30), Geared(30, 6, cls), new Inventory { Potions = 5 }, seed).Cleared) s30++;
            if (StageRun.Simulate(Content.Stage(40), Geared(40, 7, cls), new Inventory { Potions = 5 }, seed).Cleared) s40++;
            if (StageRun.Simulate(Content.Stage(40), Geared(40, 4, cls), new Inventory { Potions = 5 }, seed).Cleared) s40Low++;
        }
        _out.WriteLine($"{cls}: Mirage Queen at +6 {s30}/60, Nine-Winters at +7 {s40}/60, at +4 {s40Low}/60");
        Assert.True(s30 >= 42 && s40 >= 42, $"{cls}: {s30}/60, {s40}/60");
        Assert.True(s40Low <= 18, $"{cls} at +4: {s40Low}/60");
    }

    /// <summary>
    /// The Cinder Marches and Whisperwood (25 Sep 2026): the Vanguard clears Azhdar at +7 and the Lantern Widow at +8
    /// (StageAndGearTests); every class does within one forge level of that, and none clears the Widow at +5.
    /// </summary>
    [Theory]
    [InlineData(HeroClass.Vanguard)]
    [InlineData(HeroClass.Kestrel)]
    [InlineData(HeroClass.Wraithsworn)]
    [InlineData(HeroClass.Drumcaller)]
    public void The_fifth_and_sixth_map_bosses_ask_every_class_within_a_forge_level(HeroClass cls)
    {
        int s50 = 0, s60 = 0, s60Low = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            if (StageRun.Simulate(Content.Stage(50), Geared(50, 8, cls), new Inventory { Potions = 5 }, seed).Cleared) s50++;
            if (StageRun.Simulate(Content.Stage(60), Geared(58, 9, cls), new Inventory { Potions = 5 }, seed).Cleared) s60++;
            if (StageRun.Simulate(Content.Stage(60), Geared(58, 5, cls), new Inventory { Potions = 5 }, seed).Cleared) s60Low++;
        }
        _out.WriteLine($"{cls}: Azhdar at +8 {s50}/10, the Lantern Widow at +9 {s60}/10, at +5 {s60Low}/10");
        Assert.True(s50 >= 7 && s60 >= 7, $"{cls}: {s50}/10, {s60}/10");
        Assert.True(s60Low <= 3, $"{cls} at +5: {s60Low}/10");
    }

    /// <summary>
    /// The Bloodbirch and the Drowned Steppe (25 Sep 2026): past the +9 cap the rarity carries the climb. Every class
    /// clears the Rootfather and the Coil Mother with an Epic +9 set; none does with the Salt Sea's +9.
    /// </summary>
    [Theory]
    [InlineData(HeroClass.Vanguard)]
    [InlineData(HeroClass.Kestrel)]
    [InlineData(HeroClass.Wraithsworn)]
    [InlineData(HeroClass.Drumcaller)]
    public void The_seventh_and_eighth_map_bosses_ask_for_epic_gear(HeroClass cls)
    {
        HeroStats Epic(int level, int upgrade)
        {
            var items = new List<ItemState>();
            for (int s = 0; s < 8; s++) items.Add(new ItemState(level, Rarity.Epic, (EquipSlot)s) { UpgradeLevel = upgrade });
            return HeroFactory.FromEquipment(items, level, cls);
        }
        int s70 = 0, s80 = 0, low = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            if (StageRun.Simulate(Content.Stage(70), Epic(66, 9), new Inventory { Potions = 5 }, seed).Cleared) s70++;
            if (StageRun.Simulate(Content.Stage(80), Epic(74, 9), new Inventory { Potions = 5 }, seed).Cleared) s80++;
            if (StageRun.Simulate(Content.Stage(80), Geared(30, 9, cls), new Inventory { Potions = 5 }, seed).Cleared) low++;
        }
        _out.WriteLine($"{cls}: the Rootfather at Epic +9 {s70}/10, the Coil Mother {s80}/10, with Salt Sea gear {low}/10");
        Assert.True(s70 >= 7 && s80 >= 7, $"{cls}: {s70}/10, {s80}/10");
        Assert.Equal(0, low);
    }

    /// <summary>
    /// Colossus Graves and the Sunken Bazaar (25 Sep 2026): Hurm the Unburied and the Last Merchant-Prince (105% like the
    /// Coil Mother) fall to every class with an Epic +9 set of the map's level; the Bloodbirch's Epic +9 rarely does it.
    /// </summary>
    [Theory]
    [InlineData(HeroClass.Vanguard)]
    [InlineData(HeroClass.Kestrel)]
    [InlineData(HeroClass.Wraithsworn)]
    [InlineData(HeroClass.Drumcaller)]
    public void The_ninth_and_tenth_map_bosses_ask_for_the_maps_epic_gear(HeroClass cls)
    {
        HeroStats Epic(int level)
        {
            var items = new List<ItemState>();
            for (int s = 0; s < 8; s++) items.Add(new ItemState(level, Rarity.Epic, (EquipSlot)s) { UpgradeLevel = 9 });
            return HeroFactory.FromEquipment(items, level, cls);
        }
        int s90 = 0, s100 = 0, low = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            if (StageRun.Simulate(Content.Stage(90), Epic(82), new Inventory { Potions = 5 }, seed).Cleared) s90++;
            if (StageRun.Simulate(Content.Stage(100), Epic(90), new Inventory { Potions = 5 }, seed).Cleared) s100++;
            if (StageRun.Simulate(Content.Stage(90), Epic(66), new Inventory { Potions = 5 }, seed).Cleared) low++;
        }
        _out.WriteLine($"{cls}: Hurm at Epic +9 {s90}/20, the Last Merchant-Prince {s100}/20, Hurm with the Bloodbirch's {low}/20");
        Assert.True(s90 >= 14 && s100 >= 14, $"{cls}: {s90}/20, {s100}/20");
        Assert.True(low <= 4, $"{cls} with Bloodbirch gear: {low}/20");
    }

    /// <summary>
    /// The Thousand Markers and the Hollow Throne (26 Sep 2026, 105% like the last three): Varkesh of the Left Wing falls
    /// to every class with an Epic +9 set of the map's level and rarely to Colossus Graves' set. The Khan's Shadow, the
    /// campaign's last boss, meets heroes who can no longer level: an Epic +9 set at level 105 wins about half the time,
    /// a Legendary +9 set nearly always.
    /// </summary>
    [Theory]
    [InlineData(HeroClass.Vanguard)]
    [InlineData(HeroClass.Kestrel)]
    [InlineData(HeroClass.Wraithsworn)]
    [InlineData(HeroClass.Drumcaller)]
    public void The_last_two_map_bosses_ask_for_the_best_gear(HeroClass cls)
    {
        HeroStats Set(int level, Rarity rarity)
        {
            var items = new List<ItemState>();
            for (int s = 0; s < 8; s++) items.Add(new ItemState(level, rarity, (EquipSlot)s) { UpgradeLevel = 9 });
            return HeroFactory.FromEquipment(items, level, cls);
        }
        int varkesh = 0, varkeshLow = 0, epic = 0, legendary = 0, shadowLow = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            if (StageRun.Simulate(Content.Stage(110), Set(98, Rarity.Epic), new Inventory { Potions = 5 }, seed).Cleared) varkesh++;
            if (StageRun.Simulate(Content.Stage(110), Set(82, Rarity.Epic), new Inventory { Potions = 5 }, seed).Cleared) varkeshLow++;
            if (StageRun.Simulate(Content.Stage(120), Set(Content.MaxLevel, Rarity.Epic), new Inventory { Potions = 5 }, seed).Cleared) epic++;
            if (StageRun.Simulate(Content.Stage(120), Set(Content.MaxLevel, Rarity.Legendary), new Inventory { Potions = 5 }, seed).Cleared) legendary++;
            if (StageRun.Simulate(Content.Stage(120), Set(98, Rarity.Epic), new Inventory { Potions = 5 }, seed).Cleared) shadowLow++;
        }
        _out.WriteLine($"{cls}: Varkesh at Epic +9 {varkesh}/20, with Colossus Graves' {varkeshLow}/20; the Khan's Shadow at Epic +9 {epic}/20, Legendary +9 {legendary}/20, the Markers' Epic {shadowLow}/20");
        Assert.True(varkesh >= 14, $"{cls}: Varkesh {varkesh}/20");
        Assert.True(varkeshLow <= 4, $"{cls} with Colossus Graves gear: {varkeshLow}/20");
        Assert.True(legendary >= 17, $"{cls}: the Khan's Shadow with Legendary {legendary}/20");
        Assert.InRange(epic, 6, 16);
        Assert.True(shadowLow <= 6, $"{cls} with the Markers' gear: {shadowLow}/20");
    }
}
