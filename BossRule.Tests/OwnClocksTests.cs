using BossRule;
using Xunit;

namespace BossRule.Tests;

// 0.4 world Task 16: Greymoor's windmill cogs and Dock_16's lava waterfall sets run on each game's own clock from room
// load (probe_own_clock: the cogs 120 to 144 degrees apart at -23 deg/s, the lava loops up to 1.2 s apart, a 1 s freeze
// of either game moving them again). The game that comes in later takes the scene host's phase; a freeze resends it.
public class OwnClocksTests {
    [Fact]
    public void The_cogs_and_the_lava_sets_are_followed_and_nothing_else() {
        Assert.Equal(OwnClocks.Kind.Animator, OwnClocks.KindOf("Greymoor_windmill_cog", ""));
        Assert.Equal(OwnClocks.Kind.Animator, OwnClocks.KindOf("Greymoor_windmill_cog (1)", ""));
        Assert.Equal(OwnClocks.Kind.Loop, OwnClocks.KindOf("Lava_Waterfall Set (2)", "Control"));
        Assert.Equal(OwnClocks.Kind.None, OwnClocks.KindOf("Lava_Waterfall Set", "FSM"));
        Assert.Equal(OwnClocks.Kind.None, OwnClocks.KindOf("Lava_Waterfall L", "Control"));
        Assert.Equal(OwnClocks.Kind.None, OwnClocks.KindOf("Drop Bell (2)", "Control"));
    }

    [Fact]
    public void Organ_01s_steam_timer_and_columns_are_followed_as_loops() {
        // probe_organ_steam (b-organ-p1.log): the timer's Control loop of Waits sends the STEAM events; each column's
        // Control only answers them
        Assert.Equal(OwnClocks.Kind.Loop, OwnClocks.KindOf("Exhaust Column Timer (1)", "Control"));
        Assert.Equal(OwnClocks.Kind.Loop, OwnClocks.KindOf("Exhaust Column New (2)", "Control"));
        Assert.Equal(OwnClocks.Kind.Loop, OwnClocks.KindOf("Exhaust Column New", "Control"));
        Assert.Equal(OwnClocks.Kind.None, OwnClocks.KindOf("Exhaust Column New", "steam_slash_effect"));
        Assert.Equal(OwnClocks.Kind.None, OwnClocks.KindOf("Shake Region Down", "Shaker"));
    }

    [Fact]
    public void A_loop_with_no_wait_is_apart_by_its_state_alone() {
        Assert.False(OwnClocks.LoopApart("Up On", null, "Up On", 0.4f));  // a lead added to nothing
        Assert.True(OwnClocks.LoopApart("Up On", null, "Up Off", 0f));
        Assert.True(OwnClocks.LoopApart("Down", 0.1f, "Down", 0.6f));     // a timed loop as before
        Assert.False(OwnClocks.LoopApart("Down", 0.50f, "Down", 0.55f));
    }

    [Fact]
    public void A_phase_word_reads_back_whatever_the_culture() {
        var word = OwnClocks.Word("Greymoor_windmill_cog (1)", "-1520474871", 3.25f);
        Assert.True(OwnClocks.TryParse(word, out var path, out var state, out var value));
        Assert.Equal("Greymoor_windmill_cog (1)", path);
        Assert.Equal("-1520474871", state);
        Assert.Equal(3.25f, value);
        Assert.DoesNotContain(",", word);
        Assert.False(OwnClocks.TryParse("no bars here", out _, out _, out _));
        Assert.False(OwnClocks.TryParse(OwnClocks.Ask, out _, out _, out _));
    }

    [Fact]
    public void A_turning_cog_is_apart_only_by_its_place_in_the_cycle() {
        Assert.False(OwnClocks.Apart(OwnClocks.Kind.Animator, "1", 0.995f, "1", 1.005f)); // the cycle's wrap
        Assert.False(OwnClocks.Apart(OwnClocks.Kind.Animator, "1", 7.40f, "1", 2.41f));  // whole cycles apart
        Assert.True(OwnClocks.Apart(OwnClocks.Kind.Animator, "1", 0.30f, "1", 0.70f));
        Assert.True(OwnClocks.Apart(OwnClocks.Kind.Animator, "1", 0.30f, "2", 0.30f));
    }

