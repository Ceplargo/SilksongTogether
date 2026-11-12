namespace SSMP.Game.Client;

/// <summary>
/// (coop) Unity-free: an enemy's copy is never carried past the last place its original's game sent
/// (PredictiveInterpolation). Carried on at its last speed until the next update, the Bone Goomba's copy ran 0.04 to
/// 0.09 units past each stop dead, and its box hurt the friend's hero standing 0.065 units past where the Goomba
/// stopped, where the host's hero at that place was never hit (probe_overshoot_hurt, 2026-11-09, 4 of 4 stops).
/// </summary>
internal static class CopyStopRule {
    /// <summary>
    /// Whether a copy is carried on at its last speed past the last place sent: a hero's copy is; an enemy's copy is
    /// not while the switch holds it at that place.
    /// </summary>
    internal static bool CarriesOn(bool enemyCopy, bool holdsAtLast) => !(enemyCopy && holdsAtLast);

    /// <summary>
    /// One axis of a copy's visual offset from the last place sent, after a smoothing step: never across that place
    /// (0 where the step would carry it past).
    /// </summary>
    internal static float Settle(float before, float after) => before * after < 0f ? 0f : after;
}
