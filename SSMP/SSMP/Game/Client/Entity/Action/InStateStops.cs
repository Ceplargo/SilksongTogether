using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Action;

/// <summary>
/// (coop, 0.3.21) Whether a copy's exiting state stops one of its in-state emitters (EntityFsmActions.RegisterStateChange).
/// Bone_16's Bone Goomba Large plays one walk dust (Pt Walk) in Skid, Walk Start and Walking, each by its own
/// PlayParticleEmitterInState. When the next state's play reached the copy before its state change, that change's stop
/// for the state left stopped the dust just played: the host's Goomba walked with its dust, its copy without
/// (probe_goomba_walk_dust, 2026-11-11_030046-pair3). An emitter the entered state's own play has registered already is
/// left playing. Unity-free, so it is tested without the game.
/// </summary>
internal static class InStateStops {
    /// <summary>Whether the emitter of a state being left is stopped, given the entries registered (state, emitter).</summary>
    public static bool Stops(object emitter, string entered, IEnumerable<(string State, object? Emitter)> registered) =>
        !KeepsShared() || !registered.Any(r => r.State == entered && Equals(r.Emitter, emitter));

    /// <summary>The switch the twin skips (a skipped bool method returns false): every left state's emitter stops.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool KeepsShared() => true;
}
