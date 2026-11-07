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

    /// <summary>The kinds whose copies follow their scripted death (wasp_death's, NOTES 128).</summary>
    internal static bool KindFollows(EntityType type) => type == EntityType.WoodWasp;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool FollowsNativeDeath() => true;
}
