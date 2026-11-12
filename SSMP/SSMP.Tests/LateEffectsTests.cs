using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class LateEffectsTests {
    // Bone_03's walking Goomba: the copy made after it entered Walking, its Pt Walk not playing
    [Fact]
    public void A_state_particle_not_playing_is_started() => Assert.True(LateEffects.Needed(true, false));

    // the scene host's action came first and played it: not started again
    [Fact]
    public void One_already_playing_is_not() => Assert.False(LateEffects.Needed(true, true));

    [Fact]
    public void None_found_is_not() => Assert.False(LateEffects.Needed(false, false));
}
