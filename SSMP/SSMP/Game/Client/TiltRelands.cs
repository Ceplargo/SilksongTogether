namespace SSMP.Game.Client;

/// <summary>What a copy does with a landing its hero's game sent for a tilting platform.</summary>
internal enum TiltLanding {
    /// <summary>The copy is not on the platform yet: its own landing there, if it comes within SameS, is this one.</summary>
    Wait,

    /// <summary>The copy's own landing there (its place) came within SameS: the same landing.</summary>
    Same,

    /// <summary>The copy stands on it and landed long enough ago: its hero lifted off for a moment and landed again.</summary>
    Again
}

/// <summary>
/// (coop) Unity-free: a hero running across a tilting platform loses its contact for 0.03 to 0.08 s and lands on it
/// again (TiltPlat.OnCollisionEnter2D: Bone_03's trap door plate, probe_tilt_creak), which the copy's place, smoothed
/// over the network, never shows; so her game sends each landing, and the copy matches it with its own.
/// </summary>
internal static class TiltRelands {
    /// <summary>A sent landing and the copy's own this close (s) are one landing.</summary>
    internal const double SameS = 0.3;

    /// <summary>A sent landing: onPlat, the copy stands on the platform; since its own landing there that no sent one
    /// matched yet (double.MaxValue when none).</summary>
    internal static TiltLanding Decide(bool onPlat, double sinceUnmatchedPositionLand) => !onPlat ? TiltLanding.Wait : sinceUnmatchedPositionLand <= SameS ? TiltLanding.Same : TiltLanding.Again;
}
