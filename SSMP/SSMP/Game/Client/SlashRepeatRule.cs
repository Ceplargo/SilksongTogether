using System;

namespace SSMP.Game.Client;

/// <summary>
/// (coop, 0.3.21) Unity-free: a hero's slash clip that starts again for the same attack. In the game each Attack() raises
/// the hero's attack count and starts one slash; its clip plays from the hero's attacking state, so when another clip cuts
/// in for a frame the same clip plays again for that attack. SSMP sends a clip each time it starts, and the other game
/// made a second slash for it (Task 12's world sweep over Bone_16: Geo Rock 2 took two hits off one swing).
/// </summary>
internal static class SlashRepeatRule {
    /// <summary>How long after its start a clip played again counts as the same attack's (seconds); a guard for an attack
    /// count reset to an old value, as an attack's clip lasts well under it.</summary>
    internal const double RepeatS = 0.5;

    /// <summary>The hero's clips that Attack() plays (HeroAnimationController's attacking state).</summary>
    internal static readonly string[] AttackClips =
        ["Slash", "SlashAlt", "UpSlash", "UpSlashAlt", "DownSlash", "DownSlashAlt", "Wall Slash"];

    /// <summary>Whether this clip, starting now at this attack count, is the last sent one again for the same attack.</summary>
    internal static bool IsRepeat(string? lastClip, int lastCount, double lastTime, string clip, int count, double now) =>
        clip == lastClip && count == lastCount && Array.IndexOf(AttackClips, clip) >= 0 &&
        now - lastTime >= 0 && now - lastTime < RepeatS;
}
