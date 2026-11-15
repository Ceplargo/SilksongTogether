using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class TakeoverBodiesTests {
    // Hornet 1: her Wake state turns her dynamic, which a takeover into a later state skips
    [Fact]
    public void An_original_with_a_solid_collider_is_made_dynamic() => Assert.True(TakeoverBodies.MakesDynamic(true, true));

    // Ant_05b's Wave 5 Bone Hunter Child, hidden for its wave: made dynamic it fell through the floor (takeover_arena)
    [Fact]
    public void An_original_with_gravity_and_no_solid_collider_is_made_kinematic() =>
        Assert.False(TakeoverBodies.MakesDynamic(false, true));

    // a flyer with only trigger colliders cannot fall; kinematic it would lose the forces that move it
    [Fact]
    public void An_original_with_no_gravity_is_made_dynamic() => Assert.True(TakeoverBodies.MakesDynamic(false, false));
}
