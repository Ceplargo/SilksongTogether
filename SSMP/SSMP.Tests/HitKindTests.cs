using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class HitKindTests {
    [Fact]
    public void A_hits_kind_comes_back_as_sent() {
        var kind = HitKind.Pack(5, 2, 3);
        Assert.True(HitKind.IsHit(kind));
        Assert.Equal((5, 2, 3), HitKind.Unpack(kind));
    }

    [Fact]
    public void A_nail_hit_with_no_element_is_still_a_hit() => Assert.True(HitKind.IsHit(HitKind.Pack(0, 0, 0)));

    [Fact]
    public void A_drop_with_no_hit_is_none() => Assert.False(HitKind.IsHit(HitKind.None));

    [Fact]
    public void A_kind_noted_in_its_drops_frame_or_the_next_goes_with_it() {
        Assert.Equal(7, HitKind.ForDrop(7, notedFrame: 100, frame: 101));
        Assert.Equal(HitKind.None, HitKind.ForDrop(7, notedFrame: 100, frame: 103));
    }

    [Fact]
    public void A_missed_hit_keeps_its_kind() {
        var hits = new MissedHits();
        hits.Landed(10.0, 0.5f, HitKind.Pack(4, 0, 1));
        var due = hits.DueHits(10.1, atOnce: true);
        Assert.Single(due);
        Assert.Equal((0.5f, HitKind.Pack(4, 0, 1)), due[0]);
    }
}
