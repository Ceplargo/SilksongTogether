namespace SSMP.Game.Client;

/// <summary>
/// (coop, 0.3.21) Unity-free: the spark a nail makes on a wall (the game's "Nail Terrain Hit Effect", which
/// TerrainThunkUtils spawns from the hero's NailSlashTerrainThunk), sent by the swinging player's game with its place and
/// turn (NailWallSpark) and spawned there in the other games. SSMP skips that handler for a copy's slash, and a copy's
/// slash never met the wall in the other game, so the spark showed in the swinger's game only: Bone_04's Rock Roller
/// row, "spawned in the host's game only: Nail Terrain Hit Effect"; copy_wall_spark on pair 5, 2026-11-10 21:51.
/// </summary>
internal static class ThunkSpark {
    /// <summary>The effect info for a spark at (x, y) turned angle degrees.</summary>
    internal static byte[] Info(float x, float y, float angle) {
        var info = new byte[9];
        System.BitConverter.GetBytes(x).CopyTo(info, 0);
        System.BitConverter.GetBytes(y).CopyTo(info, 4);
        info[8] = (byte) ((int) System.Math.Round((angle % 360f + 360f) % 360f / 360f * 256f) % 256);
        return info;
    }

    /// <summary>The place and turn an effect info holds, or null for a wrong one.</summary>
    internal static (float x, float y, float angle)? Read(byte[]? info) =>
        info is { Length: 9 }
            ? (System.BitConverter.ToSingle(info, 0), System.BitConverter.ToSingle(info, 4), info[8] * 360f / 256f)
            : null;

    /// <summary>Not inlined: the copy_wall_spark check's twin skips it (no spark at a copy, as before).</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool ShowsAtCopy() => true;
}
