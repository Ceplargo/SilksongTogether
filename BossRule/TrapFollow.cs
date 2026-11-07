using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossRule;

/// <summary>
/// (0.3.20) Terrain a player sets off in their own game only (<see cref="LocalTraps"/>): crumbling platforms, chain-drop
/// platforms, collapsing floors, walk-triggered boulders, explode walls and metal deposits run their own FSMs from the
/// local hero's touch or hit, and nothing of them is synced, so a friend's crumble or boulder happened in the friend's
/// game only. The game whose own player sets one off tells the room (any room, not only the boss catalog's); every
/// other game there whose same object still waits sends it the same event, so the break follows whoever set it off,
/// as the Bell Beast's vines now do (<see cref="FreeStarts"/>).
/// </summary>
internal static class TrapFollow {
    public const string Prefix = "Trap:";
    // a room's objects come up over its first moments (an additive scene's later): looked for every LookEvery until
    // LookFor. A first look at 1 s missed a platform the friend stood on 0.7 s after coming in (0.3.20's local_traps)
    private const float LookEvery = 0.25f;
    private const float LookFor = 3.0f;

    /// <summary>Sends a room state to the room's other players; set by the client add-on.</summary>
    public static Action<string, string>? Send;

    private static string? _scene;
    private static float _sceneAt;
    private static float _nextLook;
    private static readonly Dictionary<string, (PlayMakerFSM Fsm, string? Last)> Watched = new();

    /// <summary>Every frame: this room's local-trigger terrain, and whether this game's own player just set one off.</summary>
    public static void Tick() {
        var scene = GameManager.SilentInstance?.sceneName;
        if (scene != _scene) {
            _scene = scene;
            _sceneAt = Time.unscaledTime;
            _nextLook = 0f;
            Watched.Clear();
        }

        if (scene == null) return;
        if (Time.unscaledTime >= _nextLook && Time.unscaledTime <= _sceneAt + LookFor) {
            _nextLook = Time.unscaledTime + LookEvery;
            Look();
        }

        foreach (var path in Watched.Keys.ToList()) {
            var (fsm, last) = Watched[path];
            if (fsm == null) {
                Watched.Remove(path);
                continue;
            }

            var now = fsm.ActiveStateName;
            if (now == last) continue;
            Watched[path] = (fsm, now);
            if (LocalTraps.Sets(fsm.FsmName, fsm.gameObject.name, last, now) is not { } ev) continue;
            Debug.Log($"[BossRule] Trap set off here: {path} {ev} ({scene})");
            Send?.Invoke(scene, Prefix + path + "|" + ev);
        }
    }

    /// <summary>Another game's player set off this room's object: this game's sets it off too, if it still waits.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void Echo(string word) {
        var bar = word.LastIndexOf('|');
        if (bar < 0) return;
        var (path, ev) = (word.Substring(0, bar), word.Substring(bar + 1));
        if (!Watched.TryGetValue(path, out var w) || w.Fsm == null) {
            Look();
            if (!Watched.TryGetValue(path, out w) || w.Fsm == null) return;
        }

        if (!LocalTraps.Echoes(w.Fsm.FsmName, w.Fsm.gameObject.name, w.Fsm.ActiveStateName)) return;
        Debug.Log($"[BossRule] Trap set off by the room: {path} {ev}");
        w.Fsm.SendEvent(ev);
        // the event changes its state at once: Tick sees no setting off of this game's own, so tells no one back
        Watched[path] = (w.Fsm, w.Fsm.ActiveStateName);
    }

    /// <summary>This room's scenes' terrain FSMs (only the scenes': a scan of every loaded object grew with the pool).</summary>
    private static void Look() {
        for (var i = 0; i < SceneManager.sceneCount; i++) {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded || s.name == "DontDestroyOnLoad") continue;
            foreach (var root in s.GetRootGameObjects())
            foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true)) {
                if (!LocalTraps.Kinds.Any(k => k.Fsm == fsm.FsmName &&
                                               fsm.gameObject.name.StartsWith(k.Name, StringComparison.Ordinal))) continue;
                var path = PathOf(fsm.transform);
                if (!Watched.ContainsKey(path)) Watched[path] = (fsm, fsm.ActiveStateName);
            }
        }
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
