using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class CopyPressRuleTests {
    [Fact]
    public void An_enemy_copys_own_touches_are_passed_by() {
        Assert.True(CopyPressRule.Holds(underPlayerCopy: false, predicted: true));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void A_heros_copy_and_the_games_own_touch_as_the_game_does(bool underPlayerCopy, bool predicted) {
        Assert.False(CopyPressRule.Holds(underPlayerCopy, predicted));
    }

    // Bone_03's bridges: the Goomba's body and its other colliders touch a segment; it touches it from the first in to
    // the last out, and its copy presses it that long
    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 2, null)]
    [InlineData(2, 1, null)]
    [InlineData(1, 0, false)]
    [InlineData(0, 0, null)]
    public void An_enemy_touches_from_its_first_collider_in_to_its_last_out(int before, int after, bool? touching) {
        Assert.Equal(touching, CopyPressRule.Touching(before, after));
    }
}
