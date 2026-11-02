using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using SSMP.Networking.Packet.Data;
using SSMP.Util;
using UnityEngine;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedParameter.Local

namespace SSMP.Game.Client.Entity.Action;

/// <summary>
/// The effects an enemy shows or plays for the players around it: its name card, its roar burst, camera shakes,
/// sounds and voice lines. They send no data: the copy runs them from its own action's settings.
/// </summary>
internal static partial class EntityFsmActions {
    #region DisplayBossTitle

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, DisplayBossTitle action) =>
        !string.IsNullOrEmpty(action.bossTitle.Value);

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, DisplayBossTitle action) => action.OnEnter();

    #endregion

    #region StartRoarEmitter

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, StartRoarEmitter action) =>
        !action.noVisualEffect.Value;

    /// <summary>
    /// The ends of the lasting roars the copies started that nothing has ended yet, which a copied StopRoarEmitter runs
    /// (else each would send its END later, when its copy goes, and cut whatever rings are on then).
    /// </summary>
    private static readonly List<System.Action> PendingRoarEnds = new();

    /// <summary>
    /// Applies network data to the FSM action: the roar's rings as the scene host's: a burst, or rings that go on
    /// ("START") until the scene host's state ends (for an action that stops on exit), a copied StopRoarEmitter, or the
    /// copy going (it left the room, or this game took it over), so no other room keeps them. A delayed roar comes only
    /// while the state lasts, as the game's, and none from a copy already gone. The local hero is not stunned, since
    /// the end of that stun would not come.
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, StartRoarEmitter action) {
        var cameras = global::GameManager.instance != null ? global::GameManager.instance.gameCams : null;
        var emitter = cameras != null
            ? cameras.transform.Find(action.isSmall.Value ? "Roar Wave Emitter Small" : "Roar Wave Emitter")
            : null;
        var spawnPoint = action.Fsm?.GetOwnerDefaultTarget(action.spawnPoint);
        if (emitter == null || spawnPoint == null) {
            return;
        }

        if (action.delay.Value < 0.001f) {
            Roar();
        } else {
            new ActionInState {
                Fsm = action.Fsm,
                StateName = action.State.Name,
                Coroutine = MonoBehaviourUtil.Instance.StartCoroutine(DelayRoar())
            }.Register();
        }

        IEnumerator DelayRoar() {
            yield return new WaitForSeconds(action.delay.Value);

            if (emitter != null && spawnPoint != null) {
                Roar();
            }
        }

        void Roar() {
            if (CopyGone(action)) {
                return;
            }

            var position = spawnPoint.transform.position;
            emitter.position = new Vector3(position.x, position.y, 0f);
            if (!Lasting(action)) {
                FSMUtility.SendEventToGameObject(emitter.gameObject, "BURST");
                return;
            }

            FSMUtility.SendEventToGameObject(emitter.gameObject, "START");
            var ended = false;
            MonoBehaviourUtil.Instance.OnUpdateEvent += Watch;
            PendingRoarEnds.Add(End);
            if (action.stopOnExit) {
                new ActionInState { Fsm = action.Fsm, StateName = action.State.Name, ExitAction = End }.Register();
            }

            void Watch() {
                if (CopyGone(action)) End();
            }

            void End() {
                if (ended) return;
                ended = true;
                MonoBehaviourUtil.Instance.OnUpdateEvent -= Watch;
                PendingRoarEnds.Remove(End);
                if (emitter != null) FSMUtility.SendEventToGameObject(emitter.gameObject, "END");
            }
        }
    }

    /// <summary>
    /// Whether a roar's rings go on until it ends (the game's "START"), not one burst: the main roars of Act 1's bosses
    /// do (the Bell Beast's, the Widow's screams, the Last Judge's), and the copy's single burst left the other players
    /// one flash where the host saw rings.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    private static bool Lasting(StartRoarEmitter action) => !action.roarBurst.Value;

    #endregion

    #region StopRoarEmitter

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, StopRoarEmitter action) => true;

    /// <summary>
    /// Applies network data to the FSM action: the lasting roars' rings end, as the scene host's did (both emitters, as
    /// the action does, and the copies' roars still waiting for an end; a delayed stop only while the state lasts, as the
    /// game's; the local hero's roar stun is not the copy's to end).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, StopRoarEmitter action) {
        if (action.delay.Value < 0.001f) {
            Stop();
        } else {
            new ActionInState {
                Fsm = action.Fsm,
                StateName = action.State.Name,
                Coroutine = MonoBehaviourUtil.Instance.StartCoroutine(DelayStop())
            }.Register();
        }

        IEnumerator DelayStop() {
            yield return new WaitForSeconds(action.delay.Value);

            Stop();
        }

        void Stop() {
            foreach (var end in PendingRoarEnds.ToArray()) end();

            var cameras = global::GameManager.instance != null ? global::GameManager.instance.gameCams : null;
            if (cameras == null) return;

            foreach (var name in new[] { "Roar Wave Emitter", "Roar Wave Emitter Small" }) {
                if (cameras.transform.Find(name) is { } emitter) FSMUtility.SendEventToGameObject(emitter.gameObject, "END");
            }
        }
    }

    #endregion

    #region DoCameraShake

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, DoCameraShake action) => true;

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, DoCameraShake action) {
        if (action.Camera.Value is not CameraManagerReference camera || action.Profile.Value is not CameraShakeProfile profile) {
            return;
        }

        // As the action itself: no shake for something off this player's screen
        var visible = action.VisibleRenderer.GetSafe(action);
        if (visible != null && visible.GetComponent<Renderer>() is { } renderer && !renderer.isVisible) {
            return;
        }

        if (action.Delay.Value <= 0f) {
            Shake();
        } else {
            MonoBehaviourUtil.Instance.StartCoroutine(DelayShake());
        }

        IEnumerator DelayShake() {
            yield return new WaitForSeconds(action.Delay.Value);

            Shake();
        }

        void Shake() => camera.DoShake(profile, action.Owner, action.DoFreeze.Value);
    }

    #endregion

    #region DoCameraShakeRepeating

    /// <summary>
    /// The most shakes a repeating shake makes on a copy, should the scene host's leaving of its state never arrive.
    /// </summary>
    private const int RepeatedShakesCap = 100;

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, DoCameraShakeRepeating action) => true;

    /// <summary>
    /// Applies network data to the FSM action: a shake after its delay and then every repeat delay, as the action does
    /// every frame in the scene host's game, until the host's FSM leaves the state.
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, DoCameraShakeRepeating action) {
        if (action.Camera.Value is not CameraManagerReference camera || action.Profile.Value is not CameraShakeProfile profile) {
            return;
        }

        new ActionInState {
            Fsm = action.Fsm,
            StateName = action.State.Name,
            Coroutine = MonoBehaviourUtil.Instance.StartCoroutine(Repeat())
        }.Register();
        return;

        IEnumerator Repeat() {
            if (action.Delay.Value > 0f) {
                yield return new WaitForSeconds(action.Delay.Value);
            }

            for (var i = 0; i < RepeatedShakesCap; i++) {
                var target = action.Target.GetSafe(action);
                if (!action.MaxCameraDistance.IsNone && target != null) {
                    camera.DoShakeInRange(
                        profile, action.Owner, action.MaxCameraDistance.Value, target.transform.position,
                        action.DoFreeze.Value
                    );
                } else {
                    camera.DoShake(profile, action.Owner, action.DoFreeze.Value);
                }

                if (action.RepeatDelay.Value <= 0f) {
                    yield break;
                }

                yield return new WaitForSeconds(action.RepeatDelay.Value);
            }
        }
    }

    #endregion

    #region DoCameraShakeV2, V3, V4 and RepeatingV2

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, DoCameraShakeV2 action) => true;

    /// <summary>
    /// Applies network data to the FSM action: the shake as the action makes it. Uncopied, the Savage Beastfly's wall
    /// slam (and every other boss's later-version shake: 775 actions in the room dumps) shook only the scene host's
    /// screen.
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, DoCameraShakeV2 action) =>
        ShakeLater(action, action.Camera, action.Profile, action.Target, action.MaxCameraDistance, action.DoFreeze, true,
            action.Delay, action.CancelOnExit);

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, DoCameraShakeV3 action) => true;

    /// <summary>Applies network data to the FSM action: as V2, with no shake for something off this player's screen.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, DoCameraShakeV3 action) {
        var visible = action.VisibleRenderer.GetSafe(action);
        if (visible != null && visible.GetComponent<Renderer>() is { } renderer && !renderer.isVisible) {
            return;
        }

        ShakeLater(action, action.Camera, action.Profile, null, null, action.DoFreeze, action.Vibrate.Value, action.Delay,
            action.cancelOnExit);
    }

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, DoCameraShakeV4 action) => true;

    /// <summary>Applies network data to the FSM action: as V2, with or without vibration.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, DoCameraShakeV4 action) =>
        ShakeLater(action, action.Camera, action.Profile, action.Target, action.MaxCameraDistance, action.DoFreeze,
            action.vibrate.Value, action.Delay, action.CancelOnExit);

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, DoCameraShakeRepeatingV2 action) => true;

    /// <summary>
    /// Applies network data to the FSM action: a shake after its delay and then every repeat delay until the host's FSM
    /// leaves the state, as DoCameraShakeRepeating's copy, with or without vibration.
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, DoCameraShakeRepeatingV2 action) {
        if (action.Camera.Value is not CameraManagerReference camera || action.Profile.Value is not CameraShakeProfile profile) {
            return;
        }

        new ActionInState {
            Fsm = action.Fsm,
            StateName = action.State.Name,
            Coroutine = MonoBehaviourUtil.Instance.StartCoroutine(Repeat())
        }.Register();
        return;

        IEnumerator Repeat() {
            if (action.Delay.Value > 0f) {
                yield return new WaitForSeconds(action.Delay.Value);
            }

            for (var i = 0; i < RepeatedShakesCap; i++) {
                Shake(camera, profile, action, action.Target, action.MaxCameraDistance, action.DoFreeze.Value,
                    action.vibrate.Value);
                if (action.RepeatDelay.Value <= 0f) {
                    yield break;
                }

                yield return new WaitForSeconds(action.RepeatDelay.Value);
            }
        }
    }

    /// <summary>
    /// A copied shake of the later versions: after its delay (none if its state ends first and it cancels on exit),
    /// in range of its target if it has a range, else this player's camera shakes.
    /// </summary>
    private static void ShakeLater(FsmStateAction action, FsmObject cameraField, FsmObject profileField,
        FsmOwnerDefault? target, FsmVector2? range, FsmBool doFreeze, bool vibrate, FsmFloat delay, bool cancelOnExit) {
        if (cameraField.Value is not CameraManagerReference camera || profileField.Value is not CameraShakeProfile profile) {
            return;
        }

        if (delay.Value <= 0f) {
            Shake(camera, profile, action, target, range, doFreeze.Value, vibrate);
            return;
        }

        var routine = MonoBehaviourUtil.Instance.StartCoroutine(Later());
        if (cancelOnExit) {
            new ActionInState { Fsm = action.Fsm, StateName = action.State.Name, Coroutine = routine }.Register();
        }

        IEnumerator Later() {
            yield return new WaitForSeconds(delay.Value);

            Shake(camera, profile, action, target, range, doFreeze.Value, vibrate);
        }
    }

    /// <summary>One shake, in range of the action's target as the game's later shakes do it, if it has a range.</summary>
    private static void Shake(CameraManagerReference camera, CameraShakeProfile profile, FsmStateAction action,
        FsmOwnerDefault? target, FsmVector2? range, bool doFreeze, bool vibrate) {
        var aimed = target?.GetSafe(action);
        if (range is { IsNone: false } && aimed != null) {
            camera.DoShakeInRange(profile, action.Owner, range.Value, aimed.transform.position, doFreeze, vibrate);
        } else {
            camera.DoShake(profile, action.Owner, doFreeze, vibrate);
        }
    }

    #endregion

    #region FadeNestedFadeGroup

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, FadeNestedFadeGroup action) => true;

    /// <summary>
    /// Applies network data to the FSM action: the group fades as the scene host's (the action fades it with the group's
    /// own routine). Uncopied, the Phantom's Final Fog, switched on and faded in over 5 s at her last phase, stayed
    /// see-through for the friend while the host's arena filled with fog (2,561 such fades in the room dumps).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, FadeNestedFadeGroup action) => action.OnEnter();

    #endregion

    #region CancelCameraShake

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, CancelCameraShake action) => true;

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, CancelCameraShake action) => action.OnEnter();

    #endregion

    #region SimulateDeath

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, EnemyDeathEffectsRegular.SimulateDeath action) =>
        true;

    /// <summary>
    /// Applies network data to the FSM action: the copy shows the death's effects (its sound, the camera shake when it
    /// is on screen, its blood), not its corpse or the journal's kill (the scene host's game makes those, and the save
    /// is the host's). A summoned enemy that its boss takes back dies so (the Savage Beastfly's bugs), and the Widow's
    /// and Garmond's staggers: uncopied, the friend heard no death and felt no shake (865 such actions in the room
    /// dumps, with the two below).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, EnemyDeathEffectsRegular.SimulateDeath action) =>
        EmitDeathEffects(data, TargetOf(action.Fsm, action.target));

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data,
        EnemyDeathEffectsRegular.SimulateDeathNoCorpse action) => true;

    /// <summary>Applies network data to the FSM action: as <see cref="EnemyDeathEffectsRegular.SimulateDeath"/>.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data,
        EnemyDeathEffectsRegular.SimulateDeathNoCorpse action) => EmitDeathEffects(data, TargetOf(action.Fsm, action.Target));

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, EnemyDeathEffectsRegular.SimulateDeathV2 action) =>
        true;

    /// <summary>Applies network data to the FSM action: as <see cref="EnemyDeathEffectsRegular.SimulateDeath"/>.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, EnemyDeathEffectsRegular.SimulateDeathV2 action) =>
        EmitDeathEffects(data, TargetOf(action.Fsm, action.Target));

    private static readonly System.Reflection.MethodInfo? EmitEffectsMethod = typeof(EnemyDeathEffects).GetMethod(
        "EmitEffects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
    );

    /// <summary>The death effects of a copy, without a corpse (EnemyDeathEffects.EmitEffects).</summary>
    private static void EmitDeathEffects(EntityNetworkData? data, GameObject? target) {
        // a start-up state's action comes with no data (EntityInitializer, for every copy, the scene host's too):
        // skipped, or it would play in every game
        if (data == null) {
            return;
        }

        var effects = target == null ? null : target.GetComponent<EnemyDeathEffectsRegular>();
        if (effects != null) {
            EmitEffectsMethod?.Invoke(effects, [null]);
        }
    }

    #endregion

    #region ScreenFlash

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, ScreenFlash action) => true;

    /// <summary>Applies network data to the FSM action: the same flash on this player's screen.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, ScreenFlash action) => action.OnEnter();

    #endregion

    #region ScreenFlashTrobbio

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, ScreenFlashTrobbio action) => true;

    /// <summary>Applies network data to the FSM action: Trobbio's bomb flash on this player's screen.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, ScreenFlashTrobbio action) => action.OnEnter();

    #endregion

    #region PlayRandomAudioClipTable

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, PlayRandomAudioClipTable action) =>
        WritePick(data, action.Table.Value as RandomAudioClipTable);

    /// <summary>Applies network data to the FSM action: the scene host's sound from the table (EntityFsmActions.Picks).</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, PlayRandomAudioClipTable action) =>
        PlayWithPick(data, action.Table.Value as RandomAudioClipTable, action.OnEnter);

    /// <summary>Builds network data from the FSM action: the scene host's pick from the table.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, PlayRandomAudioClipTableV2 action) =>
        WritePick(data, action.Table.Value as RandomAudioClipTable);

    /// <summary>
    /// Applies network data to the FSM action: the scene host's sound from the table, as PlayRandomAudioClipTable.
    /// Uncopied, the friend heard none of Moorwing's flaps from it.
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, PlayRandomAudioClipTableV2 action) =>
        PlayWithPick(data, action.Table.Value as RandomAudioClipTable, action.OnEnter);

    #endregion

    #region PlayAudioEvent

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, PlayAudioEvent action) => true;

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, PlayAudioEvent action) => action.OnEnter();

    #endregion

    #region PlayAudioEventRandom

    /// <summary>
    /// Builds network data from the FSM action: Unity's random state just before the scene host's action picked its clip
    /// and pitch (they are picked inside the game's AudioEventRandom, which no other hook sees), or 2 when not known.
    /// </summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, PlayAudioEventRandom action) {
        if (!CopiesPick() || _lastEventRandom.Action != action || _lastEventRandom.Frame != Time.frameCount) {
            data.Packet.Write((byte) 2);
            return true;
        }

        data.Packet.Write((byte) 1);
        data.Packet.Write(_lastEventRandom.State);
        _lastEventRandom = default;
        return true;
    }

    /// <summary>
    /// Applies network data to the FSM action: its spawn picks from the scene host's random state, so the same clip and
    /// pitch (SpawnAudioEventRandom). Each game picked its own, so the friend heard another of the Last Judge's move
    /// sounds than the host at the same moment.
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, PlayAudioEventRandom action) {
        // a start-up state's action comes with no data (EntityInitializer, for every copy, the scene host's too):
        // skipped, or the sound would play in every game
        if (data == null) {
            return;
        }

        _replayEventRandom = data.Packet.ReadByte() == 1 ? (action, data.Packet.ReadString()) : default;
        try {
            action.OnEnter();
        } finally {
            _replayEventRandom = default;
        }
    }

    #endregion

    #region AudioPlayRandomVoiceFromTable

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AudioPlayRandomVoiceFromTable action) =>
        WritePick(data, action.audioClipTable);

    /// <summary>Applies network data to the FSM action: the scene host's voice line (EntityFsmActions.Picks).</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AudioPlayRandomVoiceFromTable action) =>
        PlayWithPick(data, action.audioClipTable, action.OnEnter);

    #endregion

    #region AudioPlayRandomVoiceFromTableV2

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AudioPlayRandomVoiceFromTableV2 action) =>
        WritePick(data, action.audioClipTable.Value as RandomAudioClipTable);

    /// <summary>Applies network data to the FSM action: the scene host's voice line (EntityFsmActions.Picks).</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AudioPlayRandomVoiceFromTableV2 action) =>
        PlayWithPick(data, action.audioClipTable.Value as RandomAudioClipTable, action.OnEnter);

    #endregion

    #region SendEventToRegister

    /// <summary>
    /// The register events a boss sends that set off its arena's stage objects (FSMs on objects that are no SSMP
    /// entities, so they run in each game and only got the event in the scene host's): the Savage Beastfly's thumps,
    /// Moss Mother's trap cocoons, Sister Splinter's spikes, the Widow's bells, the Bell Beast's falling bells, the Last
    /// Judge's gate pilgrims, a camera or music change, a defeated arena's end (Act 1's bosses, 2026-10-27). Events
    /// about the local hero (HERO DAMAGED, the parry prompts) or that summon enemies (SUMMON) stay the scene host's.
    /// </summary>
    private static readonly HashSet<string> StageRegisterEvents = [
        "THUMP", "CHAPEL MUSIC STOP", "BEAST DEFEATED", "COCOON DESTROY", "SPIKE SPREE", "SPIKE CLEANUP",
        "SPINNER MID DEATH", "KILL PILGRIMS", "FINAL PHASE", "BOSS DEATH", "TENSION STOP", "LEAVE"
    ];

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SendEventToRegister action) =>
        StageRegisterEvents.Contains(action.eventName.Value);

    /// <summary>Applies network data to the FSM action: the same event to this game's listeners.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SendEventToRegister action) =>
        EventRegister.SendEvent(action.eventName.Value);

    #endregion
}
