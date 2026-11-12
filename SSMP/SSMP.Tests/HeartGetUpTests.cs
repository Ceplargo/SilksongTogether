using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class HeartGetUpTests {
    // Bone_05's Silk Heart, its Control FSM: its clip tables are its hero's get-up
    [Fact]
    public void The_silk_hearts_control_sends() => Assert.True(HeartGetUp.Sends("Silk Heart", "Control"));

    [Fact]
    public void Another_object_or_fsm_does_not() {
        Assert.False(HeartGetUp.Sends("Silk Heart", "Shell"));
        Assert.False(HeartGetUp.Sends("Pilgrim 01", "Control"));
        Assert.False(HeartGetUp.Sends(null, null));
    }

    [Fact]
    public void A_plays_state_and_action_go_and_come_back() {
        Assert.Equal((31, 2), HeartGetUp.Read(HeartGetUp.Info(31, 2)));
    }

    [Fact]
    public void Indexes_past_a_byte_or_a_wrong_info_are_none() {
        Assert.Null(HeartGetUp.Info(300, 0));
        Assert.Null(HeartGetUp.Info(0, -1));
        Assert.Null(HeartGetUp.Read(null));
        Assert.Null(HeartGetUp.Read(new byte[] { 1 }));
    }
}
