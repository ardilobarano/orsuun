using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Characters (owner, 25 Sep 2026): four slots per account, a shared depot, names chosen by the player.</summary>
public class CharacterTests
{
    [Fact]
    public void Four_slots_and_a_forty_piece_depot()
    {
        Assert.Equal(4, Characters.MaxSlots);
        Assert.Equal(40, Characters.DepotSlots);
    }

    [Theory]
    [InlineData("Temujin", null)]
    [InlineData("Börte2", null)]
    [InlineData("Al", "A name has 3 to 16 letters or digits.")]
    [InlineData("SeventeenLetters1", "A name has 3 to 16 letters or digits.")]
    [InlineData("2Fast", "A name starts with a letter.")]
    [InlineData("Wild Eagle", "Letters and digits only, no spaces.")]
    [InlineData("Shitlord", "Choose another name.")]
    public void Names_are_letters_and_digits_and_clean(string name, string? problem)
    {
        Assert.Equal(problem, Characters.NameProblem(name));
    }

    [Fact]
    public void Names_are_unique_whatever_the_case() => Assert.Equal(Characters.NameKey("Temujin"), Characters.NameKey(" TEMUJIN "));

    [Fact]
    public void Suggested_names_are_always_allowed_and_vary()
    {
        var rng = new XorShiftRandom(9);
        var seen = new HashSet<string>();
        foreach (Figure figure in new[] { Figure.Man, Figure.Woman })
            for (int i = 0; i < 400; i++)
            {
                string name = Characters.SuggestName(figure, rng);
                Assert.Null(Characters.NameProblem(name));
                seen.Add(name);
            }
        Assert.True(seen.Count > 200, $"{seen.Count} different names");
    }
}
