namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) A walking enemy's alert as a hero comes near: the game alone plays it from WalkerV2's own walk (its private
/// aggroSound, then its StartleAnim), not from an FSM action, so SSMP copied no play of it and a copy, whose walker SSMP
/// never starts, played none (0.3.21's Marrow walk: Bone_09's Dock Worker alert in the scene host's game only). The
/// startle clip plays only there, right after the sound: when the scene host's walker plays it, its entity sends a
/// Startle entry, and each copy plays its own walker's alert. An entry, not the clip: an entity's clip has one slot per
/// packet, and under jitter the server forwarded only the later of two clips that came in one tick (walker_alert under
/// --lag 50, friend first: the host's copy got Walk, never Startle). Unity-free, so it is tested without the game.
/// </summary>
internal static class StartleSound {
    /// <summary>Whether the clip the scene host's walker plays now is its startle, so its alert just played.</summary>
    public static bool Sends(string? clip, string? startleAnim) =>
        !string.IsNullOrEmpty(startleAnim) && clip == startleAnim;
}
