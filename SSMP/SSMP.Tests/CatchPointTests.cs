using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class CatchPointTests {
    [Fact]
    public void A_prediction_past_the_catcher_on_the_way_back_stops_there() {
        // gate 7's round: predicted on past the Slasher's copy while the scene host's game stalled
        Assert.Equal(37.5f, CatchPoint.Shown(32.1f, 43.6f, 37.5f));
        Assert.Equal(25f, CatchPoint.Shown(30f, 20f, 25f)); // coming back from the right
    }

    [Fact]
    public void A_prediction_short_of_the_catcher_is_shown_as_it_is() {
        Assert.Equal(35f, CatchPoint.Shown(32.1f, 35f, 37.5f));
    }

    [Fact]
    public void A_throw_flying_away_from_the_catcher_is_not_stopped() {
        Assert.Equal(45f, CatchPoint.Shown(40f, 45f, 37.5f));
        Assert.Equal(20f, CatchPoint.Shown(30f, 20f, 37.5f));
    }
}
