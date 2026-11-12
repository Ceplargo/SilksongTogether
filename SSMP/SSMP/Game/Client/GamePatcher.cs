using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SSMP.Animation.Effects.Movement;
using SSMP.Hooks;
using SSMP.Networking.Client;
using SSMP.Game.Client.Entity;
using SSMP.Game.Client.Entity.Component;
using SSMP.Util;
using UnityEngine;
using Logger = SSMP.Logging.Logger;

namespace SSMP.Game.Client;

/// <summary>
/// Registers and owns standalone multiplayer patches for player-only interactions, enemy target actions,
/// camera locks, damage feedback, and transition filtering.
/// </summary>
internal partial class GamePatcher {
    /// <summary>
    /// Binding flags used to access private instance members through reflection.
    /// </summary>
    private const BindingFlags InstanceNonPublicFlags = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>
    /// Binding flags used to access public instance members through reflection.
    /// </summary>
    private const BindingFlags InstancePublicFlags = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>
    /// Binding flags used to access private, public, and static members through reflection.
    /// </summary>
    private const BindingFlags StaticNonPublicPublicFlags =
        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

    /// <summary>
    /// Hook for retargeting <see cref="ChaseObject"/> movement before the original buzz chase logic runs.
    /// </summary>
    private Hook? _chaseObjectDoBuzzHook;

    /// <summary>
    /// Hook for retargeting <see cref="ChaseObjectV2"/> movement before the original chase logic runs.
    /// </summary>
    private Hook? _chaseObjectV2DoChaseHook;

    /// <summary>
    /// IL hook for suppressing local-only tink feedback and correcting remote-hit tink effects.
    /// </summary>
    private ILHook? _tinkEffectOnTriggerEnter2DHook;

    /// <summary>
    /// IL hook for suppressing local-only invincibility feedback caused by remote player hits.
    /// </summary>
    private ILHook? _healthManagerInvincibleHook;

    /// <summary>(coop) The IL hook on HardLandEffect.OnEnable's camera shake, for a copy's hard landing.</summary>
    private ILHook? _hardLandShakeHook;

    /// <summary>
    /// IL hook for preventing remote player hits from granting local-only rewards or side effects.
    /// </summary>
    private ILHook? _healthManagerTakeDamageHook;
    private Hook? _healthManagerTakeDamageDetour;

    /// <summary>
    /// (coop) Hooks for a copied swing's stun: HealthManager.Hit notes its hit's source and whether it is a first hit
    /// while it runs, and ApplyStunDamage lets a copied swing stun each enemy once (<see cref="RemoteStuns"/>).
    /// </summary>
    private Hook? _healthManagerHitHook;

    /// <summary>
    /// (coop, 0.3.21 Task 8) A remote attack's hits counted as the other player's while they are applied, and the
    /// camera shakes they set off kept to the owner's rule (<see cref="ShakeView"/>, <see cref="RemoteShakes"/>).
    /// </summary>
    private Hook? _damageEnemiesProcessDamageBufferHook;

    private Hook? _cameraShakeManagerDoShakeHook;

    /// <summary>(coop) The hook on AudioEventManager.TryPlayAudioClip, for KillSounds.</summary>
    private Hook? _audioEventPlayHook;

    private Hook? _healthManagerApplyStunDamageHook;

    private static GameObject? _hitSource;
    private static bool _hitIsFirst;

    /// <summary>
    /// Hook for filtering local-only PlayMaker method calls such as hero recoil and bounce feedback.
    /// </summary>
    private Hook? _callMethodProperDoMethodCallHook;

    /// <summary>
    /// Hook for allowing camera lock checks to remain valid while connected and paused.
    /// </summary>
    private Hook? _cameraLockAreaIsInApplicableGameStateHook;

    /// <summary>
    /// Hook for guarding <see cref="Crawler.StopCrawling"/> against destruction-time null reference errors.
    /// </summary>
    private Hook? _crawlerStopCrawlingHook;

    /// <summary>
    /// Hook that repairs SetParticleScale's cached parent state when a death effect is reparented or detached.
    /// </summary>
    private Hook? _setParticleScaleParentChangedHook;

    /// <summary>
    /// Hook for guarding <see cref="WalkerV2.StopWalking"/> against destruction-time null reference errors.
    /// </summary>
    private Hook? _walkerV2StopWalkingHook;

    /// <summary>
    /// Hook for guarding <see cref="Walker.Stop"/> against destruction-time null reference errors.
    /// </summary>
    private Hook? _walkerStopHook;

    /// <summary>
    /// The NetClient instance to check if we are connected to a server.
    /// </summary>
    private readonly NetClient _netClient;

    /// <summary>
    /// The static EntityManager instance used by enemy targeting to avoid scene-wide object scans.
    /// </summary>
    private static EntityManager? _entityManagerInstance;

    /// <summary>
    /// The hooks that are registered while connected to a server.
    /// </summary>
    private readonly List<Hook> _hooks = [];

    /// <summary>
    /// Whether knockback of the local player is currently suppressed because a remote player's attack is being
    /// processed. See <see cref="DamageEnemiesOnLateFixedUpdate"/>.
    /// </summary>
    private static bool _suppressHeroKnockback;

    /// <summary>
    /// (coop, 0.3.21) <paramref name="hit"/> run with the local player's knockback suppressed, as
    /// <see cref="DamageEnemiesOnLateFixedUpdate"/> does for a remote player's damager: a remote down slash's direct
    /// hit (<see cref="SSMP.Fsm.RemoteHeroDownAttack"/>) comes from its trigger, outside that hook.
    /// </summary>
    internal static bool AsRemoteAttack(Func<bool> hit) {
        var lastSuppress = _suppressHeroKnockback;
        _suppressHeroKnockback = KeepsLocalHeroOut();
        try {
            return hit();
        } finally {
            _suppressHeroKnockback = lastSuppress;
        }
    }

    /// <summary>
    /// (coop, 0.3.21) Whether a remote player's attack leaves the local hero and camera alone: every remote damager's
    /// knockback guard (<see cref="DamageEnemiesOnLateFixedUpdate"/>, <see cref="AsRemoteAttack"/>) and the tink's shake
    /// and recoil; a check's twin switches it off with a patch (pogo_hits, remote_tinks).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static bool KeepsLocalHeroOut() => true;

    public GamePatcher(NetClient netClient, EntityManager entityManager) {
        _netClient = netClient;
        _entityManagerInstance = entityManager;
    }

