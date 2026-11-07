using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) What kind of hit made a health drop, sent with the change (HealthManagerComponent): its attack type, nail
/// element and hit effects type, or None for a drop no hit made (damage over time, a direct change). The game that plays
/// another game's drops as hits (B's mirror, and the scene host's MissedHits) plays that kind, or no hit for None: every
/// drop played as a nail hit, so a tool's hit or a lasting damage on a boss showed on the friend's screen with the
/// nail's sound and slash flash (boss round six's probe; bosses Task 11). Unity-free, so it is tested without the game.
/// </summary>
internal static class HitKind {
    /// <summary>A drop no hit made.</summary>
    public const int None = 0;

    private const int Seen = 1 << 24;

    /// <summary>A hit's kind: each part a byte (the game's enum values).</summary>
    public static int Pack(int attack, int element, int effects) =>
        Seen | attack & 0xFF | (element & 0xFF) << 8 | (effects & 0xFF) << 16;

    public static bool IsHit(int kind) => (kind & Seen) != 0;

    public static (int Attack, int Element, int Effects) Unpack(int kind) =>
        (kind & 0xFF, kind >> 8 & 0xFF, kind >> 16 & 0xFF);

    /// <summary>
    /// The kind a drop goes with: the hit noted in its frame or the frame before (its health is sent the frame after),
    /// else None (a hit that took no health, a blocked one, does not lend its kind to a later drop).
    /// </summary>
    public static int ForDrop(int kind, int notedFrame, int frame) => frame - notedFrame <= 1 ? kind : None;

    /// <summary>A drop is played as its own kind of hit. Not inlined: hit_kinds' twin skips it (every drop a nail hit).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Plays() => true;
}
