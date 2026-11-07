using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BossRule;

/// <summary>
/// Moorwing's slash and the Widow's dash slash catch the hero in front in every game, and the boss goes into her multi
/// slash for it. Each move's attack box carries the game's multi-wounder FSM (hornet_multi_wounder; Moorwing's named
/// Multihitter): catching a hero it sends MULTI HIT CONNECT to the box and the three objects above it, and her Control
/// goes into its multi slash, and MULTI WOUND 3 to the hero, which wounds itself (a hit, held, a final hit). In a game
/// that follows the scene host SSMP's copy of the box catches that game's hero, which is wounded there, but the copy's
/// Control does not run, and the scene host's boss went on with her move (boss round five's check multi_hits, case b).
/// So such a game tells the scene host's game of each catch, and that game sends its own box's connect; there, until she
/// leaves the state, her multi slash's actions aimed at the global Hero (her pull and turn, Moorwing's hold) aim at a
/// stand-in kept where that player is, and those that spawn at it (Moorwing's Thread Sphere Slash, whose later hits land
/// on the game's own hero wherever they are: green run 1, the host 12 units away took a weak wound) do not run: the
/// caught player's own wound spawns its own.
/// </summary>
internal static class MultiHits {
    public const string Prefix = "Multi Hit: ";
    private const string Connect = "MULTI HIT CONNECT";
    private const string Hero = "Hero";
    private static readonly HashSet<string> Wounders = ["hornet_multi_wounder", "Multihitter"];

    /// <summary>Sends a catch to the room's other players; set by the client add-on.</summary>
    public static Action<string>? Send;

    /// <summary>Whether this game is in a session and not the scene host; set by the client add-on.</summary>
    public static Func<bool>? NotSceneHost;

    /// <summary>Whether this game is in a session and the scene host; set by the client add-on.</summary>
    public static Func<bool>? SceneHost;

    /// <summary>This player's name; set by the client add-on.</summary>
    public static Func<string>? Username;

    /// <summary>The object of the player of that name in this room, if any; set by the client add-on.</summary>
    public static Func<string, GameObject?>? PlayerObject;

    private sealed record Aimed(PlayMakerFSM Fsm, string State, Action GiveBack);

    private static readonly List<Aimed> Swapped = [];
    private static readonly LateCatches<(PlayMakerFSM Fsm, string State)> Late = new();
    private static readonly List<PlayMakerFSM> Boxes = [];
    private static string? _boxesScene;
    private static GameObject? _standIn;
    private static GameObject? _player;

    /// <summary>
    /// Harmony postfix of SendEventByName.OnEnter: a multi-wounder's first connect (to its own box) is its catch of this
    /// game's hero; in a game that follows the scene host, a copy's catch goes to the scene host's game.
    /// </summary>
    public static void SentPostfix(SendEventByName __instance) {
        if (__instance.sendEvent?.Value != Connect || __instance.Fsm == null) return;
        var path = __instance.Fsm.GameObject != null ? PathOf(__instance.Fsm.GameObject.transform) : "none";
        var toOwner = __instance.eventTarget is { target: FsmEventTarget.EventTarget.GameObject } t &&
                      t.gameObject?.OwnerOption == OwnerDefaultOption.UseOwner;
        var wounder = Wounders.Contains(__instance.Fsm.Name);
        var tells = wounder && toOwner && Tells();
        var sent = tells && MultiHitPath.IsCopy(path);
        // (coop) print only: each connect seen, so a catch this game never sent names its wounder (bosses Task 5)
        Debug.Log($"[BossRule] Connect by {__instance.Fsm.Name} at {path} in {__instance.State?.Name}: wounder {wounder}, " +
                  $"to its own box {toOwner}, tells {tells}, copy {MultiHitPath.IsCopy(path)}, sent {sent}");
        if (sent) Send?.Invoke(Prefix + MultiHitPath.Message(Username?.Invoke() ?? "", MultiHitPath.Of(path)));
        // (coop) the scene host's own catch: a late one from another game must not start her multi slash again
        if (wounder && toOwner && SceneHost?.Invoke() == true && TakesConnects()) Late.Took(path, Time.unscaledTime);
    }

    /// <summary>Whether this game tells the scene host's game of its hero's catches.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static bool Tells() => NotSceneHost?.Invoke() == true;

    /// <summary>
    /// Another player's hero was caught by the named box in its own game: in the scene host's game that box's connect,
    /// for each FSM there it takes into a state, with that state's Hero aimed at the stand-in.
    /// </summary>
    public static void Received(string message) {
        if (SceneHost?.Invoke() != true || MultiHitPath.Read(message) is not var (player, named)) return;
        var at = PlayerObject?.Invoke(player);
        var box = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).FirstOrDefault(f =>
            Wounders.Contains(f.FsmName) && f.isActiveAndEnabled && MultiHitPath.Names(PathOf(f.transform), named));
        if (at != null && box == null && HonoursLate() && Late.For(named, Time.unscaledTime, TakesConnects()) is { } moves) {
            // (coop) her move's box went here just before the catch came: she goes where the box would have taken her
            var done = new List<string>();
            foreach (var (fsm, state) in moves) {
                if (fsm == null || !fsm.isActiveAndEnabled) continue;
                Aim(fsm, state, StandIn(at));
                fsm.SetState(state);
                done.Add($"{fsm.FsmName} to {state}");
            }

            Debug.Log($"[BossRule] Multi Hit from {player} by {named}: its box went just before; {string.Join("; ", done)}");
            return;
        }

