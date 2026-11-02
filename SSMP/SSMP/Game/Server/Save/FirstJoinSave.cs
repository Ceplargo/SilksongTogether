using System.Collections.Generic;

namespace SSMP.Game.Server.Save;

/// <summary>
/// Player data for a player that joins a hosted save for the first time. Kept free of Unity types so it can be
/// tested without the game.
/// </summary>
internal static class FirstJoinSave {
    /// <summary>
    /// A copy of the host's player data with the given values replaced, so the new player continues the host's game
    /// instead of starting a new one.
    /// </summary>
    /// <param name="hostData">The host's player data, mapped by save data index.</param>
    /// <param name="resets">Encoded values that replace the host's for the new player, mapped by save data index.
    /// Only indices that the host's data contains are replaced.</param>
    /// <returns>A new dictionary; the host's data is not changed.</returns>
    public static Dictionary<ushort, byte[]> FromHost(
        IReadOnlyDictionary<ushort, byte[]> hostData,
        IReadOnlyDictionary<ushort, byte[]> resets
    ) {
        var data = new Dictionary<ushort, byte[]>();
        foreach (var pair in hostData) {
            data[pair.Key] = resets.TryGetValue(pair.Key, out var reset) ? reset : pair.Value;
        }

        return data;
    }
}