    /// <summary>
    /// Registers all gameplay hooks owned by this patcher.
    /// </summary>
    public void RegisterHooks() {
        _tinkEffectOnTriggerEnter2DHook = TryCreateILHook(
            typeof(TinkEffect),
            "TryDoTinkReactionNoDamager",
            TinkEffectOnTriggerEnter2D,
            parameterTypes: [
                typeof(GameObject), typeof(bool), typeof(bool), typeof(bool), typeof(Vector2).MakeByRefType()
            ]
        );

        _hardLandShakeHook = TryCreateILHook(typeof(HardLandEffect), "OnEnable", HardLandEffectOnEnable);

        _healthManagerInvincibleHook = TryCreateILHook(
            typeof(HealthManager), "Invincible", HealthManagerOnInvincible
        );

        _healthManagerTakeDamageHook = TryCreateILHook(
            typeof(HealthManager), "TakeDamage", HealthManagerOnTakeDamage
        );

        _healthManagerTakeDamageDetour = TryCreateHook(typeof(HealthManager), "TakeDamage", HealthManagerOnTakeDamageOnlyLanded);

        _healthManagerHitHook = TryCreateHook(typeof(HealthManager), "Hit", HealthManagerOnHit);

        _damageEnemiesProcessDamageBufferHook = TryCreateHook(
            typeof(DamageEnemies), "ProcessDamageBuffer", DamageEnemiesOnProcessDamageBuffer
        );
        _cameraShakeManagerDoShakeHook = TryCreateHook(
            typeof(CameraShakeManager), "DoShake", CameraShakeManagerOnDoShake,
            parameterTypes: [typeof(ICameraShake), typeof(UnityEngine.Object), typeof(bool), typeof(bool), typeof(bool)]
        );

        _audioEventPlayHook = TryCreateHook(
            typeof(AudioEventManager), nameof(AudioEventManager.TryPlayAudioClip), AudioEventManagerOnTryPlayAudioClip,
            StaticNonPublicPublicFlags
        );

        _healthManagerApplyStunDamageHook = TryCreateHook(
            typeof(HealthManager), "ApplyStunDamage", HealthManagerOnApplyStunDamage
        );

        _callMethodProperDoMethodCallHook = TryCreateHook(
            typeof(CallMethodProper), "DoMethodCall", CallMethodProperOnDoMethodCall
        );

        _cameraLockAreaIsInApplicableGameStateHook = TryCreateHook(
            typeof(CameraLockArea),
            "IsInApplicableGameState",
            CameraLockAreaOnIsInApplicableGameState,
            StaticNonPublicPublicFlags
        );

        EventHooks.InteractableBaseAddInsideIL += ILInteractableBaseAddInside;
        EventHooks.InteractableBaseLocalAddInsideIL += ILInteractableBaseAddInside;

        EventHooks.TransitionPointOnTriggerEnter2DIL += ILTransitionPointOnTrigger2D;
        EventHooks.TransitionPointOnTriggerStay2DIL += ILTransitionPointOnTrigger2D;

        EventHooks.CameraLockAreaAwake += OnCameraLockAreaAwake;

        _chaseObjectDoBuzzHook = TryCreateHook(
            typeof(ChaseObject), "DoBuzz", OnChaseObjectDoBuzz, InstanceNonPublicFlags
        );

        _chaseObjectV2DoChaseHook = TryCreateHook(
            typeof(ChaseObjectV2), "DoChase", OnChaseObjectV2DoChase, InstanceNonPublicFlags
        );

        RegisterDetectionHooks();

        _crawlerStopCrawlingHook = TryCreateHook(
            typeof(Crawler),
            "StopCrawling",
            OnCrawlerStopCrawling
        );

        _setParticleScaleParentChangedHook = TryCreateHook(
            typeof(EnemyDeathEffects).Assembly.GetType("SetParticleScale"),
            "OnTransformParentChanged",
            OnSetParticleScaleParentChanged
        );

        _walkerV2StopWalkingHook = TryCreateHook(
            typeof(WalkerV2),
            "StopWalking",
            OnWalkerV2StopWalking
        );

        _walkerStopHook = TryCreateHook(
            typeof(Walker),
            "Stop",
            OnWalkerStop,
            parameterTypes: typeof(Walker.StopReasons)
        );

        RegisterAggressionHooks();

        AddHook(typeof(AudioSource), nameof(AudioSource.Play), AudioSourceOnPlay, Type.EmptyTypes);
        AddHook(typeof(FlingObject), nameof(FlingObject.OnEnter), FlingObjectOnEnter);
        AddHook(typeof(HealthManager), "SpawnItemDrop", HealthManagerSpawnItemDrop);
        if (typeof(FlingUtils).GetMethod(nameof(FlingUtils.FlingObject), BindingFlags.Public | BindingFlags.Static, null,
                [typeof(FlingUtils.SelfConfig), typeof(Transform), typeof(Vector3)], null) is { } fling) {
            _hooks.Add(new Hook(fling, FlingUtilsFlingObject));
        } else {
            Logger.Error("Could not hook FlingUtils#FlingObject: method does not exist");
        }
        AddHook(typeof(DamageEnemies), nameof(DamageEnemies.LateFixedUpdate), DamageEnemiesOnLateFixedUpdate);
        AddHook(typeof(HeroController), nameof(HeroController.CanRecoil), HeroControllerOnCanRecoil);
        AddHook(typeof(HeroController), nameof(HeroController.RecoilDown), HeroControllerOnRecoilDown);
        // (coop, 0.3.21) the soft landings the game spawns for this game's hero: Landing sends only those
        AddHook(typeof(HeroController), nameof(HeroController.SpawnSoftLandingPrefab), HeroControllerOnSoftLanding);
        AddHook(typeof(HeroController), nameof(HeroController.PlaySoftLandingEffect), HeroControllerOnSoftLanding);
        // (coop, 0.3.21) this game's hero's landings on a tilting platform: RemoteTouches sends them
        AddHook(typeof(TiltPlat), "OnCollisionEnter2D", TiltPlatOnCollisionEnter2D);
        // (coop, 0.3.21) this game's hero's dirt falls from a dusty platform: RemoteTouches sends them
        AddHook(typeof(DustyPlatform), "OnCollisionEnter2D", DustyPlatformOnCollisionEnter2D);
        // (coop, 0.3.21) an enemy copy presses the bone segments its original's game sends (CopyPresses)
        AddHook(typeof(BoneSegment), "OnTriggerEnter2D", BoneSegmentOnTrigger);
        AddHook(typeof(BoneSegment), "OnTriggerExit2D", BoneSegmentOnTrigger);
        AddHook(typeof(BouncePod), nameof(BouncePod.Hit), IgnoreRemoteHit<BouncePod>);
        AddHook(typeof(BounceBalloon), nameof(BounceBalloon.Hit), IgnoreRemoteHit<BounceBalloon>);
        AddHook(typeof(HitResponse), nameof(HitResponse.Hit), HitResponseOnHit);
        AddHook(
            typeof(NailSlashTerrainThunk),
            nameof(NailSlashTerrainThunk.OnCollisionEnter2D),
            NailSlashTerrainThunkOnCollisionEnter2D
        );
    }

    /// <summary>
    /// Creates a runtime detour for an instance or static method when that method exists in the current game version.
    /// </summary>
    /// <param name="type">The type that declares the target method.</param>
    /// <param name="methodName">The name of the target method.</param>
    /// <param name="detour">The delegate that replaces the target method.</param>
    /// <param name="flags">Binding flags used to resolve the target method.</param>
    /// <param name="parameterTypes">The target method's parameter types when overload resolution is required.</param>
    /// <returns>The registered hook, or <see langword="null"/> when registration fails.</returns>
    private static Hook? TryCreateHook(
        Type? type,
        string methodName,
        Delegate detour,
        BindingFlags flags = InstancePublicFlags | InstanceNonPublicFlags,
        params Type[] parameterTypes
    ) {
        if (type == null) {
            Logger.Error($"Could not find type for #{methodName}; hook was not registered");
            return null;
        }

        var method = parameterTypes.Length == 0
            ? type.GetMethod(methodName, flags)
            : type.GetMethod(methodName, flags, null, parameterTypes, null);

        if (method == null) {
            Logger.Error($"Could not find {type.FullName}#{methodName}; hook was not registered");
            return null;
        }

        try {
            return new Hook(method, detour);
        } catch (Exception e) {
            Logger.Error($"Could not hook {type.FullName}#{methodName}:\n{e}");
            return null;
        }
    }

