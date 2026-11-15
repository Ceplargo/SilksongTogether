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

    [Fact]
    public void A_join_arrival_that_changes_the_own_value_is_refreshed_as_a_load() {
        // (0.3.23 K8) the friend's own silkMax is its stale file's at a join (9, the server's 10): a load, not a pickup
        var q = new ArrivalQueue();
        q.Add("silkMax", 9, 10, joining: true);
        Assert.Equal(new[] { ("silkMax", true) }, q.DrainEach(heroInControl: true));
    }

    [Fact]
    public void A_live_arrival_that_changes_the_own_value_is_refreshed_as_a_pickup() {
        // Review Focus 2: the other player's piece raising silkMax in play still refreshes as the game's own pickup does
        var q = new ArrivalQueue();
        q.Add("silkMax", 9, 10);
        Assert.Equal(new[] { ("silkMax", false) }, q.DrainEach(heroInControl: true));
    }

    [Fact]
    public void A_live_arrival_after_a_join_arrival_makes_it_a_pickup() {
        var q = new ArrivalQueue();
        q.Add("silkMax", 9, 10, joining: true);
        q.Add("silkMax", 10, 11);
        Assert.Equal(new[] { ("silkMax", false) }, q.DrainEach(heroInControl: true));
    }

    [Fact]
    public void An_equal_join_arrival_is_not_refreshed() {
        var q = new ArrivalQueue();
        q.Add("silkMax", 10, 10, joining: true);
        Assert.Empty(q.DrainEach(heroInControl: true));
    }
}
