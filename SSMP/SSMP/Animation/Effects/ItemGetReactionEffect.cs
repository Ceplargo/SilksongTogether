using SSMP.Game.Client;
using SSMP.Internals;
using UnityEngine;

namespace SSMP.Animation.Effects;

/// <summary>
/// (coop) A hero's item-get reaction at its copy in the other games (<see cref="ItemGetReaction"/>): as the game's own
/// CollectableItemHeroReaction does at its hero, the copy's flash, Item Get Effect R (or its small one) spawned at the
/// copy with the sent offset, turned at random, and the item sound there.
/// </summary>
internal class ItemGetReactionEffect : AnimationEffect {
    private static readonly System.Reflection.BindingFlags Private =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

    /// <inheritdoc/>
    public override byte[]? GetEffectInfo() => null;

    /// <inheritdoc/>
    public override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo) {
        if (!ItemGetReaction.Plays() || ItemGetReaction.Read(effectInfo) is not var (small, x, y)) return;
        var reaction = Object.FindAnyObjectByType<CollectableItemHeroReaction>();
        if (reaction == null) return;
        if (playerObject.GetComponent<SpriteFlash>() is { } flash) flash.flashFocusHeal();
        var at = (Vector2) playerObject.transform.position + new Vector2(x, y);
        var prefab = typeof(CollectableItemHeroReaction).GetField(small ? "itemGetEffectPrefabSmall" : "itemGetEffectPrefab", Private)
            ?.GetValue(reaction) as GameObject;
        if (prefab != null) {
            prefab.Spawn(at.ToVector3(prefab.transform.position.z)).transform.SetRotation2D(Random.Range(0f, 360f));
        }

        if (typeof(CollectableItemHeroReaction).GetField("itemGetSound", Private)?.GetValue(reaction) is AudioEvent sound) {
            sound.SpawnAndPlayOneShot(at);
        }
    }
}
