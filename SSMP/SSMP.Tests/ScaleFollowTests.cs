using SSMP.Game.Client.Entity.Action;
using Xunit;

namespace SSMP.Tests;

public class ScaleFollowTests {
    [Fact]
    public void A_value_the_copy_already_shows_is_not_set_again() {
        // an every-frame SetScale on the copy sets its scale only when its variables have changed since the frame
        // before, so a variable nothing moves never writes over the scale the scene host sent
        Assert.False(ScaleFollow.Changed(0.75f, 0.75f));
        Assert.True(ScaleFollow.Changed(0.75f, 0.9f));
    }
}
