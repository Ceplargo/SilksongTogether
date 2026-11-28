using UnityEngine;
using Logger = SSMP.Logging.Logger;

namespace SSMP.Game.Client.Save;

internal static partial class TollArrival {
    /// <summary>
    /// After another player's PlayerData bool is set here: each loaded toll machine whose value it is, still waiting in
    /// Inert, starts its paid sequence (Sequence Animation, then Open Floor's Gate.Open, Map Appear and Allow Bellbeast
    /// Call), the states the payer's machine plays after Return Control set the value. Return Control itself is skipped:
    /// it ends this player's dialogue, takes control back and checks achievements, which are the payer's. Its one step
    /// that is the machine's, ActivateInteractible off, is done here: else this player's machine still takes a press
    /// after its sequence, with no state left to answer it.
    /// </summary>
    public static void Apply(string name, bool value) {
        foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)) {
            if (fsm.FsmName != Fsm || fsm.Fsm.GetState(Start) == null ||
                !Plays(value, name, fsm.FsmVariables.FindFsmString(Variable)?.Value, fsm.ActiveStateName)) {
                continue;
            }

            Logger.Info($"(coop) {name} arrived set: {fsm.gameObject.name} plays its paid sequence here");
            fsm.GetComponent<InteractableBase>()?.Deactivate(false);
            fsm.SetState(Start);
        }
    }
}
