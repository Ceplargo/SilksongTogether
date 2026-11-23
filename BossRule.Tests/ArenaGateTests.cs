using Xunit;

namespace BossRule.Tests;

// (the owner, 2026-11-20) one shared fight, and each player's gates close only as that player walks in: Room_CrowCourt's
// arena, started in one game, shut its gates on a hero there and left the other's game without a fight
// (probe_cc_lever, b-cc-before-hostff-p4.log)
public class ArenaGateTests {
    [Fact]
    public void A_hero_outside_keeps_its_games_gates_open() =>
        Assert.False(ArenaGate.ShutsNow(inSession: true, catalogRoom: false, completed: false, hasArea: true, heroInside: false));

    [Fact]
    public void A_hero_inside_shuts_them() =>
        Assert.True(ArenaGate.ShutsNow(inSession: true, catalogRoom: false, completed: false, hasArea: true, heroInside: true));

    [Fact]
    public void The_game_alone_a_boss_room_a_won_arena_or_one_with_no_area_shut_as_the_game_does() {
        Assert.True(ArenaGate.ShutsNow(inSession: false, catalogRoom: false, completed: false, hasArea: true, heroInside: false));
        Assert.True(ArenaGate.ShutsNow(inSession: true, catalogRoom: true, completed: false, hasArea: true, heroInside: false));
        Assert.True(ArenaGate.ShutsNow(inSession: true, catalogRoom: false, completed: true, hasArea: true, heroInside: false));
        Assert.True(ArenaGate.ShutsNow(inSession: true, catalogRoom: false, completed: false, hasArea: false, heroInside: false));
    }
}
