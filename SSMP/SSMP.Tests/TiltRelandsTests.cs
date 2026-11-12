using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class TiltRelandsTests {
    // Bone_03's trap door plate: the host's hero landed at 0, 0.13 and 0.6 s, the copy once (its place shows no lift)
    [Theory]
    [InlineData(false, double.MaxValue, "Wait")]
    [InlineData(false, 0.1, "Wait")]
    [InlineData(true, 0.05, "Same")]
    [InlineData(true, 0.3, "Same")]
    [InlineData(true, 0.31, "Again")]
    [InlineData(true, double.MaxValue, "Again")]
    public void A_sent_landing_is_the_copys_own_or_another(bool onPlat, double since, string landing) {
        Assert.Equal(landing, TiltRelands.Decide(onPlat, since).ToString());
    }
}
