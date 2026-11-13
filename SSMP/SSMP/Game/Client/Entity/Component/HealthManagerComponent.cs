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

    /// <summary>Whether this kind's copy follows its scripted death (SpecialDeath.KindFollows).</summary>
    private readonly bool _followsScriptedDeath;

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

    /// <summary>(coop) This game's own last hit on the enemy and its frame, sent with the drop it made (HitKind).</summary>
    private int _pendingKind;

    private int _pendingKindFrame = -10;

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
    /// (coop) Scene host: the enemy's hurt-box poll (DamageHeroComponent.OnUpdate), run as it dies, before the death is
    /// sent (DeathOrder.ShowsLastBox). Set by the entity.
    /// </summary>
    public System.Action? PollBoxes { get; set; }

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
        HostClientPair<HealthManager> healthManager,
        bool followsScriptedDeath = false
    ) : base(netClient, entityId, gameObject) {
        _healthManager = healthManager;
        _followsScriptedDeath = followsScriptedDeath;
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

        // (coop) a hurt box that came on since this frame's poll goes out before the death, so the other games show it:
        // Bone_16's Spike Collider killed a Bone Crawler the frame after its Spike Hit 3 came on, before the poll had sent
        // it, and the friend's copy never showed that box
        if (DeathOrder.ShowsLastBox()) PollBoxes?.Invoke();

        // (coop) the killing hit's health change goes before the death, so the other games play that hit on their copy
        // before it dies (UpdateHealth's PlayMissedHits): sent next frame, it reached a copy already dead, and the
        // friend's game had no hit of the host's kills (Bone_16's Bone Circler in the lava, NOTES 368)
        if (PlaysKillHits()) {
            SendHealth(self);
        }

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

        // A scripted death can mark it dead while its native animation still runs.
        data.Packet.Write(SpecialDeath.UsesRegularPath(self.isDead, self.hasSpecialDeath, overrideSpecialDeath,
            _followsScriptedDeath));

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

        if (!SendHealth(observedHealthManager)) {
            return;
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

    /// <summary>
    /// Sends the health change since the last sent, if any. False while a controlled copy's rise waits for its
    /// correction: nothing else is sent that frame.
    /// </summary>
    private bool SendHealth(HealthManager observedHealthManager) {
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

                return false;
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
            var sentKind = newHp < previousHp ? HitKind.ForDrop(_pendingKind, _pendingKindFrame, Time.frameCount) : HitKind.None;
            hpData.Packet.Write(sentKind);

            SendData(hpData);
            if (LogsHealth) {
                Logger.Debug($"(coop) health sent: {observedHealthManager.name} {previousHp} to {newHp}, controlled {IsControlled}, " +
                             $"stun {_pendingStun}, kind {sentKind}");
            }

            _pendingStun = 0f;
            _pendingKind = HitKind.None;
        }

        return true;
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

        // (0.3.22) a player's block comes from the hitter's game, which may be a copy's (BlockedHits.SendsPlayer)
        if (!IsControlled && data.Type != EntityComponentType.Health && data.Type != EntityComponentType.BlockedHit) {
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

                var regularDeath = data.Packet.ReadBool();

                // Set a boolean to indicate that the client health manager is allowed to execute the Die method
                _allowDeath = true;
                _clientCorpse = null;
                // The copy dies when the scene host's enemy died and only takes the hit when that did, whatever its own
                // special death says: a summon its boss kills past its special death (the Beastfly's spitters), a copy
                // that missed the SetSpecialDeath of a fight already on (it joined late, or a packet was lost)
                var client = _healthManager.Client;
                var special = client.hasSpecialDeath;
                client.hasSpecialDeath = !regularDeath;
                // (coop) its death leaves out the hit sounds this game's own killing hit just played (KillSounds)
                if (SkipsKillRepeats()) KillSounds.Seen.BeginDeath(client.GetInstanceID(), Time.unscaledTimeAsDouble);
                try {
                    client.Die(attackDirection, attackType, ignoreEvasion);
                } finally {
                    KillSounds.Seen.EndDeath();
                }
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
            case EntityComponentType.BlockedHit: {
                var x = data.Packet.ReadFloat();
                var y = data.Packet.ReadFloat();
                var angle = data.Packet.ReadFloat();
                // a late entry's catch-up holds the last one, long played
                if (!alreadyInSceneUpdate) PlayBlock(x, y, angle);
                break;
            }
        }
    }

    /// <summary>(coop) HealthManager's own block feedback, which a copy plays for the scene host's block (PlayBlock).</summary>
    private static readonly FieldInfo? BlockHitPrefab = typeof(HealthManager).GetField("blockHitPrefab", HookBindingFlags);

    private static readonly FieldInfo? HasAlternateInvincibleSound =
        typeof(HealthManager).GetField("hasAlternateInvincibleSound", HookBindingFlags);

    private static readonly FieldInfo? AlternateInvincibleSound =
        typeof(HealthManager).GetField("alternateInvincibleSound", HookBindingFlags);

    private static readonly FieldInfo? RegularInvincibleAudio =
        typeof(HealthManager).GetField("regularInvincibleAudio", HookBindingFlags);

    private static readonly FieldInfo? AudioPlayerPrefab = typeof(HealthManager).GetField("audioPlayerPrefab", HookBindingFlags);

    /// <summary>
    /// (coop) This game's enemy (the copy, or the scene host's own for a player's sent block: 0.3.22) shows another
    /// game's block as the game shows its own
    /// (HealthManager.Invincible): its block flash where the sender's went, turned the same, and its invincible
    /// sound; then its tinkTimer is set, as the game sets it (BlockedHits). Not the game's camera shake.
    /// </summary>
    private void PlayBlock(float x, float y, float angle) {
        var copy = IsControlled ? _healthManager.Client : _healthManager.Host;
        if (copy == null || copy.isDead || !copy.gameObject.activeInHierarchy) return;
        if (BlockHitPrefab?.GetValue(copy) is GameObject prefab && prefab != null) {
            var flash = prefab.Spawn();
            flash.transform.position = new Vector3(x, y, 0f);
            flash.transform.eulerAngles = new Vector3(0f, 0f, angle);
        }

        if (HasAlternateInvincibleSound?.GetValue(copy) is true) {
            var source = copy.GetComponent<AudioSource>();
            if (AlternateInvincibleSound?.GetValue(copy) is AudioClip clip && clip != null && source != null) {
                source.PlayOneShot(clip);
            }
        } else if (RegularInvincibleAudio?.GetValue(copy) is AudioEvent audio) {
            audio.SpawnAndPlayOneShot(AudioPlayerPrefab?.GetValue(copy) as AudioSource, copy.transform.position);
        }

        copy.tinkTimer = 0.1f;
        if (LogsHealth) {
            Logger.Debug($"(coop) block shown: {copy.name} at ({x:0.##}, {y:0.##}), turned {angle}, frame {Time.frameCount}");
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
        var kind = data.Packet.ReadInt();

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
                MissedHits.For(_healthManager.Host.GetInstanceID()).Landed(Time.unscaledTime, stun, kind);
            } else if (isControlled && change < 0 && _healthManager.Client != null && PlaysHostHits()) {
                // (coop) B's mirror: a hit that landed in the scene host's game (its own, or another player's it
                // played), shown here once, with no stun (the scene host's game runs the enemy); the copied swings no
                // longer show it, where the copy of the enemy trailed or two copies of a swing doubled (the Bone Flyer
                // Giant: 16 hit sounds for the host's 13 hits; Lace: one of 15 missing)
                MissedHits.For(_healthManager.Client.GetInstanceID()).Landed(Time.unscaledTime, 0f, kind);
            }

            // (coop) a killing hit plays as a hit before the death it brings, as the game's own does (TakeDamage plays
            // its hit effects, then Die): PlayMissedHits plays only while the enemy lives, so the scene host's game
            // lost the flash, blood and sounds of each of another player's killing hits (NOTES 368)
            if (change < 0 && currentHp + change <= 0 && PlaysKillHits()) {
                var shown = isControlled ? _healthManager.Client : _healthManager.Host;
                if (shown != null) PlayMissedHits(shown);
            }

            var before = GetCurrentHp();
            ApplyHp(currentHp + change, triggerHostDeath: true);
            if (merges) {
                _lastHp = lastSent + change;
            }

            if (LogsHealth) {
                Logger.Debug($"(coop) health received: {previousHp} to {newHp} ({change}); here {before} to {GetCurrentHp()}, " +
                             $"last sent {lastSent} to {_lastHp}, controlled {isControlled}, stun {stun}, kind {kind}");
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
        var due = MissedHits.For(healthManager.GetInstanceID()).DueHits(Time.unscaledTime, GamePatcher.OnlyLandedHits());
        for (var i = 0; i < due.Count && PlaysMissedHits() && healthManager.hp > 0; i++) {
            // (coop) the drop's own kind of hit (HitKind); none for a drop no hit made: it showed no hit where it came
            var (attack, element, effects) = HitKind.Unpack(due[i].Kind);
            var played = HitKind.Plays();
            if (played && !HitKind.IsHit(due[i].Kind)) continue;
            var hp = healthManager.hp;
            var source = NearestRemotePlayer(healthManager.transform.position);
            healthManager.Hit(new HitInstance {
                Source = source,
                AttackType = played ? (AttackTypes) attack : AttackTypes.Nail,
                NailElement = played ? (NailElements) element : NailElements.None,
                DamageDealt = 1,
                StunDamage = due[i].Stun,
                // the hit's way, from the player: one standing to her right hits leftward
                Direction = source != null && source.transform.position.x > healthManager.transform.position.x
                    ? 180f
                    : 0f,
                MagnitudeMultiplier = 1f,
                Multiplier = 1f,
                IsFirstHit = true,
                NonLethal = true,
                HitEffectsType = played ? (EnemyHitEffectsProfile.EffectsTypes) effects : EnemyHitEffectsProfile.EffectsTypes.Full,
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

    /// <summary>
    /// (coop) Whether a copy's death on the scene host's word leaves out the hit sounds this game's own killing hit
    /// played (KillSounds). Not inlined: copy_kill_sound's twin skips it, which gives false: then they play twice.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool SkipsKillRepeats() => true;

    /// <summary>
    /// (coop) Whether another player's killing hit plays as a hit before the enemy dies. Not inlined: copy_kill_sound's
    /// twin skips it, which gives false: then the host's game plays the friend's kill as a death alone, and the friend's
    /// game the host's (its health change sent after the death).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool PlaysKillHits() => true;

    /// <summary>(coop) Whether that is this game's copy of an enemy the scene host's game runs (it dies on its word).</summary>
    internal static bool FollowsHost(HealthManager manager) =>
        ByManager.TryGetValue(manager.GetInstanceID(), out var c) && c._clientId == manager.GetInstanceID() && c.IsControlled;

    /// <summary>
    /// (coop) This game's own hit on that enemy (its own original in the scene host's game, its copy in another): its kind,
    /// sent with the drop it makes (HitKind). The note it replaced, or null when that enemy is not this game's own.
    /// </summary>
    internal static (int Kind, int Frame)? NoteOwnHit(HealthManager manager, HitInstance hit) {
        if (!ByManager.TryGetValue(manager.GetInstanceID(), out var c)) return null;
        var own = c._clientId == manager.GetInstanceID() && c.IsControlled ||
                  c._hostId == manager.GetInstanceID() && !c.IsControlled;
        if (!own) return null;
        var replaced = (c._pendingKind, c._pendingKindFrame);
        c._pendingKind = HitKind.Pack((int) hit.AttackType, (int) hit.NailElement, (int) hit.HitEffectsType);
        c._pendingKindFrame = Time.frameCount;
        return replaced;
    }

    /// <summary>(coop) The note a hit that took no health replaced, put back (HitNotes).</summary>
    internal static void PutBackOwnHit(HealthManager manager, (int Kind, int Frame) note) {
        if (ByManager.TryGetValue(manager.GetInstanceID(), out var c)) (c._pendingKind, c._pendingKindFrame) = note;
    }

    /// <summary>
    /// (coop) This game's enemy showed its block of a world object's hit (the scene host's) or its own hero's (0.3.22)
    /// (BlockedHits): the other games show theirs where
    /// its flash went (HealthManager.Invincible's place, by the hit's way and the enemy's box).
    /// </summary>
    internal static void SendBlockedHit(HealthManager manager, HitInstance hit) {
        if (!ByManager.TryGetValue(manager.GetInstanceID(), out var c)) return;
        var at = manager.transform.position;
        var box = manager.GetComponent<BoxCollider2D>();
        var (offset, size) = box != null ? (box.offset, box.size) : (Vector2.zero, Vector2.zero);
        var source = hit.Source != null ? hit.Source.transform.position : at;
        var way = DirectionUtils.GetCardinalDirection(hit.GetActualDirection(manager.transform, HitInstance.TargetType.Regular));
        var (x, y, angle) = BlockedHits.Flash(way, at.x, at.y, box != null, offset.x, offset.y, size.x, size.y,
            source.x, source.y);
        var data = ObjectPool<EntityNetworkData>.Get();
        data.Type = EntityComponentType.BlockedHit;
        data.Packet.Write(x);
        data.Packet.Write(y);
        data.Packet.Write(angle);
        c.SendData(data);
        if (LogsHealth) {
            Logger.Debug($"(coop) block sent: {manager.name} at ({x:0.##}, {y:0.##}), turned {angle}, frame {Time.frameCount}");
        }
    }

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
