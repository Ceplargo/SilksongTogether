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
    public const string Prefix = LocalTraps.TrapPrefix;
    // a room's objects come up over its first moments (an additive scene's later): looked for every LookEvery until
    // LookFor. A first look at 1 s missed a platform the friend stood on 0.7 s after coming in (0.3.20's local_traps)
    private const float LookEvery = 0.25f;
    private const float LookFor = 3.0f;

    /// <summary>Sends a room state to the room's other players; set by the client add-on.</summary>
    public static Action<string, string>? Send;

    private static string? _scene;
    private static float _sceneAt;
    private static float _nextLook;
    private static bool _echoing;
    private static readonly Dictionary<string, (PlayMakerFSM Fsm, string? Last)> Watched = new();
    // (0.3.21) platforms this game's own player set off (it tells their steps), those another game's word set off,
    // steps told before this game's platform got there ("path|@state"), and the platforms now holding a timed state's
    // wait for the step (that state, when it began, its own wait)
    private static readonly HashSet<string> Owned = new();
    private static readonly HashSet<string> Followed = new();
    private static readonly HashSet<string> Told = new();
    private static readonly Dictionary<PlayMakerFSM, (string Path, string State, float Since, float Wait)> Held = new();

    /// <summary>Every frame: this room's local-trigger terrain, and whether this game's own player just set one off.</summary>
    public static void Tick() {
        var scene = GameManager.SilentInstance?.sceneName;
        if (scene != _scene) {
            _scene = scene;
            _sceneAt = Time.unscaledTime;
            _nextLook = 0f;
            Watched.Clear();
            Owned.Clear();
            Followed.Clear();
            Told.Clear();
            Held.Clear();
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
            if (now == last) {
                LetGoLate(path, fsm);
                continue;
            }

            Watched[path] = (fsm, now);
            Track(path, fsm, now);
            if (Owned.Contains(path) && FollowsResets() &&
                LocalTraps.StepOf(fsm.FsmName, fsm.gameObject.name, last, now) is { } step) {
                Send?.Invoke(scene, Prefix + path + "|" + step); // routine: not printed
                continue;
            }

            if (LocalTraps.Sets(fsm.FsmName, fsm.gameObject.name, last, now) is not { } ev) continue;
            Debug.Log($"[BossRule] Trap set off here: {path} {ev} ({scene})");
            if (!LocalTraps.Resets.Any(r => r.Event == ev)) {
                Owned.Add(path); // this game's own player set it off: its timeline is the room's
                Followed.Remove(path);
            }

            Send?.Invoke(scene, Prefix + path + "|" + ev);
        }
    }

    /// <summary>(0.3.21) A platform another game's word set off: in a timed state it holds its own wait for the
    /// setting-off game's step, unless that step was told already; back whole, it is no one's.</summary>
    private static void Track(string path, PlayMakerFSM fsm, string? now) {
        if (now == "Idle") {
            Owned.Remove(path);
            Followed.Remove(path);
            Told.RemoveWhere(t => t.StartsWith(path + "|", StringComparison.Ordinal));
        }

        if (now != null && Followed.Contains(path) && FollowsResets() &&
            LocalTraps.NextOf(fsm.FsmName, fsm.gameObject.name, now) is { } next && !Told.Contains(path + "|@" + next))
            Held[fsm] = (path, now, Time.time, WaitOf(fsm, now));
        else Held.Remove(fsm);
    }

    /// <summary>(0.3.21) A held platform whose step never came within StepWaitS past its own wait takes it itself.</summary>
    private static void LetGoLate(string path, PlayMakerFSM fsm) {
        if (!Held.TryGetValue(fsm, out var h) || LocalTraps.HoldsStep(Time.time - h.Since, h.Wait)) return;
        Held.Remove(fsm);
        if (LocalTraps.NextOf(fsm.FsmName, fsm.gameObject.name, h.State) is not { } next) return;
        Debug.Log($"[BossRule] Trap step not told in time, taken here: {path} {next}");
        fsm.SetState(next);
        Watched[path] = (fsm, fsm.ActiveStateName);
        Track(path, fsm, fsm.ActiveStateName);
    }

    private static float WaitOf(PlayMakerFSM fsm, string state) =>
        fsm.Fsm.GetState(state)?.Actions.OfType<HutongGames.PlayMaker.Actions.Wait>().FirstOrDefault()?.time.Value ?? 0f;

    /// <summary>(0.3.21) Harmony prefix on PlayMaker's Wait.OnUpdate: a platform another game set off does not end
    /// its timed state on its own clock while it waits for the setting-off game's step.</summary>
    public static bool WaitPrefix(HutongGames.PlayMaker.Actions.Wait __instance) {
        if (Held.Count == 0) return true;
        var fsm = __instance.Fsm?.FsmComponent;
        return fsm == null || !Held.TryGetValue(fsm, out var h) || fsm.ActiveStateName != h.State;
    }

    /// <summary>(0.3.21 Task 10 Step 2) Another game's room word (path "|" event) whose set state keeps a game static
    /// for some room changes (LocalTraps.LastingOf): this game keeps it too, wherever its player is, so its next entry
    /// reads it as the doer's game does.</summary>
    public static void KeepStatic(string word) {
        if (LocalTraps.LastingOf(word) is not { } kept || !KeepsStatics()) return;
        Debug.Log($"[BossRule] Static kept from another player: {kept.Static} for {kept.Rooms} room changes");
        StaticVariableList.SetValue(kept.Static, true, kept.Rooms);
    }

    /// <summary>The switch marrow_reveals' twin turns off (a skipped bool method returns false).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool KeepsStatics() => true;

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

        // (0.3.21) the setting-off game's step: a platform it set off here takes it from the state it leads out of, or
        // keeps it for when it gets there (its own clock behind)
        if (LocalTraps.StepFrom(w.Fsm.FsmName, w.Fsm.gameObject.name, ev) is { } from) {
            if (!Followed.Contains(path) || !FollowsResets()) return;
            if (w.Fsm.ActiveStateName != from) {
                Told.Add(path + "|" + ev);
                return;
            }

            _echoing = true;
            try {
                w.Fsm.SetState(ev.Substring(1));
            } finally {
                _echoing = false;
            }

            Watched[path] = (w.Fsm, w.Fsm.ActiveStateName);
            Track(path, w.Fsm, w.Fsm.ActiveStateName);
            return;
        }

        // (0.3.21) both heroes set it off at once: the room's scene host's game tells its steps, this one follows
        if (Owned.Contains(path) && !LocalTraps.Resets.Any(r => r.Event == ev) &&
            !LocalTraps.TellsSteps(true, true, ArenaWins.SceneHost?.Invoke() == true)) {
            Owned.Remove(path);
            Followed.Add(path);
            Track(path, w.Fsm, w.Fsm.ActiveStateName);
        }

        if (!LocalTraps.Echoes(w.Fsm.FsmName, w.Fsm.gameObject.name, w.Fsm.ActiveStateName, ev)) return;
        if (LocalTraps.Resets.Any(r => r.Event == ev) && !FollowsResets()) return;
        Debug.Log($"[BossRule] Trap set off by the room: {path} {ev}");
        _echoing = true;
        try {
            if (LocalTraps.StateOf(ev) is { } state) w.Fsm.SetState(state);
            else w.Fsm.SendEvent(ev);
        } finally {
            _echoing = false;
        }
        // the event changes its state at once: Tick sees no setting off of this game's own, so tells no one back
        Watched[path] = (w.Fsm, w.Fsm.ActiveStateName);
        if (!LocalTraps.Resets.Any(r => r.Event == ev) &&
            LocalTraps.NextOf(w.Fsm.FsmName, w.Fsm.gameObject.name, "Break") != null) {
            Followed.Add(path); // another game's player set it off: its steps are that game's
            Owned.Remove(path);
        }

        Track(path, w.Fsm, w.Fsm.ActiveStateName);
    }

    /// <summary>
    /// (0.3.21) The switch crumble_reset's twin turns off (a skipped bool method returns false): another player's
    /// respawn no longer resets this game's broken platforms, as before 0.3.21.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FollowsResets() => true;

    /// <summary>
    /// (0.3.21) Harmony prefix on PlayMakerFSM.OnDisable. A state-told kind's set state can switch its own object off in
    /// the frame it enters (the Caravan Lech's Wounded State puts the Lech away): a frame watch then saw its wait state,
    /// then an empty one (marrow_reveals' watch print, c-mreveals-g16-p3-1.log), and told nothing. Going off in its set
    /// state, it tells the room here; not while another game's word sets it.
    /// </summary>
    public static void DisablePrefix(PlayMakerFSM __instance) {
        if (_echoing || GameManager.SilentInstance?.sceneName is not { } scene ||
            LocalTraps.SeenSet(__instance.FsmName, __instance.gameObject.name, __instance.ActiveStateName) is not { } ev)
            return;
        var path = PathOf(__instance.transform);
        Debug.Log($"[BossRule] Trap set off here as it went off: {path} {ev} ({scene})");
        Send?.Invoke(scene, Prefix + path + "|" + ev);
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
                if (Watched.ContainsKey(path)) continue;
                Watched[path] = (fsm, fsm.ActiveStateName);
                if (LocalTraps.Kinds.Any(k => k.Fsm == fsm.FsmName && fsm.gameObject.name.StartsWith(k.Name,
                        StringComparison.Ordinal) && k.Sets.Any(s => LocalTraps.StateOf(s.Event) != null)))
                    Debug.Log($"[BossRule] Watching {path} from {fsm.ActiveStateName} " +
                              $"({Time.unscaledTime - _sceneAt:F2} s into the room)"); // print only: when it was first seen
            }
        }
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
