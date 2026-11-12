using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MonoMod.RuntimeDetour;
using SSMP.Collection;
using SSMP.Fsm;
using SSMP.Game.Client.Entity.Action;
using SSMP.Game.Client.Entity.Component;
using SSMP.Networking.Client;
using SSMP.Networking.Packet.Data;
using SSMP.Util;
using UnityEngine;
using Logger = SSMP.Logging.Logger;
using Math_Vector2 = SSMP.Math.Vector2;

//using Logger = SSMP.Logging.Logger;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning disable CS0414 // Field is assigned but its value is never used

namespace SSMP.Game.Client.Entity;

/// <summary>
/// A networked entity that is either sending behaviour updates to the server or is entirely controlled by
/// updates from the server.
/// </summary>
internal class Entity {
    /// <summary>
    /// The scene object each client object made at the scene's root stands in for.
    /// </summary>
    private static readonly ConditionalWeakTable<GameObject, GameObject> Originals = new();

    /// <summary>
    /// The net client for networking.
    /// </summary>
    private readonly NetClient _netClient;

    /// <summary>
    /// MonoMod hook for tk2dSpriteAnimator.Play.
    /// </summary>
    private Hook? _spriteAnimatorPlayHook;

    /// <summary>
    /// MonoMod hook for ObjectPool.Recycle.
    /// </summary>
    private Hook? _objectPoolRecycleHook;

    /// <summary>
    /// MonoMod hook for ActivateGameObject.OnEnter.
    /// </summary>
    private Hook? _activateGameObjectHook;

    /// <summary>
    /// Whether the entity has a parent entity.
    /// </summary>
    private readonly bool _hasParent;
    private readonly bool _nativeFlock;
    private readonly bool _uninterruptedStartup;

    /// <summary>
    /// The ID of the entity.
    /// </summary>
    public ushort Id { get; }

    /// <summary>
    /// The type of the entity.
    /// </summary>
    public EntityType Type { get; }

    /// <summary>
    /// Host-client pair for the game objects.
    /// </summary>
    public HostClientPair<GameObject> Object { get; }

    /// <summary>
    /// Gets the list of PlayMaker FSMs on the host game object.
    /// </summary>
    public List<PlayMakerFSM> HostFsms => _fsms.Host;

    /// <summary>
    /// Gets the list of PlayMaker FSMs on the client game object.
    /// </summary>
    public List<PlayMakerFSM> ClientFsms => _fsms.Client;

    /// <summary>
    /// Host-client pair for the sprite animators.
    /// </summary>
    private readonly HostClientPair<tk2dSpriteAnimator> _animator;

    /// <summary>
    /// Bi-directional lookup for animation clip names to IDs.
    /// </summary>
    private readonly BiLookup<string, byte> _animationClipNameIds;

    /// <summary>
    /// Host-client pair for the lists of FSMs on the entity.
    /// </summary>
    private readonly HostClientPair<List<PlayMakerFSM>> _fsms;

    /// <summary>
    /// Dictionary mapping data types to entity components.
    /// </summary>
    private readonly Dictionary<EntityComponentType, EntityComponent> _components;

    /// <summary>
    /// The component that moves copies of what this enemy spawns where the scene host's go, if it spawns from the pool.
    /// </summary>
    private ThrownComponent? Thrown =>
        _components.TryGetValue(EntityComponentType.Thrown, out var component) ? component as ThrownComponent : null;

    private FlockComponent? Flock =>
        _components.TryGetValue(EntityComponentType.Flock, out var component) ? component as FlockComponent : null;

    /// <summary>
    /// Unique list of components that require periodic update calls on the host.
    /// </summary>
    private readonly List<EntityComponent> _updatableComponents;

    /// <summary>
    /// Dictionary mapping FSM actions to their entity action data instances.
    /// </summary>
    private readonly Dictionary<FsmStateAction, HookedEntityAction> _hookedActions;

    /// <summary>
    /// (coop) Each host FSM's Fsm as its actions were hooked; a template FSM's Init replaces it (TemplateRehook).
    /// </summary>
    private readonly Dictionary<PlayMakerFSM, HutongGames.PlayMaker.Fsm> _hookedFsms = new();

    /// <summary>
    /// Set of FSM action types that have been hooked to prevent duplicate hooks.
    /// </summary>
    private readonly HashSet<Type> _hookedTypes;

    /// <summary>
    /// Whether the unity game object for the host entity was originally active.
    /// </summary>
    private bool _originalIsActive;

    /// <summary>
    /// Whether the entity is controlled, i.e. in control by updates from the server.
    /// </summary>
    private bool _isControlled;

    /// <summary>
    /// Whether the scene host is determined, or alternatively whether the entity has been determined to be a host or client entity.
    /// </summary>
    private bool _isSceneHostDetermined;

    /// <summary>
    /// The last position of the entity.
    /// </summary>
    private Vector3 _lastPosition;

    /// <summary>
    /// The last scale of the entity.
    /// </summary>
    private Vector3 _lastScale;
    private float _lastScaleRefresh;

    /// <summary>
    /// Whether the game object for the entity was last active.
    /// </summary>
    private bool _lastIsActive;

    /// <summary>
    /// Whether to allow the client entity to animate itself.
    /// </summary>
    private bool _allowClientAnimation;
    private uint _childAnimationEpoch;
    private ushort _childAnimationOrder;
    private int _lastChildAnimationOrder = -1;

    /// <summary>
    /// List of snapshots for each FSM of a host entity that contain latest values for state and FSM variables.
    /// Used to check whether state/variables change and to update the server accordingly.
    /// </summary>
    private readonly List<FsmSnapshot> _fsmSnapshots;

    public Entity(
        NetClient netClient,
        ushort id,
        EntityType type,
        GameObject hostObject,
        GameObject clientObject = null,
        params EntityComponentType[] types
    ) {
        _netClient = netClient;
        Id = id;

        Type = type;
        _nativeFlock = type == EntityType.Brushflit && FlockCopies.TracksFlockCopies();
        _uninterruptedStartup = _nativeFlock || NativeStartup.KeepsHiveRunning(type);

        _isControlled = true;

        if (clientObject == null) {
            Object = new HostClientPair<GameObject> {
                Host = hostObject,
                Client = UnityEngine.Object.Instantiate(
                    hostObject,
                    hostObject.transform.position,
                    hostObject.transform.rotation
                )
            };

            DestroyManagedChildren(Object.Client);
            Originals.Add(Object.Client, hostObject);

            _hasParent = false;
        } else {
            Object = new HostClientPair<GameObject> {
                Host = hostObject,
                Client = clientObject
            };

            _hasParent = true;
        }

        Object.Client.transform.localScale = _lastScale = _hasParent
            ? Object.Host.transform.localScale
            : Object.Host.transform.lossyScale;

        // Store whether the host object was active and set it not active until we know if we are scene host
        _originalIsActive = Object.Host.activeSelf;

        _lastIsActive = _hasParent ? Object.Host.activeSelf : Object.Host.activeInHierarchy;

        //Logger.Info(
        //    $"Entity '{Object.Host.name}' was original active: {_originalIsActive}, last active: {_lastIsActive}"
        //);

        // Add a position interpolation component to the enemy so we can smooth out position updates
        // (coop, 0.3.21) not carried past the last place sent (CopyStopRule)
        Object.Client.AddComponent<PredictiveInterpolation>().EnemyCopy = true;

        // Register an update event to send position updates and check for certain value changes
        MonoBehaviourUtil.Instance.OnUpdateEvent += OnUpdate;

        _animator = new HostClientPair<tk2dSpriteAnimator> {
            Host = Object.Host.GetComponent<tk2dSpriteAnimator>(),
            Client = Object.Client.GetComponent<tk2dSpriteAnimator>()
        };
        ListenToWalker();
        CopyPresses.Listen(Object.Host, SendBonePress);
        if (_animator.Host != null) {
            _animationClipNameIds = new BiLookup<string, byte>();

            var index = 0;
            foreach (var animationClip in _animator.Host.Library.clips) {
                if (_animationClipNameIds.ContainsFirst(animationClip.name)) {
                    continue;
                }

                _animationClipNameIds.Add(animationClip.name, (byte) index++);

                if (index > byte.MaxValue) {
                    //Logger.Error($"Too many animation clips to fit in a byte for entity: {Object.Client.name}");
                    break;
                }
            }

            _spriteAnimatorPlayHook = new Hook(
                typeof(tk2dSpriteAnimator).GetMethod(
                    nameof(tk2dSpriteAnimator.Play),
                    [typeof(tk2dSpriteAnimationClip), typeof(float), typeof(float)]
                ),
                OnAnimationPlayed
            );
        }

        // Always disallow the client object from being recycled, because it will simply be destroyed
        _objectPoolRecycleHook = new Hook(
            typeof(ObjectPool).GetMethod(
                nameof(ObjectPool.Recycle),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null,
                [typeof(GameObject)],
                null
            ),
            ObjectPoolOnRecycleGameObject
        );

        // Register a hook for the ActivateGameObject action to update the active state of the host game object
        // before scene host is determined
        _activateGameObjectHook = new Hook(
            typeof(ActivateGameObject).GetMethod(
                nameof(ActivateGameObject.OnEnter),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            ),
            OnDoActivateGameObject
        );

        _fsms = new HostClientPair<List<PlayMakerFSM>> {
            Host = Object.Host.GetComponents<PlayMakerFSM>().ToList(),
            Client = Object.Client.GetComponents<PlayMakerFSM>().ToList()
        };

        _hookedActions = new Dictionary<FsmStateAction, HookedEntityAction>();
        _hookedTypes = [];
        _fsmSnapshots = [];
        foreach (var fsm in _fsms.Host) {
            ProcessHostFsm(fsm);
        }

        // Remove all components that (re-)activate FSMs
        foreach (var fsmActivator in Object.Client.GetComponents<FSMActivator>()) {
            fsmActivator.StopAllCoroutines();
            UnityEngine.Object.Destroy(fsmActivator);
        }

        foreach (var fsm in _fsms.Client) {
            ProcessClientFsm(fsm);
        }

        _components = new Dictionary<EntityComponentType, EntityComponent>();
        HandleComponents(types);

        // (coop) a corpse that follows the scene host's goes through the thrown component (HealthManagerComponent)
        if (_components.TryGetValue(EntityComponentType.Health, out var health) && health is HealthManagerComponent hm &&
            Thrown is { } thrown) {
            hm.TrackCorpse = corpse => thrown.Track(corpse);
            hm.FollowCorpse = thrown.Follow;
        }

        // (coop) its hurt boxes polled as it dies, before the death is sent (DeathOrder.ShowsLastBox)
        if (health is HealthManagerComponent dying &&
            _components.TryGetValue(EntityComponentType.DamageHero, out var boxes)) {
            dying.PollBoxes = boxes.OnUpdate;
        }

        HandleEnemyDeathEffects();

        _updatableComponents = new List<EntityComponent>();
        foreach (var component in _components.Values) {
            if (component != null &&
                component is not HealthManagerComponent &&
                !_updatableComponents.Contains(component)) {
                _updatableComponents.Add(component);
            }
        }

        // Pausing here rerolls flock presence and loses the hive's next-frame startup event.
        if (!_uninterruptedStartup) Object.Host.SetActive(false);
        Object.Client.SetActive(false);

        // // Debug code that logs each action's OnEnter method call
        // foreach (var fsm in _fsms.Host) {
        //     foreach (var state in fsm.FsmStates) {
        //         foreach (var action in state.Actions) {
        //             FsmActionHooks.RegisterFsmStateActionType(action.GetType(), stateAction => {
        //                 if (stateAction != action) {
        //                     return;
        //                 }
        //
        //                 Logger.Debug($"Entity ({Id}, {Type}) has host FSM enter action: {state.Name}, {action.GetType()}, {state.Actions.ToList().IndexOf(action)}");
        //             });
        //         }
        //     }
        // }
    }