    /// <summary>
    /// Creates a runtime IL detour for an instance or static method when that method exists in the current game version.
    /// </summary>
    /// <param name="type">The type that declares the target method.</param>
    /// <param name="methodName">The name of the target method.</param>
    /// <param name="manipulator">The IL manipulator that patches the target method body.</param>
    /// <param name="flags">Binding flags used to resolve the target method.</param>
    /// <param name="parameterTypes">The target method's parameter types when overload resolution is required.</param>
    /// <returns>The registered IL hook, or <see langword="null"/> when registration fails.</returns>
    private static ILHook? TryCreateILHook(
        Type type,
        string methodName,
        ILContext.Manipulator manipulator,
        BindingFlags flags = InstancePublicFlags | InstanceNonPublicFlags,
        params Type[] parameterTypes
    ) {
        var method = parameterTypes.Length == 0
            ? type.GetMethod(methodName, flags)
            : type.GetMethod(methodName, flags, null, parameterTypes, null);

        if (method == null) {
            Logger.Error($"Could not find {type.FullName}#{methodName}; hook was not registered");
            return null;
        }

        try {
            return new ILHook(method, manipulator);
        } catch (Exception e) {
            Logger.Error($"Could not hook {type.FullName}#{methodName}:\n{e}");
            return null;
        }
    }

    /// <summary>
    /// Deregisters and disposes all gameplay hooks owned by this patcher.
    /// </summary>
    public void DeregisterHooks() {
        _tinkEffectOnTriggerEnter2DHook?.Dispose();
        _tinkEffectOnTriggerEnter2DHook = null;

        _healthManagerInvincibleHook?.Dispose();
        _hardLandShakeHook?.Dispose();
        _hardLandShakeHook = null;
        _healthManagerInvincibleHook = null;

        _healthManagerTakeDamageHook?.Dispose();
        _healthManagerTakeDamageHook = null;

        _healthManagerTakeDamageDetour?.Dispose();
        _healthManagerTakeDamageDetour = null;

        _healthManagerHitHook?.Dispose();
        _healthManagerHitHook = null;
        _damageEnemiesProcessDamageBufferHook?.Dispose();
        _damageEnemiesProcessDamageBufferHook = null;
        _cameraShakeManagerDoShakeHook?.Dispose();
        _cameraShakeManagerDoShakeHook = null;
        _audioEventPlayHook?.Dispose();
        _audioEventPlayHook = null;

        _healthManagerApplyStunDamageHook?.Dispose();
        _healthManagerApplyStunDamageHook = null;

        _callMethodProperDoMethodCallHook?.Dispose();
        _callMethodProperDoMethodCallHook = null;

        _cameraLockAreaIsInApplicableGameStateHook?.Dispose();
        _cameraLockAreaIsInApplicableGameStateHook = null;

        EventHooks.InteractableBaseAddInsideIL -= ILInteractableBaseAddInside;
        EventHooks.InteractableBaseLocalAddInsideIL -= ILInteractableBaseAddInside;

        EventHooks.TransitionPointOnTriggerEnter2DIL -= ILTransitionPointOnTrigger2D;
        EventHooks.TransitionPointOnTriggerStay2DIL -= ILTransitionPointOnTrigger2D;

        EventHooks.CameraLockAreaAwake -= OnCameraLockAreaAwake;

        _chaseObjectDoBuzzHook?.Dispose();
        _chaseObjectDoBuzzHook = null;

        _chaseObjectV2DoChaseHook?.Dispose();
        _chaseObjectV2DoChaseHook = null;

        DisposeDetectionHooks();
        DisposeAggressionHooks();

        _crawlerStopCrawlingHook?.Dispose();
        _crawlerStopCrawlingHook = null;

        _setParticleScaleParentChangedHook?.Dispose();
        _setParticleScaleParentChangedHook = null;

        _walkerV2StopWalkingHook?.Dispose();
        _walkerV2StopWalkingHook = null;

        _walkerStopHook?.Dispose();
        _walkerStopHook = null;

        foreach (var hook in _hooks) {
            hook.Dispose();
        }

        _hooks.Clear();
        _suppressHeroKnockback = false;
    }

    /// <summary>
    /// Create a hook for the method with the given name on the given type and store it, so it can be disposed when
    /// de-registering the hooks.
    /// </summary>
    /// <param name="type">The type that declares the method to hook.</param>
    /// <param name="methodName">The name of the method to hook.</param>
    /// <param name="detour">The delegate that replaces the method.</param>
    private void AddHook(Type type, string methodName, Delegate detour, Type[]? parameterTypes = null) {
        var flags = InstanceNonPublicFlags | InstancePublicFlags;
        var method = parameterTypes == null ? type.GetMethod(methodName, flags)
            : type.GetMethod(methodName, flags, null, parameterTypes, null);
        if (method == null) {
            Logger.Error($"Could not hook {type.Name}#{methodName}: method does not exist");
            return;
        }

        _hooks.Add(new Hook(method, detour));
    }

    /// <summary>
    /// (coop, 0.3.21) An enemy's death pickup flung (FlingObject: the Rock Roller corpse's wish item): the other games keep
    /// theirs unflung to follow the scene host's, which its game follows as a flung throw (ThrownComponent).
    /// </summary>
    /// <summary>(coop, 0.3.21) The enemy whose item drop is being flung (HealthManagerSpawnItemDrop), else null.</summary>
    private static (HealthManager Enemy, SavedItem Item)? _dropping;

    /// <summary>
    /// (coop, 0.3.21) An enemy's item drop (a Pilgrim's Pilgrim Rag): another game running it leaves its own roll out,
    /// the scene host's game follows its own (ThrownComponent.SkipsOwnDrop, DropFlung).
    /// </summary>
    private static void HealthManagerSpawnItemDrop(
        Action<HealthManager, SavedItem, int, CollectableItemPickup, Transform, int> orig,
        HealthManager self, SavedItem dropItem, int count, CollectableItemPickup prefab, Transform spawnPoint, int limit) {
        if (dropItem != null && Entity.Component.ThrownComponent.SkipsOwnDrop(self)) {
            Logger.Info($"(coop) Drop {dropItem.name} of {self.name} left out: the scene host's comes");
            return;
        }

        _dropping = dropItem != null ? (self, dropItem) : null;
        try {
            orig(self, dropItem!, count, prefab, spawnPoint, limit);
        } finally {
            _dropping = null;
        }
    }

    private static void FlingUtilsFlingObject(Action<FlingUtils.SelfConfig, Transform, Vector3> orig,
        FlingUtils.SelfConfig config, Transform spawnPoint, Vector3 positionOffset) {
        orig(config, spawnPoint, positionOffset);
        // (coop) only an item drop's fling goes out (FlungThrows.SendsFling): money flings stay each game's own
        if (config.Object != null) {
            Entity.Component.ThrownComponent.DropFlung(_dropping?.Enemy, _dropping?.Item, config.Object);
        }
    }

