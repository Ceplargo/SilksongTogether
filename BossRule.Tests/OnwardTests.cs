using Xunit;

namespace BossRule.Tests;

public class OnwardTests {
    // Dock_09's first fight: Battle Gate (2), toward right1, shut only at the fight's start, so it stayed open while the
    // fight waited for a player still on the way (round three's walk-past sweep)
    private static readonly BossOnward[] Dock09 = { new("Boss Scene/Gates/Battle Gate (2)", new[] { "left1" }) };

    [Fact]
    public void A_held_fight_shuts_what_is_onward_from_the_door_its_hero_came_in_by() =>
        Assert.Equal(new[] { "Boss Scene/Gates/Battle Gate (2)" }, Onward.Shut(Dock09, "left1"));

    // the second player's game holds the start too (the first player's copy stands in its range): its player, at the
    // same door, keeps its way in open, since only what is onward from that door is shut (the review's Critical)
    [Fact]
    public void Nothing_named_for_another_door_is_shut() => Assert.Empty(Onward.Shut(Dock09, "right1"));

    [Fact]
    public void Without_a_known_way_in_nothing_is_shut() => Assert.Empty(Onward.Shut(Dock09, null));
}
