namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) Other games' copies of the scene host's FireballProjectile (the Rock Roller's bombs). Each copy ran its own
/// fuse: an idle timer that each bounce resets and that five still physics steps, or its last bounce, cut to zero, so
/// the friend's bomb warned and blew up up to 1 s before the host's, or vanished unexploded when its explosion went off
/// while the host's was not yet on (Bone_06, 2026-11-10: 7 of the friend's 15 breaks early, every one from the copy's
/// own Update, i-bone06-trace-p1-1). Now a copy's fuse is held while the host's bomb has not started its warning; once
/// it has, the copy starts its own warning at once, and it blows up when the host's blast shows here (or its own
/// warning ends first). A host bomb that blew up unwarned (it touched a hero) blows the copy up at once.
/// </summary>
internal static class FireballFollow {
    /// <summary>Seconds a hold lasts past the copy's last placement: a copy whose updates stop goes on by itself.</summary>
    public const float HoldS = 0.5f;

    /// <summary>Until when a copy placed now is held.</summary>
    public static float Until(float now) => now + HoldS;

    /// <summary>Whether a copy's fuse is held now.</summary>
    public static bool Holds(float now, float heldUntil) => now < heldUntil;

    /// <summary>
    /// (coop, 0.3.21) Whether a copy's fuse is held now, its copy still followed or not: a followed copy whose scene host's
    /// bomb has not started stays held however long the updates take (Bone_07, host first: the hold ran out between
    /// updates, the friend's copy warned 0.19 s early and blew up for one frame, its Hero Damager live where the host's
    /// game had none; the judges' trace, 2026-11-11). One no longer followed goes on by itself after HoldS, as before.
    /// </summary>
    public static bool Holds(float now, float heldUntil, bool followed) => followed && KeepsHeld() || now < heldUntil;

    /// <summary>The switch the twin skips (a skipped bool method returns false): a followed copy's hold runs out again.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool KeepsHeld() => true;

    /// <summary>Whether a copy's fuse is cut to its warning now: the host's bomb has started (warning or blown), and
    /// the copy is live and not warning yet.</summary>
    public static bool StartsWarning(bool hostStarted, bool copyActive, bool copyWarning, bool hostBlastOn = false) =>
        hostStarted && copyActive && !copyWarning && !hostBlastOn;

    /// <summary>Whether a live copy blows up at once: the host's bomb has blown up (its blast is on here, as the host's
    /// parts set it) with no warning of the copy's own, as one that touched a hero does.</summary>
    public static bool BlowsNow(bool hostStarted, bool hostBlastOn, bool copyActive) =>
        hostStarted && hostBlastOn && copyActive;
}
