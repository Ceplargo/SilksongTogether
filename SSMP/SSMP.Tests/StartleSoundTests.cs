using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class StartleSoundTests {
    [Fact]
    public void The_walkers_startle_clip_sends_its_alert() =>
        // Bone_09's Dock Worker: its WalkerV2's StartleAnim is "Startle"
        Assert.True(StartleSound.Sends("Startle", "Startle"));

    [Theory]
    [InlineData("Walk")]
    [InlineData("Turn")]
    [InlineData("Idle")]
    public void The_walkers_other_clips_send_no_alert(string clip) =>
        Assert.False(StartleSound.Sends(clip, "Startle"));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void A_walker_with_no_startle_clip_sends_no_alert(string? startle) =>
        Assert.False(StartleSound.Sends(startle, startle));
}
