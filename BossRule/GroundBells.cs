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

    /// <summary>A following game's holders the scene host's rise was applied to, until this game sees them rise.
    /// </summary>
    private static readonly HashSet<string> Applied = new();

    // a rise that comes before its holder is out and waiting, kept 0.5 s: in the gates her Charge brought the friend's
    // holders out at most 0.036 s after the scene host's rose; her Charges come seconds apart, so a kept rise never
    // reaches the next one
    private const float KeepS = 0.5f;
    private static readonly EarlyRises Early = new(KeepS);

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
            Applied.Clear();
            Early.Clear();
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
            if (fsm == null) continue;
            if (follows && fsm.gameObject.activeInHierarchy && fsm.ActiveStateName == Waiting &&
                Early.Due(path, Time.unscaledTime)) {
                fsm.SendEvent("ENTER");
                Applied.Add(path);
                Debug.Log($"[BossRule] Ground Bell rise applied as it came out: {path}");
            }

            if (!Watch.Rose(path, fsm.ActiveStateName, CountsQuickRises())) continue;
            if (!follows) {
                Debug.Log($"[BossRule] Ground Bell rise sent: {path}");
                Send?.Invoke(Prefix + path);
                continue;
            }

            // print only (a gate's friend's bell up 0.69 s after the host's, cause not proven): how this one came up
            var how = Applied.Remove(path) ? "the scene host's rise applied" : "no rise of the scene host's applied";
            Debug.Log($"[BossRule] Ground Bell rose here: {path} ({how})");
        }

        // a kept rise whose holder did not come out and wait in time: this game's holder missed it
        foreach (var path in Early.Expired(Time.unscaledTime)) {
            Debug.Log($"[BossRule] Ground Bell rise kept {KeepS:0.0} s and dropped: {path} " +
                      "(its holder was not out and waiting)");
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

    /// <summary>Whether a rise that comes before its holder is out and waiting is kept for it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static bool KeepsEarly() => true;

    /// <summary>
    /// The switch its proof turns off (a skipped bool method returns false): a rise counts only when the holder
    /// was seen waiting first, so one that came out and rose within a frame is not sent (the 0.3.18 gate).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool CountsQuickRises() => true;

    /// <summary>
    /// The scene host's holder of that path rose: this game's rises too, now if it is out and waits, else as it comes
    /// out and waits within KeepS (Tick).
    /// </summary>
    public static void Echo(string path) {
        if (!Follows()) return;
        var found = false;
        var applied = false;
        foreach (var (fsm, holder) in _holders) {
            if (holder != path || fsm == null) continue;
            found = true;
            if (fsm.gameObject.activeInHierarchy && fsm.ActiveStateName == Waiting) {
                fsm.SendEvent("ENTER");
                Applied.Add(path);
                applied = true;
                Debug.Log($"[BossRule] Ground Bell rise applied: {path}");
                continue;
            }

            // print only
            var why = fsm.gameObject.activeInHierarchy ? "in " + fsm.ActiveStateName : "not out";
            Debug.Log($"[BossRule] Ground Bell rise not applied yet: {path} ({why})");
        }

        if (!found) Debug.Log($"[BossRule] Ground Bell rise for no holder here: {path} ({_holders.Count} holders)");
        if (!applied && KeepsEarly()) Early.Came(path, Time.unscaledTime);
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
