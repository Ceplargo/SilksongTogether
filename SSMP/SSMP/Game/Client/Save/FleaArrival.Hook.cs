using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = SSMP.Logging.Logger;

namespace SSMP.Game.Client.Save;

internal static partial class FleaArrival {
    /// <summary>
    /// After another player's flea value is set here: the room's flea branches, if this player is in that room and
    /// they still hold the flea, get their own BREAK, the event a heavy hit sends, which plays their Cut and the flea's
    /// rescue and flight (Flea Rescue Activation). Only branches whose Idle breaks on BREAK (Flea Rescue Branches);
    /// the other flea rescues (barrel, cage, sleeping) wait for their own round.
    /// </summary>
    public static void Apply(string name, bool value) {
        if (RoomOf(name) is not { } room) {
            return;
        }

        var here = SceneManager.GetActiveScene().name;
        foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)) {
            if (fsm.FsmName != "Control" || !fsm.gameObject.name.StartsWith("Flea Rescue") ||
                fsm.gameObject.scene.name != room ||
                fsm.Fsm.GetState("Idle")?.Transitions.Any(t => t.EventName == "BREAK" && t.ToState == "Cut") != true) {
                continue;
            }

            if (Plays(value, room, here, fsm.ActiveStateName)) {
                Logger.Info($"(coop) {name} arrived freed: {fsm.gameObject.name} breaks out here");
                fsm.SendEvent("BREAK");
            }
        }
    }
}
