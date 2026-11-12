namespace SSMP.Game.Client;

/// <summary>
/// (coop) Unity-free: an enemy's copy presses the bone segments (BoneSegment, Bone_03's bridges) its original's game
/// sends, not the ones its own box meets (<see cref="CopyPresses"/>). PredictiveInterpolation carries a copy at its last
/// speed until the next update says it stopped or turned: the Bone Goomba stopping dead at x 17.07, its copy ran on to
/// 17.15, and at a turn 0.2 units past for longer than 0.1 s, so its box pressed segments the Goomba never reached; by
/// its last received place instead, it missed the segments the Goomba's box only grazed between two updates
/// (probe_goomba_press, copy_bone_press).
/// </summary>
internal static class CopyPressRule {
    /// <summary>
    /// Whether a collider's own touches of a segment are passed by: one carried by PredictiveInterpolation (an enemy's
    /// copy), not under a hero's copy (RemoteTouches presses for those by its hero's own place).
    /// </summary>
    internal static bool Holds(bool underPlayerCopy, bool predicted) => predicted && !underPlayerCopy;

    /// <summary>
    /// Whether an enemy began (true) or stopped (false) touching a segment, from how many of its colliders touched it
    /// before and after a trigger call; null when neither (another toucher, or its second collider).
    /// </summary>
    internal static bool? Touching(int before, int after) => before == 0 && after > 0 ? true : before > 0 && after == 0 ? false : null;
}
