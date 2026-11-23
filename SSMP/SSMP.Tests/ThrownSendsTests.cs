using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class ThrownSendsTests {
    [Fact]
    public void An_object_in_the_room_matters() {
        Assert.True(ThrownSends.Matters(60f, 8f, 120f));
        Assert.True(ThrownSends.Matters(60f, -5f, 120f));  // just under the floor, where a bell lands
        Assert.True(ThrownSends.Matters(60f, 400f, 120f));  // thrown high: it may fall back
    }

    [Fact]
    public void An_object_fallen_far_below_the_room_does_not() {
        // the Widow's bell that fell on, still active, in the final candidate's sweep (widow_bells' fallen-out case)
        Assert.False(ThrownSends.Matters(230f, -1188901.9f, 300f));
        Assert.False(ThrownSends.Matters(60f, -ThrownSends.Margin - 1f, 120f));
    }

    [Fact]
    public void An_object_flown_far_past_either_side_does_not() {
        Assert.False(ThrownSends.Matters(-ThrownSends.Margin - 1f, 8f, 120f));
        Assert.False(ThrownSends.Matters(120f + ThrownSends.Margin + 1f, 8f, 120f));
    }

    [Fact]
    public void An_update_counts_its_objects_in_a_byte() {
        Assert.Equal(255, ThrownSends.MaxSent);
    }

    [Fact]
    public void A_burst_of_prunes_is_named_a_bounded_number_of_times() {
        // 200 objects pruned at once: each update names at most MaxPruned, each number PrunedSends times, then none
        var pending = new System.Collections.Generic.Dictionary<byte, int>();
        for (var n = 1; n <= 200; n++) pending[(byte) n] = ThrownSends.PrunedSends;
        var named = new System.Collections.Generic.Dictionary<byte, int>();
        var said = new System.Collections.Generic.List<byte>();
        for (var update = 0; update < 1000 && pending.Count > 0; update++) {
            ThrownSends.Say(pending, said);
            Assert.InRange(said.Count, 1, ThrownSends.MaxPruned);
            foreach (var n in said) named[n] = (named.TryGetValue(n, out var c) ? c : 0) + 1;
        }

        Assert.Empty(pending);
        Assert.Equal(200, named.Count);
        Assert.All(named.Values, count => Assert.Equal(ThrownSends.PrunedSends, count));
        ThrownSends.Say(pending, said);
        Assert.Empty(said);
    }
}
