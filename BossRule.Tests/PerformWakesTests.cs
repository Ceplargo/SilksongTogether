using BossRule;
using Xunit;

namespace BossRule.Tests;

// 0.3.20 Task 21: the Skull King wakes when a hero in his Wake Range plays the Needolin near him (his Behaviour's State
// Check, CheckHeroPerformanceRegionV2); that FSM runs only in the game of whoever came in first, so the other player's
// Needolin woke nothing (probe_intro_pose). Each game whose own hero performs where he hears it tells the room.
public class PerformWakesTests {
    [Fact]
    public void His_room_has_his_wake() {
        var wake = PerformWakes.In("Bone_15");
        Assert.NotNull(wake);
        Assert.Equal(("Skull King", "Behaviour", "State Check", "WAKE"), (wake!.Value.Boss, wake.Value.Fsm, wake.Value.Waits, wake.Value.Event));
        Assert.Null(PerformWakes.In("Bone_05"));
    }

    [Fact]
    public void A_performance_where_he_hears_it_tells_the_room_once_a_second() {
        Assert.True(PerformWakes.Sends(performing: true, inRange: true, now: 10f, lastSent: -999f));
        Assert.False(PerformWakes.Sends(performing: true, inRange: true, now: 10.5f, lastSent: 10f));
        Assert.True(PerformWakes.Sends(performing: true, inRange: true, now: 11f, lastSent: 10f));
        Assert.False(PerformWakes.Sends(performing: true, inRange: false, now: 20f, lastSent: 10f));
        Assert.False(PerformWakes.Sends(performing: false, inRange: true, now: 20f, lastSent: 10f));
    }

    [Fact]
    public void Only_the_game_that_runs_him_wakes_him_while_he_waits() {
        Assert.True(PerformWakes.Echoes(runs: true, state: "State Check", waits: "State Check"));
        Assert.False(PerformWakes.Echoes(runs: false, state: "State Check", waits: "State Check"));
        Assert.False(PerformWakes.Echoes(runs: true, state: "Wake Antic", waits: "State Check"));
    }
}
