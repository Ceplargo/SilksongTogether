using System.Linq;
using Xunit;

namespace BossRule.Tests;

public class FightTrackerTests {
    private static FightTracker Fight(params ushort[] ids) {
        var t = new FightTracker();
        t.Start("Ant_19", "defeatedBoneFlyerGiant", ids);
        return t;
    }

    [Fact]
    public void Second_down_at_once_is_a_wipe() {
        var t = Fight(1, 2);
        Assert.Equal(OutcomeKind.Watch, t.Downed(1)!.Kind);
        var o = t.Downed(2)!;
        Assert.Equal(OutcomeKind.Wiped, o.Kind);
        Assert.Equal(new ushort[] { 1, 2 }, o.Players.OrderBy(x => x));
    }

    [Fact]
    public void Last_fighter_leaving_wipes_the_watchers() {
        var t = Fight(1, 2);
        t.Downed(1);
        var o = t.Left(2)!;
        Assert.Equal(OutcomeKind.Wiped, o.Kind);
        Assert.Equal(new ushort[] { 1 }, o.Players);
    }

    [Fact]
    public void Win_revives_watchers() {
        var t = Fight(1, 2, 3);
        t.Downed(2);
        var o = t.BossDefeated()!;
        Assert.Equal(OutcomeKind.Won, o.Kind);
        Assert.Equal(new ushort[] { 2 }, o.Players);
    }

    [Fact]
    public void Fight_survives_without_any_host_notion() {
        var t = Fight(1, 2);
        t.Downed(1);
        Assert.True(t.Active);
        Assert.Equal(OutcomeKind.Won, t.BossDefeated()!.Kind);
    }

    [Fact]
    public void Nothing_happens_after_the_end() {
        var t = Fight(1);
        t.BossDefeated();
        Assert.Null(t.Downed(1));
    }

    [Fact]
    public void A_player_who_was_not_in_the_fight_dies_normally() {
        var t = Fight(1, 2);
        Assert.Null(t.Downed(3));
        Assert.Empty(t.Watchers);
    }

    [Fact]
    public void A_fighter_who_left_is_not_in_the_wipe() {
        var t = Fight(1, 2, 3);
        t.Downed(1);
        Assert.Null(t.Left(2));
        var o = t.Downed(3)!;
        Assert.Equal(OutcomeKind.Wiped, o.Kind);
        Assert.Equal(new ushort[] { 1, 3 }, o.Players.OrderBy(x => x));
    }

    [Fact]
    public void A_second_start_during_a_fight_changes_nothing() {
        var t = Fight(1, 2);
        t.Downed(1);
        Assert.False(t.Start("Ant_19", "defeatedBoneFlyerGiant", new ushort[] { 1, 2 }));
        var o = t.Downed(2)!;
        Assert.Equal(OutcomeKind.Wiped, o.Kind);
        Assert.Equal(new ushort[] { 1, 2 }, o.Players.OrderBy(x => x));
    }

    // A down that reaches the server after the boss died is in no outcome; that watcher must still come back
    [Fact]
    public void A_watcher_comes_back_from_a_win_listed_or_not() =>
        Assert.Equal(ReturnAction.ComeBack, OutcomeRule.ForLocal(watching: true, OutcomeKind.Won));

    [Fact]
    public void A_watcher_dies_normally_after_a_wipe() =>
        Assert.Equal(ReturnAction.DieAfterAll, OutcomeRule.ForLocal(watching: true, OutcomeKind.Wiped));

    [Fact]
    public void A_player_who_does_not_watch_does_nothing_at_the_end() {
        Assert.Equal(ReturnAction.None, OutcomeRule.ForLocal(watching: false, OutcomeKind.Won));
        Assert.Equal(ReturnAction.None, OutcomeRule.ForLocal(watching: true, OutcomeKind.Watch));
    }
}
