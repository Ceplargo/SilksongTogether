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
}
