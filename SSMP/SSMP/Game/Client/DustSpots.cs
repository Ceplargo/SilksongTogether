using System;
using System.Collections.Generic;

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
