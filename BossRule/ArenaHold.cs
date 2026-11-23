using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SSMP.Api.Client;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (coop) An ordinary wave arena (a BattleScene in a room the boss catalog does not list) waits to start until every hero
/// in its room is inside it, as a boss room's start does (StartGate): each game starts its own arena as its own hero
/// walks into the arena's collider, and only the scene host's start reaches the others (ArenaWaves) and only its win
/// ends theirs (ArenaWins). So a hero whose game does not run the room, walking in alone, started its arena there alone:
/// its gates shut, the room's runner never started its own, nobody could win it, and that hero stayed shut in until the
/// other walked in too (Room_CrowCourt's lever row, probe_cc_lever, b-cc-before-hostff-p4.log, 2026-11-20). The held
/// start shuts no gate, so the first hero in can walk out again while it waits; the boss wait line shows. Only heroes in
/// the arena's room count (WaitRule.EvaluateRoom). The owner, 2026-11-20: the wait lasts WaitRule.RoomWaitS at most
/// ("it waits a few seconds for the other player to enter since it's a timing event, along with arenas"), then the fight
/// starts for all (ArenaStarts) and a hero still outside keeps its gates open until it walks in (ArenaGates).
/// Only a start by a hero walking into the arena's own collider is held (BattleScene.OnTriggerEnter2D): an arena its
/// FSM starts from elsewhere (Ant_21's, its resting guard woken by a hit) has no walk-in to wait for, and held it never
/// started (b-hold-arenas2-p4.log: Ant_21's Battle Scene Init held in every case); the scene host's start that a
/// following game copies (ArenaWaves) is no walk-in either.
/// </summary>
internal static class ArenaHold {
    /// <summary>SSMP's client manager; set by the client add-on.</summary>
    public static IClientManager? Client;

    /// <summary>Sends the waiting line for a scene to the server ("" once nobody is missing); set by the client add-on.</summary>
    public static Action<string, string>? SendStatus;

    private static BattleScene? _held;
    private static float _heldAt;
    private static string _scene = "";
    private static string _message = "";
    private static bool _letting;
    private static bool _walkingIn;

    /// <summary>Prefix on <c>BattleScene.OnTriggerEnter2D</c>: a start made inside it is a hero walking in.</summary>
    public static void TriggerPrefix() => _walkingIn = true;

    /// <summary>Finalizer on <c>BattleScene.OnTriggerEnter2D</c>: the walk-in is over, however it ended.</summary>
    public static void TriggerFinalizer() => _walkingIn = false;

    /// <summary>
    /// Whether ordinary arenas wait for everyone in the room. Not inlined: arena_alone's twin switches it off with a
    /// patch, and the hero walking in alone is shut in again.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool HoldsArenas() => true;

    /// <summary>
    /// Prefix on <c>BattleScene.StartBattle</c>, before the others (ArenaWaves' wave send checks that it ran): hold an
    /// ordinary arena's start while a hero in its room stands outside it.
    /// </summary>
    public static bool StartBattlePrefix(BattleScene __instance) {
        if (_letting || !_walkingIn || !HoldsArenas() || !InSession() || Client is not { } client) return true;
        var scene = __instance.gameObject.scene.name;
        if (ArenaWins.CatalogRoom?.Invoke(scene) != false || StartGate.BattleStarted(__instance) ||
            StartGate.BattleCompleted(__instance)) {
            return true;
        }

        if (Evaluate(__instance, client).Allowed) {
            if (_held == __instance) Clear();
            return true;
        }

        if (_held != __instance) {
            Clear();
            _held = __instance;
            _heldAt = Time.unscaledTime;
            _scene = scene;
            Debug.Log($"[BossRule] Holding the arena {__instance.name} in {scene} until everyone in the room is inside");
        }

        return false;
    }

    /// <summary>Once per frame: tell the room who is missing when that changes, and let the held start go once nobody is.</summary>
    public static void Tick() {
        if (_held == null) return;
        if (!_held || !_held.isActiveAndEnabled || StartGate.BattleStarted(_held) || StartGate.BattleCompleted(_held) ||
            !InSession() || Client is not { } client) {
            Clear();
            return;
        }

        var result = Evaluate(_held, client);
        var heldS = Time.unscaledTime - _heldAt;
        var go = WaitRule.RoomMayStart(result, heldS);
        var message = go || !LocalInside(_held) ? "" : WaitRule.RoomMessage(result, heldS);
        if (message != _message) {
            _message = message;
            SendStatus?.Invoke(_scene, message);
        }

        if (!go) return;

        var battle = _held;
        Debug.Log(result.Allowed
            ? $"[BossRule] Everyone in {_scene} is inside the arena {battle.name}: it starts after {heldS:F1} s"
            : $"[BossRule] The arena {battle.name} in {_scene} waited {WaitRule.RoomWaitS:F0} s: it starts for all " +
              $"({string.Join(", ", result.Missing)} still outside)");
        Clear();
        _letting = true;
        try {
            battle.StartBattle();
        } finally {
            _letting = false;
        }
    }

    /// <summary>In a session (ArenaWins' scene host tests are true only then): the game alone holds nothing.</summary>
    private static bool InSession() => ArenaWins.SceneHost?.Invoke() == true || ArenaWins.NotSceneHost?.Invoke() == true;

    private static void Clear() {
        if (_message != "") SendStatus?.Invoke(_scene, "");
        _message = "";
        _held = null;
    }

    private static WaitResult Evaluate(BattleScene battle, IClientManager client) {
        var area = battle.GetComponents<Collider2D>();
        bool Inside(Vector3 p) => area.Length == 0 || area.Any(c => c != null && Contains(Presence.ArenaBounds(c), p));

        var hero = HeroController.instance;
        var players = new List<(PlayerPresence, bool)> {
            (new PlayerPresence(Presence.LocalId, client.Username, true, hero != null && Inside(hero.transform.position)), true)
        };
        players.AddRange(client.Players.Select(p => (new PlayerPresence(p.Id, p.Username, true,
            p.IsInLocalScene && p.PlayerObject != null && Inside(p.PlayerObject.transform.position)), p.IsInLocalScene)));
        return WaitRule.EvaluateRoom(players);
    }

    private static bool LocalInside(BattleScene battle) =>
        HeroController.instance is { } hero && battle.GetComponents<Collider2D>()
            .Any(c => c != null && Contains(Presence.ArenaBounds(c), hero.transform.position));

    private static bool Contains(Bounds b, Vector3 p) =>
        p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;
}
