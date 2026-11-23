using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BossRule;

/// <summary>
/// (0.3.20) A boss held in a trap its room runs until a player frees her (<see cref="FreeTraps"/>): the Bell Beast hangs
/// in Bone_05's thick silk vines until a player's Special Attack hits their Special Detector, whose HIT sends DESTROY to
/// the vines' Control: it breaks every vine and sends FREE, and her Beast Anim plays Free, Land (BG CLOSE: her room's
/// two gates shut), Roar and Battle Start (BATTLE START to Bone Beast). Nothing of it is synced, so a friend who freed
/// her was shut in: its gates closed and the host's game, which runs her, never started her (0.3.20's
/// probe_bellbeast_free). The game whose own player frees her tells the room; every other game there frees its own trap
/// the same way, so her Free, the gates and the fight's start run in every game, the scene host's too, and each
/// player's Hornet plays its own Roar Lock. Her Return path's start (Return Battle's ENTER, from its own trigger) goes the
/// same way: a friend who walked in was shut in while the host's game kept her Dormant.
/// </summary>
internal static class FreeStarts {
    public const string Prefix = "Free Trap:";

    /// <summary>(0.3.23) The room's fight is on in the sending game, told again every second for a game coming in.</summary>
    public const string RunningPrefix = "Free Trap Running:";

    private const float LookEvery = 0.5f;
    private const float ResendEvery = 1f;

    /// <summary>The start's actions a late game leaves out: her title, and her roar's emitter (its hero's roar lock).</summary>
    private static readonly string[] LateOff = ["DisplayBossTitle", "StartRoarEmitter"];

    /// <summary>Sends a state for this room's fight to the room's other players; set by the client add-on.</summary>
    public static Action<string>? Send;

    private static string? _scene;
    private static (string Scene, string Trap, string Fsm, string Freed, string Event)[] _traps = [];
    private static PlayMakerFSM?[] _fsms = [];
    private static string?[] _last = [];
    private static bool[] _echoed = [];
    private static bool[] _freed = [];
    private static PlayMakerFSM? _fight;
    private static bool _late;
    private static float _nextLook, _nextResend;

    /// <summary>Every frame: this room's traps, and whether this game's own player just freed one.</summary>
    public static void Tick() {
        var scene = GameManager.SilentInstance?.sceneName;
        if (scene != _scene) {
            _scene = scene;
            _traps = FreeTraps.InRoom(scene);
            _fsms = new PlayMakerFSM?[_traps.Length];
            _last = new string?[_traps.Length];
            _echoed = new bool[_traps.Length];
            _freed = new bool[_traps.Length];
            _fight = null;
            _late = false;
        }

        var look = false;
        for (var i = 0; i < _traps.Length; i++) {
            if (_fsms[i] == null) {
                // only the path the room loaded is out (her vines on her first fight, Return Battle on her return)
                if (!look && Time.unscaledTime < _nextLook) continue;
                if (!look) _nextLook = Time.unscaledTime + LookEvery;
                look = true;
                _fsms[i] = Find(_traps[i].Trap, _traps[i].Fsm);
                _last[i] = _fsms[i]?.ActiveStateName;
                continue;
            }

            var now = _fsms[i]!.ActiveStateName;
            if (FreeTraps.Sends(_last[i], now, _traps[i].Freed, _echoed[i])) Send?.Invoke(Prefix + _traps[i].Trap);
            if (now == _traps[i].Freed && !_freed[i]) {
                _freed[i] = true;
                _nextResend = Time.unscaledTime + ResendEvery; // after the start's own word, which tells the room first
            }
            _last[i] = now;
        }

        // (0.3.23) the fight is on here: told again every second, for a player who comes into the room later
        if (_freed.Any(f => f) && Time.unscaledTime >= _nextResend && FreeTraps.FightOn(scene) is { } on) {
            _nextResend = Time.unscaledTime + ResendEvery;
            if (_fight == null) _fight = Find(on.Object, on.Fsm);
            if (FreeTraps.Resends(true, _fight?.ActiveStateName, on.State) && ResendsRunning()) Send?.Invoke(RunningPrefix + scene);
        }
    }

    /// <summary>The switch bellbeast_join's twin skips (false: the fight-on word is never sent again).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ResendsRunning() => true;

    /// <summary>
    /// (0.3.23) Another game's fight in this room is on, and this game came in after its start: this game's trap that still
    /// waits starts the game's own way (Return Battle's ENTER: her gates shut), once, without her title, which the
    /// players in the room saw at the fight's start (a friend joining mid-fight saw her gates open, and her title when it
    /// walked on into her Return trigger).
    /// </summary>
    public static void Late(string scene) {
        if (scene != _scene || _late || GameManager.SilentInstance?.sceneName != scene) return;
        for (var i = 0; i < _traps.Length; i++) {
            var fsm = _fsms[i] ?? Find(_traps[i].Trap, _traps[i].Fsm);
            var state = fsm?.Fsm?.ActiveState;
            if (fsm == null || state == null ||
                !FreeTraps.LateStarts(fsm.ActiveStateName, _traps[i].Freed, _traps[i].Event, state.Transitions.Select(t => t.EventName))) {
                continue;
            }

            _late = true;
            _echoed[i] = true;
            // her title and her roar, which stuns this game's hero, belong to the start the room saw: off in every FSM
            // under the trap (her first path's Beast Anim, a child of the vines, runs both in its Roar)
            var off = fsm.gameObject.GetComponentsInChildren<PlayMakerFSM>(true)
                .SelectMany(f => f.FsmStates).SelectMany(s => s.Actions).Where(a => LateOff.Contains(a.GetType().Name)).ToList();
            foreach (var action in off) action.Enabled = false;

            Debug.Log($"[BossRule] Late start: {_traps[i].Trap}'s {_traps[i].Event} in {state.Name}, {off.Count} title and roar actions off ({scene})");
            fsm.SendEvent(_traps[i].Event);
            return;
        }
    }

    /// <summary>Another game's player freed one of this room's traps: this game's frees too, if it still waits.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void Echo(string trapName) {
        if (GameManager.SilentInstance?.sceneName != _scene) return;
        var i = System.Array.FindIndex(_traps, t => t.Trap == trapName);
        if (i < 0) return;
        var fsm = _fsms[i] ?? Find(_traps[i].Trap, _traps[i].Fsm);
        if (fsm == null || !FreeTraps.Echoes(fsm.ActiveStateName, _traps[i].Freed)) return;
        _echoed[i] = true;
        fsm.SendEvent(_traps[i].Event);
    }

    private static PlayMakerFSM? Find(string name, string fsm) =>
        RoomScan.All<PlayMakerFSM>()
            .FirstOrDefault(f => f.gameObject.name == name && f.FsmName == fsm);
}
