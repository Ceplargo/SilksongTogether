using Xunit;

namespace BossRule.Tests;

public class CutsceneRunTests {
    // An ability shrine's Inspection FSM: Idle until the interaction, then Look Back onward (Mosstown_02)
    private static CutsceneRun Shrine() => new(new[] { "Look Back" });

    [Fact]
    public void A_waiting_cutscene_follows_another_games_start() => Assert.True(Shrine().Follows(1, "Look Back", "Idle"));

    [Fact]
    public void A_waiting_cutscene_does_not_jump_into_the_middle() => Assert.False(Shrine().Follows(5, "Bind Ready", "Idle"));

    [Fact]
    public void Once_started_it_follows_each_later_state_it_has_not_entered() {
        var run = Shrine();
        run.Entered("Look Back", followed: true);
        Assert.True(run.Follows(3, "Weaver Dialogue", "Weaver BG Up"));
    }

    // both players set it off in the same moment: each game's start reaches the other, and neither starts again
    [Fact]
    public void A_second_start_does_nothing() {
        var run = Shrine();
        run.Entered("Look Back", followed: false);
        run.Entered("Weaver BG Up", followed: false);
        Assert.False(run.Follows(1, "Look Back", "Weaver BG Up"));
    }

    // its own FSM ran ahead of the other game's: a state it passed already never moves it back
    [Fact]
    public void A_state_it_entered_already_never_moves_it_back() {
        var run = Shrine();
        run.Entered("Look Back", followed: true);
        run.Entered("Weaver BG Up", followed: false);
        run.Entered("Weaver Dialogue", followed: false);
        Assert.False(run.Follows(2, "Weaver BG Up", "Weaver Dialogue"));
    }

    [Fact]
    public void Only_its_own_states_after_the_start_are_told() {
        var run = Shrine();
        Assert.False(run.Entered("Idle", followed: false));
        Assert.True(run.Entered("Look Back", followed: false));
        Assert.True(run.Entered("Weaver Dialogue", followed: false));
        Assert.False(run.Entered("Weaver Dialogue", followed: false));
        Assert.False(run.Follows(9, "Bind Ready", "Weaver Dialogue"));  // the leader follows nobody
    }

    // A game that started by a follow tells nothing: its own states (a dialogue it ran on while the leader's had ended)
    // would have sent the leader back to them
    [Fact]
    public void A_run_started_by_a_follow_tells_nothing_and_follows_on() {
        var run = Shrine();
        Assert.False(run.Entered("Look Back", followed: true));
        Assert.False(run.Entered("Weaver BG Up", followed: false));
        Assert.True(run.Follows(4, "Dialogue End", "Weaver BG Up"));
    }

    // the leader numbers each state it tells, so a follower knows which came first
    [Fact]
    public void The_leader_numbers_each_state_it_tells() {
        var run = Shrine();
        Assert.True(run.Entered("Look Back", followed: false));
        Assert.True(run.Entered("Weaver BG Up", followed: false));
        Assert.False(run.Entered("Weaver BG Up", followed: false));
        Assert.Equal(2, run.Sent);
    }

    // a crest's To Memory branches away (Load Memory Scene false cancels it) only in its FSM's own update: set by a
    // follow, it ran on into its room change to a memory this game lacks (Memory_Beast) and the hero hung between rooms
    [Fact]
    public void A_state_its_row_skips_is_never_followed() {
        var run = new CutsceneRun(new[] { "Jump Up" }, new[] { "To Memory" });
        run.Entered("Jump Up", followed: true);
        Assert.False(run.Follows(9, "To Memory", "Cancel Rumble"));
        Assert.True(run.Follows(10, "Take Control", "Cancel Rumble"));
    }

    // a lost state's resend can come in after a newer one: it never moves the follower back (the shrine's End after its
    // Set Finished would have run the end twice)
    [Fact]
    public void A_state_told_before_one_already_seen_moves_nothing() {
        var run = Shrine();
        Assert.True(run.Follows(1, "Look Back", "Idle"));
        run.Entered("Look Back", followed: true);
        Assert.True(run.Follows(12, "Set Finished", "Get Up"));
        run.Entered("Set Finished", followed: true);
        Assert.False(run.Follows(11, "End", "Set Finished"));
    }
}
