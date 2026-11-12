namespace SSMP.Game.Client;

/// <summary>
/// (coop, 0.3.21 Task 8) The owner's rule (2026-11-08): what the other player does shakes this screen only while its
/// source shows here; the local player's own doing shakes as in the game alone. GamePatcher applies it to the camera
/// shakes a remote player's hit sets off here (DamageEnemies.ProcessDamageBuffer for a remote attack) and to a remote tink's shake.
/// </summary>
internal static class ShakeView {
    /// <summary>The rule's switch; shake_view's twin turns it off with a patch (the shakes as before 0.3.21).</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool OnlyInView() => true;

    /// <summary>
    /// Whether a camera shake goes ahead on this screen: always for the local player's own doing; while a remote
    /// player's hit is applied, only when its source shows (the switch off: always, as before).
    /// </summary>
    public static bool Shakes(bool remoteHit, bool sourceShows, bool onlyInView) =>
        !remoteHit || !onlyInView || sourceShows;

    /// <summary>
    /// Whether a tink's shake goes ahead: always for the local player's hit; for a remote player's, while its object
    /// shows (the switch off: never while remote attacks leave the local camera alone, as before).
    /// </summary>
    public static bool TinkShakes(bool local, bool keepsLocalHeroOut, bool tinkShows, bool onlyInView) =>
        local || !keepsLocalHeroOut || (onlyInView && tinkShows);
}
