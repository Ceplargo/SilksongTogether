using Xunit;

namespace BossRule.Tests;

public class WaitRuleTests {
    private static PlayerPresence P(ushort id, string name, bool connected, bool inArea) => new(id, name, connected, inArea);

    [Fact]
    public void All_in_area_is_allowed() =>
        Assert.True(WaitRule.Evaluate(new[] { P(1, "Ann", true, true), P(2, "Bob", true, true) }).Allowed);

    [Fact]
    public void Missing_players_are_named_alphabetically_with_counts() {
        var r = WaitRule.Evaluate(new[] { P(1, "Ann", true, true), P(3, "Carol", true, false), P(2, "Bob", true, false) });
        Assert.False(r.Allowed);
        Assert.Equal("Waiting for Bob, Carol (1 of 3 in the arena)", WaitRule.Message(r));
    }

    [Fact]
    public void Disconnected_players_do_not_count() =>
        Assert.True(WaitRule.Evaluate(new[] { P(1, "Ann", true, true), P(2, "Bob", false, false) }).Allowed);

    [Fact]
    public void Disconnected_players_are_not_in_the_counts() {
        var r = WaitRule.Evaluate(new[] { P(1, "Ann", true, true), P(2, "Bob", true, false), P(3, "Carol", false, false) });
        Assert.Equal("Waiting for Bob (1 of 2 in the arena)", WaitRule.Message(r));
    }

    [Fact]
    public void Alone_is_the_normal_game() => Assert.True(WaitRule.Evaluate(new[] { P(1, "Ann", true, true) }).Allowed);

    // An ordinary arena (ArenaHold, the project's ruling 2026-11-20): Room_CrowCourt's arena started alone in the
    // game that does not run the room and shut its hero in (probe_cc_lever, b-cc-before-hostff-p4.log)
    [Fact]
    public void An_ordinary_arena_waits_for_a_hero_in_its_room_outside_it() {
        var r = WaitRule.EvaluateRoom(new[] { (P(1, "Ann", true, true), true), (P(2, "Bob", true, false), true) });
        Assert.False(r.Allowed);
        Assert.Equal("Waiting for Bob (1 of 2 in the arena)", WaitRule.Message(r));
    }

    [Fact]
    public void A_player_elsewhere_in_the_world_does_not_hold_an_ordinary_arena() {
        var r = WaitRule.EvaluateRoom(new[] { (P(1, "Ann", true, true), true), (P(2, "Bob", true, false), false) });
        Assert.True(r.Allowed);
        Assert.Equal(1, r.Total);
    }

    // (the owner, 2026-11-20) an ordinary arena waits a few seconds for the other player, then starts for all
    [Fact]
    public void An_ordinary_arena_waits_up_to_its_seconds_then_starts_anyway() {
        var waiting = WaitRule.EvaluateRoom(new[] { (P(1, "Ann", true, true), true), (P(2, "Bob", true, false), true) });
        Assert.False(WaitRule.RoomMayStart(waiting, 0f));
        Assert.False(WaitRule.RoomMayStart(waiting, WaitRule.RoomWaitS - 0.1f));
        Assert.True(WaitRule.RoomMayStart(waiting, WaitRule.RoomWaitS));
        var allIn = WaitRule.EvaluateRoom(new[] { (P(1, "Ann", true, true), true), (P(2, "Bob", true, true), true) });
        Assert.True(WaitRule.RoomMayStart(allIn, 0.2f));
        Assert.Equal(5f, WaitRule.RoomWaitS);
    }

    [Fact]
    public void An_ordinary_arenas_wait_line_shows_the_whole_seconds_left() {
        var waiting = WaitRule.EvaluateRoom(new[] { (P(1, "Ann", true, true), true), (P(2, "Bob", true, false), true) });
        Assert.Equal("Waiting for Bob, 5 s", WaitRule.RoomMessage(waiting, 0f));
        Assert.Equal("Waiting for Bob, 4 s", WaitRule.RoomMessage(waiting, 1.2f));
        Assert.Equal("Waiting for Bob, 1 s", WaitRule.RoomMessage(waiting, 4.9f));
    }

    [Fact]
    public void An_ordinary_arena_starts_once_every_hero_in_the_room_is_in() =>
        Assert.True(WaitRule.EvaluateRoom(new[] { (P(1, "Ann", true, true), true), (P(2, "Bob", true, true), true) }).Allowed);
}
