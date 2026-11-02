using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) How long the other games' copy of a thrown object may go on by its body's speed after it was last placed,
/// Unity-free so SSMP.Tests compiles it. Each frame a copy is placed where the scene host's is predicted and its body
/// given the host's last speed, so it moves on between frames as the host's does; but a long frame (a hitch in that
/// game) runs all its physics steps before the next placement, and the body carried the copy on at the host's speed
/// meanwhile, its hurt box with it: the friend's copy of the Phantom's pin flew 16.8 units on in a 266 ms frame, 8.7
/// ahead of the host's slowing pin (the combined 0.3.15 gate 2026-11-01_025548's round 3; gate 121504's round 2: 8 units
/// past where it sticks).
/// </summary>
internal static class RunOn {
    /// <summary>Seconds a copy's body may carry it on after its last placement: a frame at 20 a second still does.
    /// </summary>
    public const float Limit = 0.05f;

    /// <summary>Whether a copy last placed at <paramref name="placedAt"/> stops its body at a physics step at
    /// <paramref name="stepTime"/> (game time both).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // runon's twin switches it off with a patch: then a copy runs on as before
    public static bool Stops(float stepTime, float placedAt) => stepTime - placedAt > Limit;

    /// <summary>Whether a copy last placed at <paramref name="placedAt"/> is no longer placed, the latest placement
    /// being at <paramref name="lastPlacing"/> (game time both): the scene host's update left it out (it ended there) or
    /// none came within the prediction's limit. It goes on by itself, as before the stop; stopped, it could hang short
    /// of the wall its host's copy hit (round five's review).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a patch switches it off: then every left-out copy stops as before
    public static bool Forgets(float placedAt, float lastPlacing) => placedAt < lastPlacing;
}
