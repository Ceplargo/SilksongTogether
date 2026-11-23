using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

internal static class SpecialDeath {
    /// <summary>
    /// Whether the copy dies the regular way. A kind whose scripted death the copy follows (kindFollows: the Shellwood
    /// Wasp, marked dead while its native animation runs) keeps the special path; every other kind dies the regular way,
    /// as before 0.3.19: the Widow's scripted death is her end, which runs in the scene host's game only, and following
    /// it took the friend into her memory (NOTES 322).
    /// </summary>
    internal static bool UsesRegularPath(bool markedDead, bool scripted, bool overridden, bool kindFollows) =>
        markedDead && (!(FollowsNativeDeath() && kindFollows) || overridden || !scripted);

    /// <summary>The kinds whose copies follow their scripted death: the Wasp (wasp_death's, NOTES 128) and (0.4 enemies,
    /// CRAWLSPACE) the Crypt Worm, whose death flies up, rolls and lands with its damaging body on in the scene host's
    /// game; its copy died the regular way at once, and the friend's screen had no box, roll or landing (crypt_death).
    /// </summary>
    internal static bool KindFollows(EntityType type) =>
        type == EntityType.WoodWasp || (type == EntityType.Gromling && FollowsWormDeath());

    /// <summary>Not inlined: crypt_death's twin skips it, which gives false (the worm's copy dies the regular way).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool FollowsWormDeath() => true;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool FollowsNativeDeath() => true;
}