    /// <summary>
    /// The scene object a client object stands in for, or null for any other object.
    /// </summary>
    public static GameObject? OriginalOf(GameObject copy) => Originals.TryGetValue(copy, out var original) ? original : null;

    /// <summary>
    /// The parent of the given transform; for a client object made at the scene's root, the parent of the scene object
    /// it stands in for, so its FSM's lookups find what the original's would (a boss's stage, for one).
    /// </summary>
    public static Transform? ParentOf(Transform transform) =>
        transform.parent == null && Originals.TryGetValue(transform.gameObject, out var original) && original != null
            ? original.transform.parent
            : transform.parent;

    /// <summary>
    /// Destroy the children of the given game object that are registered entities in the system themselves.
    /// Recursively go through the non-registered children as well.
    /// </summary>
    /// <param name="root">The root game object to start searching for children.</param>
    /// <summary>
    /// The corpses a copy keeps although they match the entity registry by the enemy's own name: their FSM ends the fight
    /// (the Last Judge's JUDGE DEFEATED, Sister Splinter's BOSS DEFEAT), so without them the friend's arena stayed shut;
    /// (coop) or the corpse itself explodes and hurts players near it, with no drops (a Tar Slug's: the friend saw no
    /// explosion and was never hurt by one; 0.3.21, a Rock Roller's, the same). Other kept corpses would run their own
    /// drops and quest pickups in the friend's game too.
    /// </summary>
    private static readonly HashSet<string> KeptCorpses = [
        "Corpse Last Judge", "Corpse Splinter Queen", "Corpse Tar Slug Expl", "Corpse Tar Slug Huge Expl",
        "Corpse Rock Roller"
    ];

    /// <summary>
    /// (coop) Whether a copy keeps a corpse of this name (KeptCorpses).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool KeepsCorpse(string name) =>
        KeptCorpses.Contains(name) || ServitorCorpse.KeepsServitorCorpse(name);

