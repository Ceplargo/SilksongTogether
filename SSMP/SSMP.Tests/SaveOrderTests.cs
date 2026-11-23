using SSMP.Game.Client.Save;
using Xunit;

namespace SSMP.Tests;

// probe_setpd_once (0.4 World A, 2026-11-18): each game set halfwayCrowd at once and each ended with the other's value
public class SaveOrderTests {
    private static readonly byte[] A = [2], B = [5];

    // the server kept A's write first (1), then B's (2): every game ends with B's, the later arrival
    [Fact]
    public void Two_writes_at_once_end_with_the_later_arrival_in_both_games() {
        var a = new SaveOrder();
        var b = new SaveOrder();
        a.Sent(7);
        b.Sent(7);
        // A's game: its answer, then B's write
        Assert.Null(a.Own(7, 1));
        Assert.True(a.Other(7, 2, B));
        // B's game: A's write comes before B's answer: held, then dropped as older than B's own
        Assert.False(b.Other(7, 1, A));
        Assert.Null(b.Own(7, 2));
    }

    // the same two writes, B's game's packets in the other order (A's answer late): the held later write is applied
    [Fact]
    public void A_later_write_held_while_this_games_own_was_on_the_way_is_applied_at_its_answer() {
        var a = new SaveOrder();
        a.Sent(7);
        Assert.False(a.Other(7, 2, B));
        Assert.Equal(B, a.Own(7, 1));
    }

    // no game takes a value older than one it has applied: a late resend of an older write moves nothing
    [Fact]
    public void An_older_write_after_a_newer_one_is_not_applied() {
        var a = new SaveOrder();
        Assert.True(a.Other(7, 5, B));
        Assert.False(a.Other(7, 4, A));
        Assert.False(a.Other(7, 5, A));
    }

    // nor one older than its own last answered write (an echo of an older write after a newer one: no flicker)
    [Fact]
    public void An_older_write_after_this_games_own_newer_one_is_not_applied() {
        var a = new SaveOrder();
        a.Sent(7);
        Assert.Null(a.Own(7, 9));
        Assert.False(a.Other(7, 8, A));
        Assert.True(a.Other(7, 10, B));
    }

    // a write the server did not number (0: an added value, a put-back, a join's snapshot) goes as before; each index
    // is its own; a connection's end forgets it all
    [Fact]
    public void Unnumbered_writes_and_other_indexes_go_as_before() {
        var a = new SaveOrder();
        a.Sent(7);
        Assert.True(a.Other(7, 0, B));
        Assert.True(a.Other(8, 1, B));
        a.Clear();
        Assert.True(a.Other(7, 1, A));
    }

    // a write the server answered without a number (refused): the held later write goes in at that answer
    [Fact]
    public void A_refused_own_write_lets_a_held_later_write_in() {
        var a = new SaveOrder();
        a.Sent(7);
        Assert.False(a.Other(7, 3, B));
        Assert.Equal(B, a.Own(7, 0));
    }
}
