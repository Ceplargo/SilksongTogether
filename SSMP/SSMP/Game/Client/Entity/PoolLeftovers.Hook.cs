using System;
using System.Reflection;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Logger = SSMP.Logging.Logger;
using Object = UnityEngine.Object;

namespace SSMP.Game.Client.Entity;

/// <summary>(coop, 0.4 enemies) The hook on ObjectPool.Recycle(obj, prefab) (PoolLeftovers.cs).</summary>
internal static partial class PoolLeftovers {
    private static Hook? _recycleHook;

    /// <summary>Hook the recycle; a hook that fails (a game update renamed it) is logged and recycles stay the game's.</summary>
    public static void Register() {
        try {
            _recycleHook ??= new Hook(
                typeof(ObjectPool).GetMethod(
                    nameof(ObjectPool.Recycle), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
                    [typeof(GameObject), typeof(GameObject)], null
                )!,
                Recycle
            );
        } catch (Exception e) {
            Logger.Error($"Could not hook ObjectPool.Recycle, an object whose room unloaded may stay out:\n{e}");
        }
    }

    /// <summary>Take the hook off.</summary>
    public static void Deregister() {
        _recycleHook?.Dispose();
        _recycleHook = null;
    }

    // every recycle in every room comes here: two null tests, nothing that allocates (the project)
    private static void Recycle(Action<GameObject, GameObject> orig, GameObject obj, GameObject prefab) {
        if (!Ends(obj != null, prefab != null)) {
            orig(obj, prefab);
            return;
        }

        var pool = ObjectPool.instance;
        if (pool != null) pool.spawnedObjects.Remove(obj);
        Object.Destroy(obj);
    }
}
