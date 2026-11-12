using System;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client;

/// <summary>
/// (coop, 0.3.21) A hero's item-get reaction (CollectableItemHeroReaction: its flash, Item Get Effect R and the item
/// sound at the hero) at its copy in the other games: the game whose hero reacts sends it as a player effect, as
/// HeartGetUp does, and the copies play the same effect at that player (the project 2026-11-10 18:07: what
/// shows, shows on both screens). The effect info is the small flag and the effect's offset from the hero in
/// hundredths of a unit. Unity-free, so it is tested without the game.
/// </summary>
internal static class ItemGetReaction {
    /// <summary>The effect info of a reaction, or null when its offset does not fit (over 327 units).</summary>
    public static byte[]? Info(bool small, float x, float y) {
        if (System.Math.Abs(x) > 327f || System.Math.Abs(y) > 327f) return null;
        var (sx, sy) = ((short) System.Math.Round(x * 100f), (short) System.Math.Round(y * 100f));
        return [small ? (byte) 1 : (byte) 0, (byte) sx, (byte) (sx >> 8), (byte) sy, (byte) (sy >> 8)];
    }

    /// <summary>The small flag and offset of an effect info, or null when it is not one.</summary>
    public static (bool small, float x, float y)? Read(byte[]? info) =>
        info is { Length: 5 }
            ? (info[0] != 0, (short) (info[1] | info[2] << 8) / 100f, (short) (info[3] | info[4] << 8) / 100f)
            : null;

    /// <summary>Whether a copy plays it. Not inlined: the twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Plays() => true;
}
