using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) The speed the scene host's game sends for a followed object without a body speed, Unity-free so SSMP.Tests
/// compiles it: its move since the last send over that time, but only when the move goes on the way the one before it
/// went. A step is not predicted on, a flight is: the Last Judge's slam, on her body jittering against her room's wall,
/// stepped back and forth about a unit, and the other games predicted each step on for up to 0.25 s, the friend's slam up
/// to 4.3 units past the host's (rehearsal round 1, 2026-10-31).
/// </summary>
internal static class SentSpeed {
    /// <summary>The speed to send for the move (<paramref name="moveX"/>, <paramref name="moveY"/>) made over
    /// <paramref name="elapsed"/> s after the move (<paramref name="beforeX"/>, <paramref name="beforeY"/>); none for
    /// one its boss carries (<paramref name="carried"/>, <see cref="StillsCarried"/>).</summary>
    public static (float X, float Y) Of(float beforeX, float beforeY, float moveX, float moveY, float elapsed,
        bool carried = false) =>
        elapsed <= 0f || carried && StillsCarried() || HoldsBack(beforeX, beforeY, moveX, moveY)
            ? (0f, 0f)
            : (moveX / elapsed, moveY / elapsed);

    /// <summary>(coop) Whether a boss's own object it carries (one it does not let go of: the Last Judge's Censer Slam)
    /// goes out with no speed at all: it moves with her body's jitter, a unit or more a frame, two steps the same way
    /// passed <see cref="HoldsBack"/>, and the other games predicted the third on, the friend's slam up to 6.9 units off
    /// for 0.32 s (2026-11-01).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static bool StillsCarried() => true;

    /// <summary>Whether no speed goes out: the move does not go on the way the one before it went (a step back, or
    /// a step after standing still).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch: then every speed goes out
    public static bool HoldsBack(float beforeX, float beforeY, float moveX, float moveY) =>
        beforeX * moveX + beforeY * moveY <= 0f;
}