    private static void FlingObjectOnEnter(Action<FlingObject> orig, FlingObject self) {
        var thrower = self.Fsm?.GameObject;
        var flung = self.Fsm != null && self.flungObject != null ? self.Fsm.GetOwnerDefaultTarget(self.flungObject) : null;
        if (Entity.Component.ThrownComponent.SkipsPickupFling(thrower, flung)) {
            self.Finish();
            return;
        }

        orig(self);
        Entity.Component.ThrownComponent.PickupFlung(thrower, flung);
    }

    private static void AudioSourceOnPlay(Action<AudioSource> orig, AudioSource self) {
        orig(self);
        if (self.loop) {
            LoopStarts.Played(self, Time.unscaledTime);
        }
    }

    /// <summary>
    /// Forces <see cref="ChaseObject"/> to consume the enemy-approved multiplayer target before running chase logic.
    /// </summary>
    /// <param name="orig">The original chase method.</param>
    /// <param name="self">The chase action instance.</param>
    private static void OnChaseObjectDoBuzz(Action<ChaseObject> orig, ChaseObject self) {
        ForceApprovedTargetOnFsmAction(self);

        if (self.target?.Value == null) {
            return;
        }

        orig(self);
    }

    /// <summary>
    /// Forces <see cref="ChaseObjectV2"/> to consume the enemy-approved multiplayer target before running chase logic.
    /// </summary>
    /// <param name="orig">The original chase method.</param>
    /// <param name="self">The chase action instance.</param>
    private static void OnChaseObjectV2DoChase(Action<ChaseObjectV2> orig, ChaseObjectV2 self) {
        ForceApprovedTargetOnFsmAction(self);

        if (self.target?.Value == null) {
            return;
        }

        orig(self);
    }

    /// <summary>
    /// Guards <see cref="Crawler.StopCrawling"/> against null-reference failures during destruction or disable.
    /// </summary>
    /// <param name="orig">The original <see cref="Crawler.StopCrawling"/> method.</param>
    /// <param name="self">The crawler instance.</param>
    private static void OnCrawlerStopCrawling(Action<Crawler> orig, Crawler self) {
        try {
            orig(self);
        } catch (NullReferenceException) {
            Logger.Debug("Safely caught NullReferenceException in Crawler.StopCrawling");
        }
    }

    /// <summary>
    /// Clears SetParticleScale's stale body cache after a death effect is detached.
    /// The native method leaves <c>hasParentBody</c> true, so the global callback dereferences the destroyed body.
    /// </summary>
    /// <param name="orig">The original SetParticleScale parent-change method.</param>
    /// <param name="self">The SetParticleScale component instance.</param>
    private static void OnSetParticleScaleParentChanged(Action<MonoBehaviour> orig, MonoBehaviour self) {
        orig(self);

        if (self.transform.parent != null) {
            return;
        }

        SetSetParticleScaleField(self, "parent", null);
        SetSetParticleScaleField(self, "hasParent", false);
        SetSetParticleScaleField(self, "parentBody", null);
        SetSetParticleScaleField(self, "hasParentBody", false);
        SetSetParticleScaleField(self, "unparented", true);
        SetSetParticleScaleField(self, "updated", false);
    }

    /// <summary>
    /// Writes one SetParticleScale lifecycle field when it exists in the current game version.
    /// </summary>
    /// <param name="component">The SetParticleScale instance.</param>
    /// <param name="fieldName">The private field name.</param>
    /// <param name="value">The replacement field value.</param>
    private static void SetSetParticleScaleField(MonoBehaviour component, string fieldName, object? value) {
        var field = component.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        field?.SetValue(component, value);
    }

    /// <summary>
    /// Guards <c>WalkerV2.StopWalking</c> against null-reference failures during destruction or disable.
    /// </summary>
    /// <param name="orig">The original <c>WalkerV2.StopWalking</c> method.</param>
    /// <param name="self">The walker instance.</param>
    private static void OnWalkerV2StopWalking(Action<WalkerV2> orig, WalkerV2 self) {
        try {
            orig(self);
        } catch (NullReferenceException) {
            Logger.Debug("Safely caught NullReferenceException in WalkerV2.StopWalking");
        }
    }

    /// <summary>
    /// Guards <see cref="Walker.Stop"/> against null-reference failures during destruction or disable.
    /// </summary>
    /// <param name="orig">The original <see cref="Walker.Stop"/> method.</param>
    /// <param name="self">The walker instance.</param>
    /// <param name="reason">The reason movement is being stopped.</param>
    private static void OnWalkerStop(
        Action<Walker, Walker.StopReasons> orig,
        Walker self,
        Walker.StopReasons reason
    ) {
        try {
            orig(self, reason);
        } catch (NullReferenceException) {
            Logger.Debug("Safely caught NullReferenceException in Walker.Stop");
        }
    }

    /// <summary>
    /// Determines whether a hit source belongs to the local player.
    /// </summary>
    /// <param name="source">The source object stored on a hit or attack object.</param>
    /// <returns>True when the source is local or unknown; otherwise false.</returns>
    /// <summary>(coop) Whether a hit source is another player's: under a tracked player that is not this game's hero.</summary>
    private static bool IsRemotePlayerSource(GameObject? source) {
        if (source == null) return false;
        var root = source.GetHitSourceRoot();
        var hero = HeroController.instance != null ? HeroController.instance.gameObject : null;
        foreach (var player in PlayerTargetRegistry.GetTrackedPlayers()) {
            if (player == null || player == hero) continue;
            if (source.transform.IsChildOf(player.transform) || player.transform.root == root) return true;
        }

        return false;
    }

    private static bool IsLocalHitSource(GameObject? source) {
        var root = source.GetHitSourceRoot();
        if (root == null) {
            return true;
        }

        var rootObject = root.gameObject;
        return rootObject.CompareTag("Player") || rootObject.name == "Knight";
    }

    /// <summary>
    /// Determines whether the provided hit instance belongs to the local player.
    /// </summary>
    /// <param name="hitInstance">The hit instance to inspect.</param>
    /// <returns>True when the hit is local or unknown; otherwise false.</returns>
    private static bool IsLocalHitSource(HitInstance hitInstance) {
        return IsLocalHitSource(hitInstance.Source);
    }

    /// <summary>
    /// Gets the best available world position for a hit source.
    /// </summary>
    /// <param name="source">The source object stored on a hit or attack object.</param>
    /// <returns>The root transform position when available, otherwise the source position or zero.</returns>
    private static Vector3 GetHitSourcePosition(GameObject? source) {
        var root = source.GetHitSourceRoot();
        if (root != null) {
            return root.position;
        }

        return source != null ? source.transform.position : Vector3.zero;
    }

    /// <summary>
    /// IL hook to change the behaviour of the <see cref="InteractableBase"/> to add a check for whether it is dealing
    /// with the local player.
    /// </summary>
    private void ILInteractableBaseAddInside(ILContext il) {
        try {
            // Create a cursor for this context
            var c = new ILCursor(il);

            var beforeFirstReturnLabel = c.DefineLabel();

            c.GotoNext(
                MoveType.Before,
                i => i.MatchLdarg(1),
                i => i.MatchCallvirt(typeof(Component), "get_gameObject"),
                i => i.MatchCallvirt(typeof(GameObject), "get_layer"),
                i => i.MatchLdcI4(9),
                i => i.MatchBeq(out _)
            );

            c.Emit(OpCodes.Ldarg_1);
            // Emit a delegate that pops the collider argument off the stack and pushes a boolean onto the stack
            // that indicates whether the collider's game object has the tag "Player"
            c.EmitDelegate<Func<Collider2D, bool>>(col => col.gameObject.tag == "Player");

            // Branch if the tag is not "Player" to the pre-defined label, which is before the return
            // In other words, we return if it is not the local player
            c.Emit(OpCodes.Brfalse, beforeFirstReturnLabel);

            // Goto before the next return to mark our label there, so we can branch to it
            c.GotoNext(
                MoveType.Before,
                i => i.MatchRet()
            );

            c.MarkLabel(beforeFirstReturnLabel);
        } catch (Exception e) {
            Logger.Error($"Could not change InteractableBase#AddInside IL:\n{e}");
        }
    }

