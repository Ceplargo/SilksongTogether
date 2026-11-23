using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop, 0.4 enemies Task 3 Step 5) Which of the scene host's thrown objects an update carries: one fallen far below
/// its room, or flown far past either side, can no longer matter (a Widow's bell once fell on to y -1188902, still
/// active), and an update counts its objects in a byte.
/// </summary>
internal static class ThrownSends {
    /// <summary>The most objects one update carries: its count is a byte.</summary>
    public const int MaxSent = byte.MaxValue;

    /// <summary>Units past the room's bottom or sides (its tilemap, from 0) an object may be and still matter: the
    /// camera never shows more than about 9 past them.</summary>
    public const float Margin = 20f;

    /// <summary>Whether far-out objects are left out. Off: every object still active is sent.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Prunes() => true;

    /// <summary>
    /// Whether an object at (x, y) can still matter in a room this wide: not far below it nor far past either side
    /// (above it, it may still fall back in).
    /// </summary>
    public static bool Matters(float x, float y, float width) {
        return y >= -Margin && x >= -Margin && x <= width + Margin;
    }

    /// <summary>(coop, 0.4 enemies) The most pruned numbers one update names (each a byte).</summary>
    public const int MaxPruned = 32;

    /// <summary>(coop, 0.4 enemies) How many updates name each pruned number: a lost one's resend is dropped once a
    /// newer update goes (ResendRules.DropsOldThrows), so the next ones say it again.</summary>
    public const int PrunedSends = 5;

    /// <summary>(coop, 0.4 enemies) Whether an update names the objects the scene host pruned, so the other games end
    /// their copies: left out of an update, a copy finishes by itself, and a Widow's bell copy whose bell the host
    /// pruned rose on for good, its hurt box on, through the next rooms (plan2, 2026-11-17, t3s5-merged-plan2-p1-0910).
    /// Off: pruned objects are only left out.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SaysPruned() => true;

    /// <summary>
    /// (coop, 0.4 enemies) The pruned numbers the next update names: up to <see cref="MaxPruned"/> of
    /// <paramref name="pending"/> (each number's sends left) into <paramref name="said"/>, a send counted off each; one
    /// named <see cref="PrunedSends"/> times leaves <paramref name="pending"/>.
    /// </summary>
    public static void Say(Dictionary<byte, int> pending, List<byte> said) {
        said.Clear();
        foreach (var number in pending.Keys) {
            if (said.Count >= MaxPruned) break;
            said.Add(number);
        }

        foreach (var number in said) {
            if (--pending[number] <= 0) pending.Remove(number);
        }
    }
}
