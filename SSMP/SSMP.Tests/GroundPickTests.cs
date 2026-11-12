using SSMP.Animation.Effects.Movement;
using Xunit;

namespace SSMP.Tests;

public class GroundPickTests {
    // Bone_03 right1: the host ran onto metal and the friend's game played hard ground's footsteps (its own hero's)
    [Theory]
    [InlineData(new byte[] { 4 }, 10, 4)]
    [InlineData(new byte[] { 0 }, 10, 0)]
    [InlineData(new byte[] { 12 }, 10, null)]
    [InlineData(new byte[0], 10, null)]
    [InlineData(null, 10, null)]
    public void A_copy_steps_on_the_senders_ground(byte[]? info, int count, int? ground) {
        Assert.Equal(ground, GroundPick.Of(info, count));
    }

    // Bone_03 left4: a landing mid-run has no Land clip, so the copy lands when the game spawns its landing, once a frame
    [Fact]
    public void A_soft_landing_is_sent_once_a_frame() {
        var last = int.MinValue;
        Assert.True(GroundPick.FirstThisFrame(7, ref last));
        Assert.False(GroundPick.FirstThisFrame(7, ref last));
        Assert.True(GroundPick.FirstThisFrame(8, ref last));
    }
}