    /// <summary>
    /// IL hook to change the behaviour of the <see cref="TransitionPoint"/> to add a check for whether it is dealing
    /// with the local player.
    /// </summary>
    private void ILTransitionPointOnTrigger2D(ILContext il) {
        try {
            // Create a cursor for this context
            var c = new ILCursor(il);

            ILLabel? returnLabel = null;

            c.GotoNext(
                MoveType.After,
                i => i.MatchLdarg(1),
                i => i.MatchCallvirt(typeof(Component), "get_gameObject"),
                i => i.MatchCallvirt(typeof(GameObject), "get_layer"),
                i => i.MatchLdcI4(9),
                i => i.MatchBneUn(out returnLabel)
            );

            if (returnLabel == null) {
                Logger.Error($"Could not change TransitionPoint#OnTrigger{{Enter,Stay}}2D IL:\nCould not find label");
                return;
            }

            c.Emit(OpCodes.Ldarg_1);
            // Emit a delegate that pops the collider argument off the stack and pushes a boolean onto the stack
            // that indicates whether the collider's game object has the tag "Player"
            c.EmitDelegate<Func<Collider2D, bool>>(movingObj => movingObj.gameObject.tag == "Player");

            // Branch if the tag is not "Player" to the pre-defined label, which is before the return
            // In other words, we return if it is not the local player
            c.Emit(OpCodes.Brfalse, returnLabel);
        } catch (Exception e) {
            Logger.Error($"Could not change TransitionPoint#OnTrigger{{Enter,Stay}}2D IL:\n{e}");
        }
    }

    /// <summary>
    /// IL hook to change the tink reaction flow so remote player hits do not trigger local-only feedback.
    /// </summary>
    private void TinkEffectOnTriggerEnter2D(ILContext il) {
        try {
            var c = new ILCursor(il);
            c.Emit(OpCodes.Ldarg_1);

            var isLocalPlayer = true;
            c.EmitDelegate<Action<GameObject>>(source => { isLocalPlayer = IsLocalHitSource(source); });

            // Replace the local-hero position with the remote player's root position for spawned tink effects.
            c.GotoNext(
                MoveType.After,
                i => i.MatchStloc(6)
            );

            var afterRemotePositionLabel = c.DefineLabel();
            c.EmitDelegate(() => isLocalPlayer);
            c.Emit(OpCodes.Brtrue, afterRemotePositionLabel);
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Func<GameObject, Vector3>>(GetHitSourcePosition);
            c.Emit(OpCodes.Stloc, 6);
            c.MarkLabel(afterRemotePositionLabel);

            // Skip the camera shake block for remote hits.
            c.GotoNext(
                MoveType.After,
                i => i.MatchLdarg(2),
                i => i.MatchLdloc(1),
                i => i.MatchAnd(),
                i => i.MatchBrfalse(out _)
            );

            // (coop, 0.3.21 Task 8) a remote tink's shake goes ahead while its object shows here (the owner's rule)
            var afterCameraShakeLabel = c.DefineLabel();
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<TinkEffect, bool>>(tink => ShakeView.TinkShakes(
                isLocalPlayer, KeepsLocalHeroOut(), RemoteShakes.Shows(tink), ShakeView.OnlyInView()));
            c.Emit(OpCodes.Brfalse, afterCameraShakeLabel);

            c.GotoNext(
                MoveType.Before,
                i => i.MatchLdloca(13)
            );

            c.MarkLabel(afterCameraShakeLabel);

            // Skip local recoil feedback for remote hits.
            foreach (var recoilMethod in new[] { "RecoilLeft", "RecoilDown", "RecoilRight" }) {
                c.GotoNext(
                    MoveType.Before,
                    inst => inst.MatchLdloc(4),
                    inst => inst.MatchCallvirt(typeof(HeroController), recoilMethod)
                );

                var afterRecoilLabel = c.DefineLabel();
                c.EmitDelegate(() => isLocalPlayer || !KeepsLocalHeroOut());
                c.Emit(OpCodes.Brfalse, afterRecoilLabel);
                c.GotoNext(
                    MoveType.After,
                    inst => inst.MatchLdloc(4),
                    inst => inst.MatchCallvirt(typeof(HeroController), recoilMethod)
                );
                c.MarkLabel(afterRecoilLabel);
            }

            // Skip the optional FSM event for remote hits.
            c.GotoNext(
                MoveType.Before,
                i => i.MatchLdarg(0),
                i => i.MatchLdfld(typeof(TinkEffect), "sendFSMEvent"),
                i => i.MatchBrfalse(out _),
                i => i.MatchLdarg(0),
                i => i.MatchLdfld(typeof(TinkEffect), "fsm"),
                i => i.MatchCall(typeof(UnityEngine.Object), "op_Implicit"),
                i => i.MatchBrfalse(out _)
            );

            var afterSendEventLabel = c.DefineLabel();
            c.EmitDelegate(() => isLocalPlayer);
            c.Emit(OpCodes.Brfalse, afterSendEventLabel);
            c.GotoNext(
                MoveType.After,
                i => i.MatchLdarg(0),
                i => i.MatchLdfld(typeof(TinkEffect), "fsm"),
                i => i.MatchLdarg(0),
                i => i.MatchLdfld(typeof(TinkEffect), "FSMEvent"),
                i => i.MatchCallvirt(typeof(PlayMakerFSM), "SendEvent")
            );
            c.MarkLabel(afterSendEventLabel);
        } catch (Exception e) {
            Logger.Error($"Could not change TinkEffect#OnTriggerEnter2D IL:\n{e}");
        }
    }

    private void HealthManagerOnInvincible(ILContext il) {
        try {
            var c = new ILCursor(il);
            c.Emit(OpCodes.Ldarg_1);

            var isLocalPlayer = true;
            c.EmitDelegate<Action<HitInstance>>(hitInstance => { isLocalPlayer = IsLocalHitSource(hitInstance); });

            // Skip the local recoil and blocked-hit shake for remote hits.
            c.GotoNext(
                MoveType.Before,
                i => i.MatchLdarg(0),
                i => i.MatchLdfld(typeof(HealthManager), "preventInvincibleShake"),
                i => i.MatchBrtrue(out _)
            );

            var afterLocalFeedbackLabel = c.DefineLabel();
            c.EmitDelegate(() => isLocalPlayer);
            c.Emit(OpCodes.Brfalse, afterLocalFeedbackLabel);

            c.GotoNext(
                MoveType.Before,
                i => i.MatchLdarg(0),
                i => i.MatchLdfld(typeof(HealthManager), "boxCollider"),
                i => i.MatchStloc(6)
            );

            c.MarkLabel(afterLocalFeedbackLabel);
        } catch (Exception e) {
            Logger.Error($"Could not change HealthManager#OnInvincible IL:\n{e}");
        }
    }

