namespace SSMP.Animation.Effects.Movement;

/// <summary>
/// (coop) Unity-free: a copy's footstep or landing sounds for the ground under the hero it copies (the sender's
/// PlayerData.environmentType, its effect info), not the ground under this game's hero.
/// </summary>
internal static class GroundPick {
    /// <summary>The sent ground, if it is one of this game's count grounds; else null (this game's own).</summary>
    internal static int? Of(byte[]? info, int count) => info is { Length: > 0 } && info[0] < count ? info[0] : null;

    /// <summary>Whether this frame's soft landing is its first (the game spawns one through two methods).</summary>
    internal static bool FirstThisFrame(int frame, ref int last) => frame != last && (last = frame) == frame;
}
