using System.Collections;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using SSMP.Networking.Packet.Data;
using SSMP.Util;
using UnityEngine;
using Random = UnityEngine.Random;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedParameter.Local
#pragma warning disable CS0618
#pragma warning disable CS8600
#pragma warning disable CS8618

namespace SSMP.Game.Client.Entity.Action;

internal static partial class EntityFsmActions {
    #region SpawnObjectFromGlobalPool

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SpawnObjectFromGlobalPool action) {
        // We first check whether this action results in the spawning of an entity that is managed by the
        // system. Because if so, it would already be handled by an EntitySpawn packet instead, and this will only
        // duplicate the spawning and leave it uncontrolled. So we don't send the data at all
        if (EntitySpawnEvent.Invoke(
                new EntitySpawnDetails {
                    Type = EntitySpawnType.FsmAction,
                    Action = action,
                    GameObject = action.storeObject.Value
                }
            )) {
            //Logger.Debug($"Tried getting SpawnObjectFromGlobalPool network data, but spawned object is entity");
            return false;
        }

        if (action.storeObject.Value is { } spawned) PlainSpawns.Add(spawned.GetInstanceID());

        var position = Vector3.zero;
        var euler = Vector3.up;

        var spawnPoint = action.spawnPoint.Value;
        if (spawnPoint != null) {
            position = spawnPoint.transform.position;
            if (!action.position.IsNone) {
                position += action.position.Value;
            }

            euler = !action.rotation.IsNone ? action.rotation.Value : spawnPoint.transform.eulerAngles;
        } else {
            if (!action.position.IsNone)
                position = action.position.Value;
            if (!action.rotation.IsNone)
                euler = action.rotation.Value;
        }

        data.Packet.Write(position.x);
        data.Packet.Write(position.y);
        data.Packet.Write(position.z);

        data.Packet.Write(euler.x);
        data.Packet.Write(euler.y);
        data.Packet.Write(euler.z);

        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SpawnObjectFromGlobalPool action) {
        var position = new Vector3(
            data.Packet.ReadFloat(),
            data.Packet.ReadFloat(),
            data.Packet.ReadFloat()
        );
        var euler = new Vector3(
            data.Packet.ReadFloat(),
            data.Packet.ReadFloat(),
            data.Packet.ReadFloat()
        );

        if (action.gameObject == null) {
            return;
        }

        // (coop) the scene host's spawned ones show here instead (the Last Judge's charge bombs)
        if (SSMP.Game.Client.Entity.Component.ThrownComponent.SuppressesFlung(action.Fsm.GameObject, action.gameObject.Value)) {
            return;
        }

        // (coop) a silk this copy already showed (LateSilk: its state came before this spawn) is not spawned again
        if (action.gameObject.Value.GetComponent<SilkPossession>() != null && action.storeObject.Value is { } shown
            && shown.activeInHierarchy && shown.GetComponent<SilkPossession>() != null) {
            return;
        }

        var spawnedObject = action.gameObject.Value.Spawn(position, Quaternion.Euler(euler));
        action.storeObject.Value = spawnedObject;
    }

    #endregion

    #region FlingObjectsFromGlobalPool

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, FlingObjectsFromGlobalPool action) {
        // We first check whether the game object belonging to the Rigidbody2D in the action is an object that is
        // managed by the system. Because if so, it means that we have already caught its spawning in the IL hook
        // for the action and sent an EntitySpawn packet instead. So we need not also network this action separately.
        var rigidbody = action.rb2d;
        if (rigidbody != null && rigidbody.gameObject != null && IsObjectInRegistry(rigidbody.gameObject)) {
            //Logger.Debug(
            //    "Skipping getting network data for FlingObjectsFromGlobalPool, because spawned objects are managed by
            // system"
            //);
            return false;
        }

        var position = Vector3.zero;

        var spawnPoint = action.spawnPoint.Value;
        if (spawnPoint != null) {
            position = spawnPoint.transform.position;
            if (!action.position.IsNone) {
                position += action.position.Value;
            }
        } else if (!action.position.IsNone) {
            position = action.position.Value;
        }

        if (!RandomActionValues.TryGetValue(action, out var queue)) {
            return false;
        }

        if (queue.Count == 0) {
            //Logger.Debug("Getting data for FlingObjectsFromGlobalPool has not enough items in queue 1");
            return false;
        }

        data.Packet.Write(position.x);
        data.Packet.Write(position.y);
        data.Packet.Write(position.z);

        var numSpawns = (int) queue.Dequeue();
        data.Packet.Write((byte) numSpawns);

        for (var i = 0; i < numSpawns; i++) {
            if (action.originVariationX != null) {
                if (queue.Count == 0) {
                    //Logger.Debug("Getting data for FlingObjectsFromGlobalPool has not enough items in queue 2");
                    return false;
                }

                var originVariationX = (float) queue.Dequeue();
                data.Packet.Write(originVariationX);
            } else {
                data.Packet.Write(0f);
            }

            if (action.originVariationY != null) {
                if (queue.Count == 0) {
                    //Logger.Debug("Getting data for FlingObjectsFromGlobalPool has not enough items in queue 3");
                    return false;
                }

                var originVariationY = (float) queue.Dequeue();
                data.Packet.Write(originVariationY);
            } else {
                data.Packet.Write(0f);
            }

            if (queue.Count < 2) {
                //Logger.Debug("Getting data for FlingObjectsFromGlobalPool has not enough items in queue 4");
                queue.Clear();
                return false;
            }

            var speed = (float) queue.Dequeue();
            var angle = (float) queue.Dequeue();

            data.Packet.Write(speed);
            data.Packet.Write(angle);
        }

        queue.Clear();
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, FlingObjectsFromGlobalPool action) {
        var position = new Vector3(
            data.Packet.ReadFloat(),
            data.Packet.ReadFloat(),
            data.Packet.ReadFloat()
        );

        var numSpawns = data.Packet.ReadByte();
        for (var i = 0; i < numSpawns; i++) {
            var go = action.gameObject.Value.Spawn(position, Quaternion.Euler(Vector3.zero));

            var originVariationX = data.Packet.ReadFloat();
            position.x += originVariationX;

            var originVariationY = data.Packet.ReadFloat();
            position.y += originVariationY;

            go.transform.position = position;

            var speed = data.Packet.ReadFloat();
            var angle = data.Packet.ReadFloat();

            var x = speed * Mathf.Cos(angle * ((float) System.Math.PI / 180f));
            var y = speed * Mathf.Sin(angle * ((float) System.Math.PI / 180f));

            var rigidBody = go.GetComponent<Rigidbody2D>();
            if (rigidBody == null) {
                return;
            }

            rigidBody.velocity = new Vector2(x, y);

            if (!action.FSM.IsNone) {
                FSMUtility.LocateFSM(go, action.FSM.Value).SendEvent(action.FSMEvent.Value);
            }
        }
    }

    #endregion

    #region FlingObjectsFromGlobalPoolVel

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, FlingObjectsFromGlobalPoolVel action) {
        // We first check whether the game object belonging to the Rigidbody2D in the action is an object that is
        // managed by the system. Because if so, it means that we have already caught its spawning in the IL hook
        // for the action and sent an EntitySpawn packet instead. So we need not also network this action separately.
        var rigidbody = action.rb2d;
        if (rigidbody != null && rigidbody.gameObject != null && IsObjectInRegistry(rigidbody.gameObject)) {
            //Logger.Debug(
            //    "Skipping getting network data for FlingObjectsFromGlobalPool, because spawned objects are managed by
            // system"
            //);
            return false;
        }

        var position = Vector3.zero;

        var spawnPoint = action.spawnPoint.Value;
        if (spawnPoint != null) {
            position = spawnPoint.transform.position;
            if (!action.position.IsNone) {
                position += action.position.Value;
            }
        } else if (!action.position.IsNone) {
            position = action.position.Value;
        }

        if (!RandomActionValues.TryGetValue(action, out var queue)) {
            return false;
        }

        if (queue.Count == 0) {
            //Logger.Debug("Getting data for FlingObjectsFromGlobalPoolVel has not enough items in queue 1");
            return false;
        }

        data.Packet.Write(position.x);
        data.Packet.Write(position.y);
        data.Packet.Write(position.z);

        var numSpawns = (int) queue.Dequeue();
        data.Packet.Write((byte) numSpawns);

        for (var i = 0; i < numSpawns; i++) {
            if (action.originVariationX != null) {
                if (queue.Count == 0) {
                    //Logger.Debug("Getting data for FlingObjectsFromGlobalPoolVel has not enough items in queue 2");
                    return false;
                }

                var originVariationX = (float) queue.Dequeue();
                data.Packet.Write(originVariationX);
            } else {
                data.Packet.Write(0f);
            }

            if (action.originVariationY != null) {
                if (queue.Count == 0) {
                    //Logger.Debug("Getting data for FlingObjectsFromGlobalPoolVel has not enough items in queue 3");
                    return false;
                }

                var originVariationY = (float) queue.Dequeue();
                data.Packet.Write(originVariationY);
            } else {
                data.Packet.Write(0f);
            }

            if (queue.Count < 2) {
                //Logger.Debug("Getting data for FlingObjectsFromGlobalPoolVel has not enough items in queue 4");
                queue.Clear();
                return false;
            }

            var speedX = (float) queue.Dequeue();
            var speedY = (float) queue.Dequeue();

            data.Packet.Write(speedX);
            data.Packet.Write(speedY);
        }

        queue.Clear();
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, FlingObjectsFromGlobalPoolVel action) {
        var position = new Vector3(
            data.Packet.ReadFloat(),
            data.Packet.ReadFloat(),
            data.Packet.ReadFloat()
        );

        var numSpawns = data.Packet.ReadByte();
        for (var i = 0; i < numSpawns; i++) {
            var go = action.gameObject.Value.Spawn(position, Quaternion.Euler(Vector3.zero));

            var originVariationX = data.Packet.ReadFloat();
            position.x += originVariationX;

            var originVariationY = data.Packet.ReadFloat();
            position.y += originVariationY;

            go.transform.position = position;

            var speedX = data.Packet.ReadFloat();
            var speedY = data.Packet.ReadFloat();

            var rigidBody = go.GetComponent<Rigidbody2D>();
            if (rigidBody == null) {
                return;
            }

            rigidBody.velocity = new Vector2(speedX, speedY);
        }
    }

    #endregion

    #region FlingObjectsFromGlobalPoolTime

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, FlingObjectsFromGlobalPoolTime action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, FlingObjectsFromGlobalPoolTime action) {
        var coroutine = MonoBehaviourUtil.Instance.StartCoroutine(Behaviour());

        new ActionInState {
            Fsm = action.Fsm,
            StateName = action.State.Name,
            Coroutine = coroutine
        }.Register();
        return;

        IEnumerator Behaviour() {
            while (true) {
                yield return new WaitForSeconds(action.frequency.Value);

                if (action.gameObject.Value == null) {
                    break;
                }

                var position = Vector3.zero;

                var spawnPoint = action.spawnPoint.Value;
                if (spawnPoint != null) {
                    position = spawnPoint.transform.position;
                    if (!action.position.IsNone) {
                        position += action.position.Value;
                    }
                } else if (!action.position.IsNone) {
                    position = action.position.Value;
                }

                var numSpawns = Random.Range(action.spawnMin.Value, action.spawnMax.Value + 1);
                for (var i = 0; i < numSpawns; i++) {
                    var gameObject = action.gameObject.Value.Spawn(position, Quaternion.Euler(Vector3.zero));

                    if (action.originVariationX != null) {
                        position.x += Random.Range(-action.originVariationX.Value, action.originVariationX.Value);
                    }

                    if (action.originVariationY != null) {
                        position.y += Random.Range(-action.originVariationY.Value, action.originVariationY.Value);
                    }

                    gameObject.transform.position = position;

                    var rigidBody = gameObject.GetComponent<Rigidbody2D>();
                    if (rigidBody == null) {
                        continue;
                    }

                    var speed = Random.Range(action.speedMin.Value, action.speedMax.Value);
                    var angle = Random.Range(action.angleMin.Value, action.angleMax.Value);

                    var x = speed * Mathf.Cos(angle * ((float) System.Math.PI / 180f));
                    var y = speed * Mathf.Sin(angle * ((float) System.Math.PI / 180f));

                    rigidBody.velocity = new Vector2(x, y);
                }
            }
        }
    }

    #endregion

    #region SpawnObjectFromGlobalPoolDelay

    /// <summary>
    /// Builds network data from the FSM action: the scene host's random delay (set in its OnEnter, which runs before
    /// this). The Widow's falling slashes come this way, and the other players never saw them (24 of 45 of her attacks).
    /// </summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SpawnObjectFromGlobalPoolDelay action) {
        if (action.gameObject.Value == null || IsEnemyPrefab(action.gameObject.Value)) {
            return false;
        }

        data.Packet.Write((float) AccessTools.Field(typeof(SpawnObjectFromGlobalPoolDelay), "timer").GetValue(action));
        return true;
    }

    /// <summary>Applies network data to the FSM action: the same object after the same delay, where the copy's spawn
    /// point is then, unless the scene host's FSM left the state first.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SpawnObjectFromGlobalPoolDelay action) {
        var delay = data.Packet.ReadFloat();
        var coroutine = MonoBehaviourUtil.Instance.StartCoroutine(Behaviour());

        new ActionInState {
            Fsm = action.Fsm,
            StateName = action.State.Name,
            Coroutine = coroutine
        }.Register();
        return;

        IEnumerator Behaviour() {
            yield return new WaitForSeconds(delay);

            if (action.gameObject.Value != null && !CopyGone(action)) {
                var (position, euler) = PoolSpawnPlace(action.spawnPoint.Value, action.position, action.rotation, Vector3.zero);
                var spawned = action.gameObject.Value.Spawn();
                spawned.transform.position = position;
                spawned.transform.eulerAngles = euler;
                action.storeObject.Value = spawned;
            }
        }
    }

    #endregion

    #region SpawnObjectFromGlobalPoolOverTime

    /// <summary>Builds network data from the FSM action. The Last Judge's charge drops its bombs this way, and the
    /// other players never saw them (8 of her 68 attacks).</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SpawnObjectFromGlobalPoolOverTime action) =>
        action.gameObject.Value != null && !IsEnemyPrefab(action.gameObject.Value);

    /// <summary>Applies network data to the FSM action: the same object every period while the scene host's FSM stays
    /// in the state, where the copy's spawn point is then.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SpawnObjectFromGlobalPoolOverTime action) {
        // (coop) the scene host's spawned ones show here instead, each where and when the host's was (its charge bombs)
        if (SSMP.Game.Client.Entity.Component.ThrownComponent.SuppressesFlung(action.Fsm.GameObject, action.gameObject.Value)) {
            return;
        }

        var coroutine = MonoBehaviourUtil.Instance.StartCoroutine(Behaviour());

        new ActionInState {
            Fsm = action.Fsm,
            StateName = action.State.Name,
            Coroutine = coroutine
        }.Register();
        return;

        IEnumerator Behaviour() {
            while (action.frequency.Value > 0) {
                yield return new WaitForSeconds(action.frequency.Value);

                if (action.gameObject.Value == null || CopyGone(action)) {
                    break;
                }

                var (position, euler) = PoolSpawnPlace(action.spawnPoint.Value, action.position, action.rotation, Vector3.up);
                action.gameObject.Value.Spawn(position, Quaternion.Euler(euler));
            }
        }
    }

    #endregion

    #region SpawnObjectFromGlobalPoolOverTimeV2

    /// <summary>
    /// (coop) Whether the scene host's over-time spawns of the second kind are copied (a check's twin switches it off
    /// with a patch).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool CopiesOverTimeV2() => true;

    /// <summary>Builds network data from the FSM action. The Rock Roller's trail of Delayed Pop Explosions comes this way
    /// while it rolls or flies, and the other players saw 2 or 3 of its 30 to 50 pops (0.3.21 Task 12, NOTES 367).</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SpawnObjectFromGlobalPoolOverTimeV2 action) =>
        CopiesOverTimeV2() && action.gameObject.Value != null && !IsEnemyPrefab(action.gameObject.Value);

    /// <summary>Applies network data to the FSM action: the same object every period while the scene host's FSM stays
    /// in the state, where the copy's spawn point is then, moved and scaled at random within the action's ranges as
    /// the game's own spawn is.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SpawnObjectFromGlobalPoolOverTimeV2 action) {
        var coroutine = MonoBehaviourUtil.Instance.StartCoroutine(Behaviour());

        new ActionInState {
            Fsm = action.Fsm,
            StateName = action.State.Name,
            Coroutine = coroutine
        }.Register();
        return;

        IEnumerator Behaviour() {
            while (action.frequency.Value > 0) {
                yield return new WaitForSeconds(action.frequency.Value);

                if (action.gameObject.Value == null || CopyGone(action)) {
                    break;
                }

                var (position, euler) = PoolSpawnPlace(action.spawnPoint.Value, action.position, action.rotation, Vector3.up);
                var spawned = action.gameObject.Value.Spawn(position, Quaternion.Euler(euler));
                if (action.originVariationX != null) {
                    spawned.transform.Translate(Random.Range(-action.originVariationX.Value, action.originVariationX.Value), 0f, 0f, Space.World);
                }

                if (action.originVariationY != null) {
                    spawned.transform.Translate(0f, Random.Range(-action.originVariationY.Value, action.originVariationY.Value), 0f, Space.World);
                }

                if (action.scaleMin != null && action.scaleMax != null) {
                    var scale = Random.Range(action.scaleMin.Value, action.scaleMax.Value);
                    if (Mathf.Abs(scale - 1f) > 0.001f) {
                        spawned.transform.localScale = new Vector3(scale, scale, scale);
                    }
                }
            }
        }
    }

    #endregion

    /// <summary>
    /// Whether a pool spawn's prefab is an enemy (in SSMP's registry, with health): spawned in the other games it would
    /// become a second enemy SSMP does not control, so those spawns stay the scene host's. The registry alone also
    /// takes the Last Judge's charge bombs (their name holds hers).
    /// </summary>
    private static bool IsEnemyPrefab(GameObject prefab) =>
        IsObjectInRegistry(prefab) && prefab.GetComponent<HealthManager>() != null;

    /// <summary>
    /// Whether the copy a replayed spawn belongs to is gone (its room unloaded) or hidden (this game took the room
    /// over). Its replay runs on an object that outlives rooms, and the scene host's next state change, which ends it,
    /// never reaches a player who left: a friend who left a Tar Slug's room kept dropping tar in every room after.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    private static bool CopyGone(FsmStateAction action) =>
        action.Fsm.Owner == null || !action.Fsm.Owner.gameObject.activeInHierarchy;

    /// <summary>
    /// Where a pool spawn action puts its object: at its spawn point (moved by its position) turned as the spawn point
    /// or by its rotation, else at its position and rotation.
    /// </summary>
    private static (Vector3 Position, Vector3 Euler) PoolSpawnPlace(GameObject? spawnPoint, FsmVector3 position,
        FsmVector3 rotation, Vector3 defaultEuler) {
        if (spawnPoint != null) {
            var place = spawnPoint.transform.position + (position.IsNone ? Vector3.zero : position.Value);
            return (place, rotation.IsNone ? spawnPoint.transform.eulerAngles : rotation.Value);
        }

        return (position.IsNone ? Vector3.zero : position.Value, rotation.IsNone ? defaultEuler : rotation.Value);
    }
}
