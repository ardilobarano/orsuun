using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Chat text, the word filter, the Salt Exchange's tax and account checks.</summary>
public class SocialTests
{
    [Fact]
    public void Chat_stars_bad_words_and_keeps_the_rest()
    {
        Assert.Equal("well **** that boar", Chat.Clean("well  fuck\tthat boar"));
        Assert.Equal("Canal Wardens of Essex", Chat.Clean("Canal Wardens of Essex"));
        Assert.Equal("*** now", Chat.Clean("SEX now"));
        Assert.NotNull(Chat.TextProblem(""));
        Assert.NotNull(Chat.TextProblem(new string('a', Chat.MaxLength + 1)));
        Assert.Null(Chat.TextProblem("hello"));
    }

    [Fact]
    public void Guild_channels_are_per_guild()
    {
        var id = Guid.Parse("3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90");
        Assert.Equal("g:3f2a9c1e5b7d4e8a9c217d4e5f6a8b90", Chat.GuildChannel(id));
        Assert.True(Chat.GuildChannel(id).Length <= 40);
    }

    [Fact]
    public void Exchange_takes_five_percent_from_the_seller()
    {
        Assert.Equal(5_000, Market.Tax(100_000));
        Assert.Equal(95_000, Market.Payout(100_000));
        Assert.Equal(950, Market.Payout(1_000));
        Assert.NotNull(Market.PriceProblem(999));
        Assert.Null(Market.PriceProblem(1_000));
        Assert.NotNull(Market.PriceProblem(Market.MaxPrice + 1));
    }

    [Theory]
    [InlineData("rider@steppe.org", true)]
    [InlineData("a.b+c@mail.co.uk", true)]
    [InlineData("rider", false)]
    [InlineData("rider@", false)]
    [InlineData("rider@steppe", false)]
    [InlineData("ri der@steppe.org", false)]
    [InlineData("a@b@c.org", false)]
    public void Emails_are_checked(string email, bool ok) => Assert.Equal(ok, AccountRules.EmailProblem(AccountRules.NormaliseEmail(email)) == null);

    [Theory]
    [InlineData("korstone-breaker", true)]
    [InlineData("short", false)]
    [InlineData("12345678", false)]
    [InlineData("aaaaaaaa", false)]
    public void Passwords_are_checked(string password, bool ok) => Assert.Equal(ok, AccountRules.PasswordProblem(password) == null);
}
