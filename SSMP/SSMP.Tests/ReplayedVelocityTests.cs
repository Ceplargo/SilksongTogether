using SSMP.Game.Client.Entity.Action;
using Xunit;

namespace SSMP.Tests;

// 0.3.20 Task 14: Aqueduct_01's Swamp Mosquito Skinny. The host's body is Dynamic and its velocity back to (0, 0) after a
// dash; SSMP replayed SetVelocity2d onto the copy, whose body is Kinematic and placed by PredictiveInterpolation, and
// nothing zeroed it: it kept (-29.42, -5.89), physics moved it about 0.59 units a step and the interpolation pulled it
// back each frame (0.6 and 1.2 units off the host's in about 40% of reads, 0.3.19's probe_mosquito_hold).
public class ReplayedVelocityTests {
    [Fact]
    public void A_kinematic_copy_placed_by_interpolation_keeps_no_replayed_velocity() =>
        Assert.True(ReplayedVelocity.Drops(kinematic: true, placed: true));

    [Fact]
    public void A_dynamic_copy_still_gets_it() => Assert.False(ReplayedVelocity.Drops(kinematic: false, placed: true));

    [Fact]
    public void A_kinematic_body_nothing_places_still_gets_it() =>
        Assert.False(ReplayedVelocity.Drops(kinematic: true, placed: false));
}
