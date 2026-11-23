using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HutongGames.PlayMaker;
using SSMP.Api.Client;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (coop) The Lava Challenge's entrance door waits, as an ordinary arena does (ArenaHold), up to WaitRule.RoomWaitS for
/// every hero in its room to be inside its Inside Detector before it shuts (the decision "Ordinary arenas wait up to 5 s
/// for everyone in the room": "The Lava Challenge's door is World A's, with the same 5 s"). Each game's door shuts on its
/// own hero's walk-in (its detector's ENTER, LavaDoor.IsWalkIn); held, the door stays open and the boss wait line shows,
/// then this game sends the ENTER itself. A hero still outside after the wait walks in later and its own door shuts then,
/// as before. The game alone holds nothing.
/// </summary>
internal static class LavaHold {
    /// <summary>SSMP's client manager; set by the client add-on.</summary>
    public static IClientManager? Client;

    /// <summary>Sends the waiting line for a scene to the server ("" once nobody is missing); set by the client add-on.</summary>
    public static Action<string, string>? SendStatus;

    private static Fsm? _held;
    private static string _event = "";
    private static float _heldAt;
    private static string _message = "";
    private static bool _letting;

    /// <summary>Whether the door waits. Not inlined: lava_wait's twin switches it off with a patch.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool HoldsLavaDoor() => true;

    /// <summary>Prefix on <c>Fsm.ProcessEvent(FsmEvent, FsmEventData)</c>: hold the door's walk-in while a hero is out.</summary>
    public static bool ProcessEventPrefix(Fsm __instance, FsmEvent fsmEvent) {
        if (_letting || fsmEvent == null || __instance.Owner is not PlayMakerFSM owner ||
            !LavaDoor.IsWalkIn(owner.gameObject.scene.name, owner.gameObject.name, __instance.ActiveStateName, fsmEvent.Name) ||
            !HoldsLavaDoor() || !InSession() || Client is not { } client) {
            return true;
        }

        if (Evaluate(owner, client).Allowed) {
            if (_held == __instance) Clear();
            return true;
        }

        if (_held != __instance) {
            Clear();
            _held = __instance;
            _event = fsmEvent.Name;
            _heldAt = Time.unscaledTime;
            Debug.Log("[BossRule] Holding the Lava Challenge's door until everyone in the room is inside");
        }

        return false;
    }

    /// <summary>Once per frame: the waiting line, and the held walk-in sent once everyone is in or the wait is over.</summary>
    public static void Tick() {
        if (_held == null) return;
        if (_held.Owner is not PlayMakerFSM owner || !owner || !owner.isActiveAndEnabled ||
            _held.ActiveStateName != "Wait For Trigger" || !InSession() || Client is not { } client) {
            Clear();
            return;
        }

        var result = Evaluate(owner, client);
        var heldS = Time.unscaledTime - _heldAt;
        var go = WaitRule.RoomMayStart(result, heldS);
        var message = go ? "" : WaitRule.RoomMessage(result, heldS);
        if (message != _message) {
            _message = message;
            SendStatus?.Invoke(LavaDoor.Scene, message);
        }

        if (!go) return;

        var fsm = _held;
        var name = _event;
        Debug.Log(result.Allowed
            ? $"[BossRule] Everyone in {LavaDoor.Scene} is inside: its door shuts after {heldS:F1} s"
            : $"[BossRule] The Lava Challenge's door waited {WaitRule.RoomWaitS:F0} s: it shuts " +
              $"({string.Join(", ", result.Missing)} still outside)");
        Clear();
        _letting = true;
        try {
            fsm.Event(FsmEvent.GetFsmEvent(name));
        } finally {
            _letting = false;
        }
    }

    /// <summary>In a session (ArenaWins' scene host tests are true only then): the game alone holds nothing.</summary>
    private static bool InSession() => ArenaWins.SceneHost?.Invoke() == true || ArenaWins.NotSceneHost?.Invoke() == true;

    private static void Clear() {
        if (_message != "") SendStatus?.Invoke(LavaDoor.Scene, "");
        _message = "";
        _held = null;
    }

    private static WaitResult Evaluate(PlayMakerFSM door, IClientManager client) {
        var area = door.gameObject.scene.GetRootGameObjects()
            .SelectMany(o => o.GetComponentsInChildren<Collider2D>(true))
            .Where(c => c != null && c.gameObject.name == LavaDoor.Detector).ToArray();
        bool Inside(Vector3 p) => area.Length == 0 || area.Any(c => Contains(Presence.ArenaBounds(c), p));

        var hero = HeroController.instance;
        var players = new List<(PlayerPresence, bool)> {
            (new PlayerPresence(Presence.LocalId, client.Username, true, hero != null && Inside(hero.transform.position)), true)
        };
        players.AddRange(client.Players.Select(p => (new PlayerPresence(p.Id, p.Username, true,
            p.IsInLocalScene && p.PlayerObject != null && Inside(p.PlayerObject.transform.position)), p.IsInLocalScene)));
        return WaitRule.EvaluateRoom(players);
    }

    private static bool Contains(Bounds b, Vector3 p) =>
        p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;
}
