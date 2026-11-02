using Xunit;

namespace BossRule.Tests;

public class WaitRuleTests {
    private static PlayerPresence P(ushort id, string name, bool connected, bool inArea) => new(id, name, connected, inArea);

    [Fact]
    public void All_in_area_is_allowed() =>
        Assert.True(WaitRule.Evaluate(new[] { P(1, "Ann", true, true), P(2, "Bob", true, true) }).Allowed);

    [Fact]
    public void Missing_players_are_named_alphabetically_with_counts() {
        var r = WaitRule.Evaluate(new[] { P(1, "Ann", true, true), P(3, "Carol", true, false), P(2, "Bob", true, false) });
        Assert.False(r.Allowed);
        Assert.Equal("Waiting for Bob, Carol (1 of 3 in the arena)", WaitRule.Message(r));
    }

    [Fact]
    public void Disconnected_players_do_not_count() =>
        Assert.True(WaitRule.Evaluate(new[] { P(1, "Ann", true, true), P(2, "Bob", false, false) }).Allowed);

    [Fact]
    public void Disconnected_players_are_not_in_the_counts() {
        var r = WaitRule.Evaluate(new[] { P(1, "Ann", true, true), P(2, "Bob", true, false), P(3, "Carol", false, false) });
        Assert.Equal("Waiting for Bob (1 of 2 in the arena)", WaitRule.Message(r));
    }

    [Fact]
    public void Alone_is_the_normal_game() => Assert.True(WaitRule.Evaluate(new[] { P(1, "Ann", true, true) }).Allowed);
}
