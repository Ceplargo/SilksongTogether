using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) An enemy's block of a world object's hit (an enemy's shot, a blast), shown at its copies too. The game shows
/// a block where the hit lands (HealthManager.Invincible: its block flash and its invincible sound, at most one each
/// 0.1 s by its tinkTimer), and such an object's hit lands in the scene host's game only, so its copies showed none
/// (Bone_09's Dock Flyer blocking a DF Lava Rock: sword_hit_reject in the scene host's game, none in the other,
/// 2026-11-11_095325-enemies-pair4). The scene host's game sends each block of a world object's hit it shows, with
/// where its flash went (a BlockedHit entry), and each copy shows its own enemy's block there; a copy shows no block of
/// its own for a world object's hit, so one that lands there too does not show it twice. (0.3.22) A player's block is
/// the hitter's game's: it sends the block of its own hit it shows, and the others show none of their own for it
/// (Bone_06's Rock Roller starting to roll clinked on the hitter's screen only). Unity-free, so it is tested without the game.
/// </summary>
internal static class BlockedHits {
    /// <summary>Whether a hit is a world object's: neither this game's hero's (local) nor another player's (remotePlayer),
    /// and not a hero's damage (heroDamage, HitInstance.IsHeroDamage: a hero's thrown tool, whose root is no player).</summary>
    public static bool World(bool local, bool remotePlayer, bool heroDamage) => !local && !remotePlayer && !heroDamage;

    /// <summary>
    /// Whether the scene host's game sends its enemy's block of this hit: it runs the enemy's hits (hostsHits), the hit is
    /// a world object's, the enemy blocked it with its effect on (blocked: Hit answered Invincible, and
    /// PreventInvincibleEffect is off), and the block showed now: its tinkTimer had run out before the hit (tinkBefore)
    /// and the block set it (tinkAfter).
    /// </summary>
    public static bool Sends(bool hostsHits, bool world, bool blocked, float tinkBefore, float tinkAfter) =>
        hostsHits && world && blocked && tinkBefore <= 0f && tinkAfter > 0f && SendsWorldBlocks();

    /// <summary>Whether this game's copy (copy: of an enemy the scene host's game runs) shows no block of its own for this
    /// hit: a world object's, whose block the scene host's entry shows.</summary>
    public static bool LeavesOut(bool copy, bool world) => copy && world && LeavesOutCopyBlocks();

    /// <summary>
    /// Where the game puts its block flash and how it turns it (HealthManager.Invincible), by the hit's cardinal way
    /// (0 rightward, 1 up, 2 leftward, 3 down): on the side of the enemy's box the hit came from, at the source's height;
    /// from below or above, at the source's x (from above, within the box); with no box (box false), at the enemy. x, y:
    /// the enemy's place; offsetX to height: its box; sourceX, sourceY: the hit's source's place.
    /// </summary>
    public static (float X, float Y, float Angle) Flash(int direction, float x, float y, bool box, float offsetX,
        float offsetY, float width, float height, float sourceX, float sourceY) {
        if (!box) return (x, y, 0f);
        var left = x + offsetX - width * 0.5f;
        var right = x + offsetX + width * 0.5f;
        return direction switch {
            0 => (left, sourceY, 0f),
            2 => (right, sourceY, 180f),
            1 => (sourceX, System.Math.Max(sourceY, y + offsetY - height * 0.5f), 90f),
            3 => (sourceX < left ? left : sourceX > right ? right : sourceX,
                System.Math.Min(sourceY, y + offsetY + height * 0.5f), 270f),
            _ => (x, y, 0f)
        };
    }

    /// <summary>
    /// (0.3.22) Whether this game sends its enemy's block of its own hero's hit (local), in any game, the scene host's or a
    /// copy's: the hitter's game decides a player's block, as it does the hit's damage, and the block showed now (blocked,
    /// tinkBefore, tinkAfter: as Sends).
    /// </summary>
    public static bool SendsPlayer(bool local, bool blocked, float tinkBefore, float tinkAfter) =>
        local && blocked && tinkBefore <= 0f && tinkAfter > 0f && SendsPlayerBlocks();

    /// <summary>(0.3.22) Whether this game shows no block of its own for another player's hit (remotePlayer): the
    /// hitter's game sends the block it shows.</summary>
    public static bool LeavesOutPlayer(bool remotePlayer) => remotePlayer;

    /// <summary>The switch blocked_hit_copy's twin turns off (a skipped bool method returns false): no block sent.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SendsWorldBlocks() => true;

    /// <summary>The switch blocked_hit_copy's second twin turns off (probe_blocked_hit_own_twin): a copy shows its own
    /// block of a world object's hit too.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool LeavesOutCopyBlocks() => true;

    /// <summary>The switch player_blocks' twin turns off: no player's block sent (each game left with none of its own
    /// for another player's hit).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SendsPlayerBlocks() => true;
}
