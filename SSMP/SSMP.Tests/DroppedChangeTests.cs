using SSMP.Game.Client.Save;
using Xunit;

// remote_kill_saved (0.3.21): Bone_04's Pilgrim 03 (1) killed by the friend's hit, its death saved in the host's game
// and the enemy destroyed before SSMP compared it, so the death never reached the friend's game
public class DroppedChangeTests {
    [Fact]
    public void A_stored_value_apart_from_the_last_compared_goes_out() => Assert.Equal(true, DroppedChange.Of(true, true, false));

    [Fact]
    public void A_stored_value_already_sent_goes_nowhere() => Assert.Null(DroppedChange.Of(true, true, true));

    [Fact]
    public void A_value_cleared_since_goes_out_too() => Assert.Equal(false, DroppedChange.Of(true, false, true));

    [Fact]
    public void Nothing_stored_sends_nothing() => Assert.Null(DroppedChange.Of(false, false, true));
}
