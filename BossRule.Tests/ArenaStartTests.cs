using Xunit;

namespace BossRule.Tests;

// (the owner, 2026-11-20) an ordinary arena starts on every screen at once: Room_CrowCourt's arena started alone in a game
// that does not run the room, its gates shut there with no fight to win (probe_cc_lever, b-cc-before-hostff-p4.log)
public class ArenaStartTests {
    [Fact]
    public void Another_games_start_starts_a_fresh_ordinary_arena_here() =>
        Assert.True(ArenaStart.Follows(sameRoom: true, catalogRoom: false, started: false, completed: false));

    [Fact]
    public void An_arena_already_started_or_won_here_is_left_alone() {
        Assert.False(ArenaStart.Follows(sameRoom: true, catalogRoom: false, started: true, completed: false));
        Assert.False(ArenaStart.Follows(sameRoom: true, catalogRoom: false, started: false, completed: true));
    }

    [Fact]
    public void A_boss_catalog_arena_or_another_room_does_not_follow() {
        Assert.False(ArenaStart.Follows(sameRoom: true, catalogRoom: true, started: false, completed: false));
        Assert.False(ArenaStart.Follows(sameRoom: false, catalogRoom: false, started: false, completed: false));
    }
}