    /// <summary>
    /// (coop) HardLandEffect.OnEnable sends its camera shake through HardLanding.Shake, which leaves it out while a
    /// copy's hard landing effect is spawned: the game alone shakes only for its own hero.
    /// </summary>
    private static void HardLandEffectOnEnable(ILContext il) {
        try {
            var c = new ILCursor(il);
            c.GotoNext(
                MoveType.After,
                i => i.MatchLdstr("AverageShake"),
                i => i.MatchCallvirt<PlayMakerFSM>(nameof(PlayMakerFSM.SendEvent))
            );
            c.Index--;
            c.Remove();
            c.EmitDelegate<Action<PlayMakerFSM, string>>(HardLanding.Shake);
        } catch (Exception e) {
            Logger.Error($"Could not change HardLandEffect#OnEnable IL:\n{e}");
        }
    }

    /// <summary>
    /// IL Hook to modify the behaviour of the TakeDamage method in HealthManager. This modification adds a
    /// conditional branch in case the nail swing from the HitInstance was from a remote player to ensure that
    /// soul is not gained for remote hits.
    /// </summary>
    private void HealthManagerOnTakeDamage(ILContext il) {
        try {
            var c = new ILCursor(il);

            // Remote hits must not grant the local player silk or reaper payouts.
            ILLabel? targetLabel = null;
            c.GotoNext(
                MoveType.Before,
                i => i.MatchLdarg(0),
                i => i.MatchLdfld(typeof(HealthManager), "enemyType"),
                i => i.MatchLdcI4(3),
                i => i.MatchBeq(out targetLabel)
            );

            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Func<HitInstance, bool>>(hitInstance => !IsLocalHitSource(hitInstance));
            c.Emit(OpCodes.Brtrue, targetLabel);
        } catch (Exception e) {
            Logger.Error($"Could not change HealthManager#TakeDamage IL:\n{e}");
        }
    }

    /// <summary>
    /// Hook for the 'DoMethodCall' method in the 'CallMethodProper' FSM action. This is used for the Crystal Shot
    /// game object to ensure that knockback is not applied to the local player if a remote player hits the crystal.
    /// </summary>
    /// <summary>
    /// (coop) Notes the hit's source and whether it is a first hit while the hit runs, for its stun.
    /// </summary>
    /// <summary>
    /// (coop, 0.3.21 Task 8) A remote attack's buffered hits on enemies and objects: each counts as the other player's
    /// hit while it is applied, so the camera shakes it sets off here keep to the owner's rule.
    /// </summary>
    private static void DamageEnemiesOnProcessDamageBuffer(Action<DamageEnemies> orig, DamageEnemies self) {
        // (coop) a followed copy's damage (StageBreaks): props and HitResponses only, the scene host's game hits the enemies
        if (self && Entity.Component.ThrownComponent.CopyBreakers.Contains(self)) {
            CurrentDamageBuffer(self)?.RemoveAll(r => !Entity.Component.StageBreaks.Keeps(
                true, r.Responder is Breakable, r.Responder is global::HitResponse));
        }

        var remote = self && !IsLocalHitSource(self.gameObject);
        if (remote) RemoteShakes.Begin();
        try {
            orig(self);
        } finally {
            if (remote) RemoteShakes.End();
        }
    }

    private static readonly AccessTools.FieldRef<DamageEnemies, List<DamageEnemies.HitResponse>> CurrentDamageBufferRef =
        AccessTools.FieldRefAccess<DamageEnemies, List<DamageEnemies.HitResponse>>("currentDamageBuffer");

    /// <summary>(coop) A damager's targets this frame, before ProcessDamageBuffer hits them.</summary>
    private static List<DamageEnemies.HitResponse>? CurrentDamageBuffer(DamageEnemies self) =>
        CurrentDamageBufferRef(self);

    /// <summary>
    /// (coop, 0.3.21 Task 8) A camera shake goes ahead unless a remote player's hit set it off and its source does not
    /// show on this screen (<see cref="ShakeView.Shakes"/>).
    /// </summary>
    private static void CameraShakeManagerOnDoShake(
        Action<CameraShakeManager, ICameraShake, UnityEngine.Object, bool, bool, bool> orig,
        CameraShakeManager self, ICameraShake shake, UnityEngine.Object source, bool doFreeze, bool vibrate,
        bool sendWorldForce
    ) {
        if (RemoteShakes.InRemoteHit && !ShakeView.Shakes(true, RemoteShakes.Shows(source), ShakeView.OnlyInView())) {
            Logger.Info($"(coop) a remote hit's shake left out, its source not shown here: {RemoteShakes.Described(shake, source)}");
            return;
        }

        orig(self, shake, source, doFreeze, vibrate, sendWorldForce);
    }

    private IHitResponder.HitResponse HealthManagerOnHit(
        Func<HealthManager, HitInstance, IHitResponder.HitResponse> orig,
        HealthManager self,
        HitInstance hitInstance
    ) {
        var (source, first) = (_hitSource, _hitIsFirst);
        _hitSource = hitInstance.Source;
        _hitIsFirst = hitInstance.IsFirstHit;
        // (coop) its kind goes with the drop it makes: the hero's, and the world's (the lava), not another player's.
        // Noted before the hit, as a killing hit's drop is sent from its Die within it, and put back after if it took no
        // health (HitNotes: a blast the enemy ignores took the slash's kind in that frame)
        var local = IsLocalHitSource(hitInstance);
        var remote = IsRemotePlayerSource(hitInstance.Source);
        var replaced = HitNotes.Notes(local, remote, true) ? HealthManagerComponent.NoteOwnHit(self, hitInstance) : null;
        var hp = self.hp;
        // (coop) a world object's hit: a copy shows no block of its own (its timer set, as just after one), and the scene
        // host's game sends the block it shows (BlockedHits: Bone_09's Dock Flyer blocking a DF Lava Rock)
        var world = BlockedHits.World(local, remote, hitInstance.IsHeroDamage);
        var tink = self.tinkTimer;
        var leavesOut = BlockedHits.LeavesOut(HealthManagerComponent.FollowsHost(self), world);
        if (leavesOut) self.tinkTimer = 1f;
        if (HealthManagerComponent.LogsHealth) {
            Logger.Debug($"(coop) hit: {self.name} from {PathOf(hitInstance.Source)}, first {hitInstance.IsFirstHit}, " +
                         $"frame {Time.frameCount}");
        }

        IHitResponder.HitResponse response = IHitResponder.Response.None;
        try {
            response = orig(self, hitInstance);
            return response;
        } finally {
            (_hitSource, _hitIsFirst) = (source, first);
            if (replaced is { } note && !HitNotes.Notes(local, remote, self.hp < hp)) {
                HealthManagerComponent.PutBackOwnHit(self, note);
            }

            var blocked = response.response == IHitResponder.Response.Invincible && !self.PreventInvincibleEffect;
            if (leavesOut) {
                self.tinkTimer = tink;
                if (blocked && HealthManagerComponent.LogsHealth) {
                    Logger.Debug($"(coop) block left out: {self.name}, frame {Time.frameCount}");
                }
            } else if (BlockedHits.Sends(HealthManagerComponent.HostsHits(self), world, blocked, tink, self.tinkTimer)) {
                HealthManagerComponent.SendBlockedHit(self, hitInstance);
            }
        }
    }

