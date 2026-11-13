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

    [Fact]
    public void An_arrival_equal_to_the_own_value_is_not_refreshed() {
        // the join's snapshot brings silkMax unchanged; its refresh (SPOOL MAX UP) mended a broken spool
        var q = new ArrivalQueue();
        q.Add("silkMax", 9, 9);
        q.Add("maxHealthBase", 5, 5);
        Assert.Empty(q.Drain(heroInControl: true));
    }

    [Fact]
    public void An_arrival_that_changes_the_own_value_is_refreshed() {
        var q = new ArrivalQueue();
        q.Add("silkMax", 9, 10);
        Assert.Equal(new[] { "silkMax" }, q.Drain(heroInControl: true));
    }
}