    private void DestroyManagedChildren(GameObject root) {
        // Only an explicitly allowed corpse of this root (pre-instantiated by EnemyDeathEffects) stays.
        var corpses = root.GetComponent<EnemyDeathEffects>() is { } effects
            ? typeof(EnemyDeathEffects).GetField("instantiatedCorpses", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(effects) as GameObject[] ?? []
            : [];
        Destroy(root);

        void Destroy(GameObject parent) {
            foreach (var child in parent.GetChildren()) {
                if (EntityRegistry.TryGetEntry(child, out _)) {
                    if (!corpses.Contains(child) || !KeepsCorpse(child.name.Replace("(Clone)", "").Trim())) {
                        UnityEngine.Object.Destroy(child);
                    }
                } else {
                    Destroy(child);
                }
            }
        }
    }

    /// <summary>
    /// Processes the given FSM for the host entity by hooking supported FSM actions.
    /// </summary>
    /// <param name="fsm">The Playmaker FSM to process.</param>
    private void ProcessHostFsm(PlayMakerFSM fsm) {
        //Logger.Info($"Processing host FSM: {fsm.Fsm.Name}");

        EntityInitializer.CheckPreProcessFsm(fsm);
        HookHostActions(fsm);

        var snapshot = new FsmSnapshot {
            CurrentState = fsm.ActiveStateName,
            Floats = fsm.FsmVariables.FloatVariables.Select(f => f.Value).ToArray(),
            Ints = fsm.FsmVariables.IntVariables.Select(i => i.Value).ToArray(),
            Bools = fsm.FsmVariables.BoolVariables.Select(b => b.Value).ToArray(),
            Strings = fsm.FsmVariables.StringVariables.Select(s => s.Value).ToArray(),
            Vector2s = fsm.FsmVariables.Vector2Variables.Select(v => v.Value).ToArray(),
            Vector3s = fsm.FsmVariables.Vector3Variables.Select(v => v.Value).ToArray()
        };

        _fsmSnapshots.Add(snapshot);
    }

    /// <summary>
    /// Hooks the supported actions of the given host FSM as it holds them now.
    /// </summary>
    /// <param name="fsm">The Playmaker FSM whose actions to hook.</param>
    private void HookHostActions(PlayMakerFSM fsm) {
        _hookedFsms[fsm] = fsm.Fsm;
        for (var i = 0; i < fsm.FsmStates.Length; i++) {
            var state = fsm.FsmStates[i];
            //var stateName = state.Name;

            for (var j = 0; j < state.Actions.Length; j++) {
                var action = state.Actions[j];
                if (!action.Enabled) {
                    continue;
                }

                if (!EntityFsmActions.SupportedActionTypes.Contains(action.GetType())) {
                    continue;
                }

                if (action.Fsm == null) {
                    //Logger.Error(
                    //    $"FSM in action for state ({i}, {state.Name}), action ({j}, {action.GetType()}) is null"
                    //);
                    continue;
                }

                _hookedActions[action] = new HookedEntityAction {
                    Action = action,
                    FsmIndex = _fsms.Host.IndexOf(fsm),
                    StateIndex = i,
                    ActionIndex = j
                };
                //Logger.Info(
                //    $"Created hooked action: {action.GetType()}, {_fsms.Host.IndexOf(fsm)}, {stateName}, {j}"
                //);

                if (_hookedTypes.Add(action.GetType())) {
                    FsmActionHooks.RegisterFsmStateActionType(action.GetType(), OnActionEntered);
                }
            }
        }
    }

    /// <summary>
    /// Processes the given FSM for the client entity by disabling it.
    /// </summary>
    /// <param name="fsm">The Playmaker FSM to process.</param>
    private void ProcessClientFsm(PlayMakerFSM fsm) {
        //Logger.Info($"Processing client FSM: {fsm.Fsm.Name}");
        EntityInitializer.InitializeFsm(fsm);
        fsm.enabled = false;
    }

    /// <summary>
    /// Check the host and client objects for components that are supported for networking.
    /// </summary>
    private void HandleComponents(EntityComponentType[] types) {
        //var addedComponentsString = $"Adding components to entity ({Object.Host.name}, {Id}):";

        var hostHealthManager = Object.Host.GetComponent<HealthManager>();
        var clientHealthManager = Object.Client.GetComponent<HealthManager>();
        if (hostHealthManager != null && clientHealthManager != null) {
            var healthManager = new HostClientPair<HealthManager> {
                Host = hostHealthManager,
                Client = clientHealthManager
            };

            var hmComponent = new HealthManagerComponent(
                _netClient,
                Id,
                Object,
                healthManager,
                SpecialDeath.KindFollows(Type)
            );
            _components[EntityComponentType.Death] = hmComponent;
            _components[EntityComponentType.Health] = hmComponent;
            _components[EntityComponentType.Invincibility] = hmComponent;
            _components[EntityComponentType.BlockedHit] = hmComponent;

            // Check if the object from the health manager is in any of the colosseum trial scenes and remove the
            // geo drops from them if so
            var goScene = hostHealthManager.gameObject.scene.name;
            if (goScene is "Room_Colosseum_Bronze" or "Room_Colosseum_Silver" or "Room_Colosseum_Gold") {
                clientHealthManager.SetGeoSmall(0);
                clientHealthManager.SetGeoMedium(0);
                clientHealthManager.SetGeoLarge(0);
            }

            //addedComponentsString += " Death Health Invincibility";
        }

        var climber = Object.Client.GetComponent<Climber>();
        if (climber != null) {
            _components[EntityComponentType.Climber] = new ClimberComponent(
                _netClient,
                Id,
                Object,
                climber
            );
            _components[EntityComponentType.Rotation] = new RotationComponent(
                _netClient,
                Id,
                Object
            );

            //addedComponentsString += " Climber Rotation";
        }

        var hostCollider = Object.Host.GetComponent<Collider2D>();
        var clientCollider = Object.Client.GetComponent<Collider2D>();
        if (hostCollider != null && clientCollider != null) {
            //Logger.Info($"Adding collider component to entity: {Object.Host.name}");

            var collider = new HostClientPair<Collider2D> {
                Host = hostCollider,
                Client = clientCollider
            };

            _components[EntityComponentType.Collider] = new ColliderComponent(
                _netClient,
                Id,
                Object,
                collider
            );

            //addedComponentsString += " Collider";
        }

        var hostDamageHeroes = Object.Host.GetComponentsInChildren<DamageHero>(true);
        var clientDamageHeroes = Object.Client.GetComponentsInChildren<DamageHero>(true);
        if (hostDamageHeroes.Length > 0 && clientDamageHeroes.Length > 0) {
            // (coop) the clone's paired with the original's by their path under each (DamagePairs), one it lacks left
            // out, so the scene host's damage and on/off reach the right box of the clone
            if (DamagePairs.ByPath()) {
                var clone = clientDamageHeroes;
                var originalPaths = System.Array.ConvertAll(hostDamageHeroes,
                    d => PathUnder(Object.Host.transform, d.transform));
                var clonePaths = System.Array.ConvertAll(clone, d => PathUnder(Object.Client.transform, d.transform));
                var pairs = DamagePairs.Align(originalPaths, clonePaths);
                clientDamageHeroes = System.Array.ConvertAll(pairs, i => i < 0 ? null! : clone[i]);
                if (DamagePairs.Logs) {
                    foreach (var line in DamagePairs.Unmatched(originalPaths, clonePaths, pairs)) {
                        Logger.Info($"(coop) hurt boxes of {Object.Host.name}: {line}");
                    }
                }
            }

            //Logger.Info($"Adding DamageHero component to entity: {Object.Host.name}");

            _components[EntityComponentType.DamageHero] = new DamageHeroComponent(
                _netClient,
                Id,
                Object,
                hostDamageHeroes,
                clientDamageHeroes
            );

            //addedComponentsString += " DamageHero";
        }

        var hostSources = Object.Host.GetComponentsInChildren<AudioSource>(true);
        var clientSources = Object.Client.GetComponentsInChildren<AudioSource>(true);
        if (hostSources.Length > 0 && DamagePairs.SoundsByPath()) {
            // (coop) the copy's sound sources paired with the original's by their path under each, as the hurt boxes
            // are: a copy with one more or one fewer (0.3.20's Dock Worker, one more) still follows each loop it has
            var pairs = DamagePairs.Align(
                System.Array.ConvertAll(hostSources, s => PathUnder(Object.Host.transform, s.transform)),
                System.Array.ConvertAll(clientSources, s => PathUnder(Object.Client.transform, s.transform)));
            if (DamagePairs.AnyPaired(pairs)) {
                _components[EntityComponentType.AudioLoop] = new AudioLoopComponent(_netClient, Id, Object,
                    hostSources, DamagePairs.Paired(clientSources, pairs)!);
            }
        } else if (hostSources.Length > 0 && hostSources.Length == clientSources.Length) {
            _components[EntityComponentType.AudioLoop] =
                new AudioLoopComponent(_netClient, Id, Object, hostSources, clientSources);
        }

        var hostMeshRenderer = Object.Host.GetComponent<MeshRenderer>();
        var clientMeshRenderer = Object.Client.GetComponent<MeshRenderer>();
        if (hostMeshRenderer != null && clientMeshRenderer != null) {
            //Logger.Info($"Adding MeshRenderer component to entity: {Object.Host.name}");

            var meshRenderer = new HostClientPair<MeshRenderer> {
                Host = hostMeshRenderer,
                Client = clientMeshRenderer
            };

            _components[EntityComponentType.MeshRenderer] = new MeshRendererComponent(
                _netClient,
                Id,
                Object,
                meshRenderer
            );

            //addedComponentsString += " MeshRenderer";
        }

        if (_nativeFlock) {
            _components[EntityComponentType.Flock] = new FlockComponent(_netClient, Id, Object, () => {
                var clip = _animator.Client.CurrentClip;
                if (clip == null || _animator.Client.GetClipByName(clip.name) is not { } replacement) return;
                _allowClientAnimation = true;
                _animator.Client.PlayFromFrame(clip.name,
                    System.Math.Min(_animator.Client.CurrentFrame, replacement.frames.Length - 1));
            });
        }
        EntityInitializer.RemoveClientTypes(Object.Client, Type);

        // Instantiate all types defined in the entity registry, which are passed to the constructor
        foreach (var type in types) {
            var component = ComponentFactory.InstantiateByType(type, _netClient, Id, Object);
            if (component == null) {
                //Logger.Debug($"Could not instantiate component for type: {type}");
            } else {
                _components[type] = component;
            }

            //addedComponentsString += $" {type}";
        }

        // Silksong's entity registry gives no entity the music component, so a boss's music never reached the other
        // players: an enemy whose FSMs change the music gets it (one per room, as the component is a singleton)
        if (!_components.ContainsKey(EntityComponentType.Music) && ChangesMusic(Object.Host)) {
            var music = ComponentFactory.InstantiateByType(EntityComponentType.Music, _netClient, Id, Object);
            if (music != null) {
                _components[EntityComponentType.Music] = music;
            }
        }

        // What an enemy spawns from the pool, and its stage objects, fly where the scene host's do (see ThrownComponent)
        if (ThrownComponent.HasStage(Object.Host.name) || FlungThrows.DropsFollowed(Object.Host.name) ||
            _fsms.Host.Any(fsm => fsm.FsmStates.Any(state => state.Actions.Any(action => action is SpawnObjectFromGlobalPool)))) {
            _components[EntityComponentType.Thrown] =
                ComponentFactory.InstantiateByType(EntityComponentType.Thrown, _netClient, Id, Object);
        }

        // (coop) An enemy whose FSMs change its layer keeps the scene host's for a take-over (see LayerComponent)
        if (_fsms.Host.Any(fsm => fsm.FsmStates.Any(state => state.Actions.Any(action => action is SetLayer)))) {
            _components[EntityComponentType.Layer] = new LayerComponent(_netClient, Id, Object);
        }

        //Logger.Debug(addedComponentsString);
    }

    /// <summary>(coop) A transform's path under root ("" for root itself).</summary>
    private static string PathUnder(Transform root, Transform transform) {
        var path = "";
        for (var t = transform; t != null && t != root; t = t.parent) {
            path = path.Length == 0 ? t.name : $"{t.name}/{path}";
        }

        return path;
    }

    /// <summary>
    /// Whether an FSM on the object starts a music cue or an audio snapshot.
    /// </summary>
    private static bool ChangesMusic(GameObject gameObject) =>
        gameObject.GetComponents<PlayMakerFSM>().Any(fsm => fsm.FsmStates.Any(state =>
            state.Actions.Any(action => action is ApplyMusicCue or TransitionToAudioSnapshot)
        ));

    /// <summary>
    /// Handle specifics for a set of enemies that rely on EnemyDeathEffects for additional enemies.
    /// </summary>
    private void HandleEnemyDeathEffects() {
        // Hollow Knight specific death effects. Commented out since Silksong has different entities.
        /*
        string corpseName;
        switch (Type) {
            case EntityType.Ooma:
                corpseName = "Corpse Jellyfish(Clone)";
                break;
            case EntityType.Flukemon:
                corpseName = "Corpse Flukeman(Clone)";
                break;
            case EntityType.HuskHornhead:
                corpseName = "Zombie Spider 2(Clone)";
                break;
            case EntityType.WanderingHusk:
                corpseName = "Zombie Spider 1(Clone)";
                break;
            case EntityType.DungDefender:
                corpseName = "Corpse Dung Defender(Clone)";
                break;
            case EntityType.BrokenVessel:
                corpseName = "Corpse Infected Knight(Clone)";
                break;
            case EntityType.LostKin:
                corpseName = "Corpse Infected Knight Dream(Clone)";
                break;
            default:
                return;
        }

        Logger.Debug($"Entity ({Id}, {Type}) has corpse that is also enemy, deleting death effects and corpse from client entity");

        var enemyDeathEffects = Object.Client.GetComponent<EnemyDeathEffects>();
        if (enemyDeathEffects == null) {
            Logger.Debug("  EnemyDeathEffects is null, cannot remove");
        }
        UnityEngine.Object.Destroy(enemyDeathEffects);

        var corpse = Object.Client.FindGameObjectInChildren(corpseName);
        if (corpse != null) {
            Logger.Debug($"  Destroying corpse of client object: {corpse.name}");
            UnityEngine.Object.Destroy(corpse);
        } else {
            Logger.Debug("  Could not find corpse of client object");
        }
        */
    }

    /// <summary>
    /// Callback method for entering a hooked FSM action.
    /// </summary>
    /// <param name="self">The FSM action instance that was entered.</param>
    /// <summary>
    /// (coop) An action this entity did not hook, of one of its host FSMs whose Fsm a template's Init replaced after
    /// the hooking: that FSM hooked again, its old actions dropped (TemplateRehook). False for any other action.
    /// </summary>
    private bool Rehooked(FsmStateAction self, out HookedEntityAction hooked) {
        hooked = null!;
        var owner = self.Fsm?.FsmComponent;
        if (owner == null || !_hookedFsms.TryGetValue(owner, out var was) ||
            !TemplateRehook.Needed(was, owner.Fsm) || !TemplateRehook.Rehooks()) {
            return false;
        }

        var index = _fsms.Host.IndexOf(owner);
        foreach (var stale in _hookedActions.Where(p => p.Value.FsmIndex == index).Select(p => p.Key).ToList()) {
            _hookedActions.Remove(stale);
        }

        HookHostActions(owner);
        return _hookedActions.TryGetValue(self, out hooked);
    }

    private void OnActionEntered(FsmStateAction self) {
        if (!_hookedActions.TryGetValue(self, out var hookedEntityAction) && !Rehooked(self, out hookedEntityAction)) return;
        if (_isControlled) {
            // Uninterrupted native startup can create enemies before the room role is known.
            if (_uninterruptedStartup && !_isSceneHostDetermined && self is CreateObject created) {
                EntityFsmActions.CallEntitySpawnEvent(new EntitySpawnDetails {
                    Type = EntitySpawnType.FsmAction, Action = created, GameObject = created.storeObject.Value
                });
            }
            return;
        }
        if (self is ActivateGameObject activation &&
            _components.TryGetValue(EntityComponentType.DamageHero, out var component) &&
            component is DamageHeroComponent damage &&
            DamagePairs.UsesNativeActivation(activation.resetOnExit, activation.recursive.Value,
                damage.OwnsActivation(activation.Fsm.GetOwnerDefaultTarget(activation.gameObject)))) {
            return;
        }
        //
        //Logger.Info(
        //    $"Entity ({Id}, {Type}) hooked action: {self.Fsm.Name}, {self.State.Name}, {self.GetType()} ({hookedEntityAction.FsmIndex}, {hookedEntityAction.StateIndex}, {hookedEntityAction.ActionIndex})"
        //);

        var networkData = new EntityNetworkData {
            Type = EntityComponentType.Fsm
        };

        if (_fsms.Host.Count > 1) {
            networkData.Packet.Write((byte) hookedEntityAction.FsmIndex);
        }

        networkData.Packet.Write((byte) hookedEntityAction.StateIndex);
        networkData.Packet.Write((byte) hookedEntityAction.ActionIndex);

        // Only if the GetNetworkDataFromAction method returns true do we add the entity data
        // for sending
        if (EntityFsmActions.GetNetworkDataFromAction(networkData, self)) {
            // The spawned object's number, so the other games move their copy where this game's goes
            if (self is SpawnObjectFromGlobalPool spawn) {
                networkData.Packet.Write(Thrown?.Track(spawn.storeObject.Value, (byte) hookedEntityAction.FsmIndex,
                    (byte) hookedEntityAction.StateIndex, (byte) hookedEntityAction.ActionIndex) ?? 0);
            }

            _netClient.UpdateManager.AddEntityData(Id, networkData);
        }
    }

    /// <summary>
    /// Callback method for handling updates.
    /// </summary>
    [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
    private void OnUpdate() {
        if (Object.Host == null) {
            if (_lastIsActive) {
                // If the host object was active, but now it null (or destroyed in Unity), we can send
                // to the server that the entity can be regarded as inactive
                //Logger.Info(
                //    Object.Client == null
                //        ? $"Entity ({Id}, {Type}) host and client object is null (or destroyed) and was active"
                //        : $"Entity '{Object.Client.name}' host object is null (or destroyed) and was active"
                //);

                _lastIsActive = false;

                _netClient.UpdateManager.UpdateEntityIsActive(
                    Id,
                    false
                );
            }

            FliesOn();
            return;
        }

        if (_uninterruptedStartup && !_isSceneHostDetermined) return;
        var hostObjectActive = Object.Host.activeSelf;

        if (_isControlled) {
            if (hostObjectActive) {
                if (!_isSceneHostDetermined) {
                    //Logger.Info(
                    //    $"Entity '{Object.Host.name}' host object became active, but scene host is not determined yet, re-disabling for now"
                    //);
                    _originalIsActive = true;
                } else {
                    //Logger.Info($"Entity '{Object.Host.name}' host object became active, re-disabling");
                }

                Object.Host.SetActive(false);
            }

            if (Object.Client != null &&
                Object.Client.TryGetComponent<PredictiveInterpolation>(out var interpolation)) {
                interpolation.AdaptToRTT(_netClient.UpdateManager.AverageRtt);
                interpolation.ManualUpdate(Time.deltaTime);
            }

            // The hidden host object stands where its client object is, so the scene's objects that track it (a
            // boss's stage spotlights) see the enemy where the players do
            if (Object.Client != null) {
                if (_hasParent) {
                    Object.Host.transform.localPosition = Object.Client.transform.localPosition;
                } else {
                    Object.Host.transform.position = Object.Client.transform.position;
                }
            }

            return;
        }

        foreach (var entityComponent in _updatableComponents) {
            entityComponent.OnUpdate();
        }

        var transform = Object.Host.transform;

        var refreshScale = ScaleRefresh.Due(Time.unscaledTime, _lastScaleRefresh);
        if (transform.hasChanged || refreshScale) {
            var newPosition = _hasParent ? transform.localPosition : transform.position;
            if (newPosition != _lastPosition) {
                _lastPosition = newPosition;

                _netClient.UpdateManager.UpdateEntityPosition(
                    Id,
                    new Math_Vector2(newPosition.x, newPosition.y)
                );
            }

            var newScale = _hasParent ? transform.localScale : transform.lossyScale;
            if (newScale != _lastScale || refreshScale) {
                // Complete values survive a missed earlier size or turn, and later changes in the same packet.
                var scaleData = new EntityUpdate.ScaleData {
                    origin = true,
                    x = true, y = true, z = true,
                    xScale = newScale.x, yScale = newScale.y, zScale = newScale.z
                };
                _netClient.UpdateManager.UpdateEntityScale(Id, scaleData);
                _lastScale = newScale;
                if (refreshScale) _lastScaleRefresh = Time.unscaledTime;
            }

            transform.hasChanged = false;
        }

        var newActive = _hasParent ? Object.Host.activeSelf : Object.Host.activeInHierarchy;
        if (newActive != _lastIsActive) {
            _lastIsActive = newActive;

            //Logger.Info($"Entity '{Object.Host.name}' changed active: {newActive}");

            _netClient.UpdateManager.UpdateEntityIsActive(
                Id,
                newActive
            );
        }

        // Sync Host FSM states and variables.
        // To avoid garbage collection pressure, we first check for changes using simple, direct loops
        // (avoiding generic methods with delegates/closures) and only retrieve/populate EntityHostFsmData from the pool if a change is detected.
        for (byte fsmIndex = 0; fsmIndex < _fsms.Host.Count; fsmIndex++) {
            var fsm = _fsms.Host[fsmIndex];
            var snapshot = _fsmSnapshots[fsmIndex];

            var lastStateName = snapshot.CurrentState;
            var hasStateChange = fsm.ActiveStateName != lastStateName;

            var hasFloatsChange = false;
            for (byte i = 0; i < fsm.FsmVariables.FloatVariables.Length; i++) {
                if (fsm.FsmVariables.FloatVariables[i].Value != snapshot.Floats[i]) {
                    hasFloatsChange = true;
                    break;
                }
            }

            var hasIntsChange = false;
            for (byte i = 0; i < fsm.FsmVariables.IntVariables.Length; i++) {
                if (fsm.FsmVariables.IntVariables[i].Value != snapshot.Ints[i]) {
                    hasIntsChange = true;
                    break;
                }
            }

            var hasBoolsChange = false;
            for (byte i = 0; i < fsm.FsmVariables.BoolVariables.Length; i++) {
                if (fsm.FsmVariables.BoolVariables[i].Value != snapshot.Bools[i]) {
                    hasBoolsChange = true;
                    break;
                }
            }

            var hasStringsChange = false;
            for (byte i = 0; i < fsm.FsmVariables.StringVariables.Length; i++) {
                if (snapshot.Strings[i] != null && fsm.FsmVariables.StringVariables[i].Value != snapshot.Strings[i]) {
                    hasStringsChange = true;
                    break;
                }
            }

            var hasVector2SChange = false;
            for (byte i = 0; i < fsm.FsmVariables.Vector2Variables.Length; i++) {
                if (fsm.FsmVariables.Vector2Variables[i].Value != snapshot.Vector2s[i]) {
                    hasVector2SChange = true;
                    break;
                }
            }

            var hasVector3SChange = false;
            for (byte i = 0; i < fsm.FsmVariables.Vector3Variables.Length; i++) {
                if (fsm.FsmVariables.Vector3Variables[i].Value != snapshot.Vector3s[i]) {
                    hasVector3SChange = true;
                    break;
                }
            }

            if (hasStateChange || hasFloatsChange || hasIntsChange || hasBoolsChange || hasStringsChange ||
                hasVector2SChange || hasVector3SChange) {
                var data = ObjectPool<EntityHostFsmData>.Get();

                if (hasStateChange) {
                    snapshot.CurrentState = fsm.ActiveStateName;
                    data.Types.Add(EntityHostFsmData.Type.State);
                    data.CurrentState = (byte) Array.IndexOf(fsm.FsmStates, fsm.Fsm.ActiveState);
                }

                if (hasFloatsChange) {
                    data.Types.Add(EntityHostFsmData.Type.Floats);
                    for (byte i = 0; i < fsm.FsmVariables.FloatVariables.Length; i++) {
                        var val = fsm.FsmVariables.FloatVariables[i].Value;
                        if (val != snapshot.Floats[i]) {
                            snapshot.Floats[i] = val;
                            data.Floats[i] = val;
                        }
                    }
                }

                if (hasIntsChange) {
                    data.Types.Add(EntityHostFsmData.Type.Ints);
                    for (byte i = 0; i < fsm.FsmVariables.IntVariables.Length; i++) {
                        var val = fsm.FsmVariables.IntVariables[i].Value;
                        if (val != snapshot.Ints[i]) {
                            snapshot.Ints[i] = val;
                            data.Ints[i] = val;
                        }
                    }
                }

                if (hasBoolsChange) {
                    data.Types.Add(EntityHostFsmData.Type.Bools);
                    for (byte i = 0; i < fsm.FsmVariables.BoolVariables.Length; i++) {
                        var val = fsm.FsmVariables.BoolVariables[i].Value;
                        if (val != snapshot.Bools[i]) {
                            snapshot.Bools[i] = val;
                            data.Bools[i] = val;
                        }
                    }
                }

                if (hasStringsChange) {
                    data.Types.Add(EntityHostFsmData.Type.Strings);
                    for (byte i = 0; i < fsm.FsmVariables.StringVariables.Length; i++) {
                        var val = fsm.FsmVariables.StringVariables[i].Value;
                        if (snapshot.Strings[i] != null && val != snapshot.Strings[i]) {
                            snapshot.Strings[i] = val;
                            data.Strings[i] = val;
                        }
                    }
                }

                if (hasVector2SChange) {
                    data.Types.Add(EntityHostFsmData.Type.Vector2s);
                    for (byte i = 0; i < fsm.FsmVariables.Vector2Variables.Length; i++) {
                        var val = fsm.FsmVariables.Vector2Variables[i].Value;
                        if (val != snapshot.Vector2s[i]) {
                            snapshot.Vector2s[i] = val;
                            data.Vec2s[i] = (Math_Vector2) val;
                        }
                    }
                }

                if (hasVector3SChange) {
                    data.Types.Add(EntityHostFsmData.Type.Vector3s);
                    for (byte i = 0; i < fsm.FsmVariables.Vector3Variables.Length; i++) {
                        var val = fsm.FsmVariables.Vector3Variables[i].Value;
                        if (val != snapshot.Vector3s[i]) {
                            snapshot.Vector3s[i] = val;
                            data.Vec3s[i] = (SSMP.Math.Vector3) val;
                        }
                    }
                }

                _netClient.UpdateManager.AddEntityHostFsmData(Id, fsmIndex, data);
                ObjectPool<EntityHostFsmData>.Return(data);
            }
        }
    }

    /// <summary>
    /// Callback method for when the sprite animator plays an animation.
    /// </summary>
    /// <param name="orig">The original method.</param>
    /// <param name="self">The sprite animator instance.</param>
    /// <param name="clip">The animation clip that was played.</param>
    /// <param name="clipStartTime">The start time of the animation clip.</param>
    /// <param name="overrideFps">The FPS override for the clip.</param>
    private void OnAnimationPlayed(
        Action<tk2dSpriteAnimator, tk2dSpriteAnimationClip, float, float> orig,
        tk2dSpriteAnimator self,
        tk2dSpriteAnimationClip clip,
        float clipStartTime,
        float overrideFps
    ) {
        if (self == _animator.Client) {
            if (!_allowClientAnimation) {
                //Logger.Info($"Entity '{Object.Client.name}' client animator tried playing animation");
            } else {
                // Logger.Info($"Entity '{_object.Client.name}' client animator was allowed to play animation");

                orig(self, clip, clipStartTime, overrideFps);

                _allowClientAnimation = false;
            }

            return;
        }

        orig(self, clip, clipStartTime, overrideFps);

        if (self != _animator.Host) {
            return;
        }

        if (_isControlled) {
            return;
        }

        // (coop) its walker's alert just played, which no copy's walker plays (StartleSound)
        if (PlaysStartleSound() && self.GetComponent<WalkerV2>() is var walker && walker != null &&
            StartleSound.Sends(clip.name, walker.StartleAnim)) {
            _netClient.UpdateManager.AddEntityData(Id, new EntityNetworkData { Type = EntityComponentType.Startle });
        }

        if (!_animationClipNameIds.TryGetValue(clip.name, out var animationId)) {
            //Logger.Warn($"Entity '{Object.Client.name}' played unknown animation: {clip.name}");
            return;
        }

        //Logger.Info($"Entity '{Object.Host.name}' sends animation: {clip.name}, {animationId}, {clip.wrapMode}");
        SendAnimation(animationId, (byte) clip.wrapMode);
    }

    /// <summary>
    /// Callback method for when a game object is recycled. Used to prevent client objects from being recycled, which
    /// shouldn't happen because they are instantiated manually instead of from a pool.
    /// </summary>
    private void ObjectPoolOnRecycleGameObject(Action<GameObject> orig, GameObject obj) {
        if (obj == Object.Client) {
            //Logger.Debug($"Client object of entity: {Id}, {Type} tried to be recycled");
            return;
        }

        orig(obj);
    }

    /// <summary>
    /// Callback method for when the 'active' of the host game object is changed. Used to update whether the host
    /// game object should return to what active state after the scene host is determined.
    /// </summary>
    private void OnDoActivateGameObject(
        Action<ActivateGameObject> orig,
        ActivateGameObject self
    ) {
        // If the game object in the action is not our host game object, we skip it
        if (self.Fsm.GetOwnerDefaultTarget(self.gameObject) != Object.Host || Object.Host == null) {
            orig(self);
            return;
        }

        // If the host client is determined already we skip (although this hook should have been deregistered
        if (_isSceneHostDetermined) {
            orig(self);
            return;
        }

        //Logger.Debug(
        //    $"Entity '{Object.Host.name}' tried changing active of host object, while host is not determined yet, updating original active to: {self.activate.Value}"
        //);

        // Update the original active value to whatever this action will set
        // Also, we do not let this action execute any further since we do not want it to modify our host object
        // before the scene host is determined
        _originalIsActive = self.activate.Value;
        FinishHeld(self);
    }

    /// <summary>
    /// A held <see cref="ActivateGameObject"/> still finishes (unless it runs every frame), or the state that ran it
    /// never ends: the Dock Foremen's arena switches the Thrower off at its start-up, and on a first visit (no event to
    /// move it on, only the state's end) it stayed in Init, so their fight never came.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    private static void FinishHeld(ActivateGameObject action) {
        if (!action.everyFrame) {
            action.Finish();
        }
    }

    /// <summary>
    /// Initializes the entity when the client user is the scene host.
    /// </summary>
    public void InitializeHost(uint sceneHostEpoch = 0) {
        LetGoBones();
        ResetChildAnimation(sceneHostEpoch);
        if (!_uninterruptedStartup) Object.Host.SetActive(_originalIsActive);

        // Also update the last active variable to account for this potential change
        // Otherwise we might trigger the update sending of activity twice
        _lastIsActive = _hasParent ? Object.Host.activeSelf : Object.Host.activeInHierarchy;

        //Logger.Info(
        //    $"Initializing entity '{Object.Host.name}' with active: {_originalIsActive}, sending active: {_lastIsActive}"
        //);

        _netClient.UpdateManager.UpdateEntityIsActive(Id, _lastIsActive);

        _isControlled = false;
        _isSceneHostDetermined = true;

        foreach (var component in _components.Values) {
            component.IsControlled = false;
            component.InitializeHost(sceneHostEpoch);
        }

        SendsCurrentClip();

        // Deregister the hook for updating the active value of the host object
        _activateGameObjectHook?.Dispose();
        _activateGameObjectHook = null;
    }

    /// <summary>
    /// (coop) The clip the scene host's animator already plays as this game takes the entity, sent as its animation, so
    /// the server keeps it for the other games' copies: a clip started before the entity was registered (a Swamp
    /// Barnacle's Idle as its room loads) was never sent, and the copies played no clip and showed their sprite's
    /// default frame (Aqueduct_08's barnacle closed in the host's game and open in the friend's). Not inlined:
    /// barnacle_state's twin skips it.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SendsCurrentClip() {
        var clip = _animator.Host != null ? _animator.Host.CurrentClip : null;
        if (clip == null || !_animationClipNameIds.TryGetValue(clip.name, out var animationId)) return;
        SendAnimation(animationId, (byte) clip.wrapMode);
    }

