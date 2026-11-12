using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HutongGames.PlayMaker;
using UnityEngine;

namespace BossRule;

/// <summary>
/// The catalog's cutscenes (cutscenes.json) played together by the players in a room. Each game runs its own cutscene
/// FSM on its own hero; each state one game's FSM enters on its own, from the cutscene's start on, goes to the other games
/// in the room (a numbered <see cref="CutsceneState"/>), and moves their FSM there when its <see cref="CutsceneRun"/> says
/// so. Without it a cutscene played only in the game whose player set it off: a trigger
/// its hero touched, a talk, a bind or a Needolin only that game heard.
/// </summary>
internal sealed class CutsceneFollower {
    private readonly IReadOnlyList<CutsceneEntry> _catalog;
    private readonly Action<string, string, string, ushort> _send;
    private readonly Func<bool> _runsBoss;
    private readonly List<Watched> _watched = new();
    private string? _scene;
    private float _nextFind;

    // Set while this game's FSM goes to a state another game's entered, so that state is not sent back
    private static bool _following;

    private sealed class Watched {
        public CutsceneEntry Entry = null!;
        public PlayMakerFSM? Fsm;
        public CutsceneRun Run = null!;
        public Action<FsmState>? Handler;
    }

    /// <param name="send">Sends that this room's cutscene (scene, name) entered a state, the run's how-manyth told.</param>
    /// <param name="runsBoss">Whether this game runs the room's enemies and boss (SSMP's scene host).</param>
    public CutsceneFollower(IReadOnlyList<CutsceneEntry> catalog, Action<string, string, string, ushort> send,
        Func<bool> runsBoss) {
        _catalog = catalog;
        _send = send;
        _runsBoss = runsBoss;
    }

    /// <summary>
    /// Once per frame: a new room's cutscenes are watched, each once its object has loaded (a room's parts can load a
    /// moment after it).
    /// </summary>
    public void Tick() {
        var scene = GameManager.SilentInstance?.sceneName;
        if (scene != _scene) {
            Stop();
            _scene = scene;
            if (scene == null) return;
            foreach (var entry in CutsceneCatalog.InRoom(_catalog, scene).Where(e => e.Follow && (!e.KeepsHero || FollowsRides()))) {
                _watched.Add(new Watched { Entry = entry, Run = new CutsceneRun(entry.Start, entry.Skip) });
            }
        }

        // a room's look for its cutscene FSMs reads every FSM loaded: at most twice a second until all are found
        if (Time.unscaledTime < _nextFind || _watched.All(w => w.Fsm != null)) return;
        _nextFind = Time.unscaledTime + 0.5f;
        foreach (var w in _watched.Where(w => w.Fsm == null)) {
            if (Find(w.Entry) is not { } fsm) continue;
            w.Fsm = fsm;
            Debug.Log($"[BossRule] The cutscene {w.Entry.Name} is watched ({fsm.ActiveStateName}, its run " +
                      $"{(w.Run.Started ? w.Run.Leads ? "leading" : "following" : "not started")})");
            w.Handler = state => {
                if (w.Run.Entered(state.Name, _following)) _send(_scene!, w.Entry.Name, state.Name, w.Run.Sent);
                // a following game, or one that does not run the room's boss (whose own start would give it back)
                // (a crest cancelled by a hit on its setter at the stab ends at Cancel, giving only the setter back)
                var ends = state.Name == w.Entry.Release || state.Name == w.Entry.Cancel;
                if (w.Run.Started && (!w.Run.Leads || !_runsBoss()) && ends) Release(w.Entry, state.Name);
                // the cutscene is over: what its FSM does next (the Seamstress's next talk) is each game's own
                if (w.Run.Started && ends && EndsAtRelease()) {
                    w.Run = new CutsceneRun(w.Entry.Start, w.Entry.Skip);
                }
            };
            fsm.Fsm.StateChanged += w.Handler;
        }
    }

    /// <summary>The room changed or the connection ended: nothing is watched.</summary>
    public void Stop() {
        foreach (var w in _watched.Where(w => w.Fsm != null)) w.Fsm!.Fsm.StateChanged -= w.Handler;
        _watched.Clear();
        _scene = null;
    }

    /// <summary>Another game in this room sent that its cutscene (by name) entered a state, its run's seq-th told.</summary>
    public void Received(string scene, string name, string state, ushort seq) {
        if (scene != _scene) return;
        if (_watched.FirstOrDefault(w => w.Entry.Name == name && w.Fsm != null) is { } watched) {
            Follow(watched.Fsm!, watched.Run, seq, state, watched.Entry);
        }
    }

    /// <summary>
    /// This game's cutscene FSM goes to the state another game's entered, when its run says so (a start it has not made, or
    /// a later state it has not entered).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void Follow(PlayMakerFSM fsm, CutsceneRun run, ushort seq, string state, CutsceneEntry? entry = null) {
        // an FSM off in this game (the opening's, once this game's values say it played) never runs on to its end, so its
        // follow would hold this game's hero for good
        if (!fsm.isActiveAndEnabled) {
            Debug.Log($"[BossRule] The cutscene {fsm.gameObject.name} ({fsm.FsmName}) is off: {state} not followed");
            return;
        }

