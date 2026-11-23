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

    /// <summary>(0.3.21) Whether a thrown copy's Animator always animates while a copy (<see cref="CopyCulling{TAnimator,TMode}"/>):
    /// one culled whose clips key no animation events (an event would play off screen where the game alone culls it).</summary>
    internal static bool Animates(bool culled, bool hasEvents) => culled && !hasEvents && AnimatesCopies();

    /// <summary>(0.3.21) Not inlined: blast_follow's twin skips it, which gives false.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool AnimatesCopies() => true;

    /// <summary>(0.4 enemies Task 5) Whether a boss's own object is out of sight here while SSMP's copy of the boss plays
    /// the clip of the state that puts it away (the Last Judge's Catch): the clip comes ahead of the scene host's update
    /// that ends the object, and the friend's censer showed two reads past her catch (censer_place).</summary>
    internal static bool EndsOnCatch(string? playing, string catchClip) => playing == catchClip && EndsOnOwnCatch();

    /// <summary>(0.4 enemies Task 5) Not inlined: censer_catch's twin skips it, which gives false.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool EndsOnOwnCatch() => true;
}
