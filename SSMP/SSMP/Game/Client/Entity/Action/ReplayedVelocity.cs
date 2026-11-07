using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Action;

/// <summary>
/// (coop) Whether a copy's body keeps no velocity after SSMP replays an FSM action on the copy, Unity-free so SSMP.Tests
/// compiles it. A copy whose body is kinematic and placed each frame by PredictiveInterpolation keeps none: a kinematic
/// body still moves by its velocity, and nothing zeroes it after the native's stop. Aqueduct_01's Swamp Mosquito Skinny
/// dashes by FireAtTarget on itself (its Control's Short, Long and Shoot Fly, about 30 units a second); the replay set
/// that on its copy, which kept it until the next (velocities read 30 to 60 units a second, each for about a second),
/// and stood 0.6 to 1.2 units off the host's in about a quarter of its frames (0.3.20's mosquito_dash red, 1517 of 5906
/// over 1 unit). A dynamic copy keeps its velocity: its own physics run it. Not per action type: any action that sets
/// the body's velocity (FireAtTarget, SetVelocity2d, SetVelocityAsAngle, ...) is covered, and an action that sets
/// another object's (the mosquito's bullets) is not touched.
/// </summary>
internal static class ReplayedVelocity {
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static bool Drops(bool kinematic, bool placed) => kinematic && placed;
}