    [Fact]
    public void A_lava_loop_is_apart_by_its_state_or_its_time_in_it() {
        Assert.False(OwnClocks.Apart(OwnClocks.Kind.Loop, "Lava 2", 1.00f, "Lava 2", 1.05f));
        Assert.True(OwnClocks.Apart(OwnClocks.Kind.Loop, "Lava 2", 1.00f, "Lava 2", 1.30f));
        Assert.True(OwnClocks.Apart(OwnClocks.Kind.Loop, "Lava 2", 1.00f, "Lava 3", 1.00f));
    }

    [Fact]
    public void A_lava_hazard_is_a_burst_and_its_state_reads_back() {
        Assert.Equal(OwnClocks.Kind.Burst, OwnClocks.KindOf("Lava_waterfall_hazard 3", ""));
        var on = OwnClocks.BurstState(true, -1520474871, 1.25f, 3, 2.5f);
        Assert.True(OwnClocks.TryBurst(on, out var hash, out var timer, out var flip, out var length));
        Assert.Equal(-1520474871, hash);
        Assert.Equal(1.25f, timer);
        Assert.Equal(3, flip);
        Assert.Equal(2.5f, length);
        Assert.DoesNotContain("|", on);
        Assert.Equal("off", OwnClocks.BurstState(false, 5, 1f, 0, 2.5f));
        Assert.False(OwnClocks.TryBurst("off", out _, out _, out _, out _));
    }

    [Fact]
    public void A_burst_is_apart_by_being_on_in_one_game_only_or_by_its_time_left() {
        var at = (float t) => OwnClocks.BurstState(true, 7, t, 0, 2.5f);
        Assert.False(OwnClocks.Apart(OwnClocks.Kind.Burst, "off", 0f, "off", 0f));
        Assert.True(OwnClocks.Apart(OwnClocks.Kind.Burst, "off", 0f, at(1.0f), 0.5f));
        Assert.True(OwnClocks.Apart(OwnClocks.Kind.Burst, at(1.0f), 0.5f, "off", 0f));
        Assert.False(OwnClocks.Apart(OwnClocks.Kind.Burst, at(1.00f), 0.500f, at(1.01f), 0.495f));
        Assert.True(OwnClocks.Apart(OwnClocks.Kind.Burst, at(1.00f), 0.50f, at(1.05f), 0.48f));  // 2 units of a lava fall
        // its off-timer in step, its Animator a freeze frame's 0.33 s ahead (run 6): apart by its place in its fall
        Assert.True(OwnClocks.Apart(OwnClocks.Kind.Burst, at(2.00f), 0.20f, at(2.00f), 0.336f));
        Assert.False(OwnClocks.Apart(OwnClocks.Kind.Burst, at(2.00f), 0.330f, at(2.00f), 0.336f));
        Assert.True(OwnClocks.Apart(OwnClocks.Kind.Burst, at(1.0f), 0.5f, at(1.6f), 0.3f));
        Assert.True(OwnClocks.Apart(OwnClocks.Kind.Burst, at(1.0f), 0.5f, OwnClocks.BurstState(true, 8, 1.0f, 0, 2.5f), 0.5f));
        Assert.True(OwnClocks.Apart(OwnClocks.Kind.Burst, at(1.0f), 0.5f, OwnClocks.BurstState(true, 7, 1.0f, 1, 2.5f), 0.5f));  // mirrored
    }

    [Fact]
    public void A_bursts_flip_is_its_scales_signs() {
        Assert.Equal(0, OwnClocks.FlipOf(1f, 1f));
        Assert.Equal(1, OwnClocks.FlipOf(-1f, 1f));
        Assert.Equal(2, OwnClocks.FlipOf(1f, -0.5f));
        Assert.Equal(3, OwnClocks.FlipOf(-2f, -1f));
    }

    [Fact]
    public void A_phase_is_set_ahead_by_half_the_round_trip_up_to_a_limit() {
        Assert.Equal(0.03f, OwnClocks.Lead(0.06f), 3);
        Assert.Equal(0f, OwnClocks.Lead(-1f));
        Assert.Equal(0.5f, OwnClocks.Lead(5f), 3);
    }

    [Fact]
    public void A_game_takes_a_phase_unless_it_is_the_scene_host_and_asked_nothing() {
        Assert.True(OwnClocks.Takes(false, 100f));
        Assert.True(OwnClocks.Takes(true, 0.3f));
        Assert.False(OwnClocks.Takes(true, 100f));
    }

