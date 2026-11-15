using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class RandomChildPicksTests {
    // Crawl_03's GameObject (1): two worms, each with its copy made beside it (crypt_ff, 2026-11-14)
    [Fact]
    public void A_pick_leaves_out_the_copies() =>
        Assert.Equal([0, 1], RandomChildPicks.Pickable([false, false, true, true]));

    [Fact]
    public void A_copy_between_originals_is_left_out() =>
        Assert.Equal([0, 2], RandomChildPicks.Pickable([false, true, false]));

    // a room with no entity under the picker picks as the game does
    [Fact]
    public void With_no_copy_every_child_is_pickable() =>
        Assert.Equal([0, 1, 2], RandomChildPicks.Pickable([false, false, false]));
}
