using Xunit;

namespace BossRule.Tests;

public class EarlyRisesTests {
    // GroundBells keeps a rise 0.5 s: the friend's holders came out at most 0.036 s after the host's rose
    private static EarlyRises Kept() => new(0.5f);

    [Fact]
    public void A_rise_that_came_before_its_holder_was_out_is_due_when_it_is() {
        // the gate's pair 2: the friend's holders came out 0.029 s after the host's Holder 1 rose
        var early = Kept();
        early.Came("Holder 1", 100f);
        Assert.True(early.Due("Holder 1", 100.029f));
    }

    [Fact]
    public void It_is_due_once() {
        var early = Kept();
        early.Came("Holder 3", 100f);
        Assert.True(early.Due("Holder 3", 100.02f));
        Assert.False(early.Due("Holder 3", 100.04f));
    }

    [Fact]
    public void Only_that_holders_rise_is_due() {
        var early = Kept();
        early.Came("Holder 1", 100f);
        Assert.False(early.Due("Holder 2", 100.02f));
        Assert.True(early.Due("Holder 1", 100.03f));
    }

    [Fact]
    public void A_rise_kept_past_its_time_is_not_due() {
        // its holder out only at a later Charge of hers: that one's rises come again
        var early = Kept();
        early.Came("Holder 4", 100f);
        Assert.False(early.Due("Holder 4", 100.6f));
    }

    [Fact]
    public void A_rise_kept_past_its_time_is_told_expired_once() {
        var early = Kept();
        early.Came("Holder 2", 100f);
        Assert.Empty(early.Expired(100.4f));
        Assert.Equal(new[] { "Holder 2" }, early.Expired(100.6f));
        Assert.Empty(early.Expired(100.7f));
        Assert.False(early.Due("Holder 2", 100.7f));
    }

    [Fact]
    public void A_rise_applied_in_time_never_expires() {
        var early = Kept();
        early.Came("Holder 1", 100f);
        Assert.True(early.Due("Holder 1", 100.03f));
        Assert.Empty(early.Expired(101f));
    }

    [Fact]
    public void A_new_room_drops_the_kept_rises() {
        var early = Kept();
        early.Came("Holder 2", 100f);
        early.Clear();
        Assert.False(early.Due("Holder 2", 100.02f));
        Assert.Empty(early.Expired(101f));
    }
}