    private void SendAnimation(byte animationId, byte wrapMode) {
        if (!EntityParenting.SendsChildAnimationReliably(_hasParent)) {
            _netClient.UpdateManager.UpdateEntityAnimation(Id, animationId, wrapMode);
            return;
        }

        // Parent FSM clips and child motion share one ordered path, so an old Idle cannot replace a newer turn.
        var data = new EntityNetworkData { Type = EntityComponentType.ChildAnimation };
        data.Packet.Write(_childAnimationEpoch);
        data.Packet.Write(++_childAnimationOrder);
        data.Packet.Write(animationId);
        data.Packet.Write(wrapMode);
        _netClient.UpdateManager.AddEntityData(Id, data);
    }

    private void ResetChildAnimation(uint epoch) {
        _childAnimationEpoch = epoch;
        _childAnimationOrder = 0;
        _lastChildAnimationOrder = -1;
    }

    /// <summary>
    /// Initializes the entity when the client user is a scene client. Only sets a variable to indicate the scene
    /// host has been determined.
    /// </summary>
    public void InitializeClient(uint sceneHostEpoch = 0) {
        ResetChildAnimation(sceneHostEpoch);
        _isSceneHostDetermined = true;
        if (_uninterruptedStartup) Object.Host.SetActive(false);

        // Deregister the hook for updating the active value of the host object
        _activateGameObjectHook?.Dispose();
        _activateGameObjectHook = null;

        foreach (var component in _components.Values) {
            component.InitializeClient(sceneHostEpoch);
        }
    }

