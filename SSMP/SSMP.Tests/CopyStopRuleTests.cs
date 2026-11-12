using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class CopyStopRuleTests {
    // Bone_03's Bone Goomba stopped dead at its patrol's end; its copy ran on 0.065 units and hurt the friend there
    [Fact]
    public void An_enemy_copy_is_not_carried_past_the_last_place_sent() {
        Assert.False(CopyStopRule.CarriesOn(enemyCopy: true, holdsAtLast: true));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void A_heros_copy_and_the_switch_off_carry_on(bool enemyCopy, bool holdsAtLast) {
        Assert.True(CopyStopRule.CarriesOn(enemyCopy, holdsAtLast));
    }

    [Theory]
    [InlineData(-0.1f, 0.05f, 0f)]
    [InlineData(0.1f, -0.02f, 0f)]
    [InlineData(-0.1f, -0.04f, -0.04f)]
    [InlineData(0.08f, 0.03f, 0.03f)]
    [InlineData(0f, 0f, 0f)]
    public void Smoothing_never_carries_a_copy_across_the_last_place(float before, float after, float settled) {
        Assert.Equal(settled, CopyStopRule.Settle(before, after));
    }
}
