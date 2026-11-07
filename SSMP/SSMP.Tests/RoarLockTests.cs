using SSMP.Game.Client.Entity.Action;
using Xunit;

namespace SSMP.Tests;

// 0.3.20 Task 21: a boss's stunning roar locks the hero of the game that runs the boss (StartRoarEmitter: ROAR WOUND
// CANCEL, then ROAR ENTER to the local hero; ROAR EXIT at the roar state's exit or a StopRoarEmitter). SSMP replayed only
// the rings on the copy, so the other player's Hornet never locked (intro_pose, the Skull King's Wake Roar). The copy's
// roar now locks the local hero the same way and ends it with the roar's own end, or at a hard timeout, so a missed end
// never leaves a hero locked.
public class RoarLockTests {
    [Fact]
    public void The_roars_end_ends_the_lock_once() {
        var hold = new RoarLock(10f);
        Assert.True(hold.End());
        Assert.False(hold.End());
        Assert.True(hold.Ended);
    }

    [Fact]
    public void Without_an_end_the_timeout_ends_it_once() {
        var hold = new RoarLock(10f);
        Assert.False(hold.Tick(10f + RoarLock.TimeoutS - 0.1f));
        Assert.True(hold.Tick(10f + RoarLock.TimeoutS));
        Assert.False(hold.Tick(10f + RoarLock.TimeoutS + 1f));
        Assert.True(hold.Ended);
    }

    [Fact]
    public void An_ended_lock_does_not_time_out() {
        var hold = new RoarLock(10f);
        hold.End();
        Assert.False(hold.Tick(10f + RoarLock.TimeoutS + 1f));
    }

    [Fact]
    public void The_timeout_outlasts_the_longest_copied_roar_by_a_small_margin() =>
        // the longest stunning roar lock of roar_lock's 13 Act 1 rows: the Last Judge's Intro Roar, 3.2 s (its state's
        // own wait; 3.19 to 3.21 s on the other player's screen, 0.3.20's run 2026-11-07_103419-pair4)
        Assert.InRange(RoarLock.TimeoutS, 3.2f + 0.3f, 3.2f + 1.0f);

    [Fact]
    public void Only_the_latest_lock_frees_the_hero() {
        // a stun with no end of its own (the Vampire Gnat's Zoom Lock?) is followed by a roar whose exit ends both, as
        // the game's one ROAR EXIT ends whatever lock the hero is in: the older lock's timeout must not free the hero
        var older = new RoarLock(10f);
        var newer = new RoarLock(11f);
        Assert.False(older.Frees);
        Assert.True(newer.Frees);
    }

    [Fact]
    public void The_copy_locks_only_where_the_scene_hosts_own_hero_locked() {
        // the Crawfather's Roar forced from his background idle: the game left the scene host's hero free, and the copy's
        // roar locked the other's for 2.75 s (0.3.20's roar_lock, probe_crawfather_roar)
        Assert.False(RoarLock.HostLocked(stunHero: true, delayed: false, heroLocked: false));
        Assert.True(RoarLock.HostLocked(stunHero: true, delayed: false, heroLocked: true));
        Assert.True(RoarLock.HostLocked(stunHero: true, delayed: true, heroLocked: false));  // its lock comes later
        Assert.False(RoarLock.HostLocked(stunHero: false, delayed: false, heroLocked: true));
    }

    [Theory]
    [InlineData(false, false, "ROAR ENTER")]
    [InlineData(true, false, "ROAR BURST ENTER")]
    [InlineData(false, true, "ROAR ENTER FORCED")]
    [InlineData(true, true, "ROAR BURST ENTER FORCED")]
    public void The_hero_gets_the_games_own_event(bool burst, bool forced, string sent) =>
        Assert.Equal(sent, RoarLock.EnterEvent(burst, forced));

    [Fact]
    public void A_stunning_roar_without_rings_is_still_sent() {
        Assert.True(RoarLock.Sends(noVisualEffect: true, stunHero: true));
        Assert.True(RoarLock.Sends(noVisualEffect: false, stunHero: false));
        Assert.False(RoarLock.Sends(noVisualEffect: true, stunHero: false));
    }
}