    /// <summary>Whether a copy whose original is gone is hidden at a takeover. Not inlined: host_takeover's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool HidesGoneOriginals() => true;

    /// <summary>
    /// Makes the entity a host entity if the client user became the scene host.
    /// </summary>
    public void MakeHost(uint sceneHostEpoch) {
        LetGoBones();
        ResetChildAnimation(sceneHostEpoch);
        //Logger.Info($"Making entity ({Id}, {Type}) a host entity");

        // If the client object is null, we don't have to care about doing anything for the host object anymore
        if (Object.Client == null) {
            if (Object.Host != null) {
                Object.Host.SetActive(false);
            }

            _isControlled = false;

            foreach (var component in _components.Values) {
                component.IsControlled = false;
                component.InitializeHost(sceneHostEpoch);
            }

            //Logger.Debug("  Client object null, enabling host object and returning");
            return;
        }

        // (coop) The original is gone here (its own room destroyed it while this game followed): this game has nothing
        // to drive, so its copy is hidden rather than left frozen; its MakeHost threw on Object.Host.transform and
        // stopped the whole takeover (the final plan2, 2026-11-05: Tut_03 after the host left, NOTES 347)
        if (Object.Host == null && HidesGoneOriginals()) {
            Logger.Info($"Entity {Id} ({Type}, '{Object.Client.name}'): its original is gone at the takeover; its copy hidden");
            Object.Client.SetActive(false);
            _isControlled = false;
            foreach (var component in _components.Values) {
                component.IsControlled = false;
            }

            return;
        }

        if (_hasParent) {
            //Logger.Debug("  Entity has parent, only setting local transform");

            Object.Host.transform.localPosition = _lastPosition = Object.Client.transform.localPosition;
            Object.Host.transform.localScale = _lastScale = Object.Client.transform.localScale;
        } else {
            //Logger.Debug("  Entity has no parent, calculating transform");

            var clientPos = Object.Client.transform.localPosition;
            var parentPos = Vector3.zero;
            if (Object.Host.transform.parent != null) {
                parentPos = Object.Host.transform.parent.position;
            }

            var newPosX = clientPos.x - parentPos.x;
            var newPosY = clientPos.y - parentPos.y;
            var newPosZ = clientPos.z - parentPos.z;

            Object.Host.transform.localPosition = _lastPosition = new Vector3(newPosX, newPosY, newPosZ);

            // Since the scale of the client object is the entire scale we have and the host object scale can be in a
            // hierarchy, we need to calculate what the new local scale of the host will be to match the client scale
            var clientScale = Object.Client.transform.localScale;
            var hostLocalScale = Object.Host.transform.localScale;
            var hostLossyScale = Object.Host.transform.lossyScale;

            var newScaleX = hostLocalScale.x == 0 || hostLossyScale.x == 0
                ? 0f
                : clientScale.x / (hostLossyScale.x / hostLocalScale.x);
            var newScaleY = hostLocalScale.y == 0 || hostLossyScale.y == 0
                ? 0f
                : clientScale.y / (hostLossyScale.y / hostLocalScale.y);
            var newScaleZ = hostLocalScale.z == 0 || hostLossyScale.z == 0
                ? 0f
                : clientScale.z / (hostLossyScale.z / hostLocalScale.z);

            Object.Host.transform.localScale = _lastScale = new Vector3(newScaleX, newScaleY, newScaleZ);
        }

        // Make sure that the sprite animator doesn't play the default clip after enabling the object
        if (_animator.Host != null) {
            _animator.Host.playAutomatically = false;
        }

        //.Debug("  Restoring FSM variables from snapshots");

        for (var fsmIndex = 0; fsmIndex < _fsms.Host.Count; fsmIndex++) {
            var fsm = _fsms.Host[fsmIndex];

            //Logger.Debug($"    Restoring variables for FSM: {fsm.Fsm.Name}");

            // Force initialize the host FSM, since it might have been disabled before initializing
            EntityInitializer.InitializeFsm(fsm);

            var snapshot = _fsmSnapshots[fsmIndex];

            for (var i = 0; i < snapshot.Floats.Length; i++) {
                fsm.FsmVariables.FloatVariables[i].Value = snapshot.Floats[i];
            }

            for (var i = 0; i < snapshot.Ints.Length; i++) {
                fsm.FsmVariables.IntVariables[i].Value = snapshot.Ints[i];
            }

            for (var i = 0; i < snapshot.Bools.Length; i++) {
                fsm.FsmVariables.BoolVariables[i].Value = snapshot.Bools[i];
            }

            for (var i = 0; i < snapshot.Strings.Length; i++) {
                fsm.FsmVariables.StringVariables[i].Value = snapshot.Strings[i];
            }

            for (var i = 0; i < snapshot.Vector2s.Length; i++) {
                fsm.FsmVariables.Vector2Variables[i].Value = snapshot.Vector2s[i];
            }

            for (var i = 0; i < snapshot.Vector3s.Length; i++) {
                fsm.FsmVariables.Vector3Variables[i].Value = snapshot.Vector3s[i];
            }
        }

        //Logger.Debug("  Restoring FSM states from snapshots");

        // (coop) what SSMP's copy showed of the objects the enemy throws goes to the room's own first, so each state
        // entered next acts on them as it did in the scene host's game: handed over after, the Last Judge's censer, out
        // in the last update, was shown again after her Censer Slam had put it away, and hung at her throw point
        Thrown?.HandsOver();

        var restartOnEnable = _fsms.Host.Select(fsm => fsm.Fsm.RestartOnEnable).ToArray();
        List<int>? enterAgain = null;
        for (var fsmIndex = 0; fsmIndex < _fsms.Host.Count; fsmIndex++) {
            var fsm = _fsms.Host[fsmIndex];
            var snapshot = _fsmSnapshots[fsmIndex];

            if (string.IsNullOrEmpty(snapshot.CurrentState)) {
                //.Debug("Not setting FSM state, because current state is empty");
                continue;
            }

            var state = fsm.GetStateOrNull(snapshot.CurrentState);
            if (state == null) {
                //Logger.Debug($"  Not setting FSM state, because state '{snapshot.CurrentState}' was not found");
                continue;
            }

            //Logger.Debug($"  Setting FSM state: {snapshot.CurrentState}");

            // The state the host's game was in, with all its actions: the move starts again from where the enemy is.
            // Entering it with only its continuous actions left a boss frozen there, because the one-time actions
            // that start a move (a charge's speed, the flags its end checks) never ran on the hidden original.
            // An action that needs the enemy's own start-up (Awake) throws while the original is still inactive, as
            // a crawler's StartCrawler does, and stopped the take-over of this enemy and every one after it; that
            // state is entered again once the original is active
            try {
                fsm.SetState(snapshot.CurrentState);
            } catch (Exception e) {
                Logger.Debug($"Entering {snapshot.CurrentState} of {Object.Host.name} while inactive threw: {e.Message}");
                (enterAgain ??= []).Add(fsmIndex);
            }

            // Enabling the host object below would otherwise reset the FSM to its start state (OnEnable with
            // RestartOnEnable) and re-enter the state with all actions (PlayMakerFSM.Start when not Started),
            // replaying boss intros mid-fight
            fsm.Fsm.RestartOnEnable = false;
            fsm.Fsm.Started = true;
        }

        // We need to set the isKinematic property of rigid bodies to ensure physics work again after enabling
        // the host object. In Hornet 1 this is necessary because another state sets this property normally in the
        // fight. See the "Wake" or "Refight Ready" state of the "Control" FSM on Hornet 1.
        // For the Mantis Lord and City Elevator entity, this should never be disabled, since they are always kinematic.
        var rigidBody = Object.Host.GetComponent<Rigidbody2D>();
        if (rigidBody != null) {
            //Logger.Debug("  Resetting isKinematic of Rigidbody to ensure physics work for host object");
            rigidBody.isKinematic = false;
        }

        _isControlled = false;

        foreach (var component in _components.Values) {
            component.IsControlled = false;
        }

        if (_animator.Client != null) {
            var currentClip = _animator.Client.CurrentClip;
            if (currentClip != null) {
                var clientAnimation = currentClip.name;
                var wrapMode = currentClip.wrapMode;

                //Logger.Debug($"  Animator and current clip present, updating animation: {clientAnimation}, {wrapMode}");

                LateUpdateAnimation(_animator.Host, clientAnimation, wrapMode);
            }
        }

        var clientActive = Object.Client.activeSelf;
        var flockClip = _animator.Client?.CurrentClip?.name ?? "Idle";
        Object.Client.SetActive(false);
        if (Flock is { } flock) flock.ActivateHost(clientActive, flockClip);
        else Object.Host.SetActive(clientActive);

        foreach (var fsmIndex in enterAgain ?? []) {
            try {
                _fsms.Host[fsmIndex].SetState(_fsmSnapshots[fsmIndex].CurrentState);
            } catch (Exception e) {
                Logger.Warn($"Could not enter {_fsmSnapshots[fsmIndex].CurrentState} of {Object.Host.name}: {e}");
            }
        }

        // shortcut: FSMs still inactive here keep RestartOnEnable off, so a later re-enable by the game won't reset
        // them; restore it on their enable if an enemy ever relies on that reset
        for (var fsmIndex = 0; fsmIndex < _fsms.Host.Count; fsmIndex++) {
            var fsm = _fsms.Host[fsmIndex];
            if (fsm.gameObject.activeInHierarchy) {
                fsm.Fsm.RestartOnEnable = restartOnEnable[fsmIndex];
            }
        }

        //Logger.Debug($"  Set Active of host object to: {clientActive}, disabling client object");

        _lastIsActive = _hasParent ? Object.Host.activeSelf : Object.Host.activeInHierarchy;

        foreach (var component in _components.Values) {
            component.InitializeHost(sceneHostEpoch);
        }
    }

