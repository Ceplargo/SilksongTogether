using SSMP.Game.Client.Save;
using Xunit;

// arena_after (0.3.21): with the room's win message missing, the arena left to itself kept its battle music; the grace
// is 1 s: at 2 s the fanfare came 5.5 s after the win, the edge of arena_fanfare's 6 s (Shellwood_01b, friend first)
public class ArenaWinHoldTests {
    [Fact]
    public void A_win_left_to_an_arena_that_never_ends_is_loaded_after_the_grace() {
        var hold = new ArenaWinHold();
        hold.Left(10f);
        Assert.False(hold.Due(10.9f, false));
        Assert.True(hold.Due(11.1f, false));
        Assert.False(hold.Due(11.2f, false));
    }

    [Fact]
    public void An_arena_that_began_its_own_end_keeps_it() {
        var hold = new ArenaWinHold();
        hold.Left(10f);
        Assert.False(hold.Due(11.1f, true));
        Assert.False(hold.Due(15f, false));
    }

    [Fact]
    public void No_win_left_loads_nothing() => Assert.False(new ArenaWinHold().Due(100f, false));

    [Fact]
    public void A_second_win_does_not_put_the_load_off() {
        var hold = new ArenaWinHold();
        hold.Left(10f);
        hold.Left(10.5f);
        Assert.True(hold.Due(11.1f, false));
    }
}
