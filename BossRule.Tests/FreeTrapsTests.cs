using System.Linq;
using BossRule;
using Xunit;

namespace BossRule.Tests;

// 0.3.20 Part C cause no. 1: the Bell Beast hangs in Bone_05's thick silk vines until a player's Special Attack hits
// their Special Detector; the vines' Control (Idle, then Destroy) sends FREE to her. Nothing of it is synced, so a
// friend who freed her was shut in (its gates closed, the host's game never started her).
public class FreeTrapsTests {
    // Her Return path (encountered, not defeated: after a full-party death or a rejoin) has no vines: Return Battle's
    // own trigger (Trigger2dEvent, Idle) sends ENTER, and its Scene Setup shuts the gates and starts her. Nothing of it
    // was synced either: a friend who walked in was shut in while the host's game kept her Dormant (0.3.20's
    // probe_bellbeast_return, pair 4).
    [Fact]
    public void Her_room_has_her_vines_and_her_return_start() {
        var traps = FreeTraps.InRoom("Bone_05").Select(t => (t.Trap, t.Fsm, t.Freed, t.Event)).ToArray();
        Assert.Equal([
            ("Thick Silk Vines Beast", "Control", "Destroy", "DESTROY"),
            ("Return Battle", "Start Return Battle", "Scene Setup", "ENTER")
        ], traps);
        Assert.Empty(FreeTraps.InRoom("Bone_15"));
        Assert.Empty(FreeTraps.InRoom(null));
    }

    [Fact]
    public void A_game_whose_own_player_started_her_return_tells_the_room() {
        Assert.True(FreeTraps.Sends("Idle", "Scene Setup", "Scene Setup", echoed: false));
        Assert.False(FreeTraps.Sends("Scene Setup", "Start Battle", "Scene Setup", echoed: false));
        Assert.True(FreeTraps.Echoes("Idle", "Scene Setup"));
    }

    [Fact]
    public void A_game_whose_own_player_freed_her_tells_the_room_once() {
        Assert.True(FreeTraps.Sends("Idle", "Destroy", "Destroy", echoed: false));
        Assert.False(FreeTraps.Sends("Destroy", "Destroy", "Destroy", echoed: false));
        Assert.False(FreeTraps.Sends("Idle", "Idle", "Destroy", echoed: false));
    }

    [Fact]
    public void A_game_freed_by_the_echo_tells_nobody() =>
        Assert.False(FreeTraps.Sends("Idle", "Destroy", "Destroy", echoed: true));

    [Fact]
    public void An_echo_frees_a_trap_still_waiting_only() {
        Assert.True(FreeTraps.Echoes("Idle", "Destroy"));
        Assert.False(FreeTraps.Echoes("Destroy", "Destroy"));
        Assert.False(FreeTraps.Echoes(null, "Destroy"));
    }
}
