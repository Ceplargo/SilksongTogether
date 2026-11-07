using System;
using System.Linq;

namespace BossRule;

/// <summary>
/// (0.3.20) The room objects a player's own game sets off (its hero stands on, lands on, hits or walks under them) and
/// that change the room's terrain or hurt, and how each is set off in another game. Unity-free, so BossRule.Tests
/// compiles it.
/// </summary>
internal static class LocalTraps {
    /// <summary>Each kind: its FSM's name, how its object's name starts, the state it waits in, and each state its setting
    /// off enters with the event that enters it from the waiting state.</summary>
    public static readonly (string Fsm, string Name, string[] Waits, (string State, string Event)[] Sets)[] Kinds = [
        // a hero lands on it (COLLIDE, its checks) or hits it (DAMAGED): it crumbles, breaks and rebuilds
        ("bone_crumble_plat", "bone_plat_", ["Idle", "Check Collision", "Check Collision 2", "Try H Crumble"],
            [("Crumble Antic", "CRUMBLE")]),
        // a hero lands on it (its trigger's LAND) or slashes its chain: it falls and stays down
        ("Control", "Chain Drop Platform", ["Idle"], [("Break Antic", "LAND"), ("Slash Break", "BREAK")]),
        // a floor that collapses under a hero (Bone_01's Battle Scene)
        ("Control", "Collapser Small", ["Idle"], [("Antic Type", "BREAK")]),
        // a boulder a hero's walk or hit drops: it falls and hurts (Impact Damager 2)
        ("Control", "Bone_Boulder", ["Idle", "Shake"], [("Drop Antic", "DROP")]),
        // a wall a hero strikes rumbles and explodes (Bone_07's Explode Sequence)
        ("Control", "explode_wall", ["Idle"], [("Strike", "STRIKE"), ("Set PD", "STRIKE S")]),
        // a deposit each hit wears down until it breaks
        ("Control", "tool_metal_deposit", ["Idle"], [("Hit Register Event", "HIT"), ("Instant Break", "BREAK")]),
    ];

    /// <summary>The Skull King's: they drop on his attack, which his fight's own follow sends (StageFollowed).</summary>
    private const string BossBoulders = "Bone_Boulder SK";

    private static (string Fsm, string Name, string[] Waits, (string State, string Event)[] Sets)? Kind(string fsm,
        string name) => name.StartsWith(BossBoulders, StringComparison.Ordinal) ? null : Kinds
        .Where(k => k.Fsm == fsm && name.StartsWith(k.Name, StringComparison.Ordinal))
        .Cast<(string, string, string[], (string, string)[])?>().FirstOrDefault();

    /// <summary>The event this game's own setting off of such an object sends the room (it just entered a set-off state
    /// from another), or null.</summary>
    public static string? Sets(string fsm, string name, string? before, string? now) {
        if (Kind(fsm, name) is not { } kind || before == now) return null;
        if (kind.Sets.Any(s => s.State == before)) return null;
        return kind.Sets.Where(s => s.State == now).Select(s => s.Event).FirstOrDefault();
    }

    /// <summary>Whether another game's word sets off this game's object: only one still waiting.</summary>
    public static bool Echoes(string fsm, string name, string? now) =>
        Kind(fsm, name) is { } kind && now != null && kind.Waits.Contains(now);
}
