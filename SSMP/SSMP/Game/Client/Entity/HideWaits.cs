using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

/// <summary>
/// (coop, 0.4 enemies Task 6) When the scene host's hide of a copy that follows its scripted death (a Shellwood Wasp,
/// SpecialDeath.KindFollows) is applied. Its death is reliable data and its hide an unreliable update, so after a
/// lost packet the hide came first, or in the same frame as the resent death: the copy went at 5 health, or showed its
/// death for one frame (NOTES 478; wasp_drop). Unity-free, so it is tested without the game.
/// </summary>
internal static class HideWaits {
    /// <summary>
    /// How long a copy's death shows before its hide: as long as the scene host's Wasp lies at 0 health before its
    /// own hide (about 0.19 s, NOTES 478), so a hide that came with or just after its death waits that long.
    /// </summary>
    public const float DeathShowS = 0.19f;

    /// <summary>A copy still alive waits for its death a round trip and this much more (the resend goes out with the
    /// next send after the gap shows).</summary>
    public const float MarginS = 0.1f;

    /// <summary>
    /// How long a send goes unacked before it counts as lost and its reliable data goes again: RttTracker's
    /// MaximumExpectedRtt, twice the round trip within 0.2 to 1 s.
    /// </summary>
    internal static float LossS(float rttS) => System.Math.Min(System.Math.Max(2 * rttS, 0.2f), 1f);

    /// <summary>
    /// When the hide that reached this game at hideAt is applied: once the copy's death (seen here at seenDeadAt) has
    /// shown DeathShowS; while the copy is alive and hurt, two lost sends (LossS each: its death crosses two hops, the
    /// room's game to the server and the server to this game), a round trip and MarginS later, as its death may still
    /// come; alive and unhurt, a round trip and MarginS later; else at once (a copy put away alive with no death to come
    /// still goes then).
    /// </summary>
    internal static float Due(bool alive, bool damaged, float? seenDeadAt, float hideAt, float rttS) {
        if (!Waits()) return hideAt;
        if (seenDeadAt is { } died) return System.Math.Max(hideAt, died + DeathShowS);
        if (!alive) return hideAt;
        return damaged ? hideAt + 2 * LossS(rttS) + rttS + MarginS : hideAt + rttS + MarginS;
    }

    /// <summary>The switch: off, the hide is applied when it comes, as before. Not inlined: wasp_drop's second
    /// twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool Waits() => true;
}
