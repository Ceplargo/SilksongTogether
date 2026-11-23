using Xunit;

namespace BossRule.Tests;

public class ArenaWaveTests {
    // Bone_01 with the friend outside: the host's arena started, the friend's never did (0.3.21 Part C)
    [Fact]
    public void A_host_arena_that_started_starts_this_one() => Assert.Equal(WaveStep.Start, ArenaWave.For(false, false, -1, 0));

    // Bone_01 with both in: the friend's waves counted SSMP's hidden originals and stopped at wave 3
    [Fact]
    public void A_wave_behind_the_hosts_starts_the_next() => Assert.Equal(WaveStep.Wave, ArenaWave.For(true, false, 2, 3));

    // its wave started here but its start delay has not set currentWave: started once only
    [Fact]
    public void A_wave_started_here_is_not_started_again() => Assert.Equal(WaveStep.None, ArenaWave.For(true, false, 3, 3));

    // the arena's own start is still in its start pause (started is set after it): not started twice
    [Fact]
    public void A_start_under_way_is_not_started_again() => Assert.Equal(WaveStep.None, ArenaWave.For(false, false, 0, 0));

    [Fact]
    public void An_arena_that_ended_here_is_left_alone() => Assert.Equal(WaveStep.None, ArenaWave.For(false, true, -1, 2));

    [Fact]
    public void No_host_wave_starts_nothing() => Assert.Equal(WaveStep.None, ArenaWave.For(false, false, -1, -1));

    // Dock_08's Wave 1 Dock Worker in Battle Dormant: its wave started here while this game followed (lead rejoins, 0.4)
    [Fact]
    public void A_child_still_waiting_in_a_wave_started_while_following_wakes() =>
        Assert.True(ArenaWave.WakesAgain(true, new[] { "BATTLE START", "WAKE" }));

    // out already (the runner's game woke it, its copy's state taken over): its state has no BATTLE START
    [Fact]
    public void A_child_out_already_is_left() => Assert.False(ArenaWave.WakesAgain(true, new[] { "FINISHED" }));

    // a wave this game started as the room's scene host: its own start reached its own enemies
    [Fact]
    public void A_wave_started_as_scene_host_is_left() =>
        Assert.False(ArenaWave.WakesAgain(false, new[] { "BATTLE START" }));

    [Fact]
    public void A_message_reads_back_its_path_and_wave() {
        Assert.Equal(("Battle Scene", 3), ArenaWave.Parse(ArenaWave.Message("Battle Scene", 3)));
        Assert.Equal(("Quest Scene/Final|Encounter", 0), ArenaWave.Parse(ArenaWave.Message("Quest Scene/Final|Encounter", 0)));
        Assert.Null(ArenaWave.Parse("Battle Scene"));
        Assert.Null(ArenaWave.Parse("Battle Scene|x"));
    }

    // (Ant_05b's lead rejoin) an original the runner's game killed, its hp set to 0 by SSMP, leaves its wave at the takeover
    [Fact]
    public void An_original_killed_in_the_runners_game_is_gone() {
        Assert.True(ArenaWave.Gone(0));
        Assert.True(ArenaWave.Gone(-3));
    }

    [Fact]
    public void A_living_enemy_or_a_child_with_no_health_stays() {
        Assert.False(ArenaWave.Gone(30));
        Assert.False(ArenaWave.Gone(null));
    }

    [Fact]
    public void Only_a_game_that_followed_the_room_takes_its_arenas_over() {
        Assert.True(ArenaWave.TakesOver(followed: true, sceneHostNow: true));
        Assert.False(ArenaWave.TakesOver(followed: false, sceneHostNow: true));
        Assert.False(ArenaWave.TakesOver(followed: true, sceneHostNow: false));
    }
}
