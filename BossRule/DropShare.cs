using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (0.3.21, the project's ruling) One kill gives one item in every game, as in the game alone. An enemy's saved
/// item drop comes out in each game, from the enemy's death there or its copy's, and SSMP sends no save of it (its saved
/// bool is not in SSMP's synced values), so each player could take their own copy: two items for one kill, the item
/// counts adding up (Collectables sync by difference). Two ways make such a drop: the enemy's death FSM (Bone_04's Rock
/// Roller: SetCollectablePickupItemV2 keeps the spawned pickup's saved bool, its id "Rock Roller_item_Rock Rollers"), and
/// PersistentEnemyItemDrop.DropItem (its pickup's saved bool goes; the drop's id is its droppedItemID, saved on take).
/// A spawned pickup with such an id: its take goes to the server (<see cref="DropTakes"/>); the first counts, every
/// other game takes its copy away and keeps the drop's save, as the game alone does after a take; a take that came
/// second gives its item back here. Spawned pickups without one (random enemy drops, breakables) are each game's own.
/// </summary>
internal static class DropShare {
    /// <summary>Sends this game's take of a drop (scene, id) to the server; null while not connected.</summary>
    public static Action<string, string>? Send;

    // the spawned pickups out, each with its drop id once DropItem named it (a death FSM's keeps its saved bool's)
    private static readonly Dictionary<CollectableItemPickup, string?> Spawned = [];

    // the item of each drop this game took (scene/id): its pickup is gone by the time a lost take comes back
    private static readonly Dictionary<string, SavedItem?> Taken = [];

    /// <summary>Whether drops are shared. Not inlined: drop_once's twin switches it off with a patch.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Shares() => true;

    /// <summary>After a pickup is given its item: a spawned one is watched for its take.</summary>
    public static void SetItemPostfix(CollectableItemPickup __instance) {
        try {
            if (Spawned.ContainsKey(__instance)) {  // a pooled pickup out again: its id is told anew
                Spawned[__instance] = null;
                return;
            }
            if (!__instance.name.EndsWith("(Clone)")) return;  // placed in the room: SSMP keeps its save
            Spawned.Keys.Where(p => !p).ToList().ForEach(p => Spawned.Remove(p));
            Spawned[__instance] = null;
            var pickup = __instance;
            pickup.OnPickup.AddListener(() => Took(pickup));
        } catch (Exception e) {
            Debug.LogWarning($"[BossRule] A spawned pickup is not watched: {e.GetBaseException().Message}");
        }
    }

    /// <summary>After PersistentEnemyItemDrop dropped its item: the new pickup's drop id is its droppedItemID.</summary>
    public static void DropItemPostfix(PersistentEnemyItemDrop __instance) {
        var id = Traverse.Create(__instance).Field("droppedItemID").GetValue<string>();
        foreach (var pickup in Spawned.Keys.Where(p => p && Spawned[p] == null && !Saved(p)).ToList()) {
            Spawned[pickup] = id;
        }
    }

    private static bool Saved(CollectableItemPickup pickup) =>
        pickup.GetComponent<PersistentBoolItem>() is { } saved && !string.IsNullOrEmpty(saved.ItemData.ID);

    /// <summary>The drop's id: its saved bool's (a death FSM's), or the droppedItemID DropItem gave it; else null.</summary>
    private static string? IdOf(CollectableItemPickup pickup) =>
        pickup.GetComponent<PersistentBoolItem>() is { } saved && !string.IsNullOrEmpty(saved.ItemData.ID)
            ? saved.ItemData.ID
            : Spawned.TryGetValue(pickup, out var id) ? id : null;

    private static void Took(CollectableItemPickup pickup) {
        if (!Shares() || Send == null || IdOf(pickup) is not { } id) return;
        var scene = pickup.gameObject.scene.name;
        Taken[scene + "/" + id] = Traverse.Create(pickup).Field("item").GetValue<SavedItem>();
        Debug.Log($"[BossRule] Took the drop {id} in {scene}");
        Send(scene, id);
    }

    /// <summary>A take from the server: another game's first take (its copy here goes, its save kept), or this game's
    /// take that came second (its item given back).</summary>
    public static void Received(DropTaken packet) {
        if (!Shares()) return;
        if (packet.Lost) {
            Taken.TryGetValue(packet.Scene + "/" + packet.Id, out var item);
            var counted = Counted(item);
            foreach (var collectable in counted) collectable.Take(1, false);
            if (item is FullQuestBase quest) {  // its Get counted the quest once more too; SSMP adds CompletedCount
                var quests = PlayerData.instance.QuestCompletionData;
                var completion = quests.GetData(quest.name);
                completion.CompletedCount = Math.Max(0, completion.CompletedCount - 1);
                quests.SetData(quest.name, completion);
                QuestManager.IncrementVersion();
            }
            if (counted.Count > 0 || item is FullQuestBase) {
                Debug.Log($"[BossRule] Another game took the drop {packet.Id} first: {item} given back here " +
                          $"({string.Join(", ", counted.Select(c => c.name))})");
            } else {
                Debug.LogWarning($"[BossRule] Another game took the drop {packet.Id} first; {item} is kept (not a count)");
            }
            return;
        }

        SceneData.instance.PersistentBools.SetValue(new PersistentItemData<bool> {
            ID = packet.Id, SceneName = packet.Scene, Value = true
        });
        foreach (var pickup in Spawned.Keys.Where(p => p && p.gameObject.scene.name == packet.Scene && IdOf(p) == packet.Id)
                     .ToList()) {
            Spawned.Remove(pickup);
            pickup.gameObject.Recycle();  // back to the pool it came from, or destroyed
            Debug.Log($"[BossRule] Another game took the drop {packet.Id}: its copy here goes");
        }
    }

    /// <summary>The counted items a take added: a collectable itself, or a quest's target counters (its Get adds one to
    /// each, and one to its own CompletedCount, taken back above).</summary>
    private static List<CollectableItem> Counted(SavedItem? item) => item switch {
        CollectableItem collectable => [collectable],
        FullQuestBase quest => quest.Targets.Select(t => t.Counter).OfType<CollectableItem>().ToList(),
        _ => []
    };

    /// <summary>A lost connection: the drops left out stay this game's.</summary>
    public static void Reset() {
        Spawned.Clear();
        Taken.Clear();
    }
}
