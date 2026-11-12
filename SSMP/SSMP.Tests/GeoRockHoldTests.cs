using SSMP.Game.Client.Save;
using Xunit;

// probe_geo_rock (0.3.21): the host broke Bone_04's Geo Rock 3 on its third hit, the friend's game on the second
public class GeoRockHoldTests {
    [Fact]
    public void A_count_the_copys_own_hit_reached_is_not_set_again() {
        var hold = new GeoRockHold();
        hold.Received(2, 0f);
        Assert.Null(hold.Due(0.6f, 2));
    }

    [Fact]
    public void A_hit_the_copy_missed_is_set_after_the_grace() {
        var hold = new GeoRockHold();
        hold.Received(2, 0f);
        Assert.Null(hold.Due(0.3f, 3));
        Assert.Equal(2, hold.Due(0.6f, 3));
    }

    [Fact]
    public void A_newer_count_replaces_the_waiting_one() {
        var hold = new GeoRockHold();
        hold.Received(2, 0f);
        hold.Received(1, 0.44f);
        Assert.Null(hold.Due(0.6f, 2));
        Assert.Equal(1, hold.Due(0.95f, 2));
    }

    // Bone_01c's Geo Rock 1 (1), the friend the doer (2026-11-10_180008-world-pair3): the host's 2 arrived, the friend's
    // own next hit made 1, and the held 2 undid it
    [Fact]
    public void A_held_count_never_takes_back_this_games_own_newer_hit() {
        var hold = new GeoRockHold();
        hold.Received(2, 0f);
        Assert.Null(hold.Due(0.6f, 1));
        hold.Received(1, 0.7f);
        Assert.Null(hold.Due(1.3f, 0));
    }

    // the same run: the host's 0 arrived on the friend's rock with 1 left, which stayed in Idle
    [Theory]
    [InlineData(1, 0, true)]  // the friend's rock still stands: broken as the game breaks it
    [InlineData(2, 0, true)]
    [InlineData(0, 0, false)] // already broken here
    [InlineData(2, 1, false)] // a hit the copy missed: the count only
    public void An_arrived_zero_breaks_a_standing_rock(int local, int arrived, bool expected) =>
        Assert.Equal(expected, GeoRockHold.Breaks(local, arrived));

    [Fact]
    public void A_count_is_set_once() {
        var hold = new GeoRockHold();
        hold.Received(2, 0f);
        Assert.Equal(2, hold.Due(0.6f, 3));
        Assert.Null(hold.Due(1.0f, 3));
    }
}
