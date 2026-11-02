using System;
using System.Collections;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BossRule;

/// <summary>
/// The Last Judge's door bells shake in every game in the room: they shake by <see cref="BellShrineGateLock"/>'s code,
/// which her arena's Start Gate state calls in the game whose player plays the Needolin at her gate, so they shook in
/// the scene host's game only (seen in play, 2026-10-27). That game sends the start and the stop of its bells
/// (through the arena follow message); the others shake their own bells as the lock's routine does (each rung bell's
/// timed and looping jitter, after its delays) and stop them, without the lock's events: their arenas follow the scene
/// host's (<see cref="ArenaFollower"/>).
/// </summary>
internal static class DoorBells {
    public const string Start = "Door Bells Start";
    public const string Stop = "Door Bells Stop";

    /// <summary>Sends a bells state for this room's fight to the room's other players; set by the client add-on.</summary>
    public static Action<string>? Send;

    private static Coroutine? _shaking;

    /// <summary>Harmony postfix of BellShrineGateLock.StartedPlaying: this game's bells start; the others' too.</summary>
    public static void StartedPostfix() => Send?.Invoke(Start);

    /// <summary>Harmony postfix of BellShrineGateLock.StoppedPlaying.</summary>
    public static void StoppedPostfix() => Send?.Invoke(Stop);

    /// <summary>Another game's bells started or stopped: this game's shake or stop the same.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void Echo(string state) {
        if (Object.FindFirstObjectByType<BellShrineGateLock>() is not { } gate) return;

        if (_shaking != null) {
            gate.StopCoroutine(_shaking);
            _shaking = null;
        }

        if (state == Start) {
            _shaking = gate.StartCoroutine(Shake(gate));
        } else {
            Traverse.Create(gate).Method("EndAll").GetValue();
        }
    }

    /// <summary>As the lock's PlayRoutine, the jitters only: each rung bell in turn, then their end once all rang.</summary>
    private static IEnumerator Shake(BellShrineGateLock gate) {
        var self = Traverse.Create(gate);
        yield return new WaitForSeconds(self.Field("startDelay").GetValue<float>());

        var bellDelay = self.Field("bellDelay").GetValue<float>();
        var bells = self.Field("bellLocks").GetValue<Array>();
        var rung = 0;
        for (var i = 0; i < bells.Length; i++) {
            if (i > 0) yield return new WaitForSeconds(bellDelay);

            var bell = Traverse.Create(bells.GetValue(i));
            if (!PlayerData.instance.GetBool(bell.Field("PdBool").GetValue<string>())) continue;

            bell.Field("ActivateJitter").GetValue<JitterSelfForTime>().StartTimedJitter();
            bell.Field("LoopJitter").GetValue<JitterSelf>().StartJitter();
            rung++;
        }

        if (rung < bells.Length) yield return new WaitForSeconds(bellDelay);
        else yield return new WaitForSeconds(self.Field("endDelay").GetValue<float>());

        self.Method("EndAll").GetValue();
        _shaking = null;
    }
}
