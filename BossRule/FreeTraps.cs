using System.Collections.Generic;
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

    /// <summary>
    /// (0.3.23) Each room's fight-on state: while that FSM is in it, the fight a trap started is still on (Bone_05's
    /// Battle End waits in Idle until her death). A game coming in then must not load the room as before her start.
    /// </summary>
    public static readonly (string Scene, string Object, string Fsm, string State)[] FightOns = [
        ("Bone_05", "Boss Scene", "Battle End", "Idle")
    ];

    /// <summary>The room's fight-on state, if it has one.</summary>
    public static (string Scene, string Object, string Fsm, string State)? FightOn(string? scene) =>
        FightOns.FirstOrDefault(f => f.Scene == scene) is { Scene: not null } on ? on : null;

    /// <summary>
    /// (0.3.23) Whether this game tells the room, again, that its fight is on: one of its traps was freed here (by its
    /// own player or an echo) and the fight-on FSM is still in its state. A friend who came into Bone_05 while the host
    /// fought her loaded her Return path (met, not beaten), its gates open, and walking on into Return Battle's trigger
    /// shut them and showed her title mid-fight (0.3.20's probe_bellbeast_steps, its join case; known differences).
    /// </summary>
    public static bool Resends(bool freedHere, string? fightState, string fightOn) => freedHere && fightState != null && fightState == fightOn;

    /// <summary>
    /// (0.3.23) Whether a game told the fight is on starts its own trap late: the trap still waits, in a state that
    /// takes its freeing event (Return Battle's Idle takes ENTER; Start Battle and Title take none).
    /// </summary>
    public static bool LateStarts(string? now, string freed, string ev, IEnumerable<string> stateEvents) => Echoes(now, freed) && stateEvents.Contains(ev);
}
