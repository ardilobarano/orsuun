using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>A man and a woman of every class (owner, 26 Sep 2026: "Second look per class").</summary>
public class FigureTests
{
    [Fact]
    public void Each_class_was_first_drawn_as_one_figure_and_the_other_wears_its_second_look()
    {
        Assert.Equal(Figure.Man, ItemLooks.NativeFigure(HeroClass.Vanguard));
        Assert.Equal(Figure.Woman, ItemLooks.NativeFigure(HeroClass.Kestrel));
        Assert.Equal(Figure.Man, ItemLooks.NativeFigure(HeroClass.Wraithsworn));
        Assert.Equal(Figure.Woman, ItemLooks.NativeFigure(HeroClass.Drumcaller));
        Assert.True(ItemLooks.SecondLook(HeroClass.Vanguard, Figure.Woman));
        Assert.False(ItemLooks.SecondLook(HeroClass.Kestrel, Figure.Woman));
        Assert.True(ItemLooks.SecondLook(HeroClass.Drumcaller, Figure.Man));
    }

    [Fact]
    public void A_figure_stays_through_a_class_change()
    {
        var session = new PlayerSession(new XorShiftRandom(3)) { Figure = Figure.Woman };
        Assert.True(session.SecondLook);          // a woman Vanguard
        session.SetClass(HeroClass.Kestrel);
        Assert.Equal(Figure.Woman, session.Figure);
        Assert.False(session.SecondLook);         // a Kestrel's first look
    }
}
