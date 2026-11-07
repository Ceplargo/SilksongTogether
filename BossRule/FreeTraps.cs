using System.Linq;

namespace BossRule;

/// <summary>
/// (0.3.20) The rooms whose boss waits in a trap its room runs until a player frees her, and when a game's trap was
/// freed by its own player (so it tells the room) or by an echo (so it does not). Unity-free, so BossRule.Tests compiles
/// it.
/// </summary>
internal static class FreeTraps {
    /// <summary>Each trap: its room, its object, its FSM, the state its freeing enters, and the event that frees it.</summary>
    /// <remarks>Her Return path (encountered, not defeated: after a full-party death or a rejoin) has no vines: Return
    /// Battle's own trigger (a Trigger2dEvent in Idle) sends ENTER, and its Scene Setup shuts the gates and starts her. A
    /// friend who walked in was shut in while the host's game kept her Dormant (0.3.20's probe_bellbeast_return).</remarks>
    public static readonly (string Scene, string Trap, string Fsm, string Freed, string Event)[] Traps = [
        ("Bone_05", "Thick Silk Vines Beast", "Control", "Destroy", "DESTROY"),
        ("Bone_05", "Return Battle", "Start Return Battle", "Scene Setup", "ENTER")
    ];

    /// <summary>The traps of that room (none for most).</summary>
    public static (string Scene, string Trap, string Fsm, string Freed, string Event)[] InRoom(string? scene) =>
        Traps.Where(t => t.Scene == scene).ToArray();

    /// <summary>Whether this game tells the room its trap was freed: it just entered the freed state, by its own
    /// player's doing (not an echo of another game's).</summary>
    public static bool Sends(string? before, string? now, string freed, bool echoed) =>
        !echoed && now == freed && before != freed;

    /// <summary>Whether another game's freeing frees this game's trap: only one still waiting (a state before the freed
    /// one; none read is a trap not found).</summary>
    public static bool Echoes(string? now, string freed) => now != null && now != freed && now != "Destroyed";
}
