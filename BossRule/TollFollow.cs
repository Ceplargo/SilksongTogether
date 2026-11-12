using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HutongGames.PlayMaker;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (0.3.21) A bell bench's toll paid in another game plays here as the game plays it (<see cref="TollRule"/>): the
/// machine's own retract sound and animation (its Retract Animation state's PlayAudioEvent and AnimatorPlayStateWait),
/// then its FSM from After Retract Pause, whose Stop raises the bench (BellBench.Open) and turns the machine off. The
/// rest of Retract Animation is the payer's (its dialogue's end, a message to its hero, the save), so it is not run.
/// The payer's coins play in its machine's Prompt sub-FSM 1.2 s before that save: a sound action entering there goes
/// to the other games (<see cref="TollCoins"/>), whose waiting machine plays that sub-FSM state's own sound actions
/// (Bone_01c's toll_booth_insert_coins; the world sweep's "bell: bell_toll_machine" row, probe_toll_sounds). Each
/// sound plays through the game's own action, so it sounds where, and as loud as, the payer's.
/// </summary>
internal static class TollFollow {
    private const string FsmName = TollRule.MachineFsm;
    private const float LookEvery = 1.0f;

    /// <summary>Sends this game's payment sound (scene, the sub-FSM's state) to the other games; null while not connected.</summary>
    public static System.Action<string, string>? Send;

    private static bool _playing;  // an action played here for another game's payment: not sent back

    /// <summary>Whether the payment's sounds play here. Not inlined: the toll sounds' twin switches it off.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool PlaysSounds() => true;

    /// <summary>After a sound action enters: one in the toll machine's payment sub-FSM goes to the other games.</summary>
    public static void AudioPostfix(PlayAudioEventBase __instance) {
        try {
            var host = __instance.Fsm?.Host;
            if (_playing || Send == null || host == null || !TollRule.IsMachine(host.Name, host.GameObject.name)) return;
            var scene = host.GameObject.scene.name;
            Debug.Log($"[BossRule] Toll paid here: {__instance.State.Name}'s sound goes to the other games ({scene})");
            Send(scene, __instance.State.Name);
        } catch (System.Exception e) {
            Debug.LogWarning($"[BossRule] A toll payment's sound is not sent: {e.GetBaseException().Message}");
        }
    }

    /// <summary>Another game's payment sound: each machine here still waiting unpaid plays that state's own sounds.</summary>
    public static void Received(TollCoins packet) {
        if (!PlaysSounds() || packet.Scene != GameManager.SilentInstance?.sceneName) return;
        foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)) {
            var activated = fsm.FsmVariables.GetFsmBool("Activated");
            if (!fsm.isActiveAndEnabled || !TollRule.IsMachine(fsm.FsmName, fsm.name) || activated == null ||
                !TollRule.PlaysCoinsHere(fsm.ActiveStateName, activated.Value)) continue;
            foreach (var state in fsm.Fsm.SubFsmList.SelectMany(sub => sub.States).Where(s => s.Name == packet.State)) {
                Debug.Log($"[BossRule] Toll paid in another game: {fsm.name} plays {state.Name}'s sounds ({packet.Scene})");
                Play(state.Actions);
            }
        }
    }

    // each sound action as its state's entry runs it: its own clip, place, pitch and volume
    private static void Play(IEnumerable<FsmStateAction> actions) {
        _playing = true;
        try {
            foreach (var action in actions.OfType<PlayAudioEventBase>()) action.OnEnter();
        } catch (System.Exception e) {
            Debug.LogWarning($"[BossRule] A toll sound did not play: {e.GetBaseException().Message}");
        } finally {
            _playing = false;
        }
    }

    private sealed class Retract {
        public PlayMakerFSM Fsm = null!;
        public float Due = -1f;  // -1: the animation's length is read the frame after it starts
    }

    private static string? _scene;
    private static float _nextLook;
    private static PlayMakerFSM[] _machines = [];
    private static readonly List<Retract> Retracts = [];

    /// <summary>Every frame while connected: each waiting machine whose toll another game paid retracts here.</summary>
    public static void Tick() {
        var scene = GameManager.SilentInstance?.sceneName;
        if (scene != _scene || Time.unscaledTime >= _nextLook) {
            if (scene != _scene) Retracts.Clear();
            _scene = scene;
            _nextLook = Time.unscaledTime + LookEvery;
            _machines = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)
                .Where(f => f.FsmName == FsmName && f.name.StartsWith("bell_toll_machine")).ToArray();
        }

        foreach (var fsm in _machines) {
            if (!fsm || !fsm.isActiveAndEnabled || Retracts.Any(r => r.Fsm == fsm)) continue;
            var retract = fsm.FsmStates.FirstOrDefault(s => s.Name == "Retract Animation");
            var activated = fsm.FsmVariables.GetFsmBool("Activated");
            if (retract == null || activated == null || !TollRule.PaidElsewhere(fsm.ActiveStateName, activated.Value))
                continue;

            Debug.Log($"[BossRule] Toll paid in another game: {fsm.name} retracts ({scene})");
            if (PlaysSounds()) Play(retract.Actions);
            foreach (var action in retract.Actions) {
                if (action.GetType().Name != "AnimatorPlayStateWait") continue;
                var state = (HarmonyLib.Traverse.Create(action).Field("stateName").GetValue() as FsmString)?.Value;
                if (fsm.GetComponent<Animator>() is { } animator && !string.IsNullOrEmpty(state)) {
                    animator.enabled = true;
                    animator.Play(state);
                }
            }

            Retracts.Add(new Retract { Fsm = fsm });
        }

        for (var i = Retracts.Count - 1; i >= 0; i--) {
            var r = Retracts[i];
            if (!r.Fsm) {
                Retracts.RemoveAt(i);
            } else if (r.Due < 0f) {
                var animator = r.Fsm.GetComponent<Animator>();
                r.Due = Time.time + (animator ? animator.GetCurrentAnimatorStateInfo(0).length : 0f);
            } else if (Time.time >= r.Due) {
                r.Fsm.SetState("After Retract Pause");
                Retracts.RemoveAt(i);
            }
        }
    }
}
