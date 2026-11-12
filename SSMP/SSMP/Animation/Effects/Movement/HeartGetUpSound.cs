using HutongGames.PlayMaker.Actions;
using SSMP.Game.Client;
using SSMP.Internals;
using UnityEngine;

namespace SSMP.Animation.Effects.Movement;

/// <summary>
/// (coop) A hero's Silk Heart get-up sound at its copy in the other games (<see cref="HeartGetUp"/>): her game sends the
/// state and action of each clip table its Silk Heart plays for her, and this game plays that table from its own Silk
/// Heart's same action at her copy.
/// </summary>
internal class HeartGetUpSound : AnimationEffect {
    /// <inheritdoc/>
    public override byte[]? GetEffectInfo() => null;

    /// <inheritdoc/>
    public override void Play(GameObject playerObject, CrestType crestType, byte[]? effectInfo) {
        if (!HeartGetUp.Plays() || HeartGetUp.Read(effectInfo) is not var (state, action)) return;
        var heart = GameObject.Find("Silk Heart");
        var fsm = heart != null ? FSMUtility.LocateMyFSM(heart, "Control") : null;
        if (fsm == null || state >= fsm.FsmStates.Length) return;
        var actions = fsm.FsmStates[state].Actions;
        if (action >= actions.Length || actions[action] is not PlayRandomAudioClipTable play) return;
        if (play.Table.Value is RandomAudioClipTable table) {
            table.SpawnAndPlayOneShot(playerObject.transform.position);
        }
    }
}
