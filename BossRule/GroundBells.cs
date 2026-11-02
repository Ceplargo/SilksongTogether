using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BossRule;

/// <summary>
/// The Widow's ground bells (Belltown_Shrine: Ground Bells/Holder 1 to 4) rise where and when the scene host's do. Each
/// holder's FSM waits in State 1 for anything staying in its trigger (Trigger2dEvent, OnTriggerStay2D), then sends
/// ENTER and brings its spikes out: she sets them off as she crosses them. In the other games SSMP's copy of her crossed
/// them on its own path, a little off hers, so a holder there missed its rise or rose where the scene host's had not
/// (rehearsal round 2, 2026-10-31: one missed, and two up with their spikes for 1.6 s where her copy's dash and charge
/// ended). In a game that follows the scene host a holder's own trigger does nothing, and each holder rises as the scene
/// host's rises (ENTER, through the arena follow message); the scene host's game, and one played alone, keep theirs.
/// </summary>
internal static class GroundBells {
    public const string Prefix = "Ground Bell: ";
    private const string Scene = "Belltown_Shrine";
    private const string Waiting = "State 1";
    private static readonly RiseWatch Watch = new(Waiting, "State 2");

    /// <summary>Sends a holder's rise to the room's other players; set by the client add-on.</summary>
    public static Action<string>? Send;

    /// <summary>Whether this game is in a session and not the scene host; set by the client add-on.</summary>
    public static Func<bool>? NotSceneHost;

    private static List<(PlayMakerFSM Fsm, string Path)> _holders = [];
    private static string? _room;
    private static float _nextLook;

    /// <summary>Once per frame in her room: the scene host's game tells the others each of its holders that rose.</summary>
    public static void Tick() {
        var scene = GameManager.SilentInstance?.sceneName;
        if (scene != _room) {
            _room = scene;
            _holders = [];
            Watch.Clear();
        }

        // the room's objects load a moment after its name changes
        if (scene == Scene && _holders.Count == 0 && Time.unscaledTime >= _nextLook) {
            _nextLook = Time.unscaledTime + 0.5f;
            _holders = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(f => f.FsmName == "FSM" && f.transform.parent is { name: "Ground Bells" })
                .Select(f => (f, PathOf(f.transform))).ToList();
        }

        if (_holders.Count == 0) return;
        var follows = Follows();
        foreach (var (fsm, path) in _holders) {
            if (fsm != null && Watch.Rose(path, fsm.ActiveStateName) && !follows) Send?.Invoke(Prefix + path);
        }
    }

    /// <summary>
    /// Harmony prefix of Trigger2dEvent.DoTriggerStay2D: a holder's own trigger does nothing in a game that follows the
    /// scene host (her attack boxes set it off as they cross it, and there SSMP's copy's crossed on their own path).
    /// </summary>
    public static bool TriggerStayPrefix(Trigger2dEvent __instance) {
        if (_holders.Count == 0 || !Follows()) return true;
        foreach (var (fsm, _) in _holders) {
            if (fsm != null && fsm.Fsm == __instance.Fsm) return false;
        }

        return true;
    }

    /// <summary>Whether this game's holders follow the scene host's.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static bool Follows() => NotSceneHost?.Invoke() == true;

    /// <summary>The scene host's holder of that path rose: this game's rises too, if it is out and waits.</summary>
    public static void Echo(string path) {
        if (!Follows()) return;
        foreach (var (fsm, holder) in _holders) {
            if (holder == path && fsm != null && fsm.gameObject.activeInHierarchy && fsm.ActiveStateName == Waiting) {
                fsm.SendEvent("ENTER");
            }
        }
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
