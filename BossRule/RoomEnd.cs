using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace BossRule;

/// <summary>
/// A room that ends on its own FSM's state, not a wave arena's (the Widow's: after her memory, the player who woke rings
/// her shrine, and only then does her room reach Set Sequence Completed, whose BG OPEN opens her gates). That runs in the
/// ringer's game only, so the friend's gates stayed shut and kept the friend in her room. Once this game's end FSM is in
/// the catalog's end state, the others in the room are told (once a room load); a game whose end FSM still waits in one
/// of the catalog's states before it goes there too, whichever game is the scene host by then (the ringer left the room
/// for her memory).
/// </summary>
internal sealed class RoomEnd {
    /// <summary>A follow message's state that is a room's end: "End: " and the state.</summary>
    public const string Prefix = "End: ";

    private readonly Action<string, string, string> _send;
    private string? _scene;
    private BossEntry? _entry;
    private bool _told;

    /// <param name="send">Sends that a scene's room (by its boss's flag) reached a state.</param>
    public RoomEnd(Action<string, string, string> send) => _send = send;

    /// <summary>
    /// Once per frame while connected: tell the others once this room's end FSM is in its end state.
    /// </summary>
    public void Tick(IReadOnlyList<BossEntry> bosses, string? scene) {
        if (scene != _scene) {
            _scene = scene;
            _entry = bosses.FirstOrDefault(e => e.Scene == scene && e.End != null);
            _told = false;
        }

        if (_told || _entry?.End is not { } end || EndFsm(end) is not { } fsm || fsm.ActiveStateName != end.State) return;
        _told = true;
        _send(_entry.Scene, _entry.Flag, Prefix + end.State);
    }

    /// <summary>
    /// Another game's room reached its end: this game's goes there too if it still waits before it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void Follow(BossEntry entry, string state) {
        if (entry.End is not { } end || end.State != state || EndFsm(end) is not { } fsm || !end.From.Contains(fsm.ActiveStateName)) {
            return;
        }

        fsm.SetState(state);
        Debug.Log($"[BossRule] The room of {entry.Boss} ends here as in another player's game ({state})");
    }

    private static PlayMakerFSM? EndFsm(BossEnd end) {
        var owner = GameObject.Find(end.Object);
        return owner == null ? null : owner.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == end.Fsm);
    }
}