    /// <summary>
    /// (coop) A copied swing stuns an enemy at most once, however many of its first hits land on it
    /// (<see cref="RemoteStuns"/>): each of its hits still plays and has its health put back, only a second stun is
    /// left out. In the combined 0.3.15 gate the friend's copied swing hit Mossbone Mother twice in one frame in the
    /// scene host's game, and she staggered one friend hit early.
    /// </summary>
    private void HealthManagerOnApplyStunDamage(Action<HealthManager, float> orig, HealthManager self, float stunDamage) {
        var swing = _hitIsFirst && _hitSource != null ? SwingOf(_hitSource) : null;
        var applied = swing == null || !OnceASwing() ||
                      RemoteStuns.Seen.First(self.GetInstanceID(), swing.GetInstanceID(), Time.unscaledTimeAsDouble);
        if (HealthManagerComponent.LogsHealth) {
            Logger.Debug($"(coop) stun: {self.name} {stunDamage} from {PathOf(_hitSource)}, first {_hitIsFirst}, " +
                         $"frame {Time.frameCount}, swing {(swing != null ? swing.GetInstanceID() : 0)}, " +
                         (applied ? "applied" : "left out"));
        }

        if (applied) {
            orig(self, stunDamage);
            if (_hitSource != null && SwingOf(_hitSource) == null) {
                HealthManagerComponent.AddOwnStun(self, stunDamage);  // this game's own hit: sent with its health change
            }
        }
    }

    /// <summary>
    /// (coop) In the scene host's game a copied swing no longer runs the damage branch (damage, stun, flash and sound) on
    /// an enemy whose health comes from the other players' games: each of their landed hits plays here instead, at once
    /// and with the stun it had there (HealthManagerComponent.PlayMissedHits). A blocked or invincible hit is answered in
    /// HealthManager.Hit before this, so Lace's Counter Stance still counters a copied swing. In the combined 0.3.15 gates
    /// copied swings stunned where the friend's swing had missed or been parried and missed where it had landed, so
    /// Mossbone Mother staggered a friend hit early, and the Bone Flyer Giant played 19 hit sounds for 15 hits.
    /// </summary>
    private void HealthManagerOnTakeDamageOnlyLanded(
        Action<HealthManager, HitInstance> orig, HealthManager self, HitInstance hitInstance
    ) {
        // (coop) and, B's mirror, in a game that follows the scene host, on its copy of the enemy
        if (OnlyLandedHits() && hitInstance.Source != null && SwingOf(hitInstance.Source) != null &&
            HealthManagerComponent.ShowsLandedHits(self)) {
            return;
        }

        orig(self, hitInstance);
        if (self.hp <= 0 && !self.isDead && HealthManagerComponent.FollowsHost(self)) {
            KillSounds.Seen.Killed(self.GetInstanceID(), Time.unscaledTimeAsDouble);  // it dies on the host's word
        }
    }

    /// <summary>
    /// (coop) The game's per-clip limit on its one-shot sounds (hits, deaths), which also hears what each played for
    /// <see cref="KillSounds"/>: a copy's death on the scene host's word leaves out what its killing hit played here.
    /// </summary>
    private static bool AudioEventManagerOnTryPlayAudioClip(
        Func<AudioClip, AudioSource, Vector3, bool> orig, AudioClip clip, AudioSource prefab, Vector3 position
    ) {
        if (clip != null && KillSounds.Seen.Repeats(clip.GetInstanceID())) return false;
        var played = orig(clip, prefab, position);
        if (played && clip != null) KillSounds.Seen.Played(clip.GetInstanceID(), Time.unscaledTimeAsDouble);
        return played;
    }

    /// <summary>(coop) Whether another player's hits on an enemy this game runs come only from its game's landed hits.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    internal static bool OnlyLandedHits() => true;

    /// <summary>(coop) An object's path in its scene, or "none", for the test-only health log.</summary>
    private static string PathOf(GameObject? go) {
        if (go == null) {
            return "none";
        }

        var path = go.name;
        for (var t = go.transform.parent; t != null; t = t.parent) {
            path = $"{t.name}/{path}";
        }

        return path;
    }

    /// <summary>The remote player's attack a hit's source belongs to (it or a parent with a RemoteAttackComponent).</summary>
    private static GameObject? SwingOf(GameObject source) {
        var attack = source.GetComponentInParent<RemoteAttackComponent>(true);
        return attack != null ? attack.gameObject : null;
    }

    /// <summary>(coop) Whether a copied swing stuns each enemy once.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    private static bool OnceASwing() => true;

    private void CallMethodProperOnDoMethodCall(
        Action<CallMethodProper> orig,
        CallMethodProper self
    ) {
        // If the 'behaviour' and 'methodName' strings do not match, we execute the original method and return
        if (self.behaviour.Value != "HeroController" ||
            self.methodName.Value != "RecoilLeft" &&
            self.methodName.Value != "RecoilRight" &&
            self.methodName.Value != "RecoilDown" &&
            self.methodName.Value != "Bounce"
           ) {
            orig(self);
            return;
        }

        // If the state does not match, we return as well
        if (!self.State.Name.StartsWith("No Box ") && !self.State.Name.StartsWith("Blocked ")) {
            orig(self);
            return;
        }

        Transform attacks;

        // Find either the 'Damager' or the 'Collider' game object from the FSM variables, and check up the hierarchy.
        // If it matches the local player's object, then we know it was the local player's slash and not a remote
        // player's slash
        var damager = self.Fsm.Variables.FindFsmGameObject("Damager");
        var collider = self.Fsm.Variables.FindFsmGameObject("Collider");
        if (damager != null && damager.Value != null) {
            attacks = damager.Value.transform.parent;
        } else if (collider != null && collider.Value != null && collider.Value.transform.parent != null) {
            attacks = collider.Value.transform.parent.parent;
        } else {
            orig(self);
            return;
        }

        if (attacks == null) {
            orig(self);
            return;
        }

        var knight = attacks.parent;
        if (knight == null) {
            orig(self);
            return;
        }

        if (knight.name.Equals("Knight")) {
            orig(self);
        }
    }

    /// <summary>
    /// Hook for the 'IsInApplicableGameState' method in 'CameraLockArea'. This is used to add a check
    /// for being in the pause menu and connected to a server. Otherwise, the camera will sometimes not lock while
    /// in the pause menu during host transfers.
    /// </summary>
    private bool CameraLockAreaOnIsInApplicableGameState(Func<bool> orig) {
        try {
            if (_netClient.IsConnected && PauseManager.IsMultiplayerPauseMenuOpen) {
                return true;
            }

            return orig();
        } catch (Exception e) {
            Logger.Error($"Could not change CameraLockArea#IsInApplicableGameState hook: \n{e}");
            return orig();
        }
    }

    /// <summary>
    /// Hook to add a tag include to the <see cref="TrackTriggerObjects"/> of <see cref="CameraLockArea"/> to ensure
    /// that it only triggers on the local player.
    /// </summary>
    private void OnCameraLockAreaAwake(CameraLockArea cameraLockArea) {
        cameraLockArea.tagIncludeList ??= [];
        cameraLockArea.tagIncludeList.Add("Player");
    }

