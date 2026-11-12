using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossRule;

/// <summary>
/// (0.3.21) A bench bell struck in one game swings and rings in every game in the room. Its interactive_bell FSM runs
/// from the local hero's nail only (a Trigger2dEvent on the nail's collider) and nothing of it was sent, so the bell
/// rang in the striker's game alone (probe_bench_bell). The striker's game tells the room the bell and the strike's
/// direction (<see cref="BenchBellRule.Side"/>); every other game whose same bell waits in Idle takes that direction
/// and the strike's own event, so its Get Dir plays the strike's sound, effect and shake and swings it the same way.
/// Watched as <see cref="TrapFollow"/> watches terrain.
/// </summary>
internal static class BenchBells {
    public const string Prefix = "Bell:";
    private const string Fsm = "interactive_bell";
    private const float LookEvery = 0.25f;
    private const float LookFor = 3.0f;

    /// <summary>Sends a room state to the room's other players; set by the client add-on.</summary>
    public static Action<string, string>? Send;

    private static string? _scene;
    private static float _sceneAt;
    private static float _nextLook;
    private static readonly Dictionary<string, (PlayMakerFSM Fsm, string? Last)> Watched = new();

    /// <summary>Every frame: this room's bells, and whether this game's own player just struck one.</summary>
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
            if (!BenchBellRule.Struck(last, now)) continue;
            var side = BenchBellRule.Side(fsm.FsmVariables.GetFsmFloat("Atk Dir").Value,
                fsm.FsmVariables.GetFsmFloat("Hit Force").Value);
            Debug.Log($"[BossRule] Bell struck here: {path} {side} ({scene})");
            Send?.Invoke(scene, Prefix + path + "|" + side.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Another game's player struck this room's bell: this game's swings and rings the same way, if it waits.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void Echo(string word) {
        var bar = word.LastIndexOf('|');
        if (bar < 0 || !float.TryParse(word.Substring(bar + 1), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var side)) return;
        var path = word.Substring(0, bar);
        if (!Watched.TryGetValue(path, out var w) || w.Fsm == null) {
            Look();
            if (!Watched.TryGetValue(path, out w) || w.Fsm == null) return;
        }

        if (!BenchBellRule.Echoes(w.Fsm.ActiveStateName)) return;
        Debug.Log($"[BossRule] Bell struck by the room: {path} {side}");
        // Get Dir reads the hit's direction from the nail into Atk Dir; with no nail here it keeps this one (probe_bell_dir)
        w.Fsm.FsmVariables.GetFsmFloat("Atk Dir").Value = side;
        w.Fsm.SendEvent("NAIL HIT");
        // the strike runs through in this frame: Tick sees no strike of this game's own, so tells no one back
        Watched[path] = (w.Fsm, w.Fsm.ActiveStateName);
    }

    /// <summary>This room's scenes' bells (only the scenes', as <see cref="TrapFollow"/> looks).</summary>
    private static void Look() {
        for (var i = 0; i < SceneManager.sceneCount; i++) {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded || s.name == "DontDestroyOnLoad") continue;
            foreach (var root in s.GetRootGameObjects())
            foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true)) {
                if (fsm.FsmName != Fsm) continue;
                var path = PathOf(fsm.transform);
                if (!Watched.ContainsKey(path)) Watched[path] = (fsm, fsm.ActiveStateName);
            }
        }
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