    /// <summary>
    /// Updates the position of the client entity.
    /// </summary>
    /// <param name="position">The new position.</param>
    public void UpdatePosition(Math_Vector2 position) {
        if (Object.Client == null || Object.Host == null) {
            //.Warn($"Cannot update position for entity ({Id}, {Type}), client or host object is null");
            return;
        }

        var unityPos = new Vector3(
            position.X,
            position.Y,
            _hasParent ? Object.Host.transform.localPosition.z : Object.Host.transform.position.z
        );

        // Child updates are relative to their parent; the predictor consumes world coordinates.
        if (_hasParent) {
            Object.Client.transform.localPosition = unityPos;
            return;
        }

        var positionInterpolation = Object.Client.GetComponent<PredictiveInterpolation>();
        if (positionInterpolation == null) {
            return;
        }

        positionInterpolation.SetNewPosition(unityPos);
    }

    /// <summary>
    /// Updates the scale of the client entity.
    /// </summary>
    /// <param name="scale">The new scale data.</param>
    public void UpdateScale(EntityUpdate.ScaleData scale) {
        if (Object.Client == null) {
            //Logger.Warn($"Cannot update scale for entity ({Id}, {Type}), client object is null");
            return;
        }

        var transform = Object.Client.transform;
        var localScale = transform.localScale;

        if (scale.x) {
            if (scale.xFlipped) {
                var currentScaleX = localScale.x;

                if (currentScaleX > 0 != scale.xPos) {
                    currentScaleX *= -1;

                    localScale.x = currentScaleX;
                }
            } else {
                localScale.x = scale.xScale;
            }
        }

        if (scale.y) {
            if (scale.yFlipped) {
                var currentScaleY = localScale.y;

                if (currentScaleY > 0 != scale.yPos) {
                    currentScaleY *= -1;

                    localScale.y = currentScaleY;
                }
            } else {
                localScale.y = scale.yScale;
            }
        }

        if (scale.z) {
            if (scale.zFlipped) {
                var currentScaleZ = localScale.z;

                if (currentScaleZ > 0 != scale.zPos) {
                    currentScaleZ *= -1;

                    localScale.z = currentScaleZ;
                }
            } else {
                localScale.z = scale.zScale;
            }
        }

        transform.localScale = localScale;
    }

    /// <summary>
    /// Updates the animation of the client entity.
    /// </summary>
    /// <param name="animationId">The ID of the animation.</param>
    /// <param name="wrapMode">The wrap mode of the animation clip.</param>
    /// <param name="alreadyInSceneUpdate">Whether this update is when entering a new scene.</param>
    public void UpdateAnimation(
        byte animationId,
        tk2dSpriteAnimationClip.WrapMode wrapMode,
        bool alreadyInSceneUpdate
    ) {
        if (_animator.Client == null) {
            //Logger.Warn($"Entity '{Object.Client.name}' received animation while client animator does not exist");
            return;
        }

        if (!_animationClipNameIds.TryGetValue(animationId, out var clipName)) {
            //Logger.Warn($"Entity '{Object.Client.name}' received unknown animation ID: {animationId}");
            return;
        }

        //Logger.Info($"Entity '{Object.Client.name}' received animation: {animationId}, {clipName}, {wrapMode}");

        // All paths lead to calling the Play method of the sprite animator that is hooked, so we allow the call
        // through the hook
        _allowClientAnimation = true;

        if (alreadyInSceneUpdate) {
            // Since this is an animation update from an entity that was already present in a scene,
            // we need to determine where to start playing this specific animation
            LateUpdateAnimation(_animator.Client, clipName, wrapMode);
        }

        // Otherwise, default to just playing the clip
        _animator.Client.Play(clipName);
    }

    private WalkerV2? _hostWalker;
    private UnityEngine.Events.UnityAction<bool>? _onWalking, _onRunning;

