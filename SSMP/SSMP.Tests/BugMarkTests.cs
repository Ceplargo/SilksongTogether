using System;
using SSMP.Game.Server;
using Xunit;

namespace SSMP.Tests;

public class BugMarkTests {
    private static readonly DateTime Time = new(2026, 10, 27, 3, 12, 45);

    [Fact]
    public void Line_names_the_time_who_the_note_and_every_room() =>
        Assert.Equal(
            "[BUG] 2026-10-27 03:12:45 Client: \"the boss froze\" | Host in Tut_01, Client in Tut_02",
            BugMark.Line(Time, "Client", "the boss froze", [("Host", "Tut_01"), ("Client", "Tut_02")])
        );

    [Fact]
    public void A_note_with_line_breaks_stays_on_one_line() =>
        Assert.Equal("a b c d e", BugMark.Clean("a\r\nb\tc\u2028d\u0000\u0085e"));

    [Fact]
    public void A_long_note_is_cut_to_80() => Assert.Equal(new string('x', 80), BugMark.Clean(new string('x', 200)));

    [Fact]
    public void Spaces_are_trimmed_and_runs_become_one() => Assert.Equal("a b", BugMark.Clean("  a \n\n  b  "));

    [Fact]
    public void An_empty_note_leaves_the_quotes_out() =>
        Assert.Equal(
            "[BUG] 2026-10-27 03:12:45 Host | Host in Tut_01",
            BugMark.Line(Time, "Host", " \r\n", [("Host", "Tut_01")])
        );

    [Fact]
    public void A_player_with_no_room_says_so() =>
        Assert.Equal(
            "[BUG] 2026-10-27 03:12:45 Host: \"x\" | Host in Tut_01, Client in no room yet",
            BugMark.Line(Time, "Host", "x", [("Host", "Tut_01"), ("Client", null)])
        );

    [Fact]
    public void Every_line_starts_with_the_prefix() =>
        Assert.StartsWith(BugMark.Prefix, BugMark.Line(Time, "Host", null, []));
}
