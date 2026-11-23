using System;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Action;

/// <summary>
/// (coop, 0.4) A FlingObjectsFromGlobalPoolVelTime replay: each fling's velocity from its own x and y speed ranges, where
/// FlingObjectsFromGlobalPoolTime's comes from a speed and an angle. SSMP copied no such action, so a copy never flung
/// its objects: the Giant Centipede's Spit Floor (its two FlingObjectsFromGlobalPoolTime switched off in the game) threw
/// about 14 shots, hazards live 2.2 s, in the scene host's game and none in the other (Bellway_Centipede_Arena, 7 of 7
/// spits; probe_centipede_spit, 2026-11-20). Unity-free, so it is tested without the game.
/// </summary>
internal static class FlingVelTime {
    /// <summary>A fling's velocity: its x from the x range, its y from the y range (range: Unity's Random.Range).</summary>
    public static (float X, float Y) Velocity(float minX, float maxX, float minY, float maxY, Func<float, float, float> range) =>
        (range(minX, maxX), range(minY, maxY));

    /// <summary>Not inlined: centipede_spit's twin skips it, which gives false (the action not sent, as before).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Copies() => true;
}
