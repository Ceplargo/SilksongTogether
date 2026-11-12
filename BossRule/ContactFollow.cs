using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossRule;

/// <summary>
/// (0.3.21) A room's ActivateChildrenOnContact switches its children on when any body enters its box, in that game
/// only: Bone_10's Bone Crawler came out in the game of the player who walked in, the other player's showing none
/// (probe_reveals, c-reveals-p2-2.log). The game where it went off tells the room; every other game there whose same
/// box has not gone off sets it off with its own hero, so its children come out there too.
/// </summary>
internal static class ContactFollow {
    public const string Prefix = "Contact:";

    /// <summary>Sends a room state to the room's other players; set by the client add-on.</summary>
    public static Action<string, string>? Send;

    private static bool _echoing;
    private static readonly MethodInfo Enter = AccessTools.Method(typeof(ActivateChildrenOnContact), "OnTriggerEnter2D");
    private static readonly AccessTools.FieldRef<ActivateChildrenOnContact, bool> Activated =
        AccessTools.FieldRefAccess<ActivateChildrenOnContact, bool>("activated");

    /// <summary>Harmony postfix on ActivateChildrenOnContact.OnTriggerEnter2D: this game's went off.</summary>
    public static void EnterPostfix(ActivateChildrenOnContact __instance) {
        var scene = GameManager.SilentInstance?.sceneName;
        if (_echoing || scene == null) return;
        Send?.Invoke(scene, Prefix + PathOf(__instance.transform));
    }

    /// <summary>Another game's went off: this game's goes off too, with its own hero, if it has not.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void Echo(string path) {
        var contact = Find(path);
        if (contact == null || Activated(contact)) return;
        if (HeroController.SilentInstance?.GetComponent<Collider2D>() is not { } hero) return;
        Debug.Log($"[BossRule] Contact by the room: {path}");
        _echoing = true;
        try {
            Enter.Invoke(contact, [hero]);
        } finally {
            _echoing = false;
        }
    }

    private static ActivateChildrenOnContact? Find(string path) {
        for (var i = 0; i < SceneManager.sceneCount; i++) {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded || s.name == "DontDestroyOnLoad") continue;
            foreach (var root in s.GetRootGameObjects())
            foreach (var c in root.GetComponentsInChildren<ActivateChildrenOnContact>(true))
                if (PathOf(c.transform) == path) return c;
        }

        return null;
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
