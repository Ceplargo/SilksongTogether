using Xunit;

namespace BossRule.Tests;

public class FrameGapsTests {
    private const string Box = "Black Thread States Thread Only Variant/Normal World/Boss Scene/Spinner Boss/NewCharge Hit";

    // the friend's game held 500 ms (freeze_catch's stall), then its catch goes a frame later
    [Fact]
    public void A_freeze_just_before_is_the_longest_frame() {
        var frames = new FrameGaps();
        frames.Add(10.000f, 0.008f);
        frames.Add(10.508f, 0.508f);
        frames.Add(10.516f, 0.008f);
        Assert.Equal(0.508f, frames.Longest(10.52f), 3);
    }

    // a freeze longer ago than the window says nothing of this catch
    [Fact]
    public void An_old_freeze_is_forgotten() {
        var frames = new FrameGaps();
        frames.Add(10.0f, 0.5f);
        for (var t = 10.008f; t < 11f; t += 0.008f) frames.Add(t, 0.008f);
        Assert.Equal(0.008f, frames.Longest(11f), 3);
    }

    // the stamp goes through the message and back; a message from before 0.3.23 carries none
    [Fact]
    public void A_catch_carries_its_games_freeze() {
        var (message, frozen) = MultiHitPath.Unstamp(MultiHitPath.Stamp(MultiHitPath.Message("Friend|1", "Spinner Boss/NewCharge Hit"), 0.508f));
        Assert.Equal(("Friend|1", "Spinner Boss/NewCharge Hit"), MultiHitPath.Read(message));
        Assert.Equal(0.508f, frozen, 3);
        Assert.Equal(0f, MultiHitPath.Unstamp("Friend|Spinner Boss/NewCharge Hit").FrozenS);
    }

    // freeze_catch's case: the catch came 0.6 s after her box went, sent after a 0.5 s freeze of its game
    [Fact]
    public void A_catch_late_by_its_games_freeze_still_takes_her() {
        var late = new LateCatches<string>();
        late.Saw(Box, 10f, ["Control to Multihit Slash"]);
        Assert.Null(late.For("Spinner Boss/NewCharge Hit", 10.6f));
        Assert.Equal(["Control to Multihit Slash"], late.For("Spinner Boss/NewCharge Hit", 10.6f, true, FrameGaps.Grace(0.5f)));
    }

    // a long freeze adds no more than MaxGraceS: by then she is in another move
    [Fact]
    public void A_freeze_adds_at_most_its_cap() {
        Assert.Equal(FrameGaps.MaxGraceS, FrameGaps.Grace(3f));
        Assert.Equal(0f, FrameGaps.Grace(0f));
        var late = new LateCatches<string>();
        late.Saw(Box, 10f, ["Control to Multihit Slash"]);
        Assert.Null(late.For("Spinner Boss/NewCharge Hit", 10f + LateCatches<string>.KeepS + FrameGaps.MaxGraceS + 0.01f,
            true, FrameGaps.Grace(3f)));
    }
}
