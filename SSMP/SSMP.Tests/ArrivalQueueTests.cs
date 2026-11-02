using SSMP.Game.Client.Save;
using Xunit;

namespace SSMP.Tests;

public class ArrivalQueueTests {
    [Fact]
    public void Names_wait_until_hero_in_control_and_come_out_once() {
        var q = new ArrivalQueue();
        q.Add("maxHealthBase");
        q.Add("maxHealthBase");
        q.Add("silkMax");
        Assert.Empty(q.Drain(heroInControl: false));
        Assert.Equal(new[] { "maxHealthBase", "silkMax" }, q.Drain(heroInControl: true));
        Assert.Empty(q.Drain(heroInControl: true));
    }
}
