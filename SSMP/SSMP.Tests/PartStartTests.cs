using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class PartStartTests {
    [Fact]
    public void A_child_turned_on_shows_its_animations_start_at_once() {
        // a Rock Roller bomb's copy blast, turned on after the frame's animations ran, showed its last blast's full size
        Assert.True(PartStart.Evaluates(wasOn: false, isOn: true));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void A_child_left_on_or_turned_off_keeps_its_animations(bool wasOn, bool isOn) {
        Assert.False(PartStart.Evaluates(wasOn, isOn));
    }
}
