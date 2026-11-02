using Xunit;

namespace BossRule.Tests;

public class ArenaEndTests {
    // Greymoor_04 in the friend's game: started by the friend's own walk-in, its waves counting SSMP's hidden originals
    // (0.3.16's softlock); the host's win reaches it
    [Fact]
    public void A_running_arena_whose_win_came_ends() => Assert.Equal(ArenaStep.End, ArenaEnd.For(true, false, true));

    // its scene host's arena is still on a wave: a later wave must not be cut short
    [Fact]
    public void A_running_arena_without_a_win_keeps_going() => Assert.Equal(ArenaStep.None, ArenaEnd.For(true, false, false));

    // the friend outside at the door: its arena never started, so a walk-in after the win would have started it
    [Fact]
    public void A_won_arena_that_never_started_here_ends_too() => Assert.Equal(ArenaStep.End, ArenaEnd.For(false, false, true));

    // Bone_01 in the friend's game: SSMP's sync of its saved object completed it while it ran (BG QUICK OPEN only)
    [Fact]
    public void An_arena_the_shared_save_completed_while_it_ran_opens_its_gates() =>
        Assert.Equal(ArenaStep.Open, ArenaEnd.For(true, true, false));

    [Fact]
    public void An_arena_that_ended_is_left_alone() => Assert.Equal(ArenaStep.None, ArenaEnd.For(false, true, true));
}
