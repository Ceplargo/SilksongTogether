using System;
using System.Collections.Generic;

namespace SSMP.Game.Server.Save;

/// <summary>
/// Rules for the mod save file (user[slot].modsav) that sits next to a hosted save slot. Kept free of Unity types so
/// it can be tested without the game.
/// </summary>
internal static class ModSaveLifecycle {
    /// <summary>
    /// A host save with less play time than this is a new game, so a mod save next to it belongs to an old game.
    /// </summary>
    private const float NewGamePlayTimeSeconds = 60f;

    /// <summary>
    /// How far the mod save's recorded play time may run ahead of its slot's. The game writes the slot after the mod
    /// save is written, so a crash or a failed write in between leaves the slot one autosave (3 minutes) behind; an
    /// earlier game in the slot is hours apart.
    /// </summary>
    private const float AheadOfSlotSeconds = 600f;

    /// <summary>
    /// Whether the slot's mod save belongs to an earlier game in the same slot and should be set aside.
    /// </summary>
    /// <param name="recordedPlayTimeSeconds">The host's play time the mod save recorded when it was written; null in
    /// mod saves written before it was recorded.</param>
    /// <param name="hostPlayTimeSeconds">The play time of the host's loaded save.</param>
    public static bool ShouldArchive(float? recordedPlayTimeSeconds, float hostPlayTimeSeconds) =>
        recordedPlayTimeSeconds is { } recorded
            ? hostPlayTimeSeconds + AheadOfSlotSeconds < recorded
            : hostPlayTimeSeconds < NewGamePlayTimeSeconds;

    /// <summary>
    /// The path to move a mod save to: "[path].[reason]-[yyyyMMddHHmmss]", with a counter when that is taken.
    /// </summary>
    /// <param name="modSavePath">The path of the mod save.</param>
    /// <param name="reason">Why it is moved, such as "old" or "unreadable".</param>
    /// <param name="now">The current time.</param>
    /// <param name="exists">Whether a file exists at a path.</param>
    public static string ArchivePath(string modSavePath, string reason, DateTime now, Func<string, bool> exists) {
        var path = $"{modSavePath}.{reason}-{now:yyyyMMddHHmmss}";
        var candidate = path;
        for (var count = 2; exists(candidate); count++) {
            candidate = $"{path}-{count}";
        }

        return candidate;
    }

    /// <summary>
    /// The global save data at host start: the host's loaded save, plus what only the mod save holds. The host's save
    /// wins: the host may have played on alone since the mod save was written, and a mod save from an older version
    /// lacks values that sync now. (coop, 0.3.21) World objects (hostOnly) come from the host's save alone: its game
    /// stores every arriving one, and it never keeps a semi-persistent one (a killed Pilgrim), which a mod save did.
    /// </summary>
    /// <param name="hostSave">The global save data from the host's loaded save.</param>
    /// <param name="modSave">The global save data from the mod save.</param>
    /// <param name="hostOnly">Whether a value is the host's save's alone (a world object's).</param>
    public static Dictionary<ushort, byte[]> MergeGlobalSaveData(
        Dictionary<ushort, byte[]> hostSave,
        Dictionary<ushort, byte[]> modSave,
        Func<ushort, bool> hostOnly
    ) {
        var merged = new Dictionary<ushort, byte[]>(hostSave);
        foreach (var (index, value) in modSave) {
            if (hostOnly(index)) continue;
            merged.TryAdd(index, value);
        }

        return merged;
    }

    /// <summary>
    /// The message refusing a change of the item sharing setting, or null when the change is allowed.
    /// </summary>
    /// <param name="locked">Whether the hosted save has fixed its item sharing value.</param>
    /// <param name="current">The current item sharing value.</param>
    /// <param name="requested">The requested item sharing value.</param>
    public static string? LockRefusal(bool locked, bool current, bool requested) =>
        locked && current != requested
            ? $"Item sharing is fixed for this save ({(current ? "on" : "off")}). For a new save, change ItemSharing "
              + "in ssmp\\modsettings.json before hosting."
            : null;
}
