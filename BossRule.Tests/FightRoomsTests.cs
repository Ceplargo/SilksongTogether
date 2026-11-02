using System;
using System.Collections.Concurrent;
using System.Threading;
using Xunit;

namespace BossRule.Tests;

public class FightRoomsTests {
    [Fact]
    public void A_leave_on_another_thread_while_fights_start_breaks_nothing() {
        // SSMP handles packets on its network thread, and a timed-out player's leave on that player's send thread
        var rooms = new FightRooms();
        var errors = new ConcurrentQueue<Exception>();

        Thread Run(Action<int> step) {
            var thread = new Thread(() => {
                try {
                    for (var i = 0; i < 5_000; i++) step(i);
                } catch (Exception e) {
                    errors.Enqueue(e);
                }
            });
            thread.Start();
            return thread;
        }

        var starts = Run(i => rooms.Start($"Room_{i}", "flag", new ushort[] { 1, 2 }));
        var leaves = Run(_ => rooms.Left(1));
        Assert.True(starts.Join(TimeSpan.FromSeconds(60)) && leaves.Join(TimeSpan.FromSeconds(60)), "a thread hung");
        Assert.Empty(errors);
    }

    [Fact]
    public void A_leave_ends_the_fights_it_empties_in_other_rooms_only() {
        var rooms = new FightRooms();
        rooms.Start("Ant_19", "defeatedBoneFlyerGiant", new ushort[] { 1, 2 });
        rooms.Start("Library_13", "defeatedTrobbio", new ushort[] { 1 });
        Assert.Equal(OutcomeKind.Watch, rooms.Downed("Ant_19", 2)!.Kind);
        var ended = rooms.Left(1, stillIn: "Library_13");
        var (scene, outcome) = Assert.Single(ended);
        Assert.Equal("Ant_19", scene);
        Assert.Equal(OutcomeKind.Wiped, outcome.Kind);
        Assert.Equal(new ushort[] { 2 }, outcome.Players);
        Assert.Equal(OutcomeKind.Won, rooms.BossDefeated("Library_13")!.Kind);
    }
}
