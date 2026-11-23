using SSMP.Game.Client.Entity.Action;
using Xunit;

namespace SSMP.Tests;

public class FlingVelTimeTests {
    // the Giant Centipede's Spit Floor: x from -10 to -3 and y from 21 to 24 (Bellway_Centipede_Arena's dump)
    [Fact]
    public void Its_x_comes_from_the_x_range_and_its_y_from_the_y_range() =>
        Assert.Equal((-10f, 21f), FlingVelTime.Velocity(-10f, -3f, 21f, 24f, (min, _) => min));

    [Fact]
    public void Each_takes_its_own_ranges_top() =>
        Assert.Equal((-3f, 24f), FlingVelTime.Velocity(-10f, -3f, 21f, 24f, (_, max) => max));
}