    [Fact]
    public void A_phase_word_is_moved_ahead_by_the_lead_before_it_is_compared() {
        Assert.Equal(("Lava 2", 0.56f), Round(OwnClocks.Ahead(OwnClocks.Kind.Loop, "Lava 2", 0.5f, 0.06f, 0f)));
        Assert.Equal(("123", 0.55f), Round(OwnClocks.Ahead(OwnClocks.Kind.Animator, "123", 0.5f, 0.1f, 2f)));
        Assert.Equal((OwnClocks.Off, 0f), OwnClocks.Ahead(OwnClocks.Kind.Burst, OwnClocks.Off, 0f, 0.05f, 0f));
        var (state, value) = OwnClocks.Ahead(OwnClocks.Kind.Burst, OwnClocks.BurstState(true, 7, 2.0f, 1, 2.5f), 0.2f, 0.05f, 0f);
        Assert.True(OwnClocks.TryBurst(state, out var hash, out var left, out var flip, out var length));
        Assert.Equal((7, 1.95f, 1, 2.5f, 0.22f), (hash, (float)System.Math.Round(left, 3), flip, length, (float)System.Math.Round(value, 3)));
        // the lag run's case: this game's lava fall one lead (0.056 s) behind the scene host's word, as it lands
        var theirs = OwnClocks.BurstState(true, 7, 2.40f, 0, 2.4666667f);
        Assert.False(OwnClocks.Apart(OwnClocks.Kind.Burst, theirs, 0.04f, theirs, 0.04f));
        var now = OwnClocks.Ahead(OwnClocks.Kind.Burst, theirs, 0.04f, 0.056f, 0f);
        Assert.True(OwnClocks.Apart(OwnClocks.Kind.Burst, theirs, 0.04f, now.State, now.Value));
    }

    private static (string, float) Round((string State, float Value) p) => (p.State, (float)System.Math.Round(p.Value, 3));

    [Fact]
    public void A_word_carries_its_send_time_and_lands_at_its_real_age() {
        var text = OwnClocks.Stamp(978.138f, "Lava_Waterfall Set (1)/Lava_waterfall_hazard 5|off|0");
        Assert.True(OwnClocks.TryStamp(text, out var at, out var word));
        Assert.Equal((978.138f, "Lava_Waterfall Set (1)/Lava_waterfall_hazard 5|off|0"), (at, word));
        Assert.True(OwnClocks.TryStamp(OwnClocks.Ask, out var none, out var ask));
        Assert.True(float.IsNaN(none));
        Assert.Equal(OwnClocks.Ask, ask);
        Assert.False(OwnClocks.TryStamp("x@?", out _, out _));
        // the other game's clock 10 s ahead: an ask at 100.0, its pong stamped 110.05, landing at 100.1
        var offset = OwnClocks.Offset(100.0f, 100.1f, 110.05f);
        Assert.Equal(10f, offset, 3);
        // the lag run's late word: sent at its 978.138, landing here at 968.394, is 0.256 s old (not the 0.052 lead)
        Assert.Equal(0.256f, OwnClocks.Age(968.394f, 978.138f, offset), 2);
        Assert.Equal(0f, OwnClocks.Age(968.0f, 978.138f, offset));      // never younger than now
        Assert.Equal(0.5f, OwnClocks.Age(975.0f, 978.138f, offset), 3);  // at most MaxLeadS
    }

    [Fact]
    public void A_game_that_came_in_asks_again_until_it_is_answered_for_a_while() {
        Assert.True(OwnClocks.AsksAgain(false, 1.0f, 0.6f));
        Assert.False(OwnClocks.AsksAgain(true, 1.0f, 0.6f));   // answered
        Assert.False(OwnClocks.AsksAgain(false, 1.0f, 0.2f));  // asked a moment ago
        Assert.False(OwnClocks.AsksAgain(false, 9.0f, 0.6f));  // long in the room: alone there
    }

    [Fact]
    public void Only_a_frame_long_enough_to_move_a_clock_is_a_freeze() {
        Assert.True(OwnClocks.Froze(1.0f));
        Assert.True(OwnClocks.Froze(0.3f));
        Assert.False(OwnClocks.Froze(0.05f));
    }
}
