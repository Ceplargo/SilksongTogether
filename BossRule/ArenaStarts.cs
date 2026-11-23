using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BossRule;

/// <summary>
/// (coop) An ordinary wave arena (a BattleScene in a room the boss catalog does not list) starts in every game in its room
/// as soon as it starts in any one (the owner, 2026-11-20: "No, change it"; the other half of ArenaWins). Each game starts
/// its own arena as its own hero walks in (or as its own detector or lever starts it), and only the scene host's start
/// reached the others (ArenaWaves). So a hero whose game does not run the room, walking in alone, started its arena alone:
/// its gates shut, the room's runner never started its own, nobody could win it, and that hero stayed shut in
/// (Room_CrowCourt's lever row, probe_cc_lever, b-cc-before-hostff-p4.log). Now a game whose arena starts by its own act
/// tells the room, and every game there starts its own. A hero still outside is shut out until the win (the owner
/// accepted that), which ArenaWins then brings to every game.
/// </summary>
internal static class ArenaStarts {
    private static bool _following;

    /// <summary>
    /// The arenas that began in this game (by its own act or a follow), by object: a start stays false through its start
    /// pause, so a second StartBattle then (both games starting at once, each told of the other's) made its first wave
    /// twice. By object, not by room and path: a path's set kept across a room's reload held every later fight of the
    /// arena back (b-gt2-cc-arena-p4.log: every case after the first "never started"); a reload makes new objects.
    /// </summary>
    private static readonly HashSet<int> Begun = new();

    /// <summary>
    /// Prefix on <c>BattleScene.StartBattle</c>: an ordinary arena that already began here and is not won is not started
    /// again (its start pause keeps started false, and a second start made its first wave twice: a follow and
    /// ArenaWaves' copy of the host's start, or both games starting at once).
    /// </summary>
    public static bool StartBattlePrefix(BattleScene __instance) {
        if (!InSession() || StartGate.BattleCompleted(__instance)) return true;
        var room = __instance.gameObject.scene.name;
        if (ArenaWins.CatalogRoom?.Invoke(room) != false || !Begun.Contains(__instance.GetInstanceID())) return true;
        Debug.Log($"[BossRule] Arena {__instance.name} in {room} began here already: not started again");
        return false;
    }

    /// <summary>
    /// Whether a game follows another game's arena start. Not inlined: arena_alone's twin switches it off with a patch,
    /// and the hero walking in alone is shut in with no fight again.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FollowsStarts() => true;

    /// <summary>
    /// Postfix on <c>BattleScene.StartBattle</c>: an ordinary arena that started here by this game's own act (not as a
    /// follow) is announced to the room.
    /// </summary>
    public static void StartBattlePostfix(BattleScene __instance, bool __runOriginal) {
        if (!__runOriginal || !InSession()) return;
        var room = __instance.gameObject.scene.name;
        if (ArenaWins.CatalogRoom?.Invoke(room) != false || StartGate.BattleCompleted(__instance)) return;
        var path = PathOf(__instance.transform);
        Begun.Add(__instance.GetInstanceID());
        if (_following) return;  // a follow is not told again
        ArenaWins.Send?.Invoke(room, ArenaStart.Prefix + path);
        Debug.Log($"[BossRule] Arena started here, sent to the room: {path} ({room}) at {Time.unscaledTime:F3}");
    }

    /// <summary>Another game in room started its arena at path: this game's starts too, if it is there and fresh.</summary>
    public static void Received(string room, string path) {
        var here = GameManager.SilentInstance?.sceneName;
        foreach (var battle in Object.FindObjectsByType<BattleScene>(FindObjectsSortMode.None)) {
            if (PathOf(battle.transform) != path || battle.gameObject.scene.name != room) continue;
            var started = StartGate.BattleStarted(battle);
            var follows = ArenaStart.Follows(here == room, ArenaWins.CatalogRoom?.Invoke(room) != false, started,
                StartGate.BattleCompleted(battle));
            Debug.Log($"[BossRule] Arena Started received: {path} ({room}) at {Time.unscaledTime:F3}: " +
                      (follows ? FollowsStarts() ? "this game's starts" : "not followed (switch off)" : $"left (started {started})"));
            if (!follows || !FollowsStarts() || Begun.Contains(battle.GetInstanceID())) return;
            _following = true;
            try {
                battle.StartBattle();
            } finally {
                _following = false;
            }

            return;
        }
    }

    /// <summary>In a session (ArenaWins' scene host tests are true only then): the game alone sends nothing.</summary>
    private static bool InSession() => ArenaWins.SceneHost?.Invoke() == true || ArenaWins.NotSceneHost?.Invoke() == true;

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
