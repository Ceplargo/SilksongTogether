using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BossRule;

/// <summary>
/// (coop) An ordinary wave arena (a BattleScene in a room the boss catalog does not list) ends in every game in its room
/// once the game that runs the room wins it. Each game starts its own arena as its own hero walks in; in a game that does
/// not run the room its waves count SSMP's hidden originals of the enemies, which never leave there, so it never ended by
/// itself and kept whoever walked in second shut in (0.3.16; the Act 1 sweep's arena probe, Greymoor_04). The scene
/// host's game tells the room each arena it wins (the arena follow message), and a following game ends its own then, or
/// once the win reaches it through the shared save first: the arena's PlayerData bool (which the arena reads only at its
/// Start) or its saved object (whose sync completes it with only a BG QUICK OPEN). The catalog's rooms keep
/// StartGate.EndFollowed.
/// </summary>
internal static class ArenaWins {
    /// <summary>A follow message's state that is an arena's win: "Arena Won: " and the arena's path.</summary>
    public const string Prefix = "Arena Won: ";

    /// <summary>Sends a room's follow state to its other players; set by the client add-on.</summary>
    public static Action<string, string>? Send;

    /// <summary>Whether this game is in a session and runs its room; set by the client add-on.</summary>
    public static Func<bool>? SceneHost;

    /// <summary>Whether this game is in a session and does not run its room; set by the client add-on.</summary>
    public static Func<bool>? NotSceneHost;

    /// <summary>Whether the boss catalog lists a room; set by the client add-on.</summary>
    public static Func<string, bool>? CatalogRoom;

    /// <summary>The paths of the arenas the scene host's game won in this room since this game came in.</summary>
    private static readonly HashSet<string> Won = new();
    private static string? _room;
    private static float _nextLook;
    private static readonly RoomCatalog Rooms = new();

    /// <summary>
    /// Prefix on <c>BattleScene.DoEndBattle</c>: the scene host's game tells the room's other players its arena is won.
    /// It never throws, which would skip the arena's own end.
    /// </summary>
    public static void EndPrefix(BattleScene __instance) {
        var room = GameManager.SilentInstance?.sceneName;
        if (room == null || SceneHost?.Invoke() != true || CatalogRoom?.Invoke(room) != false ||
            StartGate.BattleCompleted(__instance)) return;
        try {
            Send?.Invoke(room, Prefix + PathOf(__instance.transform));
        } catch (InvalidOperationException e) {
            // SSMP's connection closed before its disconnect reached this frame (the code review, 2026-11-02)
            Debug.Log($"[BossRule] An arena's win was not sent: {e.Message}");
        }
    }

    /// <summary>The scene host's game won its arena at path in room: this game's ends at the next look.</summary>
    public static void Received(string room, string path) {
        NewRoom(room);
        Won.Add(path);
        _nextLook = 0f;
    }

    /// <summary>Once per frame: every 0.5 s in a game that does not run its room, each of its arenas takes its step.</summary>
    public static void Tick() {
        var room = GameManager.SilentInstance?.sceneName;
        NewRoom(room);
        if (room == null || Time.unscaledTime < _nextLook || NotSceneHost?.Invoke() != true ||
            Rooms.Skipped(room, CatalogRoom)) return;
        _nextLook = Time.unscaledTime + 0.5f;
        foreach (var battle in Object.FindObjectsByType<BattleScene>(FindObjectsSortMode.None)) {
            if (!battle.isActiveAndEnabled) continue;
            var won = Won.Contains(PathOf(battle.transform)) ||
                      battle.setPDBoolOnEnd is { Length: > 0 } flag && PlayerData.instance.GetBool(flag);
            var step = ArenaEnd.For(StartGate.BattleStarted(battle), StartGate.BattleCompleted(battle), won);
            if (step != ArenaStep.None) EndHere(battle, step);
        }
    }

    /// <summary>
    /// End: the arena's own end, as the scene host's ran (its gates' BG OPEN, its camera locks off, its end events).
    /// Open: one the shared save completed while it ran stops, and its gates get the BG OPEN some wait for.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void EndHere(BattleScene battle, ArenaStep step) {
        if (step == ArenaStep.End) {
            battle.DoEndBattle();
        } else {
            StartGate.BattleStarted(battle) = false;
            if (battle.openGatesOnEnd) battle.SendEventToChildren("BG OPEN");
            if (EndsRest() && battle.isActiveAndEnabled) battle.StartCoroutine(Rest(battle));
        }

        Debug.Log($"[BossRule] The arena {PathOf(battle.transform)} was won in the room: {step} here");
    }

    /// <summary>
    /// The switch the twin turns off (a skipped bool method returns false): Open runs no more of the arena's end.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool EndsRest() => true;

    /// <summary>
    /// Open's rest of the game's own end (EndBattle), after its pause as the scene host's game had it: the arena's camera
    /// locks off, its battle-only objects off, its end event and its end music. The shared save's BattleCompleted runs none
    /// of them, so the battle music and a camera lock stayed in the game that does not run the room until its player left
    /// (boss round six's probe: Shellwood_01b's music and lock, Room_Forge's lock B). Its end scene already took
    /// BattleCompleted's BATTLE COMPLETED, so it gets no BATTLE END, which the scene host's game never sends after that
    /// (the round's review).
    /// </summary>
    private static IEnumerator Rest(BattleScene battle) {
        yield return new WaitForSeconds(battle.battleEndPause);
        if (battle == null) yield break;
        if (battle.camLocks != null && !battle.dontDisableCamlocksOnEnd) battle.camLocks.SetActive(false);
        if (battle.activeDuringBattle != null) battle.activeDuringBattle.SetActive(false);
        if (battle.disableActiveBeforeBattleAtEnd && battle.activeBeforeBattle != null) {
            battle.activeBeforeBattle.SetActive(false);
        }

        if (!string.IsNullOrEmpty(battle.battleEndEventRegister)) EventRegister.SendEvent(battle.battleEndEventRegister);
        if (battle.musicCueEnd != null) {
            GameManager.instance.AudioManager.ApplyMusicCue(battle.musicCueEnd, 0f, 0f, applySnapshot: false);
        }

        if (battle.snapshotEnd != null) battle.snapshotEnd.TransitionTo(2f);
        Debug.Log($"[BossRule] The arena {PathOf(battle.transform)}'s end ran here after its pause");
    }

    private static void NewRoom(string? room) {
        if (room == _room) return;
        _room = room;
        Won.Clear();
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
