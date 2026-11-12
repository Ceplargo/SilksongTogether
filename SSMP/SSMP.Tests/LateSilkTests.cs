using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class LateSilkTests {
    // Bone_18's Pilgrim 05: its copy made after the silk spawned, so the copied PlayPossess finds no silk
    [Fact]
    public void A_possess_call_with_no_silk_spawns_it() => Assert.True(LateSilk.Needed(false, "SilkPossession"));

    [Fact]
    public void One_that_found_its_silk_does_not() => Assert.False(LateSilk.Needed(true, "SilkPossession"));

    // the other copied CallMethodProper objects (Colosseum Manager, Mantis Throne, Radiance)
    [Fact]
    public void Another_behaviour_does_not() => Assert.False(LateSilk.Needed(false, "BattleScene"));

    [Fact]
    public void The_spawner_is_the_one_storing_the_calls_target() {
        var spawns = new (string?, string)[] { ("Rock", "rock spawn"), ("Silk", "silk spawn"), (null, "unnamed") };
        Assert.Equal("silk spawn", LateSilk.Spawner("Silk", spawns));
    }

    [Fact]
    public void None_stores_it_or_the_target_has_no_name() {
        var spawns = new (string?, string)[] { ("Rock", "rock spawn"), (null, "unnamed") };
        Assert.Null(LateSilk.Spawner("Silk", spawns));
        Assert.Null(LateSilk.Spawner(null, spawns));
        Assert.Null(LateSilk.Spawner("", spawns));
    }

    // the Pilgrim's Spawn Silk goes on FINISHED to Pray; a late copy that learns the scene host's is in Pray shows it
    [Fact]
    public void The_state_after_the_spawn_has_the_silk_out() {
        var spawnState = new (string?, string?)[] { ("FINISHED", "Pray"), ("ACT 3", "Act 3 BattleReady") };
        Assert.True(LateSilk.OutIn("Pray", spawnState));
    }

    [Fact]
    public void Another_state_or_an_event_other_than_finished_does_not() {
        var spawnState = new (string?, string?)[] { ("FINISHED", "Pray"), ("ACT 3", "Act 3 BattleReady") };
        Assert.False(LateSilk.OutIn("Walk", spawnState));
        Assert.False(LateSilk.OutIn("Act 3 BattleReady", spawnState));
        Assert.False(LateSilk.OutIn(null, spawnState));
    }

    // Bone_11: a Quick Wake Pilgrim's Spawn Silk sends FINISHED before its spawn, so it prays with no silk
    [Fact]
    public void A_test_ahead_of_the_spawn_that_sends_an_event_skips_it() =>
        Assert.True(LateSilk.Skipped(new[] { null, "FINISHED" }));

    [Fact]
    public void Tests_that_send_nothing_do_not() {
        Assert.False(LateSilk.Skipped(new string?[] { null, null }));
        Assert.False(LateSilk.Skipped(new[] { "" }));
        Assert.False(LateSilk.Skipped(new string?[0]));
    }
}
