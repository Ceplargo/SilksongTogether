using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using SSMP.Networking.Packet.Data;
using SSMP.Util;
using UnityEngine;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedParameter.Local
#pragma warning disable CS0618
#pragma warning disable CS8600
#pragma warning disable CS8618

namespace SSMP.Game.Client.Entity.Action;

internal static partial class EntityFsmActions {
    #region SetFsmBool

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetFsmBool action) {
        if (action.setValue == null) {
            return false;
        }

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == action.Fsm.GameObject) {
            return false;
        }

        var setValue = action.setValue.Value;
        data.Packet.Write(setValue);

        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetFsmBool action) {
        var setValue = data.Packet.ReadBool();

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var fsm = ActionHelpers.GetGameObjectFsm(gameObject, action.fsmName.Value);
        if (fsm == null) {
            return;
        }

        var fsmBool = fsm.FsmVariables.FindFsmBool(action.variableName.Value);

        fsmBool?.Value = setValue;
    }

    #endregion

    #region SetFsmInt

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetFsmInt action) {
        if (action.setValue == null) {
            return false;
        }

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == action.Fsm.GameObject) {
            return false;
        }

        var setValue = action.setValue.Value;
        data.Packet.Write(setValue);

        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetFsmInt action) {
        var setValue = data.Packet.ReadInt();

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var fsm = ActionHelpers.GetGameObjectFsm(gameObject, action.fsmName.Value);
        if (fsm == null) {
            return;
        }

        var fsmInt = fsm.FsmVariables.GetFsmInt(action.variableName.Value);

        fsmInt?.Value = setValue;
    }

    #endregion

    #region SetFsmFloat

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetFsmFloat action) {
        // TODO: if action.setValue can be a reference, make sure to network it
        if (action.setValue == null) {
            return false;
        }

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        return gameObject != action.Fsm.GameObject;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetFsmFloat action) {
        if (action.setValue == null) {
            return;
        }

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var fsm = ActionHelpers.GetGameObjectFsm(gameObject, action.fsmName.Value);
        if (fsm == null) {
            return;
        }

        var fsmFloat = fsm.FsmVariables.GetFsmFloat(action.variableName.Value);

        fsmFloat?.Value = action.setValue.Value;
    }

    #endregion

    #region SetFsmString

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetFsmString action) {
        // TODO: if action.setValue can be a reference, make sure to network it
        if (action.setValue == null) {
            return false;
        }

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        return gameObject != action.Fsm.GameObject;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetFsmString action) {
        if (action.setValue == null) {
            return;
        }

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var fsm = ActionHelpers.GetGameObjectFsm(gameObject, action.fsmName.Value);
        if (fsm == null) {
            return;
        }

        var fsmString = fsm.FsmVariables.GetFsmString(action.variableName.Value);
        if (fsmString == null) {
            return;
        }

        fsmString.Value = action.setValue.Value;
    }

    #endregion

    #region SendEventByName

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SendEventByName action) {
        return action.eventTarget.gameObject.GameObject.Value != action.Fsm.GameObject.gameObject;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SendEventByName action) {
        if (action.delay.Value < 1.0 / 1000.0) {
            SendCopiedEvent(action.Fsm, action.eventTarget, action.sendEvent.Value);
        } else {
            // We need to delay the event sending ourselves, because the FSM that we are executing in is not enabled
            // The usual implementation of SendEventByName will thus not work
            MonoBehaviourUtil.Instance.StartCoroutine(DelayEvent());

            IEnumerator DelayEvent() {
                yield return new WaitForSeconds(action.delay.Value);

                SendCopiedEvent(action.Fsm, action.eventTarget, action.sendEvent.Value);
            }
        }
    }

    /// <summary>
    /// Send a copied FSM event. The copy is made before its original's start-up has run, so an FSM variable that
    /// start-up fills with a scene object (the Dock Guard Slasher's "Boss Scene", where it sends LAVA START and its
    /// calls for minions and the Thrower) is empty on the copy: such a target is taken from the original's FSM, so the
    /// other players' arena moves on with the scene host's.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    private static void SendCopiedEvent(HutongGames.PlayMaker.Fsm fsm, FsmEventTarget eventTarget, string eventName) {
        if (eventTarget.target is FsmEventTarget.EventTarget.GameObject or FsmEventTarget.EventTarget.GameObjectFSM &&
            eventTarget.gameObject != null) {
            TargetOf(fsm, eventTarget.gameObject);
        }

        fsm.Event(eventTarget, eventName);
    }

    /// <summary>
    /// The object a copy's action aims at. A variable that is empty on the copy is filled from the original's FSM
    /// first (the copy is made before the original's start-up fills such variables with scene objects).
    /// </summary>
    private static GameObject? TargetOf(HutongGames.PlayMaker.Fsm fsm, FsmOwnerDefault target) =>
        target.OwnerOption == OwnerDefaultOption.UseOwner ? fsm.GameObject : TargetOf(fsm, target.GameObject);

    /// <summary>The object a copy's object variable holds, filled from the original's FSM as above.</summary>
    private static GameObject? TargetOf(HutongGames.PlayMaker.Fsm fsm, FsmGameObject? variable) {
        if (variable is { UseVariable: true, Value: null } &&
            Entity.OriginalOf(fsm.GameObject) is { } original &&
            original.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == fsm.Name) is { } originalFsm &&
            originalFsm.FsmVariables.FindFsmGameObject(variable.Name)?.Value is { } found) {
            variable.Value = found;
        }

        return variable?.Value;
    }

    #endregion

    #region SendEventByNameV2

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SendEventByNameV2 action) {
        return action.eventTarget.gameObject.GameObject.Value != action.Fsm.GameObject.gameObject;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SendEventByNameV2 action) {
        if (action.delay.Value < 1.0 / 1000.0) {
            SendCopiedEvent(action.Fsm, action.eventTarget, action.sendEvent.Value);
        } else {
            // We need to delay the event sending ourselves, because the FSM that we are executing in is not enabled
            // The usual implementation of SendEventByNameV2 will thus not work
            MonoBehaviourUtil.Instance.StartCoroutine(DelayEvent());

            IEnumerator DelayEvent() {
                yield return new WaitForSeconds(action.delay.Value);

                SendCopiedEvent(action.Fsm, action.eventTarget, action.sendEvent.Value);
            }
        }
    }

    #endregion

    #region SendHealthManagerDeathEvent

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SendHealthManagerDeathEvent action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SendHealthManagerDeathEvent action) {
        var gameObject = action.target.OwnerOption == OwnerDefaultOption.UseOwner
            ? action.Owner
            : action.target.GameObject.Value;

        if (gameObject == null) {
            return;
        }

        var healthManager = gameObject.GetComponent<HealthManager>();
        if (healthManager == null) {
            return;
        }

        healthManager.SendDeathEvent();
    }

    #endregion
}