        // a crest reloads its room in each game, and the leader's can end its reload and tell Set Return while this
        // one's is under way (seen under lag): an FSM is not set in the middle of its room change, and this game's
        // own entry plays the rest
        if (MidRoomChange()) {
            Debug.Log($"[BossRule] The cutscene {fsm.gameObject.name} ({fsm.FsmName}) between rooms: " +
                      $"{state} not followed");
            return;
        }

        if (!run.Follows(seq, state, fsm.ActiveStateName)) return;

        // a ride whose states would take this game's hero along (the caravan's fade and room change): what the room shows
        if (entry?.LeaveAt is { Count: > 0 } leaveAt) {
            RideLeave.Told(fsm, state, leaveAt.Contains(state));
            Debug.Log($"[BossRule] The cutscene {fsm.gameObject.name} ({fsm.FsmName}) plays another game's {state} here");
            return;
        }

        var starting = !run.Started;
        var keepsHero = entry?.KeepsHero == true;  // a ride is its rider's: this game's hero stays its player's
        _following = true;
        try {
            // the setter's own walk up to it (a shrine's interaction) took that hero's control before the start: this
            // game's hero stops with it; the cutscene's end gives both back (the shrine's Set Finished)
            if (starting && !keepsHero && HeroController.SilentInstance is { } hero) hero.RelinquishControl();
            if (!keepsHero) CloseMessages();
            fsm.SetState(state);
        } finally {
            _following = false;
        }

        Debug.Log($"[BossRule] The cutscene {fsm.gameObject.name} ({fsm.FsmName}) follows another game's to {state}");
    }

    /// <summary>
    /// The messages up in this game (a crest's, an item's, a skill's) closed as their end closes them: the setter's
    /// game closed its own and went on. Left up, a crest's message was torn down by its room's reload before it ended,
    /// and its pause of pickups, the inventory and the pause menu stayed (UIMsgProxy.SetIsInMsg(false) never ran).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void CloseMessages() {
        // a message's object is active only while it shows (UIMsgBase.DoMsg turns it off at its end)
        foreach (var msg in UnityEngine.Object.FindObjectsByType<UIMsgProxy>(FindObjectsSortMode.None)) {
            msg.StopAllCoroutines();
            msg.SetIsInMsg(false);
            msg.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Whether a run ends at its release state. An NPC's cutscene FSM is the NPC's whole life (the Seamstress's
    /// Dialogue): a run that went on told her next talk to the other games, which followed it, its lines on their
    /// screens and their heroes held.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch (it then reads false)
    public static bool EndsAtRelease() => true;

    /// <summary>Whether the rides (keeps_hero: the Bellway's beast, the caravan) are followed (0.3.21).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // ride_follow's twin switches it off with a patch (it then reads false)
    public static bool FollowsRides() => true;

    /// <summary>Whether this game's hero is between rooms: its room change has not ended.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch (it then reads false)
    public static bool MidRoomChange() => HeroController.SilentInstance is { cState.transitioning: true };

    /// <summary>
    /// This game's cutscene reached the entry's release state with its hero still held, where nothing in this game gives
    /// it back: the Widow's talk (End) leaves the hero to her roar, which runs only in the game that runs her; a shrine
    /// followed past its End (a late or lost state) reaches Set Finished held. The hero takes input again here, unless
    /// it is changing room, respawning or dead.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void Release(CutsceneEntry entry, string at) {
        if (HeroController.SilentInstance is not { acceptingInput: false } hero ||
            hero.cState.transitioning || hero.cState.hazardRespawning || hero.cState.dead) {
            var h = HeroController.SilentInstance;
            Debug.Log($"[BossRule] The cutscene {entry.Name} gives nothing back at {at}: " + (h == null
                ? "no hero"
                : $"accepting input {h.acceptingInput}, transitioning {h.cState.transitioning}, " +
                  $"respawning {h.cState.hazardRespawning}, dead {h.cState.dead}, locks {h.HeroLockState}"));
            return;
        }
        hero.StartAnimationControl();
        hero.RegainControl();
        Debug.Log($"[BossRule] The cutscene {entry.Name} gives the hero back at {at}");
    }

    /// <summary>The entry's FSM on the object at its path in this room, an active one first.</summary>
    private static PlayMakerFSM? Find(CutsceneEntry entry) {
        var name = entry.Object.Substring(entry.Object.LastIndexOf('/') + 1);
        return UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(f => f.FsmName == entry.Fsm && f.gameObject.name == name && PathOf(f.transform) == entry.Object)
            .OrderByDescending(f => f.gameObject.activeInHierarchy)
            .FirstOrDefault();
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
