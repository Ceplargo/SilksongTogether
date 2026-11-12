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

    [Fact]
    public void A_message_reads_back_its_path_and_wave() {
        Assert.Equal(("Battle Scene", 3), ArenaWave.Parse(ArenaWave.Message("Battle Scene", 3)));
        Assert.Equal(("Quest Scene/Final|Encounter", 0), ArenaWave.Parse(ArenaWave.Message("Quest Scene/Final|Encounter", 0)));
        Assert.Null(ArenaWave.Parse("Battle Scene"));
        Assert.Null(ArenaWave.Parse("Battle Scene|x"));
    }
}
