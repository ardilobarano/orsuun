using System.Linq;
using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

public class MapQuestTests
{
    [Fact]
    public void Every_map_has_a_chain_of_hunt_korstones_and_its_commander()
    {
        Assert.Equal(Content.Maps.Length, MapQuests.All.Length);
        foreach (MapDef map in Content.Maps)
        {
            MapQuestDef quest = MapQuests.For(map.Id)!;
            Assert.NotNull(quest);
            Assert.Equal(new[] { QuestKind.Hunt, QuestKind.Korstones, QuestKind.Commander }, quest.Steps.Select(s => s.Kind));
            Assert.All(quest.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Ask)));
            Assert.Equal(3, quest.Steps.Select(s => s.Camp).Distinct().Count());
        }
    }

    [Fact]
    public void A_maps_commander_stands_on_that_map()
    {
        foreach (MapDef map in Content.Maps)
        {
            BossDef boss = Content.Boss(MapQuests.CommanderOf(map.Id))!;
            Assert.Contains(1000 + map.Id, Content.CommanderPlaces(boss));
            Assert.Equal(map.Id, MapQuests.MapOfCommander(boss.Id));
        }
        Assert.Equal("Old Greyjaw", Content.Boss(MapQuests.CommanderOf(1))!.Name);
    }

    [Fact]
    public void Only_the_current_step_counts_and_it_stops_at_its_target()
    {
        var p = QuestProgress.Parse("");
        Assert.False(p.Add(1, QuestKind.Korstones, 3));                 // the first step asks for hunting
        Assert.True(p.Add(1, QuestKind.Hunt, 400));
        Assert.True(p.Add(1, QuestKind.Hunt, 400));
        Assert.Equal(MapQuests.HuntSeconds, p.Done(1));
        Assert.True(p.Ready(1));
        Assert.False(p.Add(1, QuestKind.Hunt, 10));
        p.Advance(1);
        Assert.Equal(1, p.Step(1));
        Assert.False(p.Ready(1));
        Assert.True(p.Add(1, QuestKind.Korstones, 3));
        Assert.Equal(0, p.Done(2));                                      // another map's chain is its own
        p.Advance(1); p.Advance(1);
        Assert.True(p.Finished(1));
        Assert.Null(p.Current(1));
        Assert.False(p.Add(1, QuestKind.Commander, 1));
    }

    [Fact]
    public void Progress_survives_a_round_trip_and_ignores_rubbish()
    {
        var p = QuestProgress.Parse("");
        p.Add(3, QuestKind.Hunt, 120);
        p.Advance(5);
        string text = p.Serialize();
        Assert.Equal("3:0:120;5:1:0", text);
        var back = QuestProgress.Parse(text + ";99:1:1;x;4:a:2");
        Assert.Equal(120, back.Done(3));
        Assert.Equal(1, back.Step(5));
        Assert.Equal(text, back.Serialize());
    }

    [Fact]
    public void Chains_open_with_their_maps_and_pay_by_the_maps_last_stage()
    {
        Assert.True(MapQuests.Open(1, 0));
        Assert.False(MapQuests.Open(2, 0));
        Assert.True(MapQuests.Open(2, 10));
        Assert.Equal(1, MapQuests.MapOfPlace(7));
        Assert.Equal(2, MapQuests.MapOfPlace(11));
        Assert.Equal(0, MapQuests.MapOfPlace(Content.SaltFlats));
        Assert.True(MapQuests.Sorn(5, last: true) > MapQuests.Sorn(5, last: false));
        Assert.True(MapQuests.Sorn(6, last: false) > MapQuests.Sorn(5, last: false));
        Assert.Equal("Hunt 10 minutes on The Oathfields", MapQuests.Task(MapQuests.For(1)!, MapQuests.For(1)!.Steps[0]));
        Assert.Equal("Fight Warlord Tul-Gorak when it rises", MapQuests.Task(MapQuests.For(2)!, MapQuests.For(2)!.Steps[2]));

        var inventory = new Inventory();
        ItemState piece = MapQuests.Piece(4, inventory, new XorShiftRandom(7));
        Assert.Equal(Rarity.Epic, piece.Rarity);
        Assert.Equal(Content.Stage(40).GearItemLevel, piece.ItemLevel);
    }
}
