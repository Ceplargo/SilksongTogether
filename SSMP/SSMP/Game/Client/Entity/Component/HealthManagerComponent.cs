using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod.RuntimeDetour;
using SSMP.Networking.Client;
using SSMP.Networking.Packet.Data;
using SSMP.Util;
using UnityEngine;
using Logger = SSMP.Logging.Logger;

#pragma warning disable CS0414 // Field is assigned but its value is never used

namespace SSMP.Game.Client.Entity.Component;

/// <inheritdoc />
/// This component manages the <see cref="HealthManager"/> component of the entity.
internal class HealthManagerComponent : EntityComponent {
    private const BindingFlags HookBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const float ControlledHealCorrectionDelaySeconds = 0.4f;

    /// <summary>
    /// Host-client pair of health manager components of the entity.
    /// </summary>
    private readonly HostClientPair<HealthManager> _healthManager;

    /// <summary>
    /// (coop) Each loaded component by its original's and its copy's health manager (instance ids), for GamePatcher's
    /// hit hooks.
    /// </summary>
    private static readonly Dictionary<int, HealthManagerComponent> ByManager = new();

    private readonly int _hostId, _clientId;

    /// <summary>
    /// (coop) The stun this game's own hits gave its copy of the enemy since its last health change was sent: sent with
    /// the next, so the scene host's game plays that landed hit with the stun it had here.
    /// </summary>
    private float _pendingStun;

    /// <summary>
    /// Host death effects used to capture the emitted corpse snapshot.
    /// </summary>
    private readonly EnemyDeathEffects? _hostDeathEffects;

    /// <summary>
    /// Client death effects used to enable local physics on emitted corpses.
    /// </summary>
    private readonly EnemyDeathEffects? _clientDeathEffects;

    /// <summary>
    /// The host corpse emitted by the current death call.
    /// </summary>
    private GameObject? _hostCorpse;

    /// <summary>
    /// The client corpse emitted by the current death call.
    /// </summary>
    private GameObject? _clientCorpse;

    /// <summary>
    /// (coop) Scene host: hands a corpse to the enemy's thrown component, which numbers it and sends where it is until
    /// it is switched off; 0 when it is not followed. Set by the entity.
    /// </summary>
    public System.Func<GameObject, byte>? TrackCorpse { get; set; }

    /// <summary>
    /// (coop) Other games: hands their corpse to the thrown component, to follow the scene host's of that number.
    /// </summary>
    public System.Action<byte, GameObject>? FollowCorpse { get; set; }

    /// <summary>
    /// (coop) The corpses that follow the scene host's until they are switched off: a Dock Bomber's rolls on after it
    /// lands, by each game's own physics, and its gas went off where it stopped, 0.6 to 1.2 units from the host's.
    /// </summary>
    private static readonly HashSet<string> FollowedCorpses = ["Corpse Dock Bomber"];

    /// <summary>
    /// (coop) Whether a corpse of this name follows the scene host's (FollowedCorpses).
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool FollowsCorpse(string name) => FollowedCorpses.Contains(name.Replace("(Clone)", "").Trim());

    /// <summary>
    /// Boolean indicating whether the health manager of the client entity is allowed to die.
    /// </summary>
    private bool _allowDeath;

    /// <summary>
    /// MonoMod hook for HealthManager.Die.
    /// </summary>
    private Hook? _healthManagerDieHook;

    /// <summary>
    /// The last value for the "invincible" variable of the health manager.
    /// </summary>
    private bool _lastInvincible;

    /// <summary>
    /// The last synced HP value of the health manager.
    /// </summary>
    private int _lastHp;

    /// <summary>
    /// The last value for the "invincibleFromDirection" variable of the health manager.
    /// </summary>
    private int _lastInvincibleFromDirection;

    /// <summary>
    /// The current scene host epoch from our perspective.
    /// </summary>
    private uint _currentHealthEpoch;

    /// <summary>
    /// The highest received scene host epoch.
    /// </summary>
    private uint _lastReceivedHealthEpoch;