    /// <summary>
    /// Hook for <see cref="DamageEnemies"/>.<see cref="DamageEnemies.LateFixedUpdate"/>, which is where a damager
    /// evaluates its collisions and calls <see cref="IHitResponder.Hit"/> on everything it hit.
    /// <para>
    /// Interactable objects that knock the player back when hit (such as the bounce pods in Greymoor, levers and
    /// anything with a <see cref="TinkEffect"/>) do so by calling into <see cref="HeroController.instance"/>
    /// directly, because in a single player game the only thing that can hit them is the local player. Remote
    /// players' attacks are replicated locally, so without this patch a remote player hitting such an object knocks
    /// the local player back as well. While a remote player's damager is being processed, we therefore suppress the
    /// knockback that would be applied to the local player.
    /// </para>
    /// </summary>
    private void DamageEnemiesOnLateFixedUpdate(Action<DamageEnemies> orig, DamageEnemies self) {
        if (!RemoteAttackComponent.IsRemoteAttack(self.gameObject)) {
            orig(self);
            return;
        }

        // Store the previous value instead of resetting to false, since damagers can be processed nested
        var lastSuppress = _suppressHeroKnockback;
        _suppressHeroKnockback = KeepsLocalHeroOut();

        try {
            orig(self);
        } finally {
            _suppressHeroKnockback = lastSuppress;
        }
    }

    /// <summary>
    /// Hook for <see cref="HeroController"/>.<see cref="HeroController.CanRecoil"/> to prevent the local player from
    /// recoiling horizontally while a remote player's attack is being processed. This is the single check that all
    /// horizontal recoil methods of <see cref="HeroController"/> go through, so patching it here also prevents the
    /// side effects of those methods, such as cancelling the local player's attack.
    /// </summary>
    private bool HeroControllerOnCanRecoil(Func<HeroController, bool> orig, HeroController self) {
        if (_suppressHeroKnockback) {
            return false;
        }

        return orig(self);
    }

    /// <summary>
    /// Hook for <see cref="HeroController"/>.<see cref="HeroController.RecoilDown"/> to prevent the local player from
    /// being pushed down while a remote player's attack is being processed. Downwards recoil does not go through
    /// <see cref="HeroController.CanRecoil"/>, so it needs its own patch.
    /// </summary>
    private static readonly FieldInfo? TiltDropRoutine =
        typeof(TiltPlat).GetField("dropRoutine", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>A landing of this game's hero on a tilting platform: its body ran (a new drop) for her collider.</summary>
    private static void TiltPlatOnCollisionEnter2D(Action<TiltPlat, Collision2D> orig, TiltPlat self, Collision2D other) {
        var before = TiltDropRoutine?.GetValue(self);
        orig(self, other);
        var hero = HeroController.SilentInstance;
        if (hero != null && other.gameObject == hero.gameObject && !ReferenceEquals(before, TiltDropRoutine?.GetValue(self))) {
            RemoteTouches.HeroLandedOnTilt();
        }
    }

    private static readonly FieldInfo? DustRunning =
        typeof(DustyPlatform).GetField("isRunning", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>A dirt fall of a dusty platform for this game's hero: its body ran (isRunning set) for her collider.</summary>
    private static void DustyPlatformOnCollisionEnter2D(
        Action<DustyPlatform, Collision2D> orig,
        DustyPlatform self,
        Collision2D other
    ) {
        var before = DustRunning?.GetValue(self) is true;
        orig(self, other);
        var hero = HeroController.SilentInstance;
        if (hero != null && other.gameObject == hero.gameObject && !before && DustRunning?.GetValue(self) is true) {
            RemoteTouches.HeroLandedOnDust(self.transform.position);
        }
    }

    /// <summary>
    /// A collider met or left a bone segment: an enemy copy's own touch is passed by (its original's game sends its
    /// presses); an enemy original's begun or ended touch goes to its Entity to send (CopyPresses).
    /// </summary>
    private static void BoneSegmentOnTrigger(Action<BoneSegment, Collider2D> orig, BoneSegment self, Collider2D other) {
        if (CopyPresses.Bypass || other == null) {
            orig(self, other);
            return;
        }

        if (CopyPresses.Holds(other)) return;
        var root = CopyPresses.RootOf(other);
        var before = root != null ? CopyPresses.Count(self, root) : 0;
        orig(self, other);
        if (root != null) CopyPresses.Touched(self, root, before);
    }

    private static void HeroControllerOnSoftLanding(Action<HeroController> orig, HeroController self) {
        SSMP.Animation.Effects.Movement.Landing.SoftLanded();
        orig(self);
    }

    private void HeroControllerOnRecoilDown(Action<HeroController> orig, HeroController self) {
        if (_suppressHeroKnockback) {
            return;
        }

        orig(self);
    }

    /// <summary>
    /// Hook for the <see cref="IHitResponder.Hit"/> implementation of <see cref="BouncePod"/> and
    /// <see cref="BounceBalloon"/> to ignore hits from remote players entirely. Both of these fling the local player
    /// away from a downwards hit after a delay: the pod through a static queue that is handled in a later frame and
    /// the balloon through a coroutine. That happens outside the scope in which
    /// <see cref="DamageEnemiesOnLateFixedUpdate"/> suppresses knockback, so it has to be prevented here instead.
    /// </summary>
    private IHitResponder.HitResponse IgnoreRemoteHit<T>(
        Func<T, HitInstance, IHitResponder.HitResponse> orig,
        T self,
        HitInstance damageInstance
    ) where T : IHitResponder {
        if (RemoteAttackComponent.IsRemoteAttack(damageInstance.Source)) {
            return IHitResponder.Response.None;
        }

        return orig(self, damageInstance);
    }

    /// <summary>
    /// (coop) Hook for <see cref="HitResponse"/>.<see cref="HitResponse.Hit"/>: a remote player's hit on the local
    /// player's cocoon is ignored, since each game's cocoon is its own player's and both lie at the room's corpse
    /// marker (<see cref="PersonalCocoon"/>). Every other object that answers hits this way still takes them.
    /// </summary>
    private IHitResponder.HitResponse HitResponseOnHit(
        Func<HitResponse, HitInstance, IHitResponder.HitResponse> orig,
        HitResponse self,
        HitInstance damageInstance
    ) {
        if (PersonalCocoon.KeepsRemoteHitsOff() && PersonalCocoon.Is(self.gameObject.name) &&
            RemoteAttackComponent.IsRemoteAttack(damageInstance.Source)) {
            return IHitResponder.Response.None;
        }

        return orig(self, damageInstance);
    }

    /// <summary>
    /// Hook for <see cref="NailSlashTerrainThunk"/>.<see cref="NailSlashTerrainThunk.OnCollisionEnter2D"/> to ignore
    /// terrain collisions of remote players' nail slashes. The terrain thunk recoils the local player when a slash
    /// hits a wall with a <see cref="TinkEffect"/>. It also dereferences the <see cref="HeroController"/> of the
    /// player that the slash belongs to, which does not exist for a remote player.
    /// </summary>
    private void NailSlashTerrainThunkOnCollisionEnter2D(
        Action<NailSlashTerrainThunk, Collision2D> orig,
        NailSlashTerrainThunk self,
        Collision2D collision
    ) {
        if (RemoteAttackComponent.IsRemoteAttack(self.gameObject)) {
            return;
        }

        orig(self, collision);
    }
}