    /// <summary>(coop) The scene host's walker events go to every copy (WalkEvents).</summary>
    private void ListenToWalker() {
        _hostWalker = Object.Host != null ? Object.Host.GetComponent<WalkerV2>() : null;
        if (_hostWalker == null) return;
        _onWalking = on => SendWalkEvent(false, on);
        _onRunning = on => SendWalkEvent(true, on);
        _hostWalker.OnWalking?.AddListener(_onWalking);
        _hostWalker.OnRunning?.AddListener(_onRunning);
    }

    private void SendWalkEvent(bool running, bool on) {
        if (_isControlled || !CopiesWalkEvents()) return;
        var data = new EntityNetworkData { Type = EntityComponentType.WalkEvent };
        data.Packet.Write(WalkEvents.Encode(running, on));
        _netClient.UpdateManager.AddEntityData(Id, data);
    }

    /// <summary>(coop) The bone segments this copy presses because the scene host's game sent them (CopyPresses).</summary>
    private readonly HashSet<BoneSegment> _bonesPressed = [];

    /// <summary>(coop) The scene host's enemy began or stopped touching a bone segment: sent by its place.</summary>
    private void SendBonePress(Vector2 at, bool press) {
        if (_isControlled || !CopyPresses.HoldsCopyPresses()) return;
        var data = new EntityNetworkData { Type = EntityComponentType.BonePress };
        data.Packet.Write(at.x);
        data.Packet.Write(at.y);
        data.Packet.Write(press);
        _netClient.UpdateManager.AddEntityData(Id, data);
    }

    /// <summary>
    /// (coop) The copy presses or lets go the sent segment with its box. A late entry's catch-up is passed by: its
    /// presses would play long-past dusts.
    /// </summary>
    private void PressSentBone(EntityNetworkData data, bool alreadyInSceneUpdate) {
        if (!_isControlled || alreadyInSceneUpdate || data.Packet.Length != 9 || Object.Client == null ||
            !CopyPresses.HoldsCopyPresses()) return;
        var x = data.Packet.ReadFloat();
        var y = data.Packet.ReadFloat();
        var press = data.Packet.ReadBool();
        var segment = CopyPresses.At(x, y);
        var box = Object.Client.GetComponent<Collider2D>();
        if (segment == null || box == null) return;
        if (press ? _bonesPressed.Add(segment) : _bonesPressed.Remove(segment)) CopyPresses.Press(segment, box, press);
    }

    /// <summary>(coop) The copy lets go every segment it pressed (a take-over, or the entity gone).</summary>
    private void LetGoBones() {
        var box = Object.Client != null ? Object.Client.GetComponent<Collider2D>() : null;
        foreach (var segment in _bonesPressed) {
            if (box != null) CopyPresses.Press(segment, box, false);
        }

        _bonesPressed.Clear();
    }

    /// <summary>(coop) Whether a copy's walker fires the scene host's walker events. Not inlined: walk_events' twin
    /// skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool CopiesWalkEvents() => true;

    /// <summary>(coop, 0.3.21) The copy's loop sources not playing before a walk event.</summary>
    private static List<AudioSource> SilentLoops(GameObject copy) {
        var silent = new List<AudioSource>();
        foreach (var source in copy.GetComponentsInChildren<AudioSource>(true)) {
            if (source.loop && !source.isPlaying) silent.Add(source);
        }

        return silent;
    }

    /// <summary>
    /// (coop, 0.3.21) A copy's loop the walk event's switch started counts as just played: it plays on awake, with no
    /// Play for GamePatcher's hook to see, so it still stood at its first sample when the scene host's loop report came
    /// and the loop follow played it again from its start (Bone_09's Dock Worker (2) copy's Walk Voice Loop, "extra:
    /// Dock_Worker_idle_loop 1" in both orders, 2026-11-11_095325-enemies-pair4 and _100050-enemies-pair4).
    /// </summary>
    private static void MarkAwakeLoops(List<AudioSource> silent) {
        if (!MarksAwakeLoops()) return;
        foreach (var source in silent) {
            if (source != null && source.isPlaying) LoopStarts.Played(source, Time.unscaledTime);
        }
    }

    /// <summary>Not inlined: the walk_loop_starts check's twin skips it (the loop follow plays the copy's walk loop
    /// again).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool MarksAwakeLoops() => true;

