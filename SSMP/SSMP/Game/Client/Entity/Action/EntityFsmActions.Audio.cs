using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using SSMP.Game.Client.Entity.Component;
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
    #region AudioPlay

    ///<summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AudioPlay action) {
        return true;
    }

    ///<summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AudioPlay action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null || !audioSource.enabled) {
            return;
        }

        EndFade(audioSource);
        var audioClip = action.oneShotClip.Value as AudioClip;
        if (audioClip == null) {
            audioSource.Play();

            if (action.volume.IsNone) {
                return;
            }

            audioSource.volume = action.volume.Value;
            return;
        }

        if (!action.volume.IsNone) {
            audioSource.PlayOneShot(audioClip, action.volume.Value);
            return;
        }

        audioSource.PlayOneShot(audioClip);
    }

    #endregion

    #region AudioPlaySimple

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AudioPlaySimple action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AudioPlaySimple action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) {
            return;
        }

        EndFade(audioSource);
        var audioClip = action.oneShotClip.Value as AudioClip;
        if (audioClip == null) {
            if (!audioSource.isPlaying) {
                audioSource.Play();
            }

            if (!action.volume.IsNone) {
                audioSource.volume = action.volume.Value;
            }
        } else {
            if (!action.volume.IsNone) {
                audioSource.PlayOneShot(audioClip, action.volume.Value);
            } else {
                audioSource.PlayOneShot(audioClip);
            }
        }
    }

    #endregion

    #region AudioPlayV2

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AudioPlayV2 action) => true;

    /// <summary>
    /// Applies network data to the FSM action: the one-shot clip, else the source's own clip if it is not playing, as
    /// the game's action starts them (its end event is the scene host's FSM's). Uncopied, the friend heard none of these
    /// plays: the Bone Hunters' notice, the pilgrims' stomps, the Dock Flyers' and the Tar Slugs' loops.
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AudioPlayV2 action) {
        var target = TargetOf(action.Fsm, action.gameObject);
        var audioSource = target == null ? null : target.GetComponent<AudioSource>();
        if (audioSource == null) {
            return;
        }

        var audioClip = action.oneShotClip.Value as AudioClip;
        // a start-up state's action comes with no data (EntityInitializer, for every copy, the scene host's too): its
        // loop plays, a one-shot would sound in every game at once
        if (data == null && audioClip != null) {
            return;
        }

        EndFade(audioSource);
        if (audioClip == null) {
            if (!audioSource.isPlaying) {
                audioSource.Play();
            }

            if (!action.volume.IsNone) {
                audioSource.volume = action.volume.Value;
            }
        } else if (!action.volume.IsNone) {
            audioSource.PlayOneShot(audioClip, action.volume.Value);
        } else {
            audioSource.PlayOneShot(audioClip);
        }
    }

    #endregion

    #region AudioPlaySimpleV2

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AudioPlaySimpleV2 action) => true;

    /// <summary>
    /// Applies network data to the FSM action: the one-shot clip, else the source's own clip from its start time if it
    /// is not playing, as the game's action does. Uncopied, the friend heard none of these plays (the Shell Fossil
    /// Mimics' flight loop, the Roof Crabs' walk).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AudioPlaySimpleV2 action) {
        var target = TargetOf(action.Fsm, action.gameObject);
        var audioSource = target == null ? null : target.GetComponent<AudioSource>();
        if (audioSource == null) {
            return;
        }

        var audioClip = action.oneShotClip.Value as AudioClip;
        // a start-up state's action comes with no data, as for AudioPlayV2: its loop plays, a one-shot is skipped
        if (data == null && audioClip != null) {
            return;
        }

        EndFade(audioSource);
        if (audioClip == null) {
            if (!action.volume.IsNone) {
                audioSource.volume = action.volume.Value;
            }

            if (!action.startTime.IsNone) {
                audioSource.time = action.startTime.Value;
            }

            if (!audioSource.isPlaying) {
                audioSource.Play();
            }
        } else if (!action.volume.IsNone) {
            audioSource.PlayOneShot(audioClip, action.volume.Value);
        } else {
            audioSource.PlayOneShot(audioClip);
        }
    }

    #endregion

    #region FadeAudio

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, FadeAudio action) => true;

    /// <summary>
    /// Applies network data to the FSM action: the source's volume (its blend controller's when it has one, as the game
    /// sets it) goes from the start volume to the end one over the fade time, as the game's action fades it each frame,
    /// and is set to the end one when the scene host's state ends. Uncopied, a sound the host's game faded out went on
    /// loud in the friend's (the Savage Beastfly's song antic) and one it faded in stayed silent (the Last Judge's spin).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, FadeAudio action) {
        var target = TargetOf(action.Fsm, action.gameObject);
        var source = target == null ? null : target.GetComponent<AudioSource>();
        if (source == null) {
            return;
        }

        var blend = source.GetComponent<VolumeBlendController>();
        var modifier = blend != null ? blend.GetSharedFSMModifier() : null;
        var start = action.startVolume.IsNone ? Volume() : action.startVolume.Value;
        var end = action.endVolume.Value;
        var time = action.time.Value;
        SetVolume(start);
        new ActionInState {
            Fsm = action.Fsm,
            StateName = action.State.Name,
            Coroutine = MonoBehaviourUtil.Instance.StartCoroutine(Fade()),
            ExitAction = () => {
                if (source != null) {
                    SetVolume(end);
                }
            }
        }.Register();
        return;

        float Volume() => modifier != null ? modifier.Volume : source.volume;

        void SetVolume(float volume) {
            if (modifier != null) {
                modifier.Volume = volume;
            } else {
                source.volume = volume;
            }
        }

        IEnumerator Fade() {
            var from = Time.timeAsDouble;
            while (source != null && time > 0f && Time.timeAsDouble < from + time) {
                SetVolume(Mathf.Lerp(start, end, (float) ((Time.timeAsDouble - from) / time)));
                yield return null;
            }

            if (source != null) {
                SetVolume(end);
            }
        }
    }

    #endregion

    #region SetAudioSource

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetAudioSource action) => true;

    /// <summary>
    /// Applies network data to the FSM action: the source switched on or off as the scene host's (one switched on that
    /// plays on awake starts its sound; a copied play skips a source that is off). Uncopied, the friend's copies kept
    /// their start-up sources (15 Act 1 kinds: the Bone Hunters, Goombas and Crawlers, the Aspid Collectors).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetAudioSource action) {
        var target = TargetOf(action.Fsm, action.gameObject);
        if (target != null && target.TryGetComponent<AudioSource>(out var source)) {
            source.enabled = action.active.Value;
        }
    }

    #endregion

    #region SetRandomAudioClipFromTable

    /// <summary>
    /// Builds network data from the FSM action: the scene host's pick from its table, when the action set it at once (a
    /// delayed set comes after this is sent, and is not copied).
    /// </summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetRandomAudioClipFromTable action) {
        if (action.delay.Value != 0f || !WritePick(data, action.Table.Value as RandomAudioClipTable)) return false;
        // (coop) its at-once play is replayed with it: not again by the loop follow's restart count (LoopStarts.SentAction,
        // as SetAudioClip's; Lace's Dormant hum played twice at once in the friend's game, enemy_plays, NOTES 321)
        if (action.AutoPlay.Value && action.AudioSource.Value is { } holder &&
            holder.TryGetComponent<AudioSource>(out var source)) {
            LoopStarts.SentAction(source);
        }

        return true;
    }

    /// <summary>
    /// Applies network data to the FSM action: the source gets the scene host's clip from the table (its own pick when not
    /// known), the table's volume and pitch, loops, and plays if the action plays it. Uncopied, the friend heard none of
    /// the pilgrims' idle voices (17 Act 1 kinds), Lace's hum before her fight or the Widow's dying scream.
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetRandomAudioClipFromTable action) {
        var table = action.Table.Value as RandomAudioClipTable;
        var target = TargetOf(action.Fsm, action.AudioSource);
        var source = target == null ? null : target.GetComponent<AudioSource>();
        PlayWithPick(data, table, () => {
            if (table == null || source == null) {
                return;
            }

            source.clip = table.SelectClip(forcePlay: true);
            source.volume = table.SelectVolume();
            source.pitch = table.SelectPitch();
            source.loop = true;
            if (action.AutoPlay.Value) {
                EndFade(source);
                source.Play();
            }
        });
    }

    #endregion

    #region AudioPlayerOneShot

    /// <summary>
    /// Builds network data from the FSM action: the scene host's weighted pick and pitch when it played at once (one
    /// after a delay picks later, so its copy picks its own).
    /// </summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AudioPlayerOneShot action) {
        var player = action.storePlayer.Value;
        return WriteWeightedPick(data, action.delay.Value == 0f && action.audioClips.Length > 0,
            player != null ? player.GetComponent<AudioSource>() : null);
    }

    /// <summary>
    /// Applies network data to the FSM action: the scene host's pick at its pitch (its own when not known), after the
    /// action's delay as the game plays it. Each game picked its own, so the friend heard another of Lace's sword
    /// sounds than the host, and a delayed one came early.
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AudioPlayerOneShot action) {
        // a start-up state's action comes with no data (EntityInitializer, for every copy, the scene host's too):
        // skipped, or the sound would play in every game
        if (data == null || action.audioClips.Length == 0) {
            return;
        }

        var pick = ReadWeightedPick(data);
        if (action.delay.Value > 0f) {
            MonoBehaviourUtil.Instance.StartCoroutine(Later());
            return;
        }

        Play();
        return;

        IEnumerator Later() {
            yield return new WaitForSeconds(action.delay.Value);
            Play();
        }

        void Play() {
            // the spawn point is often a variable the boss's start-up fills (the Fourth Chorus's Torso), empty on the copy
            var spawn = TargetOf(action.Fsm, action.spawnPoint);
            if (spawn == null) {
                return;
            }

            var audioPlayer = action.audioPlayer.Value.Spawn(spawn.transform.position, Quaternion.Euler(Vector3.up));

            var audioSource = audioPlayer.GetComponent<AudioSource>();

            action.storePlayer.Value = audioPlayer;

            var index = PickRules.PickIn(pick?.Index ?? ActionHelpers.GetRandomWeightedIndex(action.weights),
                action.audioClips.Length);
            if (index != null && action.audioClips[index.Value] is { } audioClip) {
                audioSource.pitch = pick?.Pitch ?? Random.Range(action.pitchMin.Value, action.pitchMax.Value);
                audioSource.PlayOneShot(audioClip);
            }

            audioSource.volume = action.volume.Value;
        }
    }

    #endregion

    #region AudioPlayerOneShotSingle

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AudioPlayerOneShotSingle action) {
        return !action.audioPlayer.IsNone && !action.spawnPoint.IsNone && action.spawnPoint.Value != null;
    }

    /// <summary>
    /// Applies network data to the FSM action: the clip from the spawn point after the action's delay, as the game plays
    /// it. The spawn point is often a variable the boss's start-up fills (the Fourth Chorus's hands), which was empty on
    /// the copy, so the friend heard none of her hand slams; it is filled from the original's FSM.
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AudioPlayerOneShotSingle action) {
        if (action.audioPlayer.IsNone || action.spawnPoint.IsNone) {
            return;
        }

        if (action.delay.Value > 0f) {
            MonoBehaviourUtil.Instance.StartCoroutine(Later());
            return;
        }

        Play();
        return;

        IEnumerator Later() {
            yield return new WaitForSeconds(action.delay.Value);
            Play();
        }

        void Play() {
            var spawn = TargetOf(action.Fsm, action.spawnPoint);
            var audioPlayer = action.audioPlayer.Value;
            if (spawn == null || audioPlayer == null) {
                return;
            }

            PlaySingle(action, audioPlayer.Spawn(spawn.transform.position, Quaternion.Euler(Vector3.up)));
        }
    }

    /// <summary>The single clip of a copied AudioPlayerOneShotSingle on its spawned player.</summary>
    private static void PlaySingle(AudioPlayerOneShotSingle action, GameObject audioPlayer) {
        var audioSource = audioPlayer.GetComponent<AudioSource>();
        action.storePlayer.Value = audioPlayer;

        var audioClip = action.audioClip.Value as AudioClip;
        audioSource.pitch = Random.Range(action.pitchMin.Value, action.pitchMax.Value);
        audioSource.volume = action.volume.Value;

        if (audioClip == null) {
            return;
        }

        audioSource.PlayOneShot(audioClip);
    }

    #endregion

    #region SetAudioClip

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetAudioClip action) {
        var target = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (action.autoPlay?.Value == true && target != null &&
            target.TryGetComponent<AudioSource>(out var source) && source.loop) {
            LoopStarts.SentAction(source);
        }
        return true;
    }

    /// <summary>
    /// Applies network data to the FSM action: the clip set and, when the action plays it, played as the game's action
    /// does (from its start if the clip changed) and stopped when the scene host's state ends if it stops on exit. The
    /// copy set the clip and never played it: Moorwing's idle loop never sounded in the friend's game (41 of 41 missing).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetAudioClip action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) {
            return;
        }

        var clip = audioSource.clip;
        audioSource.clip = action.audioClip.Value as AudioClip;
        // a start-up state's action comes with no data (EntityInitializer, for every copy, the scene host's too): a loop
        // plays, a one-shot would sound in every game at once
        if (action.autoPlay?.Value != true || data == null && !audioSource.loop) {
            return;
        }

        if (audioSource.clip != clip) {
            audioSource.Stop();
            audioSource.time = 0f;
        }

        EndFade(audioSource);
        audioSource.Play();
        if (action.stopOnExit?.Value == true) {
            new ActionInState {
                Fsm = action.Fsm,
                StateName = action.State.Name,
                ExitAction = () => audioSource.Stop()
            }.Register();
        }
    }

    #endregion

    #region SetAudioPitch

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetAudioPitch action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetAudioPitch action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) {
            return;
        }

        audioSource.pitch = action.pitch.Value;
    }

    #endregion

    #region AudioStop

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AudioStop action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AudioStop action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) {
            return;
        }

        audioSource.Stop();
    }

    #endregion

    #region AudioStopV2

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AudioStopV2 action) => true;

    /// <summary>
    /// Applies network data to the FSM action: the sound stops as the game stops it, faded out over the fade time
    /// (the volume down with the square of the time left, as its VolumeFade; the next play sets it again). Uncopied, the
    /// Savage Beastfly's charge loop never stopped in the other players' games, so it went on after her charge and
    /// her next charges did not start it (the copied AudioPlaySimple plays only a source that is not playing).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AudioStopV2 action) {
        var source = action.Fsm.GetOwnerDefaultTarget(action.gameObject)?.GetComponent<AudioSource>();
        if (source == null) {
            return;
        }

        var fade = action.fadeTime.IsNone ? 0f : action.fadeTime.Value;
        if (fade < 0.01f || !source.isPlaying) {
            source.Stop();
            return;
        }

        EndFade(source);
        var volume = source.volume;
        PendingFades[source] = (MonoBehaviourUtil.Instance.StartCoroutine(Fade(source.clip)), volume);
        return;

        IEnumerator Fade(AudioClip? clip) {
            var start = Time.timeAsDouble;
            while (source != null && source.isPlaying && Time.timeAsDouble < start + fade) {
                var left = (float) (start + fade - Time.timeAsDouble) / fade;
                source.volume = left * left * volume;
                yield return null;
            }

            if (source == null) {
                yield break;
            }

            PendingFades.Remove(source);
            if (clip == null || source.clip == clip) {
                source.Stop();
            }
        }
    }

    /// <summary>
    /// The copied AudioStopV2 fades still running, by source, with the volume each started from. The scene host's game
    /// cuts a fade short when its state ends (most stop states end at once), so a copied play on the source would
    /// find it still playing and skip its start, and the fade then stopped the new sound (a review, 2026-10-28).
    /// </summary>
    private static readonly Dictionary<AudioSource, (Coroutine Routine, float Volume)> PendingFades = new();

    /// <summary>A copied fade on this source still running ends now: stopped, at the volume it started from.</summary>
    private static void EndFade(AudioSource source) {
        if (!PendingFades.TryGetValue(source, out var fade)) {
            return;
        }

        PendingFades.Remove(source);
        MonoBehaviourUtil.Instance.StopCoroutine(fade.Routine);
        source.volume = fade.Volume;
        source.Stop();
    }

    #endregion

    #region SetAudioVolume

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetAudioVolume action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetAudioVolume action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) {
            return;
        }

        audioSource.volume = action.volume.Value;
    }

    #endregion

    #region AudioPlayRandomVoice

    /// <summary>Builds network data from the FSM action: the scene host's pick of its clips and its pitch.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AudioPlayRandomVoice action) {
        // with no clips or no target the scene host's action played nothing and stopped nothing
        var target = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (action.audioClips == null || action.audioClips.Length == 0 || target == null) {
            return false;
        }

        return WriteWeightedPick(data, true, target.GetComponent<AudioSource>());
    }

    /// <summary>
    /// Applies network data to the FSM action: the copy stops its previous sound if the action does, and plays the
    /// scene host's pick at its pitch (its own pick when not known). Uncopied, the friend heard no voice from the
    /// enemies that call from a clip list (the Far Fields' flyers and beetles at the Bone Hunter guards and the Ant
    /// Trapper, the Savage Beastfly's bugs: 1,254 such actions in the room dumps).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AudioPlayRandomVoice action) {
        // a start-up state's action comes with no data (EntityInitializer, for every copy, the scene host's too):
        // skipped, or the call would sound in every game
        if (data == null) {
            return;
        }

        var pick = ReadWeightedPick(data);
        var target = TargetOf(action.Fsm, action.gameObject);
        var source = target == null ? null : target.GetComponent<AudioSource>();
        if (source == null || action.audioClips == null || action.audioClips.Length == 0) {
            return;
        }

        if (action.stopPreviousSound) {
            source.Stop();
        }

        var index = PickRules.PickIn(pick?.Index ?? ActionHelpers.GetRandomWeightedIndex(action.weights),
            action.audioClips.Length);
        if (index == null || action.audioClips[index.Value] == null) {
            return;
        }

        EndFade(source);
        source.pitch = pick?.Pitch ?? Random.Range(action.pitchMin.Value, action.pitchMax.Value);
        source.PlayOneShot(action.audioClips[index.Value]);
    }

    #endregion

    #region AudioPlayRandom

    /// <summary>Builds network data from the FSM action: the scene host's pick of its clips and its pitch.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AudioPlayRandom action) {
        if (action.audioClips.Length == 0) {
            return false;
        }

        var target = action.gameObject.Value;
        return WriteWeightedPick(data, true, target != null ? target.GetComponent<AudioSource>() : null);
    }

    /// <summary>
    /// Applies network data to the FSM action: the scene host's pick at its pitch (its own pick when not known). Each
    /// game picked its own, so the friend heard another of Lace's laughs than the host at the same moment.
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AudioPlayRandom action) {
        // a start-up state's action comes with no data (EntityInitializer, for every copy, the scene host's too):
        // skipped, or the sound would play in every game
        if (data == null || action.audioClips == null || action.audioClips.Length == 0) {
            return;
        }

        var pick = ReadWeightedPick(data);
        var audioSource = action.gameObject.Value.GetComponent<AudioSource>();

        var index = PickRules.PickIn(pick?.Index ?? ActionHelpers.GetRandomWeightedIndex(action.weights),
            action.audioClips.Length);
        if (index == null) {
            return;
        }

        var audioClip = action.audioClips[index.Value];
        if (audioClip == null) {
            return;
        }

        audioSource.pitch = pick?.Pitch ?? Random.Range(action.pitchMin.Value, action.pitchMax.Value);
        audioSource.PlayOneShot(audioClip);
    }

    #endregion

    #region AudioPlayInState

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, AudioPlayInState action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, AudioPlayInState action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) {
            return;
        }

        if (!audioSource.isPlaying) {
            audioSource.Play();
        }

        if (!action.volume.IsNone) {
            audioSource.volume = action.volume.Value;
        }

        void ExitAction() {
            audioSource.Stop();
        }

        new ActionInState {
            Fsm = action.Fsm,
            StateName = action.State.Name,
            ExitAction = ExitAction
        }.Register();
    }

    #endregion
}
