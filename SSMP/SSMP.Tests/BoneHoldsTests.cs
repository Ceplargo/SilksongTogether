using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class BoneHoldsTests {
    // probe_bridge_walk 234751-pair2: Segment 6 left at 2.49 s and back at 2.53 s, a second press and dust
    [Fact]
    public void A_step_off_and_back_is_no_release_and_no_press() {
        var holds = new BoneHolds();
        holds.Left(6, 2.49);
        Assert.Empty(holds.Due(2.52));
        Assert.True(holds.Back(6));
        Assert.Empty(holds.Due(2.80));
    }

    [Fact]
    public void A_segment_left_for_the_hold_is_released_once() {
        var holds = new BoneHolds();
        holds.Left(6, 2.70);
        Assert.Empty(holds.Due(2.75));
        Assert.Equal([6], holds.Due(2.81));
        Assert.Empty(holds.Due(3.0));
        Assert.False(holds.Back(6));
    }

    [Fact]
    public void A_segment_never_left_is_a_new_press() => Assert.False(new BoneHolds().Back(3));
}
