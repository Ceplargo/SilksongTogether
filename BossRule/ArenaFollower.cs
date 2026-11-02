using System;
using System.Linq;
using System.Runtime.CompilerServices;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace BossRule;

/// <summary>
/// An arena whose intro runs in the scene host's game only (the Last Judge's first fight: the Needolin played at her
/// gate, the bell locks, her leap): once the scene host's arena reaches the catalog's follow state, the other games'
/// arenas, still waiting where their players played nothing, go there too, so their gates close and their cameras lock
/// for the fight. Each game runs its own arena, so without this the friend fought her with the gate open.
/// </summary>
internal sealed class ArenaFollower {
    /// <summary>A follow message's state that is one of the catalog's echo states: "Echo: " and the state.</summary>
    public const string EchoPrefix = "Echo: ";

    private readonly Action<string, string, string> _send;
    private PlayMakerFSM? _watched;
    private BossEntry? _entry;
    private string? _lastState;

    /// <param name="send">Sends that a scene's fight (by flag) reached a state.</param>
    public ArenaFollower(Action<string, string, string> send) => _send = send;

    /// <summary>
    /// The scene host's game, at the fight's start: watch its arena for the follow state.
    /// </summary>
    public void Watch(BossEntry entry, PlayMakerFSM? arena) {
        if (entry.Follow == null || arena == null) return;
        _entry = entry;
        _watched = arena;
        _lastState = null;
    }

    public void Stop() {
        _entry = null;
        _watched = null;
        _lastState = null;
    }

    /// <summary>
    /// Once per frame: tell the others when the watched arena enters one of the catalog's echo states, and once it is in
    /// the follow state.
    /// </summary>
    public void Tick() {
        if (_entry?.Follow == null || _watched == null) return;
        var state = _watched.ActiveStateName;
        if (state != _lastState) {
            var echo = _entry.Follow.EchoOn(_lastState, state);
            _lastState = state;
            if (echo != null) _send(_entry.Scene, _entry.Flag, EchoPrefix + echo);
        }

        if (state != _entry.Follow.State) return;

        _send(_entry.Scene, _entry.Flag, _entry.Follow.State);
        Stop();
    }

    /// <summary>
    /// The scene host's arena reached a state: this game's arena goes there if it still waits in one of the catalog's
    /// states before it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void FollowArena(BossEntry entry, string state) {
        if (entry.Follow == null || entry.Follow.State != state) return;
        if (StartGate.FindFsm(entry.Start) is not { } arena || !entry.Follow.From.Split('|').Contains(arena.ActiveStateName)) return;

        arena.SetState(state);
        Debug.Log($"[BossRule] The arena of {entry.Boss} follows the scene host's to {state}");
    }

    /// <summary>
    /// The scene host's arena entered one of the catalog's echo states: this game's arena runs that state's actions
    /// without entering it (it would check this game's own hero, who plays nothing, and end the song at once). A sound's
    /// stop among them stops its source now: its fade needs the state.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void EchoState(BossEntry entry, string state) {
        if (entry.Follow?.Echoes(state) != true) return;
        if (StartGate.FindFsm(entry.Start) is not { } arena) return;
        if (arena.Fsm.States.FirstOrDefault(s => s.Name == state) is not { } echoed) return;

        foreach (var action in echoed.Actions) {
            if (!action.Enabled) continue;

            if (action is AudioStopV2 stop) {
                var source = arena.Fsm.GetOwnerDefaultTarget(stop.gameObject);
                if (source != null && source.TryGetComponent<AudioSource>(out var audio)) audio.Stop();
                continue;
            }

            action.OnEnter();
        }

        Debug.Log($"[BossRule] The arena of {entry.Boss} runs the scene host's {state}");
    }
}