        if (at == null || box == null) {
            // (coop) under lag the catch can come after her move ended here (multi_hits' lag run, the Widow's charge)
            Debug.Log($"[BossRule] Multi Hit from {player} by {named}: " +
                      (box == null ? "no such box out here" : "that player is not in this room"));
            return;
        }

        var seen = new List<string>();
        if (TakesConnects()) Late.Took(PathOf(box.transform), Time.unscaledTime); // a later late catch: no restart
        var up = box.transform;
        for (var i = 0; i < 4 && up != null; i++, up = up.parent) {
            foreach (var fsm in up.GetComponents<PlayMakerFSM>()) {
                var state = fsm.isActiveAndEnabled ? ConnectState(fsm.Fsm) : null;
                seen.Add($"{fsm.FsmName} in {fsm.ActiveStateName} to {state ?? "nothing"}");
                if (state == null) continue;
                Aim(fsm, state, StandIn(at));
                fsm.SendEvent(Connect);
            }
        }

        Debug.Log($"[BossRule] Multi Hit from {player} by {named}: {string.Join("; ", seen)}");
    }

    /// <summary>Once per frame: the stand-in kept at its player, and each state's Hero given back once she left it.</summary>
    public static void Tick() {
        SeeBoxes();
        if (Swapped.Count == 0) return;
        if (_standIn != null && _player != null) _standIn.transform.position = _player.transform.position;
        for (var i = Swapped.Count - 1; i >= 0; i--) {
            var aimed = Swapped[i];
            if (aimed.Fsm != null && aimed.Fsm.ActiveStateName == aimed.State) continue;
            aimed.GiveBack();
            Swapped.RemoveAt(i);
        }
    }

    /// <summary>
    /// In the scene host's game, each multi-wounder box out now and what its connect would do (LateCatches); the room's
    /// boxes are found again whenever a scene loads or unloads.
    /// </summary>
    private static void SeeBoxes() {
        if (SceneHost?.Invoke() != true) return;
        // the active room and how many scenes have loaded: a boss's own scene loads after the hero enters (Moorwing's),
        // and sceneCount counts it already while it loads, before its objects are there
        var loaded = 0;
        for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) {
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isLoaded) loaded++;
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "|" + loaded;
        // a box destroyed since: the room was left and entered again with the same count (the review, 2026-11-05)
        if (scene != _boxesScene || Boxes.Any(b => b == null)) {
            if (_boxesScene?.Split('|')[0] != scene.Split('|')[0]) Late.Clear(); // a new room, not its boss's scene
            _boxesScene = scene;
            Boxes.Clear();
            Boxes.AddRange(Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(f => Wounders.Contains(f.FsmName)));
        }

        foreach (var box in Boxes) {
            if (box == null || !box.isActiveAndEnabled) continue;
            var moves = new List<(PlayMakerFSM, string)>();
            var up = box.transform;
            for (var i = 0; i < 4 && up != null; i++, up = up.parent) {
                foreach (var fsm in up.GetComponents<PlayMakerFSM>()) {
                    if (fsm.isActiveAndEnabled && ConnectState(fsm.Fsm) is { } state) moves.Add((fsm, state));
                }
            }

            if (moves.Count > 0) Late.Saw(PathOf(box.transform), Time.unscaledTime, moves);
        }
    }

    /// <summary>
    /// A box's connect taken here (the scene host's own catch, or another game's in time) uses up its late catch, so a
    /// second hero's catch coming late does not start her multi slash again. Not inlined: double_catch's twin skips it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TakesConnects() => true;

    /// <summary>A catch that comes just after its box went is honoured. Not inlined: late_catch's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool HonoursLate() => true;

    /// <summary>The state the connect takes that FSM into from where it is now, if any.</summary>
    private static string? ConnectState(Fsm fsm) =>
        fsm.ActiveState?.Transitions.Concat(fsm.GlobalTransitions).FirstOrDefault(t => t.EventName == Connect)?.ToState;

    /// <summary>
    /// That state's actions' Hero (an object or an owner field on the global) aimed at the stand-in, and those that spawn
    /// at it switched off.
    /// </summary>
    private static void Aim(PlayMakerFSM fsm, string state, GameObject standIn) {
        var actions = fsm.Fsm.GetState(state)?.Actions ?? [];
        foreach (var action in actions) {
            var spawns = action.GetType().Name.StartsWith("SpawnObjectFromGlobalPool") ||
                         action.GetType().Name.StartsWith("FlingObjectsFromGlobalPool");
            foreach (var field in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) {
                var own = field.GetValue(action);
                object? aimed = own switch {
                    FsmGameObject { UseVariable: true, Name: Hero } => new FsmGameObject { Value = standIn },
                    FsmOwnerDefault { OwnerOption: OwnerDefaultOption.SpecifyGameObject, GameObject: { UseVariable: true, Name: Hero } } =>
                        new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = new FsmGameObject { Value = standIn } },
                    _ => null
                };
                if (aimed == null) continue;
                if (spawns) {
                    var enabled = action.Enabled;
                    Swapped.Add(new Aimed(fsm, state, () => action.Enabled = enabled));
                    action.Enabled = false;
                    break;
                }

                Swapped.Add(new Aimed(fsm, state, () => field.SetValue(action, own)));
                field.SetValue(action, aimed);
            }
        }
    }

    /// <summary>An object with a still body (her pull moves it, not a player) where that player is.</summary>
    private static GameObject StandIn(GameObject player) {
        if (_standIn == null) {
            _standIn = new GameObject("Multi Hit Stand-in");
            _standIn.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        }

        _player = player;
        _standIn.transform.position = player.transform.position;
        return _standIn;
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
