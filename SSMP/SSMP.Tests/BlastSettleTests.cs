using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class BlastSettleTests {
    // the Thrower's stomp blast switched on: his place and facing go with it
    [Fact]
    public void Switching_on_a_hurt_box_sends_the_facing() {
        Assert.Equal(BlastSettle.FacesRight, BlastSettle.Flag(true, true, 1f));
        Assert.Equal(BlastSettle.FacesLeft, BlastSettle.Flag(true, true, -1.5f));
    }

    // an effect with no hurt box, or a switch-off, sends no place
    [Fact]
    public void Anything_else_sends_no_place() {
        Assert.Equal(BlastSettle.NoPlace, BlastSettle.Flag(true, false, 1f));
        Assert.Equal(BlastSettle.NoPlace, BlastSettle.Flag(false, true, -1f));
    }

    // the friend reads back what the host wrote; any other byte has no place after it
    [Fact]
    public void The_flag_reads_back() {
        Assert.True(BlastSettle.HasPlace(BlastSettle.Flag(true, true, -1f)));
        Assert.Equal(-1f, BlastSettle.Sign(BlastSettle.Flag(true, true, -1f)));
        Assert.Equal(1f, BlastSettle.Sign(BlastSettle.Flag(true, true, 1f)));
        Assert.False(BlastSettle.HasPlace(BlastSettle.NoPlace));
        Assert.False(BlastSettle.HasPlace(7));
    }

    // the host's turn reached the friend's body before the activation: the switch-on sees the host's place and facing at
    // the activation, and the body ends at its own place, with the facing as updated
    [Fact]
    public void A_turn_that_came_first_stays() {
        var body = (x: 5f, facing: -1f);
        (float x, float facing) atSwitchOn = (0f, 0f);
        BlastSettle.PosedFor(body, (3f, 1f), p => body = p, () => atSwitchOn = body);
        Assert.Equal((3f, 1f), atSwitchOn);
        Assert.Equal((5f, -1f), body);
    }
}
