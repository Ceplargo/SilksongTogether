using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class MissedHitsTests {
    [Fact]
    public void A_landed_hit_its_copied_swing_showed_is_not_due() {
        var hits = new MissedHits();
        hits.Landed(10.0);
        hits.Shown(10.2);
        Assert.Empty(hits.Due(11.0));
    }

    [Fact]
    public void A_copied_swing_shown_before_the_health_came_answers_it() {
        var hits = new MissedHits();
        hits.Shown(10.0);
        hits.Landed(10.3);
        Assert.Empty(hits.Due(11.0));
    }

    [Fact]
    public void A_landed_hit_no_copied_swing_showed_is_due_once_its_window_passed() {
        // Mossbone Mother's Swoop: her copy trailed her dive, the friend's swing landed on it and its copied swing
        // passed over her in the scene host's game
        var hits = new MissedHits();
        hits.Landed(10.0);
        Assert.Empty(hits.Due(10.4));
        Assert.Single(hits.Due(10.6));
        Assert.Empty(hits.Due(11.0));
    }

    [Fact]
    public void Each_copied_swing_answers_one_landed_hit() {
        var hits = new MissedHits();
        hits.Landed(10.0);
        hits.Landed(10.1);
        hits.Shown(10.2);
        Assert.Single(hits.Due(11.0));
    }

    [Fact]
    public void A_copied_swing_outside_the_window_answers_nothing() {
        var hits = new MissedHits();
        hits.Landed(10.0);
        hits.Shown(10.8);
        Assert.Single(hits.Due(11.0));
        hits.Landed(11.5);
        Assert.Single(hits.Due(12.1));
    }

    [Fact]
    public void A_landed_hit_is_due_with_the_stun_it_had_in_its_game() {
        // a tool's or skill's hit stuns as in the player's own game, not 1
        var hits = new MissedHits();
        hits.Landed(10.0, 0.5f);
        hits.Landed(10.1, 2f);
        Assert.Equal([0.5f, 2f], hits.Due(11.0));
    }

    [Fact]
    public void With_only_landed_hits_each_is_due_at_once_whatever_a_copied_swing_showed() {
        // the ruling after the combined 0.3.15 test runs: copied swings stunned where the friend's swing
        // missed and missed where it landed, so in the scene host's game the friend's landed hits are the only source
        var hits = new MissedHits();
        hits.Landed(10.0, 1f);
        Assert.Equal([1f], hits.Due(10.01, atOnce: true));
        hits.Landed(10.5, 1f);
        hits.Landed(10.52, 1f);
        Assert.Equal([1f, 1f], hits.Due(10.53, atOnce: true));
        Assert.Empty(hits.Due(10.54, atOnce: true));
    }

    [Fact]
    public void Each_enemy_has_its_own_record_until_the_room_changes() {
        MissedHits.Clear();
        MissedHits.For(7).Landed(10.0);
        Assert.Same(MissedHits.For(7), MissedHits.For(7));
        Assert.Empty(MissedHits.For(8).Due(11.0));
        Assert.Single(MissedHits.For(7).Due(11.0));
        MissedHits.For(7).Landed(12.0);
        MissedHits.Clear();
        Assert.Empty(MissedHits.For(7).Due(13.0));
    }
}
