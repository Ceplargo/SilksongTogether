using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (0.3.22) Shakra's first greeting in the walker's game plays in the other games as a copy (<see cref="GreetingRule"/>):
/// each state her greeting enters there goes to the other games (Ring Shout, Dialogue, and her Rest Animation's end), and
/// each voice line she speaks. A game whose own greeting does not run plays, of each state, her own animation clips, her
/// sounds as the game plays them, her ring flash and the event that stops her singing; at the walker's end she rests
/// again and her own Rest Animation sings. Nothing holds or moves its hero, opens a dialogue or a shop, or saves.
/// </summary>
internal static class GreetingFollow {
    /// <summary>Sends a state (scene, FSM, state) or a voice line (scene, Voice, its table) to the other games; null while
    /// not connected.</summary>
    public static System.Action<string, string, string>? Send;

    private static bool _own;  // this game's own greeting runs (its hero walked up)
    private static string? _scene;
    private static readonly Dictionary<string, RandomAudioClipTable> Tables = new();

    /// <summary>Every frame: a new room ends this game's own greeting.</summary>
    public static void Tick() {
        var scene = GameManager.SilentInstance?.sceneName;
        if (scene == _scene) return;
        _scene = scene;
        _own = false;
    }

    /// <summary>Before a state is entered: her greeting's state in this game's own greeting goes to the other games.</summary>
    public static void EnterPrefix(FsmState __instance) {
        try {
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Name is not ("Ring Shout" or "Dialogue" or "Rest Animation")) return;
            var owner = fsm.GameObject;
            if (owner == null || !GreetingRule.Follows(owner.name, fsm.Name)) return;
            if (GreetingRule.Starts(fsm.Name, __instance.Name)) _own = true;
            if (Send != null && GreetingRule.Sends(_own, fsm.Name, __instance.Name)) {
                Send(owner.scene.name, fsm.Name, __instance.Name);
            }

            if (GreetingRule.Ends(fsm.Name, __instance.Name)) _own = false;
        } catch (System.Exception e) {
            Debug.LogWarning($"[BossRule] A greeting state is not sent: {e.GetBaseException().Message}");
        }
    }

    /// <summary>Before an NPC speaks a line (NPCSpeakingAudio.PlayVoice): hers in this game's own greeting goes to the
    /// other games. The copy's own line (no runner) is not sent back.</summary>
    public static void VoicePrefix(RandomAudioClipTable audioTable, NPCSpeakingAudio runner) {
        try {
            if (!_own || Send == null || runner == null || audioTable == null || runner.gameObject.name != GreetingRule.Npc) return;
            Send(runner.gameObject.scene.name, GreetingRule.Voice, audioTable.name);
        } catch (System.Exception e) {
            Debug.LogWarning($"[BossRule] A greeting line is not sent: {e.GetBaseException().Message}");
        }
    }

    /// <summary>Another game's greeting: this game plays it on her, unless its own greeting runs.</summary>
    public static void Received(GreetingState packet) {
        try {
            if (packet.Scene != GameManager.SilentInstance?.sceneName || !GreetingRule.Plays(_own)) return;
            var fsms = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)
                .Where(f => f.name == GreetingRule.Npc).ToArray();
            if (fsms.Length == 0) return;
            var her = fsms[0].gameObject;
            if (packet.Fsm == GreetingRule.Voice) {
                if (FindTable(her, packet.State) is { } table) NPCSpeakingAudio.PlayVoice(table, her.transform.position);
                return;
            }

            var fsm = fsms.FirstOrDefault(f => f.FsmName == packet.Fsm);
            if (fsm == null) return;
            if (GreetingRule.Ends(packet.Fsm, packet.State)) {
                // she rests again as in the walker's game, and her own Idle starts her song (its Resting, which her
                // END ANIM put off; set straight to the walker's state, she cancelled it once: green run 155007's
                // Sing to Idle), unless her own Rest Animation moved on
                if (fsm.ActiveStateName != "Idle") return;
                if (fsm.FsmVariables.GetFsmBool("Resting") is { } resting) resting.Value = true;
                else fsm.SetState(packet.State);
                return;
            }

            var state = fsm.FsmStates.FirstOrDefault(s => s.Name == packet.State);
            if (state == null) return;
            Debug.Log($"[BossRule] Greeting in another game: {her.name} plays {packet.Fsm}'s {packet.State} ({packet.Scene})");
            foreach (var action in state.Actions) Play(fsm, her, action);
        } catch (System.Exception e) {
            Debug.LogWarning($"[BossRule] Another game's greeting did not play: {e.GetBaseException().Message}");
        }
    }

    private static void Play(PlayMakerFSM fsm, GameObject her, FsmStateAction action) {
        var type = action.GetType();
        var owner = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(FsmOwnerDefault))
            .Select(f => f.GetValue(action) as FsmOwnerDefault).FirstOrDefault(o => o != null);
        var atOwner = owner?.OwnerOption == OwnerDefaultOption.UseOwner;
        var target = owner != null ? fsm.Fsm.GetOwnerDefaultTarget(owner) : null;
        var sendEvent = (type.GetField("sendEvent")?.GetValue(action) as FsmString)?.Value;
        switch (GreetingRule.Of(type.Name, atOwner, target != null ? target.name : null, sendEvent)) {
            case GreetingRule.Play.Clip:
                var clip = (type.GetField("clipName")?.GetValue(action) as FsmString)?.Value;
                var animator = her.GetComponent<tk2dSpriteAnimator>();
                if (animator != null && !string.IsNullOrEmpty(clip) && animator.GetClipByName(clip) != null) animator.Play(clip);
                break;
            case GreetingRule.Play.Sound:
            case GreetingRule.Play.Event:
                action.OnEnter();  // as its state's entry runs it: its own clip, place, pitch and volume; or her END ANIM
                break;
            case GreetingRule.Play.Activate:
                target!.SetActive(true);
                break;
        }
    }

    // her default voice, or the table a line of hers overrode it with (her first line's battle voice)
    private static RandomAudioClipTable? FindTable(GameObject her, string name) {
        var speaking = her.GetComponent<NPCSpeakingAudio>();
        if (speaking != null && speaking.Table != null && speaking.Table.name == name) return speaking.Table;
        if (Tables.TryGetValue(name, out var known) && known != null) return known;
        var found = Resources.FindObjectsOfTypeAll<RandomAudioClipTable>().FirstOrDefault(t => t.name == name);
        if (found != null) Tables[name] = found;
        return found;
    }
}
