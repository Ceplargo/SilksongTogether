using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
    #region SetParticleEmission

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetParticleEmission action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetParticleEmission action) {
        if (action.Fsm == null) {
            return;
        }

        if (action.emission == null) {
            return;
        }

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var particleSystem = gameObject.GetComponent<ParticleSystem>();
        if (particleSystem == null) {
            return;
        }

#pragma warning disable CS0618
        particleSystem.enableEmission = action.emission.Value;
#pragma warning restore CS0618
    }

    #endregion

    #region SetParticleEmissionRate

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetParticleEmissionRate action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetParticleEmissionRate action) {
        if (action.gameObject == null) {
            return;
        }

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var particleSystem = gameObject.GetComponent<ParticleSystem>();
        if (particleSystem == null) {
            return;
        }

        Action();

        if (action.everyFrame) {
            MonoBehaviourUtil.Instance.OnUpdateEvent += Action;

            new ActionInState {
                Fsm = action.Fsm,
                StateName = action.State.Name,
                ExitAction = () => MonoBehaviourUtil.Instance.OnUpdateEvent -= Action
            }.Register();
        }

        return;

        void Action() {
#pragma warning disable CS0618
            particleSystem.emissionRate = action.emissionRate.Value;
#pragma warning restore CS0618
        }
    }

    #endregion

    #region SetParticleEmissionSpeed

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetParticleEmissionSpeed action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetParticleEmissionSpeed action) {
        if (action.gameObject == null) {
            return;
        }

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var particleSystem = gameObject.GetComponent<ParticleSystem>();
        if (particleSystem == null) {
            return;
        }

        Action();

        if (action.everyFrame) {
            MonoBehaviourUtil.Instance.OnUpdateEvent += Action;

            new ActionInState {
                Fsm = action.Fsm,
                StateName = action.State.Name,
                ExitAction = () => MonoBehaviourUtil.Instance.OnUpdateEvent -= Action
            }.Register();
        }

        void Action() {
#pragma warning disable CS0618
            particleSystem.startSpeed = action.emissionSpeed.Value;
#pragma warning restore CS0618
        }
    }

    #endregion

    #region PlayParticleEmitter

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, PlayParticleEmitter action) {
        return true;
    }

    /// <summary>
    /// Applies network data to the FSM action: played as the game plays it, again over a burst whose particles still
    /// live (the copy played only when the system had none left, so the friend's game skipped every second burst of the
    /// Phantom's dragoon dust and showed the last one's dark tail); a start-up state's (no data) only when not playing.
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, PlayParticleEmitter action) {
        if (action.emit == null) {
            return;
        }

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var particleSystem = gameObject.GetComponent<ParticleSystem>();
        if (particleSystem == null) {
            return;
        }

        if (action.emit.Value > 0) {
            particleSystem.Emit(action.emit.Value);
            return;
        }

        if (data == null || !PlaysAgain()) {
            if (!particleSystem.isPlaying) {
                particleSystem.Play();
            }

            return;
        }

        if (action.resetIfPlaying && particleSystem.isPlaying) {
            particleSystem.Stop();
        }

        particleSystem.Play();
    }

    /// <summary>Whether a copied particle play plays again over living particles, as the game's action does.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    private static bool PlaysAgain() => true;

    #endregion

    #region PlayParticleEmitterInState

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, PlayParticleEmitterInState action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, PlayParticleEmitterInState action) {
        if (action.gameObject == null) {
            return;
        }

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var particleSystem = gameObject.GetComponent<ParticleSystem>();
        if (particleSystem == null) {
            return;
        }

        particleSystem.Play();

        new ActionInState {
            Fsm = action.Fsm,
            StateName = action.State.Name,
            ExitAction = () => {
                if (particleSystem.isPlaying) {
                    particleSystem.Stop();
                }
            }
        }.Register();
    }

    #endregion

    #region StopParticleEmitter

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, StopParticleEmitter action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, StopParticleEmitter action) {
        if (action.gameObject == null) {
            return;
        }

        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var particleSystem = gameObject.GetComponent<ParticleSystem>();
        if (particleSystem == null) {
            return;
        }

        if (particleSystem.isPlaying) {
            particleSystem.Stop();
        }
    }

    #endregion

    #region PlayParticleEmitterChildren

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, PlayParticleEmitterChildren action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action: plays the particle systems of the object's children.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, PlayParticleEmitterChildren action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var particleSystems = new List<ParticleSystem>();
        foreach (Transform child in gameObject.transform) {
            var particleSystem = child.GetComponent<ParticleSystem>();
            if (particleSystem == null) {
                continue;
            }

            particleSystem.Play();
            if (action.resetTimeIfPlaying) {
                particleSystem.time = 0f;
            }

            particleSystems.Add(particleSystem);
        }

        if (!action.stopOnStateExit) {
            return;
        }

        new ActionInState {
            Fsm = action.Fsm,
            StateName = action.State.Name,
            ExitAction = () => {
                foreach (var particleSystem in particleSystems) {
                    if (particleSystem != null) {
                        particleSystem.Stop();
                    }
                }
            }
        }.Register();
    }

    #endregion

    #region SetParticleEmissionTemp

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetParticleEmissionTemp action) => true;

    /// <summary>
    /// Applies network data to the FSM action: the emission set for its time, and back once that has passed or the
    /// scene host's FSM leaves the state, whichever comes first.
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetParticleEmissionTemp action) {
        var target = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        var particles = target != null ? target.GetComponent<ParticleSystem>() : null;
        if (particles == null) {
            return;
        }

        var on = action.emission.Value;
        var ended = false;
        SetEmission(on);
        new ActionInState {
            Fsm = action.Fsm,
            StateName = action.State.Name,
            Coroutine = MonoBehaviourUtil.Instance.StartCoroutine(Restore()),
            ExitAction = () => {
                if (!ended) {
                    SetEmission(!on);
                }
            }
        }.Register();
        return;

        IEnumerator Restore() {
            yield return new WaitForSeconds(action.time.Value);

            ended = true;
            SetEmission(!on);
        }

        void SetEmission(bool enabled) {
            if (particles != null) {
                var emission = particles.emission;
                emission.enabled = enabled;
            }
        }
    }

    #endregion

    #region StopParticleEmittersInChildren

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, StopParticleEmittersInChildren action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, StopParticleEmittersInChildren action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        foreach (var particleSystem in gameObject.GetComponentsInChildren<ParticleSystem>()) {
            if (particleSystem.isPlaying) {
                particleSystem.Stop();
            }
        }
    }

    #endregion

    #region SetParticleEmissionChildren

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetParticleEmissionChildren action) {
        return true;
    }

    /// <summary>
    /// Applies network data to the FSM action: emission on or off for every particle system under the object, once
    /// (the action can repeat it every frame).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetParticleEmissionChildren action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.Target);
        if (gameObject == null) {
            return;
        }

        var particleSystems = gameObject.GetComponentsInChildren<ParticleSystem>(true);
        SetEmission(action.SetEmission.Value);

        if (action.stopOnExit) {
            new ActionInState {
                Fsm = action.Fsm,
                StateName = action.State.Name,
                ExitAction = () => SetEmission(false)
            }.Register();
        }

        void SetEmission(bool enabled) {
            foreach (var particleSystem in particleSystems) {
                if (particleSystem != null) {
                    var emission = particleSystem.emission;
                    emission.enabled = enabled;
                }
            }
        }
    }

    #endregion

    #region SetMeshRenderer

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetMeshRenderer action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetMeshRenderer action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null) {
            return;
        }

        meshRenderer.enabled = action.active.Value;
    }

    #endregion

    #region Tk2dSpriteSetColor

    /// <summary>
    /// Builds network data from the FSM action: the colour the scene host's sprite is set to. Not an every-frame set
    /// of a variable: it follows what another action does to the variable (the Dock guards' bomb blackening eases it
    /// back to white with EaseColor, which is not copied), and sent once its first value stayed on the copy for good.
    /// </summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, Tk2dSpriteSetColor action) {
        if (action.everyframe && action.color.UseVariable && LeavesEasedColours()) {
            return false;
        }

        WriteColor(data, action.color.Value);
        return true;
    }

    /// <summary>Whether an every-frame set of a colour variable is left out (the copy keeps its own colour).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    private static bool LeavesEasedColours() => true;

    /// <summary>
    /// Applies network data to the FSM action: the copy's sprite gets the scene host's colour, once (an every-frame set
    /// is not held: the next state's colour can come before the state change, and a held one would paint over it). A
    /// summoned enemy waits black and turns to its colours as it comes in (the Savage Beastfly's Bone Spitters in the
    /// Far Fields): uncopied, the friend saw it fight as a black shape (6,243 such actions in the room dumps).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, Tk2dSpriteSetColor action) {
        // a start-up state's action comes with no data (EntityInitializer, also when this game becomes the scene host):
        // skipped, so a summon already fighting is not painted with its dormant black
        if (data == null) {
            return;
        }

        var color = ReadColor(data);
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        var sprite = gameObject == null ? null : gameObject.GetComponent<tk2dBaseSprite>();
        if (sprite == null) {
            return;
        }

        EndTween(sprite);
        sprite.color = color;
    }

    #endregion

    #region Tk2dSpriteTweenColor

    /// <summary>Builds network data from the FSM action: the colour the scene host's sprite turns to.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, Tk2dSpriteTweenColor action) {
        WriteColor(data, action.color.Value);
        return true;
    }

    /// <summary>
    /// Applies network data to the FSM action: the copy's sprite turns from its colour to the scene host's over the
    /// action's time, and has it once the state ends, as the game's (4,909 such actions in the room dumps).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, Tk2dSpriteTweenColor action) {
        // a start-up state's action comes with no data: skipped, as the set's
        if (data == null) {
            return;
        }

        var color = ReadColor(data);
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        var sprite = gameObject == null ? null : gameObject.GetComponent<tk2dBaseSprite>();
        if (sprite == null) {
            return;
        }

        EndTween(sprite);
        var duration = action.duration.Value;
        if (duration <= 0f) {
            // the game's tween of no time sets the colour at once (a coroutine would end inside StartCoroutine)
            sprite.color = color;
            return;
        }

        var routine = MonoBehaviourUtil.Instance.StartCoroutine(Tween(sprite.color));
        Tweens[sprite] = (routine, color);
        new ActionInState {
            Fsm = action.Fsm,
            StateName = action.State.Name,
            // its own tween only: a later state's tween on this sprite can have started before this state's exit came
            ExitAction = () => EndTween(sprite, routine)
        }.Register();
        return;

        IEnumerator Tween(Color start) {
            for (var elapsed = 0f; elapsed <= duration; elapsed += Time.deltaTime) {
                if (sprite == null) {
                    Tweens.Remove(sprite!); // destroyed: Unity null, the same key
                    yield break;
                }

                sprite.color = Color.Lerp(start, color, elapsed / duration);
                yield return null;
            }

            Tweens.Remove(sprite);
            if (sprite != null) {
                sprite.color = color;
            }
        }
    }

    /// <summary>The copied colour tweens still running, by sprite, with the colour each ends at.</summary>
    private static readonly Dictionary<tk2dBaseSprite, (Coroutine Routine, Color Color)> Tweens = new();

    /// <summary>
    /// A copied colour tween on this sprite still running (that one only, if given) ends now, at its colour.
    /// </summary>
    private static void EndTween(tk2dBaseSprite sprite, Coroutine? only = null) {
        if (!Tweens.TryGetValue(sprite, out var tween) || only != null && tween.Routine != only) {
            return;
        }

        Tweens.Remove(sprite);
        if (tween.Routine != null) {
            MonoBehaviourUtil.Instance.StopCoroutine(tween.Routine);
        }

        if (sprite != null) {
            sprite.color = tween.Color;
        }
    }

    private static void WriteColor(EntityNetworkData data, Color color) {
        data.Packet.Write(color.r);
        data.Packet.Write(color.g);
        data.Packet.Write(color.b);
        data.Packet.Write(color.a);
    }

    private static Color ReadColor(EntityNetworkData data) =>
        new(data.Packet.ReadFloat(), data.Packet.ReadFloat(), data.Packet.ReadFloat(), data.Packet.ReadFloat());

    #endregion

    #region ActivateGameObject

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, ActivateGameObject action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return false;
        }

        return !IsObjectInRegistry(gameObject);
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, ActivateGameObject action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        void SetActiveRecursively(GameObject go, bool state) {
            go.SetActive(state);

            foreach (UnityEngine.Component component in go.transform) {
                SetActiveRecursively(component.gameObject, state);
            }
        }

        if (action.recursive.Value) {
            SetActiveRecursively(gameObject, action.activate.Value);
        } else {
            gameObject.SetActive(action.activate.Value);
        }

        // As the action does when its state ends: the object goes back once the scene host's FSM leaves the state
        // (the Vampire Gnat's Slash Terrain Detector stayed on for the friend after its first dive)
        if (action.resetOnExit) {
            new ActionInState {
                Fsm = action.Fsm,
                StateName = action.State.Name,
                ExitAction = () => {
                    if (gameObject == null) {
                        return;
                    }

                    if (action.recursive.Value) {
                        SetActiveRecursively(gameObject, !action.activate.Value);
                    } else {
                        gameObject.SetActive(!action.activate.Value);
                    }
                }
            }.Register();
        }
    }

    #endregion

    #region ActivateGameObjectDelay

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, ActivateGameObjectDelay action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        return gameObject != null && !IsObjectInRegistry(gameObject);
    }

    /// <summary>
    /// Applies network data to the FSM action: the object is switched after the action's delay, and put back as it was
    /// once the scene host's FSM leaves the state if the action resets on exit (the Vampire Gnat's slashes in its dives).
    /// </summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, ActivateGameObjectDelay action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var switched = false;
        var initial = gameObject.activeSelf;
        new ActionInState {
            Fsm = action.Fsm,
            StateName = action.State.Name,
            Coroutine = MonoBehaviourUtil.Instance.StartCoroutine(Switch()),
            ExitAction = () => {
                if (switched && action.resetOnExit && gameObject != null) {
                    gameObject.SetActive(initial);
                }
            }
        }.Register();
        return;

        IEnumerator Switch() {
            if (action.delay.Value > 0f) {
                yield return new WaitForSeconds(action.delay.Value);
            }

            // (coop) the object may be gone by now (destroyed in the wait): then nothing switches
            switched = DelayedSwitch.Run(gameObject, go => go != null, go => go.activeSelf, (go, on) => go.SetActive(on),
                action.activate.Value, out var before);
            if (switched) initial = before;
        }
    }

    #endregion

    #region Tk2dPlayAnimation

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, Tk2dPlayAnimation action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return false;
        }

        return !IsObjectInRegistry(gameObject);
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, Tk2dPlayAnimation action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var animator = gameObject.GetComponent<tk2dSpriteAnimator>();
        if (animator == null) {
            return;
        }

        animator.Play(action.clipName.Value);
    }

    #endregion

    #region Tk2dPlayFrame

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, Tk2dPlayFrame action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return false;
        }

        return !IsObjectInRegistry(gameObject);
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, Tk2dPlayFrame action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var animator = gameObject.GetComponent<tk2dSpriteAnimator>();
        if (animator == null) {
            return;
        }

        animator.PlayFromFrame(action.frame.Value);
    }

    #endregion

    #region Tk2dPlayAnimationWithEvents

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, Tk2dPlayAnimationWithEvents action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return false;
        }

        return !IsObjectInRegistry(gameObject);
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, Tk2dPlayAnimationWithEvents action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var animator = gameObject.GetComponent<tk2dSpriteAnimator>();
        if (animator == null) {
            return;
        }

        animator.Play(action.clipName.Value);
    }

    #endregion

    #region SpawnBlood

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SpawnBlood action) {
        // if (GlobalPref.Instance == null) {
        //     return false;
        // }
        //
        // data.Packet.Write((short) action.spawnMin.Value);
        // data.Packet.Write((short) action.spawnMax.Value);
        //
        // data.Packet.Write(action.speedMin.Value);
        // data.Packet.Write(action.speedMax.Value);
        // data.Packet.Write(action.angleMin.Value);
        // data.Packet.Write(action.angleMax.Value);
        //
        // return true;
        return false;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SpawnBlood action) {
        // var spawnMin = data.Packet.ReadShort();
        // var spawnMax = data.Packet.ReadShort();
        //
        // var speedMin = data.Packet.ReadFloat();
        // var speedMax = data.Packet.ReadFloat();
        //
        // var angleMin = data.Packet.ReadFloat();
        // var angleMax = data.Packet.ReadFloat();
        //
        // var position = action.position.Value;
        // if (action.spawnPoint.Value != null) {
        //     position += action.spawnPoint.Value.transform.position;
        // }
        //
        // GlobalPrefabDefaults.Instance.SpawnBlood(
        //     position,
        //     spawnMin,
        //     spawnMax,
        //     speedMin,
        //     speedMax,
        //     angleMin,
        //     angleMax,
        //     action.colorOverride.IsNone ? new Color?() : action.colorOverride.Value
        // );
    }

    #endregion

    #region SpawnBloodTime

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SpawnBloodTime action) {
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SpawnBloodTime action) {
        // var position = action.position.Value;
        // if (action.spawnPoint.Value != null) {
        //     position += action.spawnPoint.Value.transform.position;
        // }
        //
        // var spawnMin = (short) action.spawnMin.Value;
        // var spawnMax = (short) action.spawnMax.Value;
        //
        // var speedMin = action.speedMin.Value;
        // var speedMax = action.speedMax.Value;
        //
        // var angleMin = action.angleMin.Value;
        // var angleMax = action.angleMax.Value;
        //
        // var color = action.colorOverride.IsNone ? new Color?() : action.colorOverride.Value;
        //
        // var coroutine = MonoBehaviourUtil.Instance.StartCoroutine(Behaviour());
        //
        // new ActionInState {
        //     Fsm = action.Fsm,
        //     StateName = action.State.Name,
        //     Coroutine = coroutine
        // }.Register();
        //
        // IEnumerator Behaviour() {
        //     while (true) {
        //         yield return new WaitForSeconds(action.delay.Value);
        //
        //         if (GlobalPrefabDefaults.Instance == null) {
        //             break;
        //         }
        //
        //         GlobalPrefabDefaults.Instance.SpawnBlood(
        //             position,
        //             spawnMin,
        //             spawnMax,
        //             speedMin,
        //             speedMax,
        //             angleMin,
        //             angleMax,
        //             color
        //         );
        //     }
        // }
    }

    #endregion

    #region SetSpriteRenderer

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetSpriteRenderer action) {
        return action.gameObject != null;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetSpriteRenderer action) {
        var gameObject = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
        if (gameObject == null) {
            return;
        }

        var spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) {
            spriteRenderer.enabled = action.active.Value;
        }
    }

    #endregion

    #region SetAnimatorTrigger

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, SetAnimatorTrigger action) {
        data.Packet.Write(action.trigger.Value);
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, SetAnimatorTrigger action) {
        var trigger = data.Packet.ReadString();
        var animator = GetAnimator(action.Fsm, action.gameObject);
        if (animator != null) {
            animator.SetTrigger(trigger);
        }
    }

    #endregion

    #region ResetAnimatorTrigger

    /// <summary>Builds network data from the FSM action.</summary>
    private static bool GetNetworkDataFromAction(EntityNetworkData data, ResetAnimatorTrigger action) {
        data.Packet.Write(action.trigger.Value);
        return true;
    }

    /// <summary>Applies network data to the FSM action.</summary>
    private static void ApplyNetworkDataFromAction(EntityNetworkData data, ResetAnimatorTrigger action) {
        var trigger = data.Packet.ReadString();
        var animator = GetAnimator(action.Fsm, action.gameObject);
        if (animator != null) {
            animator.ResetTrigger(trigger);
        }
    }

    #endregion

    /// <summary>Gets the Animator on the action's target object, or null if it has none.</summary>
    private static Animator? GetAnimator(HutongGames.PlayMaker.Fsm? fsm, HutongGames.PlayMaker.FsmOwnerDefault target) {
        var gameObject = fsm?.GetOwnerDefaultTarget(target);
        return gameObject == null ? null : gameObject.GetComponent<Animator>();
    }
}
