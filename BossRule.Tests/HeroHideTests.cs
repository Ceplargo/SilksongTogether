using BossRule;
using Xunit;

namespace BossRule.Tests;

public class HeroHideTests {
    // the Widow's bind: the binder's hero hidden in its own game
    [Fact]
    public void A_hidden_hero_reads_back_hidden() =>
        Assert.Equal(("Hornet", false, 7), HeroHide.Parse(HeroHide.Message("Hornet", false, 7)));

    [Fact]
    public void A_shown_hero_reads_back_shown() => Assert.Equal(("a|b", true, 8), HeroHide.Parse(HeroHide.Message("a|b", true, 8)));

    [Theory]
    [InlineData("Hornet")]
    [InlineData("Hornet|1")]
    [InlineData("Hornet|2|3")]
    [InlineData("|1|3")]
    [InlineData("Hornet|1|x")]
    public void Anything_else_is_not_one(string text) => Assert.Null(HeroHide.Parse(text));

    [Fact]
    public void A_change_goes() => Assert.True(HeroHide.Sends(false, true, false));

    [Fact]
    public void Still_hidden_does_not_go_again() => Assert.False(HeroHide.Sends(false, false, false));

    [Fact]
    public void Nothing_goes_in_a_scene_change() => Assert.False(HeroHide.Sends(false, true, true));

    [Fact]
    public void A_rooms_first_look_goes_only_hidden() {
        Assert.True(HeroHide.Sends(false, null, false));
        Assert.False(HeroHide.Sends(true, null, false));
    }

    // under lag a hide then a show 0.1 s apart can come the other way round: the hide, older, is dropped
    [Fact]
    public void An_older_count_is_dropped() => Assert.False(HeroHide.Newer(4, 5));

    [Fact]
    public void A_newer_count_is_kept() {
        Assert.True(HeroHide.Newer(6, 5));
        Assert.True(HeroHide.Newer(0, null));
    }
}
