using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossRule;

/// <summary>
/// (optimization fix J) Every component of a type in the loaded room scenes, for the timed looks of CutsceneFollower,
/// FreeStarts, TollFollow and FightFollow (HiddenAreas' masks list themselves since fix C). FindObjectsByType walks every object the game holds, and late in a
/// long session the game's own effect pool (GlobalPool, in DontDestroyOnLoad) holds tens of thousands: each look took 25 to
/// 75 ms, and together they froze both games about every 0.5 s in plan2's boss block (mini PC job 62: 119 freezes of
/// 0.1 s or more in her 40 s fight, 1 with the four looks off). The rooms' own objects are all those looks want.
/// shortcut: DontDestroyOnLoad's objects (the hero, the pool) are not looked in; a catalog entry that names one would
/// need them added. FromRooms skipped gives FindObjectsByType, as before.
/// </summary>
internal static class RoomScan {
    public static List<T> All<T>(bool inactive = false) where T : Component {
        if (!FromRooms()) {
            return [..Object.FindObjectsByType<T>(inactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)];
        }

        var found = new List<T>();
        var each = new List<T>();
        for (var i = 0; i < SceneManager.sceneCount; i++) {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects()) {
                root.GetComponentsInChildren(inactive, each);
                found.AddRange(each);
            }
        }

        return found;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool FromRooms() => true;
}
