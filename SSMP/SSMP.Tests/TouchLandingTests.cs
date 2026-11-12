using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class TouchLandingTests {
    [Fact]
    public void A_copy_standing_on_the_platform_is_on_top() {
        Assert.True(TouchLanding.OnTop(10.02f, 4f, 5f, 10f, 3f, 8f));
        // its smoothed place a little into the platform
        Assert.True(TouchLanding.OnTop(9.9f, 4f, 5f, 10f, 3f, 8f));
    }

    [Fact]
    public void A_copy_in_the_air_above_or_under_it_is_not() {
        Assert.False(TouchLanding.OnTop(11f, 4f, 5f, 10f, 3f, 8f));
        Assert.False(TouchLanding.OnTop(8f, 4f, 5f, 10f, 3f, 8f));
    }

    [Fact]
    public void A_copy_beside_it_is_not() {
        Assert.False(TouchLanding.OnTop(10f, 8.5f, 9.5f, 10f, 3f, 8f));
        // only touching its edge
        Assert.False(TouchLanding.OnTop(10f, 8f, 9f, 10f, 3f, 8f));
    }
}
