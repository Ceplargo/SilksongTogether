using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class HideWaitsTests {
    // the hive's Wasps, put away alive and unhurt when the hive dies: a round trip, as before
    [Fact]
    public void An_unhurt_live_copys_hide_waits_a_round_trip() =>
        Assert.Equal(10f + 0.1f + HideWaits.MarginS, HideWaits.Due(true, false, null, 10f, 0.1f), 3);

    // NOTES 478, wasp_drop --lag 50 (2026-11-17_230948-pair2): the hide came while the copy was at 5 health and its
    // death came 0.24 s later, lost once on each hop (the room's game to its server, the server to the friend)
    [Fact]
    public void A_hurt_live_copys_hide_waits_two_lost_sends_and_a_round_trip() =>
        Assert.Equal(10f + 2 * 0.2f + 0.05f + HideWaits.MarginS, HideWaits.Due(true, true, null, 10f, 0.05f), 3);

    // a slow link: a lost send shows only after twice the round trip
    [Fact]
    public void A_lost_send_shows_after_twice_the_round_trip() =>
        Assert.Equal(10f + 2 * 0.6f + 0.3f + HideWaits.MarginS, HideWaits.Due(true, true, null, 10f, 0.3f), 3);

    // wasp_drop plain (2026-11-18): the resent death and the hide came in one frame, the death shown for one frame
    [Fact]
    public void A_hide_with_its_death_waits_for_the_death_to_show() =>
        Assert.Equal(10f + HideWaits.DeathShowS, HideWaits.Due(false, true, 10f, 10f, 0.1f), 3);

    // in step: the death showed its time before the hide came, so nothing waits
    [Fact]
    public void A_hide_after_its_death_has_shown_is_applied_at_once() =>
        Assert.Equal(10f, HideWaits.Due(false, true, 10f - 0.25f, 10f, 0.1f), 3);

    // a copy put away with no death to come (dead some other way, health at 0 with no death) goes at once
    [Fact]
    public void A_copy_neither_alive_nor_seen_dead_goes_at_once() =>
        Assert.Equal(10f, HideWaits.Due(false, true, null, 10f, 0.1f), 3);
}
