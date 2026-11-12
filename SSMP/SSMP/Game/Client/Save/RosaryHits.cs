using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (coop, 0.3.21) Another player's hits on a live RosaryCache (a rosary string), owed here when its saved value arrives
/// (<see cref="PersistentApply.ForCache"/>). The copy of their swing hits it as it does the game alone's; the value can
/// come first, and loading it hid the beads with no fling. So the copy's swing gets a short wait, and the hits still owed
/// after it are played as the game's own hit plays them (RosaryCache.Hit: each its fling, sound and shake). Its shakes
/// keep to the owner's view rule as a remote hit's do (<see cref="RemoteShakes"/>).
/// </summary>
internal static class RosaryHits {
    /// <summary>
    /// shortcut: a fixed wait for the copy's swing (it came 10 to 20 ms after the value in run 2026-11-10_023548); a
    /// swing later than this hits once more, one more fling; send the swing's hit with the value if that is ever seen.
    /// </summary>
    private const float WaitS = 0.5f;

    private static readonly AccessTools.FieldRef<RosaryCache, PersistentIntItem> Persistent =
        AccessTools.FieldRefAccess<RosaryCache, PersistentIntItem>("persistent");

    private static readonly System.Reflection.MethodInfo StateGetter =
        AccessTools.PropertyGetter(typeof(RosaryCache), "State");

    /// <summary>The scene's RosaryCache that saves itself in item, or null.</summary>
    public static RosaryCache? CacheOf(PersistentIntItem item) {
        foreach (var cache in Object.FindObjectsByType<RosaryCache>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if (cache.gameObject.scene == item.gameObject.scene && Persistent(cache) == item) return cache;
        }

        return null;
    }

    /// <summary>Its hits taken so far (State, what its PersistentIntItem saves).</summary>
    public static int State(RosaryCache cache) => (int) StateGetter.Invoke(cache, null);

    /// <summary>Its hits up to value, played after the wait unless the copy's swing (or this game's) took them.</summary>
    public static void Owe(RosaryCache cache, int value) => cache.StartCoroutine(Play(cache, value));

    private static IEnumerator Play(RosaryCache cache, int value) {
        yield return new WaitForSeconds(WaitS);
        RemoteShakes.Begin();
        try {
            // shortcut: struck from its own place (its beads fly one way), not the hitter's side, as HolderHits does
            for (var state = State(cache); state < value; state = State(cache)) {
                cache.Hit(new HitInstance {
                    Source = cache.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1, Multiplier = 1f
                });
                if (State(cache) == state) break;
            }
        } finally {
            RemoteShakes.End();
        }
    }
}
