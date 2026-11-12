using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop, 0.3.21) A copy's child that SSMP turns on, as the scene host's is, shows its animations' start at once. SSMP
/// turns copies' parts on after every update (ThrownComponent's late update), after the frame's animations ran, so an
/// Animator under it kept its last pose until the next frame, and the physics step before that used it: a pooled Rock
/// Roller bomb's copy blast hurt at its last blast's full size (2.22 across) for one frame where the host's starts at
/// 1.33 and grows (Bone_01, the judges' trace 2026-11-11; 2 of 3 blasts on pair 2). The scene host's own game turns its
/// blast on in its update, before the animations.
/// </summary>
internal static class PartStart {
    /// <summary>Whether a child's Animators are evaluated at their start now: it was off and is turned on.</summary>
    public static bool Evaluates(bool wasOn, bool isOn) => isOn && !wasOn && EvaluatesStart();

    /// <summary>Not inlined: the blast_start check's twin skips it (the child keeps its last pose a frame).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EvaluatesStart() => true;
}
