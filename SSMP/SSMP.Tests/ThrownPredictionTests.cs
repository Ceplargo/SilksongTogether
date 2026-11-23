using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class ThrownPredictionTests {
    private const float Gravity = -9.81f;

    [Fact]
    public void A_dynamic_body_falls_as_its_gravity_pulls_it() {
        // the Widow's bell at its apex while the scene host's game froze: the copy flew on level (t3-show-p1-a)
        var (x, y) = ThrownPrediction.Predict(55.7f, 17.6f, 9f, 4f, 6f, Gravity, 0.25f);
        Assert.Equal(55.7f + 9f * 0.25f, x, 3);
        Assert.Equal(17.6f + 4f * 0.25f + 0.5f * Gravity * 6f * 0.25f * 0.25f, y, 3);
    }

    [Fact]
    public void A_body_with_no_gravity_goes_straight() {
        var (x, y) = ThrownPrediction.Predict(10f, 5f, 20f, -4f, 0f, Gravity, 0.1f);
        Assert.Equal(12f, x, 3);
        Assert.Equal(4.6f, y, 3);
    }

    [Fact]
    public void A_falling_body_leaves_the_prediction_at_the_speed_it_has_there() {
        var (vx, vy) = ThrownPrediction.Speed(9f, 4f, 6f, Gravity, 0.25f);
        Assert.Equal(9f, vx, 3);
        Assert.Equal(4f + Gravity * 6f * 0.25f, vy, 3);
    }

    [Fact]
    public void A_copy_moved_by_its_fsm_holds_once_it_is_max_lead_on() {
        // the Last Judge's censer: her tween stopped at its top, the copy went on 14 units at its last speed
        var (x, y) = ThrownPrediction.Held(51.9f, 34.4f, 55f, 0f, 0.25f, 0.5f);
        Assert.Equal(52.4f, x, 3);
        Assert.Equal(34.4f, y, 3);
        var (dx, dy) = ThrownPrediction.Held(0f, 0f, 30f, 40f, 1f, 0.5f);
        Assert.Equal(0.3f, dx, 3);
        Assert.Equal(0.4f, dy, 3);
    }

    [Fact]
    public void A_copy_moved_by_its_fsm_short_of_max_lead_goes_on_at_its_speed() {
        var (x, y) = ThrownPrediction.Held(10f, 5f, 2f, -1f, 0.1f, 0.5f);
        Assert.Equal(10.2f, x, 3);
        Assert.Equal(4.9f, y, 3);
    }

    // (0.4, the FollowsFlight side by side) a copy waits at its prediction's end through a freeze of the scene host's
    // game, and no longer: one whose updates had stopped waited there for good, its hurt box on
    // (a stale update, a huge dt: a copy whose updates had stopped long ago is released, not kept still)
    [Theory]
    [InlineData(0f, false)]
    [InlineData(0.8f, false)]
    [InlineData(1f, false)]
    [InlineData(1.01f, true)]
    [InlineData(30f, true)]
    [InlineData(1e30f, true)]
    [InlineData(float.PositiveInfinity, true)]
    public void A_copy_waits_at_its_predictions_end_through_a_freeze_and_no_longer(float past, bool released) {
        Assert.Equal(released, ThrownPrediction.Releases(past));
    }

    // a copy moved by its FSM, its last update far in the past (a long freeze, a stale update) or a huge dt, stays
    // within max lead of it
    [Theory]
    [InlineData(1000f)]
    [InlineData(1e30f)]
    public void A_copy_moved_by_its_fsm_stays_within_max_lead_after_any_time(float dt) {
        var (x, y) = ThrownPrediction.Held(10f, 5f, 30f, -40f, dt, 0.5f);
        Assert.True(float.IsFinite(x) && float.IsFinite(y));
        Assert.True(System.Math.Sqrt((x - 10f) * (x - 10f) + (y - 5f) * (y - 5f)) <= 0.5001);
    }

    // a fall at the 0.25 s cap with a large gravity scale (the Widow's bells: 6) stays finite and within a few units
    [Fact]
    public void A_fall_at_the_cap_stays_finite_and_near() {
        var (x, y) = ThrownPrediction.Predict(55.7f, 17.6f, 9f, 4f, 6f, Gravity, 0.25f);
        Assert.True(float.IsFinite(x) && float.IsFinite(y));
        Assert.True(System.Math.Abs(y - 17.6f) < 5f);
    }

    // a non-finite speed or time (none is sent today: SentSpeed returns 0 for no elapsed time) keeps the copy at its
    // last update
    [Theory]
    [InlineData(float.NaN, 0f, 0.1f)]
    [InlineData(float.PositiveInfinity, 0f, 0.1f)]
    [InlineData(0f, float.NegativeInfinity, 0.1f)]
    [InlineData(2f, -1f, float.PositiveInfinity)]
    public void A_copy_moved_by_its_fsm_with_a_non_finite_speed_stays_at_its_update(float vx, float vy, float dt) {
        var (x, y) = ThrownPrediction.Held(10f, 5f, vx, vy, dt, 0.5f);
        Assert.Equal((10f, 5f), (x, y));
    }

    [Fact]
    public void A_copy_let_go_out_of_its_room_ends_at_once() {
        Assert.True(ThrownPrediction.EndsLetGo(0f, inRoom: false));
    }

    [Fact]
    public void A_copy_let_go_in_its_room_ends_only_past_the_longest_real_life() {
        // the Widow's bell copy let go rose on in the room for good (plan2, 2026-11-17)
        Assert.False(ThrownPrediction.EndsLetGo(ThrownPrediction.LetGoLife - 0.1f, inRoom: true));
        Assert.True(ThrownPrediction.EndsLetGo(ThrownPrediction.LetGoLife + 0.1f, inRoom: true));
    }

    [Fact]
    public void A_landed_copy_rests_through_the_update_that_still_carries_the_fall() {
        // the Beastfly's tar shot: the update in hand at its landing still went down and right (probe_tarshot_air)
        Assert.False(ThrownPrediction.HoldEnds(false, 0.3f, 0.2f, 14f, -9f));
        Assert.False(ThrownPrediction.HoldEnds(false, 0f, 0f, 0f, 0f));
    }

    [Fact]
    public void A_landed_copy_follows_again_once_the_scene_host_has_stopped_too() {
        Assert.True(ThrownPrediction.HoldEnds(true, 0.05f, 0.01f, 0f, 0f));
    }

    [Fact]
    public void A_landed_copy_rests_while_the_scene_host_still_lands_near_it() {
        // its last send before landing, a few frames behind the copy's own landing
        Assert.False(ThrownPrediction.HoldEnds(true, 0.2f, 0.3f, 3f, -6f));
    }

    [Fact]
    public void A_landed_copy_never_rests_where_the_scene_host_is_not() {
        // the scene host's did not land there, or bounced on: more than HoldAway from the copy's rest
        Assert.True(ThrownPrediction.HoldEnds(true, 0.6f, 0f, 3f, 4f));
        Assert.True(ThrownPrediction.HoldEnds(true, 0f, -2f, 0f, -12f));
    }

    [Fact]
    public void A_landed_copy_rests_through_late_updates_still_coming_toward_it() {
        // under lag the scene host's sends before its own landing arrive after the copy's: a unit back, still falling
        Assert.False(ThrownPrediction.HoldEnds(true, -0.6f, 0.8f, 9f, -12f));
    }

    [Fact]
    public void A_landed_copy_leaves_its_surface_to_the_scene_host() {
        // the tar shot's Splat rays: the copy's own found no surface where the host's did, and it broke at once
        Assert.True(ThrownPrediction.RaysHeld(landed: true, flew: true));
    }

    [Fact]
    public void A_copy_not_landed_by_its_own_fsm_checks_its_own_surface() {
        Assert.False(ThrownPrediction.RaysHeld(landed: false, flew: true));  // still flying
        Assert.False(ThrownPrediction.RaysHeld(landed: true, flew: false));  // kinematic as made: never flew
    }
}
