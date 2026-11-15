using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop, 0.3.23 enemies Task 18) A followed corpse's fuse runs on the scene host's clock in the games that follow it. A
/// Dock Bomber's corpse sets its gas off 1.75 s after its own landing (its Control FSM: Land 1 s, Explode Antic 0.75 s),
/// on each game's own clock, so a freeze while it flew put one game's gas early or late: after the scene host's game froze,
/// the other's went off up to about the freeze early (NOTES 347; the known-differences row of 2026-11-05). The following
/// game's corpse copy now waits in its held state until the scene host's corpse goes off (CorpseFuse). Unity-free, so it
/// is tested without the game.
/// </summary>
internal static class CorpseFuses {
    /// <summary>A corpse's fuse: its FSM, the state before the held one, the held state and the state that goes off.</summary>
    public readonly record struct Fuse(string Fsm, string Before, string Held, string Next);

    /// <summary>The corpses whose fuse waits for the scene host's, by name (FollowedCorpses' Dock Bomber).</summary>
    private static readonly Dictionary<string, Fuse> Fuses = new() {
        ["Corpse Dock Bomber"] = new Fuse("Control", "Land", "Explode Antic", "Explode")
    };

    /// <summary>The fuse of a corpse of this name that waits for the scene host's, or null.</summary>
    public static Fuse? Of(string name) =>
        Fuses.TryGetValue(name.Replace("(Clone)", "").Trim(), out var fuse) && WaitsForHost() ? fuse : null;

    /// <summary>
    /// The state a held corpse copy goes to as it is let go, or null to leave it on its own clock: hostsWent, the scene
    /// host's corpse went off (it left the update); active, the copy's state. A copy still before its explosion goes
    /// off with the scene host's; one let go for another reason (its room taken over, unloaded) keeps its own fuse.
    /// </summary>
    public static string? ReleaseTo(Fuse fuse, bool hostsWent, string? active) =>
        hostsWent && (active == fuse.Before || active == fuse.Held) ? fuse.Next : null;

    /// <summary>The switch gas_clock's twin turns off (a skipped bool method returns false): each corpse keeps its own
    /// clock.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool WaitsForHost() => true;
}
