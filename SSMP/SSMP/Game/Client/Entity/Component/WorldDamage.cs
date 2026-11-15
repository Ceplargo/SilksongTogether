using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop, 0.3.23 K16) A world object's hit on an enemy (a hazard, another enemy's attack: BlockedHits.World) takes
/// health in the scene host's game only. Each game ran the hit on its own body: the scene host's game on the original,
/// a game that follows it on its copy, which sent its drop, and the scene host's game merged that drop as another
/// player's hit. So the enemy lost health twice for one hit: Coral_02's Coral Judge held at a Sand Centipede died
/// after 0.6 s in co-op, 1.17 s alone (the decision 2026-11-03 "Sand Centipede damage is an old fault", NOTES 195;
/// probe_env_hits). A copy now leaves such a hit's damage out; the scene host's drop comes to it with the hit's kind
/// and plays there as a hit (HealthManagerComponent's mirror). Players' hits are unchanged: each player's own hit takes
/// health in that player's game and is sent. Unity-free, so it is tested without the game.
/// </summary>
internal static class WorldDamage {
    /// <summary>Whether this game applies a hit's damage to an enemy: copy, this game's copy of an enemy the scene host's
    /// game runs (HealthManagerComponent.FollowsHost); world, a world object's hit (BlockedHits.World).</summary>
    public static bool Applies(bool copy, bool world) => !(copy && world && OnceAcrossGames());

    /// <summary>The switch env_hit_once's twin turns off (a skipped bool method returns false): each game applies it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool OnceAcrossGames() => true;
}
