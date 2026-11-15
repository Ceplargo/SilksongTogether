using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using Logger = SSMP.Logging.Logger;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop, 0.3.23 enemies Task 18) Other games: a followed corpse copy held in its fuse's held state (CorpseFuses) until
/// the scene host's corpse goes off. The held state's Wait is set past any fuse; letting go puts its own time back, and,
/// when the scene host's corpse went off, sets the copy off at once.
/// </summary>
internal static class CorpseFuse {
    /// <summary>A copy held: its FSM, the held state's Wait, that Wait's own time and the fuse.</summary>
    private sealed record Hold(PlayMakerFSM Fsm, Wait Wait, float Own, CorpseFuses.Fuse Fuse);

    /// <summary>A held Wait's time: longer than any copy lives.</summary>
    private const float HeldTime = 1e6f;

    private static readonly Dictionary<GameObject, Hold> Held = new();

    /// <summary>Holds a corpse copy this game follows, if its kind's fuse waits for the scene host's.</summary>
    public static void Start(GameObject? corpse) {
        if (corpse == null || Held.ContainsKey(corpse) || CorpseFuses.Of(corpse.name) is not { } fuse) {
            return;
        }

        var fsm = corpse.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == fuse.Fsm);
        var wait = fsm?.FsmStates.FirstOrDefault(s => s.Name == fuse.Held)?.Actions.OfType<Wait>().FirstOrDefault();
        if (fsm == null || wait == null) {
            return;
        }

        Held[corpse] = new Hold(fsm, wait, wait.time.Value, fuse);
        wait.time.Value = HeldTime;
        if (HealthManagerComponent.LogsHealth) {
            Logger.Debug($"(coop) corpse fuse held: {corpse.name}, in {fsm.ActiveStateName}, frame {Time.frameCount}");
        }
    }

    /// <summary>Lets a held copy go: hostsWent, the scene host's corpse went off.</summary>
    public static void Release(GameObject? corpse, bool hostsWent) {
        if (corpse is null || !Held.TryGetValue(corpse, out var hold)) {
            return;
        }

        Held.Remove(corpse);
        hold.Wait.time.Value = hold.Own;
        if (hold.Fsm == null || !hold.Fsm.gameObject.activeInHierarchy) {
            return;
        }

        var active = hold.Fsm.ActiveStateName;
        if (CorpseFuses.ReleaseTo(hold.Fuse, hostsWent, active) is { } next) {
            hold.Fsm.SetState(next);
        }

        if (HealthManagerComponent.LogsHealth) {
            Logger.Debug($"(coop) corpse fuse let go: {corpse.name}, from {active}, the host's went {hostsWent}, frame " +
                         $"{Time.frameCount}");
        }
    }
}
