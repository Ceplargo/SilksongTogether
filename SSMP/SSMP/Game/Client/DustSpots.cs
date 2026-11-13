using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client;

/// <summary>
/// (coop, 0.3.21) Unity-free: a dusty platform's dirt fall at another player's copy comes from that player's game
/// (DustLand), not from the copy's place. Bone_08's left3 walk (run 2026-11-10_185527-world-pair3) played the dirt fall
/// in the friend's game only, 0.19 s before the walking host's hero landed below: the copy's box, its place smoothed
/// over the network, came within TouchLanding.Tolerance of the platform's top where her own game played none. Her game
/// sends the place of the platform it played the dirt fall for; the other game finds its own platform there.
/// </summary>
internal static class DustSpots {
    /// <summary>How far (units) a platform may be from the sent place: the same platform in each game.</summary>
    internal const float Within = 0.5f;

    /// <summary>The effect info for a platform at (x, y).</summary>
    internal static byte[] Info(float x, float y) {
        var info = new byte[8];
        BitConverter.GetBytes(x).CopyTo(info, 0);
        BitConverter.GetBytes(y).CopyTo(info, 4);
        return info;
    }

    /// <summary>The place an effect info holds, or null for a wrong one.</summary>
    internal static (float x, float y)? Read(byte[]? info) =>
        info is { Length: 8 } ? (BitConverter.ToSingle(info, 0), BitConverter.ToSingle(info, 4)) : null;

    /// <summary>
    /// (0.3.22) Whether this game sends a dirt fall it just played: its own hero landed, or a live enemy did. An enemy
    /// lands only in the game that runs it (another game's copy meets no platform: Bone_06's Bone Circler (1),
    /// probe_plat_dust 2026-11-12_181024 and later, the dirt in the host's game only). A corpse, a geo piece or debris
    /// falls by each game's own physics, so its dirt is each game's own (the project's ruling, 2026-11-12).
    /// </summary>
    internal static bool Sends(bool hero, bool liveEnemy) => hero || liveEnemy && SendsEnemyLandings();

    /// <summary>The switch enemy_dust's twin turns off (a skipped bool method returns false): no enemy's landing goes.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool SendsEnemyLandings() => true;

    /// <summary>The index of the spot nearest (x, y) within Within, or -1.</summary>
    internal static int Nearest(IReadOnlyList<(float x, float y)> spots, float x, float y) {
        var best = -1;
        var bestSq = Within * Within;
        for (var i = 0; i < spots.Count; i++) {
            var dx = spots[i].x - x;
            var dy = spots[i].y - y;
            var sq = dx * dx + dy * dy;
            if (sq > bestSq) continue;
            best = i;
            bestSq = sq;
        }

        return best;
    }
}
