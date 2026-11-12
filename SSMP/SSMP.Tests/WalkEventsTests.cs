using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class WalkEventsTests {
    // Bone_09's Dock Worker (1): its walker's OnWalking switched on its Walk Voice Loop in the scene host's game only
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void A_walker_event_arrives_as_sent(bool running, bool on) {
        Assert.Equal((running, on), WalkEvents.Decode(WalkEvents.Encode(running, on)));
    }
}