    /// <summary>(coop) WalkerV2's private alert, which its walk plays as a hero comes near (StartleSound).</summary>
    private static readonly FieldInfo? WalkerAggroSound =
        typeof(WalkerV2).GetField("aggroSound", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>(coop) Whether a copy plays its walker's alert with the host's startle (the scene host's game sends it,
    /// the others play it). Not inlined: walker_alert's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool PlaysStartleSound() => true;

    /// <summary>(coop) The copy's walker plays its own alert where it stands (StartleSound).</summary>
    private void PlayStartle() {
        var walker = _animator.Client != null ? _animator.Client.GetComponent<WalkerV2>() : null;
        if (walker != null && WalkerAggroSound?.GetValue(walker) is AudioEventRandom alert) {
            alert.SpawnAndPlayOneShot(walker.transform.position);
        }
    }

    /// <summary>
    /// Update the animation for the given animator with the given clip name and wrap mode. This assumes that we need
    /// to replicate animation behaviour for a late update.
    /// </summary>
    /// <param name="animator">The sprite animator to update.</param>
    /// <param name="clipName">The name of the animation clip.</param>
    /// <param name="wrapMode">The wrap mode for the animation.</param>
    private void LateUpdateAnimation(
        tk2dSpriteAnimator animator,
        string? clipName,
        tk2dSpriteAnimationClip.WrapMode wrapMode
    ) {
        if (wrapMode == tk2dSpriteAnimationClip.WrapMode.Loop) {
            animator.Play(clipName);
            return;
        }

        var clip = animator.GetClipByName(clipName);

        //Logger.Debug($"Entity ({Id}, {Type}) LateUpdateAnimation: {clip.name}, {wrapMode}");

        switch (wrapMode) {
            case tk2dSpriteAnimationClip.WrapMode.LoopSection:
                // The clip loops in a specific section in the frames, so we start playing
                // it from the start of that section
                animator.PlayFromFrame(clipName, clip.loopStart);
                return;
            case tk2dSpriteAnimationClip.WrapMode.Once or tk2dSpriteAnimationClip.WrapMode.Single: {
                // Since the clip was played once, it stops on the last frame,
                // so we emulate that by only "playing" the last frame of the clip
                var clipLength = clip.frames.Length;
                animator.PlayFromFrame(clipName, clipLength - 1);

                // Logger.Info(
                // $"  Played animation: {clipName}, {clipLength - 1} on {_animator.Client.name}, {_animator.Client.GetHashCode()}");
                break;
            }
        }
    }

    /// <summary>Whether this copy has sent its start-up presence events in this game.</summary>
    private bool _announced;

    /// <summary>
    /// Sends, once, the presence register events the original sends at its start (CopyPresence): the copy's FSMs are
    /// off, so this game's listeners (Sherma at the bone gate) never heard them.
    /// </summary>
    private void Announce() {
        _announced = true;
        foreach (var fsm in _fsms.Client) {
            foreach (var state in fsm.FsmStates) {
                foreach (var action in state.Actions) {
                    if (action is SendEventToRegister send && CopyPresence.Announces(send.eventName?.Value)) {
                        EventRegister.SendEvent(send.eventName!.Value);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Updates whether the game object for the client entity is active.
    /// </summary>
    /// <param name="active">The new value for active.</param>
    public void UpdateIsActive(bool active) {
        if (Object.Client != null) {
            //Logger.Info($"Entity '{Object.Client.name}' received active: {active}");
            Object.Client.SetActive(active);
            if (active && _isControlled && !_announced) Announce();
        } else {
            //Logger.Warn($"Entity ({Id}, {Type}) could not update active, because client object is null");
        }
    }

    /// <summary>
    /// Updates generic data for the client entity.
    /// </summary>
    /// <param name="entityNetworkData">A list of data to update the client entity with.</param>
    /// <param name="alreadyInSceneUpdate">Whether this data is from an already in scene update.</param>
    /// <returns>(coop) The index the data stopped at for the next frame, with a death still to come
    /// (DeathOrder.WaitsForBox); -1 when all was applied.</returns>
    public int UpdateData(List<EntityNetworkData> entityNetworkData, bool alreadyInSceneUpdate) {
        var death = entityNetworkData.FindIndex(d => d.Type == EntityComponentType.Death);
        var lastBox = death < 0
            ? -1
            : entityNetworkData.FindLastIndex(death, d => d.Type == EntityComponentType.DamageHero);
        for (var i = 0; i < entityNetworkData.Count; i++) {
            var data = entityNetworkData[i];
            if (death >= 0 && _isControlled &&
                DeathOrder.WaitsForBox(i, lastBox, death, BoxOnFrame(), Time.frameCount)) {
                return i;
            }

            if (data.Type == EntityComponentType.WalkEvent) {
                // shortcut: a late player's catch-up holds only the last event (walking or running); send both if a
                // copy is ever found in the wrong one
                if (!_isControlled || data.Packet.Length != 1 || !CopiesWalkEvents()) continue;
                var walker = Object.Client != null ? Object.Client.GetComponent<WalkerV2>() : null;
                if (walker == null) continue;
                var (running, on) = WalkEvents.Decode(data.Packet.ReadByte());
                var silent = SilentLoops(Object.Client);
                (running ? walker.OnRunning : walker.OnWalking)?.Invoke(on);
                MarkAwakeLoops(silent);
                continue;
            }

            if (data.Type == EntityComponentType.BonePress) {
                PressSentBone(data, alreadyInSceneUpdate);
                continue;
            }

            if (data.Type == EntityComponentType.Startle) {
                // a late entry's catch-up holds the last one, long played
                if (_isControlled && !alreadyInSceneUpdate && PlaysStartleSound()) PlayStartle();
                continue;
            }

            if (data.Type == EntityComponentType.ChildAnimation) {
                if (!_isControlled || !_hasParent || data.Packet.Length != 8) continue;
                var epoch = data.Packet.ReadUInt();
                var order = data.Packet.ReadUShort();
                if (EntityParenting.IsOldChildAnimation(epoch, order, _childAnimationEpoch, _lastChildAnimationOrder))
                    continue;
                _childAnimationEpoch = epoch;
                _lastChildAnimationOrder = order;
                var animationId = data.Packet.ReadByte();
                var wrapMode = (tk2dSpriteAnimationClip.WrapMode) data.Packet.ReadByte();
                UpdateAnimation(animationId, wrapMode, alreadyInSceneUpdate);
                continue;
            }

            if (data.Type == EntityComponentType.Fsm) {
                PlayMakerFSM fsm;

                if (_fsms.Client == null) {
                    continue;
                }

                if (_fsms.Client.Count > 1) {
                    // Do a check on the length of the data
                    if (data.Packet.Length < 3) {
                        continue;
                    }

                    var fsmIndex = data.Packet.ReadByte();
                    if (fsmIndex >= _fsms.Client.Count) {
                        continue;
                    }

                    fsm = _fsms.Client[fsmIndex];
                } else {
                    // Do a check on the length of the data
                    if (data.Packet.Length < 2) {
                        continue;
                    }

                    if (_fsms.Client.Count == 0) {
                        continue;
                    }

                    fsm = _fsms.Client[0];
                }

                if (fsm == null || fsm.FsmStates == null) {
                    continue;
                }

                var stateIndex = data.Packet.ReadByte();
                var actionIndex = data.Packet.ReadByte();

                if (stateIndex >= fsm.FsmStates.Length) {
                    continue;
                }

                var state = fsm.FsmStates[stateIndex];
                if (state?.Actions == null || actionIndex >= state.Actions.Length) {
                    continue;
                }

                var action = state.Actions[actionIndex];

                //Logger.Info(
                //    $"Received entity network data for FSM: {fsm.Fsm.Name}, {state.Name}, {actionIndex} ({action.GetType()})"
                //);

                EntityFsmActions.ApplyNetworkDataFromAction(data, action);
                KeepCopyStill();

                // The scene host's number for what it spawned (see ThrownComponent)
                if (action is SpawnObjectFromGlobalPool spawn) {
                    var number = data.Packet.ReadByte();
                    if (spawn.gameObject?.Value != null) {
                        Thrown?.Follow(number, spawn.storeObject.Value);
                    }
                }

                continue;
            }

            if (_components.TryGetValue(data.Type, out var component)) {
                component.Update(data, alreadyInSceneUpdate);
            } else if (data.Type == EntityComponentType.Music) {
                // The room's one music component can sit on another enemy here than in the scene host's game
                MusicComponent.Instance?.Update(data, alreadyInSceneUpdate);
            }
        }

        return -1;
    }

    /// <summary>(coop) The frame the scene host's data last turned a hurt box of the copy on, or -1.</summary>
    private int BoxOnFrame() =>
        _components.TryGetValue(EntityComponentType.DamageHero, out var component) &&
        component is DamageHeroComponent damage ? damage.BoxOnFrame : -1;

    /// <summary>
    /// Update the FSMs of the host entity to prepare for host transfer or disconnects.
    /// </summary>
    /// <summary>
    /// (coop) After a replayed action: the copy's own body keeps no velocity when it is kinematic and placed each frame
    /// by PredictiveInterpolation (<see cref="ReplayedVelocity"/>).
    /// </summary>
    private void KeepCopyStill() {
        if (Object.Client == null || !Object.Client.TryGetComponent<Rigidbody2D>(out var body)) return;
        if (ReplayedVelocity.Drops(body.isKinematic, Object.Client.TryGetComponent<PredictiveInterpolation>(out _))) {
            body.velocity = Vector2.zero;
        }
    }

    /// <param name="hostFsmData">Dictionary mapping FSM index to data.</param>
    public void UpdateHostFsmData(Dictionary<byte, EntityHostFsmData> hostFsmData) {
        foreach (var (fsmIndex, data) in hostFsmData) {
            if (_fsms.Host.Count <= fsmIndex) {
                //Logger.Warn($"Tried to update host FSM data for unknown FSM index: {fsmIndex}");
                continue;
            }

            if (_fsms.Client == null || _fsms.Client.Count <= fsmIndex || _fsms.Client[fsmIndex] == null) {
                continue;
            }

            var hostFsm = _fsms.Host[fsmIndex];
            var snapshot = _fsmSnapshots[fsmIndex];

            if (data.Types.Contains(EntityHostFsmData.Type.State)) {
                var states = hostFsm.FsmStates;
                if (states.Length <= data.CurrentState) {
                    //Logger.Warn($"Tried to update host FSM state for unknown state index: {data.CurrentState}");
                } else {
                    var stateName = states[data.CurrentState].Name;

                    snapshot.CurrentState = stateName;

                    // Also propagate this state change to the EntityFsmActions class with the client FSM for the
                    // same index
                    EntityFsmActions.RegisterStateChange(_fsms.Client[fsmIndex].Fsm, stateName);
                    // (coop) a copy made after the scene host's silk spawned shows it once it learns of the state
                    // that has it out (LateSilk): whoever enters a praying Pilgrim's room second saw no strands
                    if (LateSilk.Spawns()) EntityFsmActions.ShowLateSilk(_fsms.Client[fsmIndex].Fsm, stateName);
                    // (coop) and the state's in-state particles a late copy never got the play of (LateEffects)
                    if (LateEffects.Plays()) EntityFsmActions.PlayLateEffects(_fsms.Client[fsmIndex].Fsm, stateName);
                }
            }

            var clientFsm = _fsms.Client[fsmIndex];

            if (data.Types.Contains(EntityHostFsmData.Type.Floats)) {
                foreach (var (index, val) in data.Floats) {
                    if (index < hostFsm.FsmVariables.FloatVariables.Length) {
                        hostFsm.FsmVariables.FloatVariables[index].Value = val;
                        snapshot.Floats[index] = val;
                    }

                    if (index < clientFsm.FsmVariables.FloatVariables.Length) {
                        clientFsm.FsmVariables.FloatVariables[index].Value = val;
                    }
                }
            }

            if (data.Types.Contains(EntityHostFsmData.Type.Ints)) {
                foreach (var (index, val) in data.Ints) {
                    if (index < hostFsm.FsmVariables.IntVariables.Length) {
                        hostFsm.FsmVariables.IntVariables[index].Value = val;
                        snapshot.Ints[index] = val;
                    }

                    if (index < clientFsm.FsmVariables.IntVariables.Length) {
                        clientFsm.FsmVariables.IntVariables[index].Value = val;
                    }
                }
            }

            if (data.Types.Contains(EntityHostFsmData.Type.Bools)) {
                foreach (var (index, val) in data.Bools) {
                    if (index < hostFsm.FsmVariables.BoolVariables.Length) {
                        hostFsm.FsmVariables.BoolVariables[index].Value = val;
                        snapshot.Bools[index] = val;
                    }

                    if (index < clientFsm.FsmVariables.BoolVariables.Length) {
                        clientFsm.FsmVariables.BoolVariables[index].Value = val;
                    }
                }
            }

            if (data.Types.Contains(EntityHostFsmData.Type.Strings)) {
                foreach (var (index, val) in data.Strings) {
                    if (index < hostFsm.FsmVariables.StringVariables.Length) {
                        hostFsm.FsmVariables.StringVariables[index].Value = val;
                        snapshot.Strings[index] = val;
                    }

                    if (index < clientFsm.FsmVariables.StringVariables.Length) {
                        clientFsm.FsmVariables.StringVariables[index].Value = val;
                    }
                }
            }

            if (data.Types.Contains(EntityHostFsmData.Type.Vector2s)) {
                foreach (var (index, val) in data.Vec2s) {
                    if (index < hostFsm.FsmVariables.Vector2Variables.Length) {
                        hostFsm.FsmVariables.Vector2Variables[index].Value = (Vector2) val;
                        snapshot.Vector2s[index] = (Vector2) val;
                    }

                    if (index < clientFsm.FsmVariables.Vector2Variables.Length) {
                        clientFsm.FsmVariables.Vector2Variables[index].Value = (Vector2) val;
                    }
                }
            }

            if (data.Types.Contains(EntityHostFsmData.Type.Vector3s)) {
                foreach (var (index, val) in data.Vec3s) {
                    if (index < hostFsm.FsmVariables.Vector3Variables.Length) {
                        hostFsm.FsmVariables.Vector3Variables[index].Value = (Vector3) val;
                        snapshot.Vector3s[index] = (Vector3) val;
                    }

                    if (index < clientFsm.FsmVariables.Vector3Variables.Length) {
                        clientFsm.FsmVariables.Vector3Variables[index].Value = (Vector3) val;
                    }
                }
            }
        }
    }

    /// <summary>
    /// (coop) Scene host, once the enemy's object is gone: what it threw flies on in the other games. An enemy's object is
    /// destroyed at its death (a Spine Floater's, right after its burst lets its spines go), and the thrown component,
    /// updated with the others, stopped sending them, so the friend's spines went their own way (1.6 and 24 units from
    /// the host's a second later).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void FliesOn() => Thrown?.OnUpdate();

    /// <summary>
    /// Destroys the entity.
    /// </summary>
    public void Destroy() {
        MonoBehaviourUtil.Instance.OnUpdateEvent -= OnUpdate;
        LetGoBones();
        // (coop) by reference: on a room change the original is already destroyed, and Unity's != null would keep its
        // entry (and this whole Entity) in CopyPresses for the rest of the session
        if (!ReferenceEquals(Object.Host, null)) CopyPresses.Stop(Object.Host);

        if (_hostWalker != null) {
            if (_onWalking != null) _hostWalker.OnWalking?.RemoveListener(_onWalking);
            if (_onRunning != null) _hostWalker.OnRunning?.RemoveListener(_onRunning);
        }

        _spriteAnimatorPlayHook?.Dispose();
        _spriteAnimatorPlayHook = null;

        _objectPoolRecycleHook?.Dispose();
        _objectPoolRecycleHook = null;

        foreach (var component in _components.Values.Distinct()) {
            component.Destroy();
        }

        GamePatcher.OnEntityDestroyed(Object.Host);
        GamePatcher.OnEntityDestroyed(Object.Client);
    }

    /// <summary>
    /// Get the list of client FSMs.
    /// </summary>
    /// <returns>A list containing the client FSM instances.</returns>
    public List<PlayMakerFSM> GetClientFsms() {
        return _fsms.Client;
    }
}
