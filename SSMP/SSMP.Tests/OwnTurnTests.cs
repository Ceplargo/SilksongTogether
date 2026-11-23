using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class OwnTurnTests {
    [Fact]
    public void An_entity_that_turns_by_its_rotation_component_replays_its_own_turn() =>
        // the Bell Goomba (registry components: Rotation): its Dive Launch turn with its Dive Collider
        Assert.True(OwnTurn.Replays(true, true));

    [Fact]
    public void An_entity_without_a_rotation_component_does_not() =>
        Assert.False(OwnTurn.Replays(true, false));

    [Fact]
    public void An_object_outside_the_registry_always_does() =>
        Assert.True(OwnTurn.Replays(false, false));
}
