using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class StillResendTests {
    // the hero just stopped: nothing yet
    [Fact]
    public void Nothing_goes_again_before_the_first_wait() {
        var sent = 0;
        Assert.False(StillResend.Due(50, ref sent));
        Assert.Equal(0, sent);
    }

    // the Act 1 sweep under lag: a lost last update left the copy off for 3 s; the place goes again at 0.1 and 0.3 s
    [Fact]
    public void The_place_goes_again_at_each_wait_once() {
        var sent = 0;
        Assert.True(StillResend.Due(120, ref sent));
        Assert.False(StillResend.Due(150, ref sent));
        Assert.True(StillResend.Due(310, ref sent));
        Assert.False(StillResend.Due(5000, ref sent));
        Assert.Equal(2, sent);
    }

    // a long frame past both waits: one send now, the second the frame after
    [Fact]
    public void A_long_frame_past_both_waits_sends_twice_in_turn() {
        var sent = 0;
        Assert.True(StillResend.Due(400, ref sent));
        Assert.True(StillResend.Due(410, ref sent));
        Assert.False(StillResend.Due(420, ref sent));
    }
}
