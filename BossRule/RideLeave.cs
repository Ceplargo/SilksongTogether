using System.Linq;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace BossRule;

/// <summary>
/// (0.3.21) What a game whose player stayed shows of a ride another game's player took, state by state as the rider's
/// game tells them (cutscenes.json's <c>leave_at</c>): each state's register events (the flea music and the troupe's
/// sounds stop, the caravan's travel start) and its sound; at a leave state the ride's object goes (its own Disable state
/// turns the caravan's children off; here the caravan itself goes off, which its FSM's log shows). Its FSM is not set to those states: they fade this game's screen, take its
/// hero and change its room (the caravan's Fade Down, Fade Up and Change Scene).
/// </summary>
internal static class RideLeave {
    public static void Told(PlayMakerFSM fsm, string state, bool leaves) {
        var told = fsm.FsmStates.FirstOrDefault(s => s.Name == state);
        foreach (var action in told?.Actions ?? []) {
            switch (action) {
                case SendEventToRegister send:
                    EventRegister.SendEvent(send.eventName.Value);
                    break;
                case { } play when play.GetType().Name == "PlayAudioEvent":
                    var clip = (Traverse.Create(play).Field("audioClip").GetValue() as FsmObject)?.Value as AudioClip;
                    if (clip) new AudioEvent { Clip = clip, PitchMin = 1f, PitchMax = 1f, Volume = 1f }
                        .SpawnAndPlayOneShot(fsm.transform.position);
                    break;
            }
        }

        if (!leaves) return;
        fsm.gameObject.SetActive(false);
    }
}
