using SSMP.Game.Client;
using SSMP.Internals;
using UnityEngine;

namespace SSMP.Animation.Effects;

/// <summary>
/// (coop, 0.3.21) A hero's nail spark on a wall, sent by her game (AnimationManager's handler of the game's
/// NailSlashTerrainThunk.AnyThunked) with the spark's place and turn: the game's "Nail Terrain Hit Effect" spawns there in
/// this game too (<see cref="ThunkSpark"/>). The wall is the same in every game, so the place is the sent one, not the
/// copy's.
/// </summary>
internal class NailWallSpark : AnimationEffect {
    /// <inheritdoc/>
    public override byte[]? GetEffectInfo() => null;

    /// <inheritdoc/>
    public override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo) {
        if (!ThunkSpark.ShowsAtCopy() || ThunkSpark.Read(effectInfo) is not var (x, y, angle)) return;
        var spark = global::GlobalSettings.Effects.EnemyNailTerrainThunk;
        if (spark) spark.Spawn(new Vector3(x, y, 0f), Quaternion.Euler(0f, 0f, angle));
    }
}