    /// <summary>
    /// Whether a controlled client-side HP increase should be rolled back to the last authoritative value.
    /// </summary>
    private bool _hasPendingControlledHealCorrection;

    /// <summary>
    /// Unscaled time at which the next controlled heal correction should be applied.
    /// </summary>
    private float _pendingControlledHealCorrectionAt;

    public HealthManagerComponent(
        NetClient netClient,
        ushort entityId,
        HostClientPair<GameObject> gameObject,
        HostClientPair<HealthManager> healthManager
    ) : base(netClient, entityId, gameObject) {
        _healthManager = healthManager;
        _hostId = healthManager.Host.GetInstanceID();
        _clientId = healthManager.Client.GetInstanceID();
        ByManager[_hostId] = this;
        ByManager[_clientId] = this;

        _hostDeathEffects = gameObject.Host.GetComponent<EnemyDeathEffects>();
        if (_hostDeathEffects != null) {
            _hostDeathEffects.CorpseEmitted += OnHostCorpseEmitted;
        }

        _clientDeathEffects = gameObject.Client.GetComponent<EnemyDeathEffects>();
        if (_clientDeathEffects != null) {
            _clientDeathEffects.CorpseEmitted += OnClientCorpseEmitted;
        }

        _lastInvincible = healthManager.Host.IsInvincible;
        _lastHp = healthManager.Host.hp;
        _lastInvincibleFromDirection = healthManager.Host.InvincibleFromDirection;

        // Get the largest overload of Die from HealthManager, because that is the method that is getting called by
        // all other overloads regardless
        var dieMethod = typeof(HealthManager).GetMethod(
            nameof(HealthManager.Die),
            HookBindingFlags,
            Type.DefaultBinder,
            [
                typeof(float?), typeof(AttackTypes), typeof(NailElements), typeof(GameObject),
                typeof(bool), typeof(float), typeof(bool), typeof(bool)
            ],
            null
        );

        if (dieMethod == null) {
            throw new MissingMethodException(
                typeof(HealthManager).FullName,
                $"{nameof(HealthManager.Die)}(float?, {nameof(AttackTypes)}, bool)"
            );
        }

        _healthManagerDieHook = new Hook(dieMethod, HealthManagerOnDie);
        MonoBehaviourUtil.Instance.OnUpdateEvent += OnUpdate;
    }

    /// <summary>
    /// Captures the corpse emitted by the host death lifecycle.
    /// </summary>
    private void OnHostCorpseEmitted(GameObject corpse) {
        _hostCorpse = corpse;
    }

    /// <summary>
    /// Enables local physics for a remote corpse and all of its body parts.
    /// </summary>
    private void OnClientCorpseEmitted(GameObject corpse) {
        if (corpse == null) {
            return;
        }

        _clientCorpse = corpse;
        RestoreCorpsePhysics(corpse);
    }

    /// <summary>
    /// Restores simulation for a corpse and all of its body parts on the local client, each with the body type it has
    /// in the scene host's game: the one it had before SSMP made the copy's bodies kinematic, else the one the game's
    /// death gave it. Making every body dynamic let gravity and explosions push off a corpse its own FSM moves as a
    /// kinematic body (the Last Judge's, 4 to 11 units, so her explosions went off elsewhere).
    /// </summary>
    /// <param name="corpse">The emitted corpse to make physics-driven.</param>
    private static void RestoreCorpsePhysics(GameObject corpse) {
        if (corpse == null) {
            return;
        }

        foreach (var rigidbody in corpse.GetComponentsInChildren<Rigidbody2D>(true)) {
            if (rigidbody == null) {
                continue;
            }

            rigidbody.simulated = true;
            rigidbody.bodyType = CorpseBodyType(EntityInitializer.OriginalBodyType(rigidbody) ?? rigidbody.bodyType);
        }
    }

    /// <summary>
    /// The body type a remote corpse's rigidbody gets: its own. Not inlined: the death_places check's twin skips it,
    /// which gives the default, Dynamic, as before this fix.
    /// </summary>
    /// <param name="own">The body type the corpse's rigidbody has in the scene host's game.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static RigidbodyType2D CorpseBodyType(RigidbodyType2D own) => own;

