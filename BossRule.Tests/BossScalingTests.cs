using Xunit;

namespace BossRule.Tests;

public class BossScalingTests {
    [Theory]
    [InlineData(1, 50, 0, 1.0f)]
    [InlineData(2, 50, 0, 1.5f)]
    [InlineData(4, 50, 0, 2.5f)]
    [InlineData(3, 30, 0, 1.6f)]
    [InlineData(3, 50, 200, 2.0f)]
    public void Multiplier_follows_players_or_the_fixed_value(int players, int perPlayer, int fixedPct, float expected) =>
        Assert.Equal(expected, BossScaling.Multiplier(players, perPlayer, fixedPct), 3);

    [Fact]
    public void Health_and_switch_points_scale_together() {
        Assert.Equal(2000, BossScaling.ScaleHp(1000, 2f));
        Assert.Equal(1000f, BossScaling.ScaleThreshold(500f, 2f));
    }

    [Fact]
    public void Multiplier_never_drops_below_a_tenth() =>
        Assert.Equal(0.1f, BossScaling.Multiplier(2, -500, 0), 3);

    [Fact]
    public void Health_rounds_to_the_nearest_whole_number_and_stays_above_zero() {
        Assert.Equal(371, BossScaling.ScaleHp(247, 1.5f));
        Assert.Equal(1, BossScaling.ScaleHp(3, 0.1f));
    }

}
