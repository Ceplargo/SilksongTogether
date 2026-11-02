using SSMP.Game.Server;
using Xunit;

namespace SSMP.Tests;

public class UsernameRulesTests {
    [Fact]
    public void A_typed_name_wins() => Assert.Equal("Sam", UsernameRules.Choose("Sam", "Hornet"));

    [Theory]
    [InlineData("Hornet")]
    [InlineData("Jörg2")]
    [InlineData("abcdefghij0123456789")]
    public void An_empty_box_takes_a_Steam_name_the_server_accepts(string steam) =>
        Assert.Equal(steam, UsernameRules.Choose("", steam));

    [Theory]
    [InlineData("Bo jini")]
    [InlineData("Sam!")]
    [InlineData("Sam😀")]
    [InlineData("abcdefghij0123456789x")]
    [InlineData("")]
    [InlineData(null)]
    public void Otherwise_the_box_stays_empty_for_the_old_message(string? steam) =>
        Assert.Equal("", UsernameRules.Choose("", steam));
}
