using System;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using SSMP.Networking.Packet.Data;
using UnityEngine;
using Object = UnityEngine.Object;
using Logger = SSMP.Logging.Logger;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedParameter.Local
#pragma warning disable CS0618
#pragma warning disable CS8600
#pragma warning disable CS8618

namespace SSMP.Game.Client.Entity.Action;

internal static partial class EntityFsmActions {
    #region SetSpecialDeath

    /// <summary>Builds network data from the FSM action: the value the scene host's set (it can come from a variable).</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetSpecialDeath action) {
        if (action.hasSpecialDeath.IsNone) return false;

        data.Packet.Write(action.hasSpecialDeath.Value);
        return true;
    }

    /// <summary>
    /// Applies network data to the FSM action: the copy's special death on or off as the scene host's, so a game that
    /// takes the enemy over has it right. A copy kept the special death a boss starts with, so the copied death was only a
    /// hit there: no corpse, no BOSS DEFEATED, and the Savage Beastfly's gates stayed shut for the friend (the copied death
    /// now follows the host's outcome too).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetSpecialDeath action) {
        var value = data.Packet.ReadBool();
        if (action.Fsm?.GetOwnerDefaultTarget(action.target) is { } target && target.GetComponent<HealthManager>() is { } health) {
            health.hasSpecialDeath = value;
        }
    }

    #endregion

    #region ActivateAllChildren

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(
        EntityNetworkData _,
        HutongGames.PlayMaker.Actions.ActivateAllChildren __
    ) => true;

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(
        EntityNetworkData _,
        HutongGames.PlayMaker.Actions.ActivateAllChildren action
    ) => action.OnEnter();

    #endregion

    #region SetTag

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetTag action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetTag action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        gameObject.tag = action.tag.Value;
    }

    #endregion

    #region DestroyObject

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, DestroyObject action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, DestroyObject action) {
        var gameObject = action.gameObject.Value;
        if (gameObject == null) {
            return;
        }

        var delay = action.delay.Value;
        if (delay <= 0) {
            Object.Destroy(gameObject);
        } else {
            Object.Destroy(gameObject, delay);
        }

        if (action.detachChildren.Value) {
            gameObject.transform.DetachChildren();
        }
    }

    #endregion

    #region SetStringValue

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetStringValue action) {
        return action is { stringVariable: not null, stringValue: not null };
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetStringValue action) {
        action.stringVariable.Value = action.stringValue.Value;
    }

    #endregion

    #region GetRandomChild

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, GetRandomChild action) {
        if (!RandomActionValues.TryGetValue(action, out var queue)) {
            return false;
        }

        if (queue.Count == 0) {
            //Logger.Debug("Getting data for GetRandomChild has not enough items in queue");
            return false;
        }

        var randomIndex = (int) queue.Dequeue();
        data.Packet.Write((byte) randomIndex);

        queue.Clear();

        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, GetRandomChild action) {
        var randomIndex = data.Packet.ReadByte();

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var childCount = gameObject.transform.childCount;
        if (childCount == 0) {
            return;
        }

        action.storeResult.Value = gameObject.transform.GetChild(randomIndex).gameObject;
    }

    #endregion

    #region DestroyComponent

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, DestroyComponent action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, DestroyComponent action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var component = gameObject.GetComponent(ReflectionUtils.GetGlobalType(action.component.Value));
        if (component == null) {
            return;
        }

        // A client-side controller body can be made kinematic, but destroying it invalidates cached references in
        // native death/corpse components. Corpse-owned bodies must remain intact for the native death lifecycle.
        if (component is Rigidbody2D rigidbody) {
            EntityInitializer.ConfigureClientRigidbody(rigidbody);
            return;
        }

        Object.Destroy(component);
    }

    #endregion

    #region AddComponent

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AddComponent action) {
        return !action.removeOnExit.Value;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AddComponent action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var component = gameObject.AddComponent(ReflectionUtils.GetGlobalType(action.component.Value));
        action.storeComponent.Value = component;
    }

    #endregion

    #region PreBuildTK2DSprites

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, PreBuildTK2DSprites action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, PreBuildTK2DSprites action) {
        var gameObject = action.gameObject.Value;
        if (gameObject == null) {
            return;
        }

        var sprites = action.useChildren
            ? gameObject.GetComponentsInChildren<tk2dSprite>(true)
            : gameObject.GetComponents<tk2dSprite>();

        foreach (var sprite in sprites) {
            sprite.ForceBuild();
        }
    }

    #endregion

    #region CallMethodProper

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, CallMethodProper action) {
        //Logger.Debug($"Getting network data for CallMethodProper: {action.Fsm.GameObject.name}, {action.Fsm.Name}");

        return action.Fsm.GameObject.name.StartsWith("Colosseum Manager") &&
               action.Fsm.Name.Equals("Battle Control") ||
               action.Fsm.GameObject.name.StartsWith("Mantis Lord Throne") &&
               action.Fsm.Name.Equals("Mantis Throne Main") ||
               action.Fsm.GameObject.name.Equals("Radiance") &&
               action.Fsm.Name.Equals("Control") ||
               // (coop) a possessed enemy's silk, linked to its enemy (SetEnemy) and played (PlayPossess) in every game:
               // uncopied, the copy's silk had no enemy and recycled itself at once (SilkPossession.Update), so the
               // friend's game showed no strands and played no possess sound (the Pilgrims, NOTES 368)
               action.behaviour.Value == nameof(SilkPossession);
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, CallMethodProper action) {
        if (action.behaviour.Value == null) {
            return;
        }

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        // (coop) a copy made after the scene host's silk spawned has none: spawned here as its FSM would (LateSilk)
        if (LateSilk.Needed(gameObject != null, action.behaviour.Value) && LateSilk.Spawns()) {
            gameObject = SpawnLateSilk(action);
        }

        if (gameObject == null) {
            return;
        }

        var component = gameObject.GetComponent(action.behaviour.Value) as MonoBehaviour;
        if (component == null) {
            return;
        }

        var type = component.GetType();
        var methodInfo = type.GetMethod(action.methodName.Value);
        if (methodInfo == null) {
            return;
        }

        var parameterInfo = methodInfo.GetParameters();

        object obj;
        if (parameterInfo.Length == 0) {
            obj = methodInfo.Invoke(component, null);
        } else {
            var paramArray = new object[action.parameters.Length];

            for (var i = 0; i < action.parameters.Length; i++) {
                var fsmVar = action.parameters[i];
                fsmVar.UpdateValue();
                paramArray[i] = fsmVar.GetValue();
            }

            try {
                obj = methodInfo.Invoke(component, paramArray);
            } catch (Exception e) {
                Logger.Error($"Error applying CallMethodProper:\n{e}");
                return;
            }
        }

        if (action.storeResult.Type == VariableType.Unknown) {
            return;
        }

        action.storeResult.SetValue(obj);
    }

    /// <summary>
    /// The silk of a copy made after the scene host's spawned it: the FSM's own SpawnObjectFromGlobalPool into the
    /// call's Silk variable, run here at its spawn point, then linked to its enemy (SetEnemy), as its Spawn Silk state
    /// would have. Null when the FSM has no such spawn.
    /// </summary>
    private static GameObject? SpawnLateSilk(CallMethodProper action) {
        var spawns = new System.Collections.Generic.List<(string?, (FsmState, SpawnObjectFromGlobalPool))>();
        foreach (var state in action.Fsm.States) {
            foreach (var other in state.Actions) {
                if (other is SpawnObjectFromGlobalPool spawn && spawn.gameObject?.Value != null) {
                    spawns.Add((spawn.storeObject?.Name, (state, spawn)));
                }
            }
        }

        var (inState, found) = LateSilk.Spawner(action.gameObject?.GameObject?.Name, spawns);
        // a silk its spawn state skips (a Quick Wake Pilgrim) the game alone never shows either
        return found == null || SpawnSkipped(found, inState) ? null : SpawnSilk(inState, found, action.Fsm);
    }

    /// <summary>
    /// (coop) A copy's silk shown once it learns the scene host's FSM is in the state its silk's spawn leads to (the
    /// Pilgrim's Pray, LateSilk.OutIn): its own spawn into an empty Silk variable, run here, linked to its enemy.
    /// </summary>
    internal static void ShowLateSilk(HutongGames.PlayMaker.Fsm fsm, string stateName) {
        // a copy not out (another world's Pilgrim: the Black Thread World's, off in the scene host's game too) shows none
        if (fsm.GameObject == null || !fsm.GameObject.activeInHierarchy) return;
        foreach (var state in fsm.States) {
            foreach (var other in state.Actions) {
                if (other is not SpawnObjectFromGlobalPool spawn || spawn.gameObject?.Value == null
                    || spawn.gameObject.Value.GetComponent<SilkPossession>() == null
                    || string.IsNullOrEmpty(spawn.storeObject?.Name) || spawn.storeObject.Value != null) continue;
                if (!LateSilk.OutIn(stateName, state.Transitions.Select(t => ((string?) t.EventName, (string?) t.ToState)))
                    || SpawnSkipped(spawn, state)) {
                    continue;
                }

                var silk = SpawnSilk(state, spawn, fsm);
                Logger.Info($"(coop) late silk shown: {fsm.GameObject.name} in {stateName}, {silk.name} at {silk.transform.position}");
            }
        }
    }

    /// <summary>
    /// Whether the spawn's state leaves before it (LateSilk.Skipped): its bool tests ahead of the spawn, read here as they
    /// would read in this game (its own FSM's bools, the shared save's PlayerData).
    /// </summary>
    // (spawn first: two parameters with an action second are taken for a Get/Apply method by the type initializer)
    private static bool SpawnSkipped(SpawnObjectFromGlobalPool spawn, FsmState state) {
        var events = new System.Collections.Generic.List<string?>();
        foreach (var ahead in state.Actions) {
            if (ahead == spawn) break;
            switch (ahead) {
                case BoolTest test:
                    events.Add((test.boolVariable.Value ? test.isTrue : test.isFalse)?.Name);
                    break;
                case PlayerDataBoolTest test when global::GameManager.instance != null:
                    events.Add((global::GameManager.instance.GetPlayerDataBool(test.boolName.Value) ? test.isTrue : test.isFalse)?.Name);
                    break;
            }
        }

        return LateSilk.Skipped(events);
    }

    /// <summary>
    /// The silk of the FSM's spawn action, at its spawn point, stored in its variable, put under its enemy where the state
    /// does that (SetParent of the silk) and linked to it.
    /// </summary>
    private static GameObject SpawnSilk(FsmState state, SpawnObjectFromGlobalPool found, HutongGames.PlayMaker.Fsm fsm) {
        var point = found.spawnPoint.Value != null ? found.spawnPoint.Value.transform.position
            : fsm.GameObject.transform.position;
        if (!found.position.IsNone) point += found.position.Value;

        var silk = found.gameObject.Value.Spawn(point, Quaternion.identity);
        found.storeObject.Value = silk;
        if (state.Actions.Any(a => a is SetParent parent && parent.gameObject?.GameObject?.Name == found.storeObject.Name)) {
            silk.transform.SetParent(fsm.GameObject.transform, true);
        }

        if (silk.GetComponent<SilkPossession>() is { } possession) possession.SetEnemy(fsm.GameObject);
        return silk;
    }

    #endregion

    #region SendMessage

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SendMessage action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SendMessage action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        object parameter = action.functionCall.ParameterType switch {
            "Array" => action.functionCall.ArrayParameter.Values,
            "Color" => action.functionCall.ColorParameter.Value,
            "Enum" => action.functionCall.EnumParameter.Value,
            "GameObject" => action.functionCall.GameObjectParameter.Value,
            "Material" => action.functionCall.MaterialParameter.Value,
            "Object" => action.functionCall.ObjectParameter.Value,
            "Quaternion" => action.functionCall.QuaternionParameter.Value,
            "Rect" => action.functionCall.RectParamater.Value,
            "Texture" => action.functionCall.TextureParameter.Value,
            "Vector2" => action.functionCall.Vector2Parameter.Value,
            "Vector3" => action.functionCall.Vector3Parameter.Value,
            "bool" => action.functionCall.BoolParameter.Value,
            "float" => action.functionCall.FloatParameter.Value,
            "int" => action.functionCall.IntParameter.Value,
            "string" => action.functionCall.StringParameter.Value,
            _ => null
        };

        switch (action.delivery) {
            case SendMessage.MessageType.SendMessage:
                gameObject.SendMessage(action.functionCall.FunctionName, parameter, action.options);
                break;
            case SendMessage.MessageType.SendMessageUpwards:
                gameObject.SendMessageUpwards(action.functionCall.FunctionName, parameter, action.options);
                break;
            case SendMessage.MessageType.BroadcastMessage:
                gameObject.BroadcastMessage(action.functionCall.FunctionName, parameter, action.options);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    #endregion

    #region EndGGBossScene

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, EndGGBossScene action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, EndGGBossScene action) {
        if (BossSceneController.Instance) {
            BossSceneController.Instance.EndBossScene();
        }
    }

    #endregion
}
