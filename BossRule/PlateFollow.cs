using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossRule;

/// <summary>
/// (0.4 world Task 7) A TempPressurePlate (Dock_15's steam door plate) goes down under the local hero and fires
/// Activated, which opens its TempGate, in that game only: the door opened in the presser's game alone, for either
/// presser (probe_t7_gates). The game whose plate went down tells the room at the end of its drop (PreActivate, its
/// Activate gateOpenDelay later); every other game there whose same plate is still up drops it the same way (its own
/// drop, PreActivate, then Activate), so its door opens too, at about the same moment. Each game's door then closes on
/// its own clock, kept open while its own hero is in range, as in the game alone.
/// </summary>
internal static class PlateFollow {
    public const string Prefix = "Plate:";

    /// <summary>Sends a room state to the room's other players; set by the client add-on.</summary>
    public static Action<string, string>? Send;

    // plates dropped by another game's word: their PreActivate, at the drop's end, tells no one back
    private static readonly HashSet<TempPressurePlate> Echoed = new();
    private static readonly MethodInfo Drop = AccessTools.Method(typeof(PressurePlateBase), "StartDrop");
    private static readonly AccessTools.FieldRef<TempPressurePlate, bool> Down =
        AccessTools.FieldRefAccess<TempPressurePlate, bool>("isActivated");

    /// <summary>Harmony postfix on TempPressurePlate.PreActivate: this game's own hero dropped it.</summary>
    public static void PreActivatePostfix(TempPressurePlate __instance) {
        var echoed = Echoed.Remove(__instance);
        if (GameManager.SilentInstance?.sceneName is not { } scene ||
            !LocalTraps.PlateTells(Down(__instance), echoed)) return;
        var path = PathOf(__instance.transform);
        Debug.Log($"[BossRule] Plate pressed here: {path} ({scene})");
        Send?.Invoke(scene, Prefix + path);
    }

    /// <summary>Another game's plate went down: this game's goes down too, if it is still up and not dropping (its
    /// collider goes off as its drop starts).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void Echo(string path) {
        var plate = Find(path);
        if (plate == null || Down(plate) || plate.GetComponent<Collider2D>() is { enabled: false }) return;
        Debug.Log($"[BossRule] Plate pressed by the room: {path}");
        Echoed.Add(plate);
        Drop.Invoke(plate, [true]);
    }

    private static TempPressurePlate? Find(string path) {
        for (var i = 0; i < SceneManager.sceneCount; i++) {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded || s.name == "DontDestroyOnLoad") continue;
            foreach (var root in s.GetRootGameObjects())
            foreach (var p in root.GetComponentsInChildren<TempPressurePlate>(true))
                if (PathOf(p.transform) == path) return p;
        }

        return null;
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
