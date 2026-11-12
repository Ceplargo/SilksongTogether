using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class FireballFollowTests {
    [Fact]
    public void A_copys_fuse_is_held_until_its_hold_runs_out() {
        var until = FireballFollow.Until(10f);
        Assert.True(FireballFollow.Holds(10.2f, until));
        Assert.True(FireballFollow.Holds(10.49f, until));
        Assert.False(FireballFollow.Holds(10.51f, until)); // no placement for HoldS: the copy goes on by itself
    }

    [Fact]
    public void The_copy_warns_once_the_hosts_bomb_has() {
        Assert.True(FireballFollow.StartsWarning(hostStarted: true, copyActive: true, copyWarning: false));
    }

    [Fact]
    public void A_copy_blows_up_at_once_when_the_hosts_bomb_blew_up_unwarned() {
        // the host's bomb touched a hero and blew up with no warning: a warning here would blow the copy up a second
        // time, 0.33 s after the host's blast it showed (i-rrfb-p1-2, at (181.6, 14.0))
        Assert.True(FireballFollow.BlowsNow(hostStarted: true, hostBlastOn: true, copyActive: true));
        Assert.False(FireballFollow.StartsWarning(hostStarted: true, copyActive: true, copyWarning: false, hostBlastOn: true));
    }

    [Fact]
    public void A_warning_or_blown_copy_does_not_blow_up_again() {
        Assert.False(FireballFollow.BlowsNow(hostStarted: true, hostBlastOn: false, copyActive: true));
        Assert.False(FireballFollow.BlowsNow(hostStarted: true, hostBlastOn: true, copyActive: false));
        Assert.False(FireballFollow.BlowsNow(hostStarted: false, hostBlastOn: true, copyActive: true));
    }

    [Fact]
    public void The_copy_does_not_warn_before_the_host_or_twice_or_once_blown() {
        Assert.False(FireballFollow.StartsWarning(hostStarted: false, copyActive: true, copyWarning: false));
        Assert.False(FireballFollow.StartsWarning(hostStarted: true, copyActive: true, copyWarning: true));
        Assert.False(FireballFollow.StartsWarning(hostStarted: true, copyActive: false, copyWarning: false));
    }

    // the judges' Bone_07 trace (2026-11-11): updates came late, the hold ran out, and the friend's copy warned 0.19 s
    // early and blew up for one frame. A followed copy stays held however late its updates come
    [Fact]
    public void A_followed_copy_stays_held_past_the_time_out() => Assert.True(FireballFollow.Holds(10.9f, FireballFollow.Until(10f), true));

    // one no longer followed (the host's update dropped it, or this game runs the room) goes on by itself as before
    [Fact]
    public void A_copy_no_longer_followed_runs_out_as_before() {
        var until = FireballFollow.Until(10f);
        Assert.False(FireballFollow.Holds(10.9f, until, false));
        Assert.True(FireballFollow.Holds(10.2f, until, false));
    }
}
