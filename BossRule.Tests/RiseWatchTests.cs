using Xunit;

namespace BossRule.Tests;

public class RiseWatchTests {
    // the Widow's ground bell holders wait in State 1 for anything in their trigger, then raise their spikes in State 2
    private static RiseWatch Holders() => new("State 1", "State 2");

    [Fact]
    public void A_holder_that_rises_is_told_once() {
        var watch = Holders();
        Assert.False(watch.Rose("Holder 1", "State 1"));
        Assert.True(watch.Rose("Holder 1", "State 2"));
        Assert.False(watch.Rose("Holder 1", "State 2"));
    }

    [Fact]
    public void A_holder_that_waits_again_is_told_at_its_next_rise() {
        // State 3 puts its spikes away and the holder; her next Charge brings it out again in State 1
        var watch = Holders();
        watch.Rose("Holder 2", "State 1");
        watch.Rose("Holder 2", "State 2");
        watch.Rose("Holder 2", "State 3");
        watch.Rose("Holder 2", "State 1");
        Assert.True(watch.Rose("Holder 2", "State 2"));
    }

    [Fact]
    public void A_holder_whose_wait_lasted_under_a_frame_is_told() {
        // the 0.3.18 gate: her Charge brought Holder 3 out under her and it rose in the same long frame, so the look saw
        // it put away (State 3), then up (State 2), never waiting
        var watch = Holders();
        watch.Rose("Holder 3", "State 1");
        watch.Rose("Holder 3", "State 2");
        watch.Rose("Holder 3", "State 3");
        Assert.True(watch.Rose("Holder 3", "State 2"));
    }

    [Fact]
    public void The_old_rule_misses_it() {
        var watch = Holders();
        watch.Rose("Holder 3", "State 3", quick: false);
        Assert.False(watch.Rose("Holder 3", "State 2", quick: false));
    }

    [Fact]
    public void A_holder_first_seen_risen_is_not_told() {
        // a game entering the room while a holder is up did not see it rise
        Assert.False(Holders().Rose("Holder 3", "State 2"));
    }
}
