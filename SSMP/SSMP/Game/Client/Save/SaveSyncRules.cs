using System;
using System.Collections.Generic;

namespace SSMP.Game.Client.Save;

/// <summary>
/// Decides whether a save value is shared by the group or kept per player. Kept free of Unity types so it can be
/// tested without the game.
/// </summary>
internal static class SaveSyncRules {
    /// <summary>
    /// Whether a save value is one shared value for the group (true) or kept per player (false).
    /// </summary>
    /// <param name="kind">The value's kind, or null for a value that save-data.json does not label.</param>
    /// <param name="legacyShared">Whether the value's legacy SyncType is Server; used when it has no kind.</param>
    /// <param name="itemSharing">Whether item sharing is on for this save.</param>
    public static bool IsShared(SaveKind? kind, bool legacyShared, bool itemSharing) => kind switch {
        SaveKind.Shared => true,
        SaveKind.Personal => false,
        SaveKind.Item => itemSharing,
        _ => legacyShared
    };

    /// <summary>
    /// (0.3.22, from the original SSMP's 4d7ca26) Apply each value of the save a joining player gets. A value whose
    /// apply throws is reported and skipped, so one bad value no longer stops the join before the game is entered.
    /// </summary>
    /// <param name="values">The save's values by index.</param>
    /// <param name="apply">Applies one value.</param>
    /// <param name="skipped">Told each value that threw, with its exception.</param>
    public static void ApplyEach(
        IEnumerable<KeyValuePair<ushort, byte[]>> values,
        Action<ushort, byte[]> apply,
        Action<ushort, Exception> skipped
    ) {
        foreach (var pair in values) {
            try {
                apply(pair.Key, pair.Value);
            } catch (Exception e) {
                skipped(pair.Key, e);
            }
        }
    }
}