    /// <summary>
    /// Applies the host corpse's initial transform and velocity to the local corpse.
    /// </summary>
    /// <param name="corpse">The local corpse receiving the snapshot.</param>
    /// <param name="position">The host corpse's world position.</param>
    /// <param name="rotation">The host corpse's world rotation around the z-axis.</param>
    /// <param name="velocity">The host corpse's initial linear velocity.</param>
    private static void ApplyCorpseSnapshot(
        GameObject corpse,
        Vector2 position,
        float rotation,
        Vector2 velocity
    ) {
        if (corpse == null) {
            return;
        }

        var transform = corpse.transform;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, rotation);

        var rigidbody = corpse.GetComponent<Rigidbody2D>();
        if (rigidbody == null) {
            return;
        }

        rigidbody.position = position;
        rigidbody.rotation = rotation;
        rigidbody.linearVelocity = velocity;
    }

    /// <summary>
    /// Callback method for when the health manager dies.
    /// </summary>
    private void HealthManagerOnDie(
        Action<HealthManager, float?, AttackTypes, NailElements, GameObject, bool, float, bool, bool> orig,
        HealthManager self,
        float? attackDirection,
        AttackTypes attackType,
        NailElements nailElements,
        GameObject gameObject,
        bool ignoreEvasion,
        float corpseFlingMultiplier,
        bool overrideSpecialDeath,
        bool disallowDropFlying
    ) {
        if (self != _healthManager.Host && self != _healthManager.Client) {
            InvokeOrig();
            return;
        }

        if (self == _healthManager.Client) {
            if (!_allowDeath) {
                Logger.Info("HealthManager Die was called on client entity");
            } else {
                Logger.Info("HealthManager Die was called on client entity, but it is allowed death");

                InvokeOrig();

                _allowDeath = false;
            }

            return;
        }

        Logger.Info("HealthManager Die was called on host entity");

        _hostCorpse = null;
        InvokeOrig();

        var data = ObjectPool<EntityNetworkData>.Get();
        data.Type = EntityComponentType.Death;

        if (attackDirection.HasValue) {
            data.Packet.Write(true);
            data.Packet.Write(attackDirection.Value);
        } else {
            data.Packet.Write(false);
        }

        data.Packet.Write((byte) attackType);

        data.Packet.Write(ignoreEvasion);

        data.Packet.Write(_hostCorpse != null);
        if (_hostCorpse != null) {
            var corpsePosition = _hostCorpse.transform.position;
            var corpseRigidbody = _hostCorpse.GetComponent<Rigidbody2D>();

            data.Packet.Write(corpsePosition.x);
            data.Packet.Write(corpsePosition.y);
            data.Packet.Write(_hostCorpse.transform.eulerAngles.z);
            data.Packet.Write(corpseRigidbody?.linearVelocity.x ?? 0f);
            data.Packet.Write(corpseRigidbody?.linearVelocity.y ?? 0f);
            // (coop) the thrown component's number for it, 0 when it is not followed
            data.Packet.Write(FollowsCorpse(_hostCorpse.name) ? (byte) (TrackCorpse?.Invoke(_hostCorpse) ?? 0) : (byte) 0);
        }

        // Whether the call killed it (a special death only takes a hit), so the copy's outcome is the same
        data.Packet.Write(self.isDead);

        _hostCorpse = null;

        SendData(data);
        return;

        // Utility method to invoke the original method with all the original arguments
        void InvokeOrig() {
            orig(
                self, attackDirection, attackType, nailElements, gameObject, ignoreEvasion, corpseFlingMultiplier,
                overrideSpecialDeath, disallowDropFlying
            );
        }
    }

    /// <summary>
    /// Callback method for updates to check whether health or invincibility changes.
    /// </summary>
    /// <inheritdoc />
    public override void OnUpdate() {
        var observedHealthManager = IsControlled ? _healthManager.Client : _healthManager.Host;
        if (observedHealthManager == null) {
            return;
        }

        // (coop) another player's landed hits, here in every game: the scene host's of the others (B), and in the
        // others the scene host's and theirs (B's mirror)
        PlayMissedHits(observedHealthManager);

        var newHp = observedHealthManager.hp;
        if (newHp != _lastHp) {
            if (IsControlled && newHp > _lastHp) {
                if (_hasPendingControlledHealCorrection) {
                    if (Time.unscaledTime >= _pendingControlledHealCorrectionAt) {
                        _hasPendingControlledHealCorrection = false;
                        ApplyHp(_lastHp, triggerHostDeath: false);
                    }
                } else {
                    _hasPendingControlledHealCorrection = true;
                    _pendingControlledHealCorrectionAt = Time.unscaledTime + ControlledHealCorrectionDelaySeconds;
                }

                return;
            }

            _hasPendingControlledHealCorrection = false;
            var previousHp = _lastHp;
            _lastHp = newHp;

            // Obtain a pooled network data instance to avoid new allocations
            var hpData = ObjectPool<EntityNetworkData>.Get();
            hpData.Type = EntityComponentType.Health;

            hpData.Packet.Write(previousHp);
            hpData.Packet.Write(newHp);
            hpData.Packet.Write(_currentHealthEpoch);
            hpData.Packet.Write(_pendingStun);

            SendData(hpData);
            if (LogsHealth) {
                Logger.Debug($"(coop) health sent: {observedHealthManager.name} {previousHp} to {newHp}, controlled {IsControlled}, " +
                             $"stun {_pendingStun}");
            }

            _pendingStun = 0f;
        }

        var newInvincible = _healthManager.Host.IsInvincible;
        var newInvincibleFromDir = _healthManager.Host.InvincibleFromDirection;

        // Only retrieve and populate invincibilityData from the pool if a value has actually changed,
        // preventing unnecessary allocations on 99.9% of frames.
        if (newInvincible != _lastInvincible || newInvincibleFromDir != _lastInvincibleFromDirection) {
            _lastInvincible = newInvincible;
            _lastInvincibleFromDirection = newInvincibleFromDir;

            var invincibilityData = ObjectPool<EntityNetworkData>.Get();
            invincibilityData.Type = EntityComponentType.Invincibility;

            invincibilityData.Packet.Write(newInvincible);
            invincibilityData.Packet.Write((byte) newInvincibleFromDir);

            SendData(invincibilityData);
        }
    }

    /// <inheritdoc />
    public override void InitializeHost(uint sceneHostEpoch) {
        ResetHealthOrderingForEpoch(sceneHostEpoch);
        var currentHp = GetCurrentHp();
        ApplyHp(currentHp, triggerHostDeath: false);
    }

    /// <inheritdoc />
    public override void InitializeClient(uint sceneHostEpoch) {
        ResetHealthOrderingForEpoch(sceneHostEpoch);
        var currentHp = GetCurrentHp();
        ApplyHp(currentHp, triggerHostDeath: false);
    }

    /// <summary>
    /// Resets health epoch tracking for a new scene-host epoch.
    /// </summary>
    /// <param name="sceneHostEpoch">The new scene-host epoch assigned by the server.</param>
    private void ResetHealthOrderingForEpoch(uint sceneHostEpoch) {
        _currentHealthEpoch = sceneHostEpoch;
        _lastReceivedHealthEpoch = sceneHostEpoch;
    }

    /// <inheritdoc />
    public override void Update(EntityNetworkData data, bool alreadyInSceneUpdate) {
        Logger.Info("Received health manager update");

        if (!IsControlled && data.Type != EntityComponentType.Health) {
            Logger.Info("  Entity was not controlled");
            return;
        }

        switch (data.Type) {
            case EntityComponentType.Death: {
                var attackDirection = new float?();
                if (data.Packet.ReadBool()) {
                    attackDirection = data.Packet.ReadFloat();
                }

                var attackType = (AttackTypes) data.Packet.ReadByte();
                var ignoreEvasion = data.Packet.ReadBool();

                var hasCorpseSnapshot = data.Packet.ReadBool();
                var corpsePosition = Vector2.zero;
                var corpseRotation = 0f;
                var corpseVelocity = Vector2.zero;
                byte corpseNumber = 0;

                if (hasCorpseSnapshot) {
                    corpsePosition = new Vector2(data.Packet.ReadFloat(), data.Packet.ReadFloat());
                    corpseRotation = data.Packet.ReadFloat();
                    corpseVelocity = new Vector2(data.Packet.ReadFloat(), data.Packet.ReadFloat());
                    corpseNumber = data.Packet.ReadByte();
                }

                var died = data.Packet.ReadBool();

                // Set a boolean to indicate that the client health manager is allowed to execute the Die method
                _allowDeath = true;
                _clientCorpse = null;
                // The copy dies when the scene host's enemy died and only takes the hit when that did, whatever its own
                // special death says: a summon its boss kills past its special death (the Beastfly's spitters), a copy
                // that missed the SetSpecialDeath of a fight already on (it joined late, or a packet was lost)
                var client = _healthManager.Client;
                var special = client.hasSpecialDeath;
                client.hasSpecialDeath = !died;
                client.Die(attackDirection, attackType, ignoreEvasion);
                if (!client.isDead) client.hasSpecialDeath = special;

                if (hasCorpseSnapshot && _clientCorpse != null) {
                    ApplyCorpseSnapshot(_clientCorpse, corpsePosition, corpseRotation, corpseVelocity);
                    if (corpseNumber != 0) {
                        FollowCorpse?.Invoke(corpseNumber, _clientCorpse);
                    }
                }

                break;
            }
            case EntityComponentType.Health:
                UpdateHealth(data, alreadyInSceneUpdate);
                break;
            case EntityComponentType.Invincibility: {
                var newInvincible = data.Packet.ReadBool();
                var newInvincibleFromDir = data.Packet.ReadByte();

                if (_healthManager.Host != null) {
                    _healthManager.Host.IsInvincible = newInvincible;
                    _healthManager.Host.InvincibleFromDirection = newInvincibleFromDir;
                }

                if (_healthManager.Client == null) {
                    return;
                }

                _healthManager.Client.IsInvincible = newInvincible;
                _healthManager.Client.InvincibleFromDirection = newInvincibleFromDir;
                break;
            }
        }
    }

    /// <summary>
    /// Applies a health update from the network.
    /// Scene snapshots are applied as absolute HP,
    /// while live updates are merged as HP deltas
    /// so delayed packets do not overwrite local damage or healing.
    /// </summary>
    private void UpdateHealth(EntityNetworkData data, bool alreadyInSceneUpdate) {
        _hasPendingControlledHealCorrection = false;

        var previousHp = data.Packet.ReadInt();
        var newHp = data.Packet.ReadInt();
        var healthEpoch = data.Packet.ReadUInt();
        var stun = data.Packet.ReadFloat();

        if (alreadyInSceneUpdate) {
            ResetHealthOrderingForEpoch(healthEpoch);
            ApplyHp(newHp, triggerHostDeath: false);
        } else {
            // Cache once; avoids re-evaluating the IsControlled check twice below on every packet
            var isControlled = IsControlled;

            if (healthEpoch < _lastReceivedHealthEpoch) {
                return;
            }

            if (healthEpoch > _lastReceivedHealthEpoch) {
                _lastReceivedHealthEpoch = healthEpoch;
                if (isControlled) {
                    _currentHealthEpoch = healthEpoch;
                }
            }

            // (coop) The change goes onto this game's current health, so a hit of its own that it has not sent yet
            // (its health below the last sent) stays, and the last sent moves only by the change, so the next frame
            // still sends that hit. A controlled copy's health above the last sent is not built on: its correction
            // puts such a rise back
            var merges = MergesChanges();
            var currentHp = !isControlled ? GetCurrentHp() : merges ? System.Math.Min(GetCurrentHp(), _lastHp) : _lastHp;
            var lastSent = _lastHp;
            var change = newHp - previousHp;
            if (!isControlled && change < 0 && _healthManager.Host != null) {
                // (coop) another player's hit landed in its game, with that stun: played here (see MissedHits)
                MissedHits.For(_healthManager.Host.GetInstanceID()).Landed(Time.unscaledTime, stun);
            } else if (isControlled && change < 0 && _healthManager.Client != null && PlaysHostHits()) {
                // (coop) B's mirror: a hit that landed in the scene host's game (its own, or another player's it
                // played), shown here once, with no stun (the scene host's game runs the enemy); the copied swings no
                // longer show it, where the copy of the enemy trailed or two copies of a swing doubled (the Bone Flyer
                // Giant: 16 hit sounds for the host's 13 hits; Lace: one of 15 missing)
                MissedHits.For(_healthManager.Client.GetInstanceID()).Landed(Time.unscaledTime, 0f);
            }

            var before = GetCurrentHp();
            ApplyHp(currentHp + change, triggerHostDeath: true);
            if (merges) {
                _lastHp = lastSent + change;
            }

            if (LogsHealth) {
                Logger.Debug($"(coop) health received: {previousHp} to {newHp} ({change}); here {before} to {GetCurrentHp()}, " +
                             $"last sent {lastSent} to {_lastHp}, controlled {isControlled}, stun {stun}");
            }
        }
    }

    /// <summary>
    /// (coop) In the scene host's game, another player's landed hits on this enemy (MissedHits), each played once as a
    /// hit plays: its flash, sound and stun (the stun it had in that player's game), never lethal, its health put back
    /// (that came with the other game's change). With GamePatcher.OnlyLandedHits (the ruling after the
    /// combined 0.3.15 gates) every landed hit plays at once, as copied swings no longer damage such an enemy here: they
    /// stunned where that player's swing had missed or been parried, and missed where it had landed, so Mossbone Mother
    /// staggered a friend hit early. Without it only those its copied swing missed play, Window after: her copy trailed
    /// her Swoop 2 to 4 units, so the friend's swings landed on the copy and their copied swings passed over her (the new
    /// 0.3.14 gate's boss_hits).
    /// </summary>
    private static void PlayMissedHits(HealthManager healthManager) {
        var due = MissedHits.For(healthManager.GetInstanceID()).Due(Time.unscaledTime, GamePatcher.OnlyLandedHits());
        for (var i = 0; i < due.Count && PlaysMissedHits() && healthManager.hp > 0; i++) {
            var hp = healthManager.hp;
            var source = NearestRemotePlayer(healthManager.transform.position);
            healthManager.Hit(new HitInstance {
                Source = source,
                AttackType = AttackTypes.Nail,
                NailElement = NailElements.None,
                DamageDealt = 1,
                StunDamage = due[i],
                // the hit's way, from the player: one standing to her right hits leftward
                Direction = source != null && source.transform.position.x > healthManager.transform.position.x
                    ? 180f
                    : 0f,
                MagnitudeMultiplier = 1f,
                Multiplier = 1f,
                IsFirstHit = true,
                NonLethal = true,
                HitEffectsType = EnemyHitEffectsProfile.EffectsTypes.Full,
                SilkGeneration = HitSilkGeneration.None
            });
            healthManager.hp = hp;
        }
    }

    /// <summary>
    /// (coop) Whether this game runs that enemy (the scene host's original) and so takes its other players' hits from
    /// their games' health changes.
    /// </summary>
    internal static bool HostsHits(HealthManager manager) =>
        ByManager.TryGetValue(manager.GetInstanceID(), out var c) && c._hostId == manager.GetInstanceID() && !c.IsControlled;

    /// <summary>
    /// (coop) Whether this game shows another player's hits on that enemy from that player's landed hits: the scene
    /// host's original in the host's game (B), the copy in the others (B's mirror).
    /// </summary>
    internal static bool ShowsLandedHits(HealthManager manager) =>
        ByManager.TryGetValue(manager.GetInstanceID(), out var c) &&
        (c._hostId == manager.GetInstanceID() && !c.IsControlled || c._clientId == manager.GetInstanceID() && c.IsControlled);

    /// <summary>
    /// (coop) Whether a game that follows the scene host plays each of its health drops as a hit (B's mirror). Not
    /// inlined: boss_hits' twin skips it, which gives false: then the friend's game shows none of the host's hits.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool PlaysHostHits() => true;

    /// <summary>(coop) A stun this game's own hit gave its copy of that enemy: sent with its next health change.</summary>
    internal static void AddOwnStun(HealthManager manager, float stun) {
        if (ByManager.TryGetValue(manager.GetInstanceID(), out var c) && c._clientId == manager.GetInstanceID() &&
            c.IsControlled) {
            c._pendingStun += stun;
        }
    }

    /// <summary>(coop) Whether this game plays another player's landed hits its copied swing missed.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    private static bool PlaysMissedHits() => true;

    /// <summary>
    /// (coop) Whether each health change this game sends and each it receives is logged, with this game's health and
    /// last sent before and after applying it. Off unless the test plugin (CoopQA) turns it on: the third 0.3.14 gate's
    /// Mossbone Mother staggered one friend hit early with all 15 of the friend's hits shown, and its cause needs each
    /// game's health changes.
    /// </summary>
    internal static bool LogsHealth;

    /// <summary>The nearest other player's object in this game to that place, or null if none.</summary>
    private static GameObject? NearestRemotePlayer(Vector3 at) {
        var hero = HeroController.instance != null ? HeroController.instance.gameObject : null;
        GameObject? nearest = null;
        foreach (var player in PlayerTargetRegistry.GetTrackedPlayers()) {
            if (player == hero) continue;
            if (nearest == null ||
                (player.transform.position - at).sqrMagnitude < (nearest.transform.position - at).sqrMagnitude) {
                nearest = player;
            }
        }

        return nearest;
    }

    /// <summary>
    /// (coop) Whether the other game's change of health goes onto this game's current health, keeping a hit of its own
    /// not sent yet, rather than onto the health it last sent: that lost such a hit in both games when this game's copy
    /// follows the scene host's, and in the scene host's game kept it there unsent, so the two games' health differed.
    /// Not inlined: hit_merge's twin switches it off with a patch.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool MergesChanges() => true;

    /// <summary>
    /// Gets the health value from the locally active side of the entity.
    /// </summary>
    /// <returns>The current HP value.</returns>
    private int GetCurrentHp() {
        var healthManager = IsControlled ? _healthManager.Client : _healthManager.Host;
        return healthManager != null ? healthManager.hp : _lastHp;
    }

    /// <summary>
    /// Applies HP to both entity copies and optionally runs host death when a remote hit was lethal.
    /// </summary>
    /// <param name="newHp">The HP value to apply.</param>
    /// <param name="triggerHostDeath">Whether the scene host should run death when HP crosses zero.</param>
    private void ApplyHp(int newHp, bool triggerHostDeath) {
        var wasAlive = GetCurrentHp() > 0;

        _lastHp = newHp;

        if (_healthManager.Host != null) {
            _healthManager.Host.hp = newHp;
        }

        if (_healthManager.Client != null) {
            _healthManager.Client.hp = newHp;
        }

        if (triggerHostDeath && !IsControlled && wasAlive && newHp <= 0 && _healthManager.Host != null) {
            _healthManager.Host.Die(null, AttackTypes.Generic, true);
        }
    }

    /// <inheritdoc />
    public override void Destroy() {
        if (_hostDeathEffects != null) {
            _hostDeathEffects.CorpseEmitted -= OnHostCorpseEmitted;
        }

        if (_clientDeathEffects != null) {
            _clientDeathEffects.CorpseEmitted -= OnClientCorpseEmitted;
        }

        _healthManagerDieHook?.Dispose();
        _healthManagerDieHook = null;
        MonoBehaviourUtil.Instance.OnUpdateEvent -= OnUpdate;
        foreach (var id in new[] { _hostId, _clientId }) {
            if (ByManager.TryGetValue(id, out var c) && c == this) {
                ByManager.Remove(id);
            }
        }
    }
}
