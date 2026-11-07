using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

internal static class ThrownCopies {
    /// <summary>
    /// Whether a throw the scene host's update lists gets a copy here: none yet, on the room's first update or any
    /// later one (a throw its dying parent took away, RestoresMissingThrows), but not one whose copy ended itself here
    /// (it broke on the friend, or landed first: made again, it showed and flew on, the whole-branch review of
    /// 2026-11-05).
    /// </summary>
    internal static bool NeedsCopy(bool first, bool hasCopy, bool endedItself) =>
        !hasCopy && !(endedItself && LeavesEndedCopies()) && (first || RestoresMissingThrows());

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool LeavesEndedCopies() => true;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool RestoresMissingThrows() => true;

    internal static bool StopsAtNativeEnd(bool recovered) => recovered && EndsRecoveredThrows();

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool EndsRecoveredThrows() => true;
}
