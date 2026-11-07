using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BossRule;

/// <summary>
/// (0.3.20) A boss a player's performance wakes (<see cref="PerformWakes"/>): the Skull King waits in his Behaviour's
/// State Check until a hero in his Wake Range plays the Needolin near him (CheckHeroPerformanceRegionV2, radius 4). That
/// FSM runs only in the game that runs him (whoever came in first); in the other game he is SSMP's copy, so the other
/// player's Needolin woke nothing (0.3.20's probe_intro_pose, Task 21). Every game whose own hero performs where he hears
/// it tells the room; the game that runs him, if he still waits, sends him his own WAKE (BossRule's wait rule still holds
/// it until every player is in his range).
/// </summary>
internal static class PerformWake {
    public const string Prefix = "Perform Wake:";

    /// <summary>Sends a room state to the room's other players; set by the client add-on.</summary>
    public static Action<string, string>? Send;

    private static float _lastSent = -999f;

    /// <summary>Every frame: whether this game's own hero performs where this room's boss hears it.</summary>
    public static void Tick() {
        var scene = GameManager.SilentInstance?.sceneName;
        if (PerformWakes.In(scene) is not { } wake || !HeroPerformanceRegion.IsPerforming) return;
        var boss = Body(wake.Boss);
        if (boss == null) return;
        var range = AlertRange.Find(boss, wake.Range);
        var inRange = range != null && range.IsHeroInRange() &&
                      HeroPerformanceRegion.GetAffectedStateWithRadius(boss.transform, wake.Radius) !=
                      HeroPerformanceRegion.AffectedState.None;
        if (!PerformWakes.Sends(true, inRange, Time.unscaledTime, _lastSent)) return;
        _lastSent = Time.unscaledTime;
        Debug.Log($"[BossRule] Performed where {wake.Boss} hears it: the room told ({scene})");
        Send?.Invoke(scene!, Prefix + wake.Boss);
    }

    /// <summary>Another game's hero performed where this room's boss hears it: this game wakes him if it runs him and he
    /// still waits.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void Echo(string bossName) {
        if (PerformWakes.In(GameManager.SilentInstance?.sceneName) is not { } wake || wake.Boss != bossName) return;
        var fsm = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)
            .FirstOrDefault(f => f.gameObject.name == wake.Boss && f.FsmName == wake.Fsm);
        if (!PerformWakes.Echoes(fsm != null && fsm.enabled && fsm.gameObject.activeInHierarchy, fsm?.ActiveStateName,
                wake.Waits)) return;
        Debug.Log($"[BossRule] Woken by another player's performance: {wake.Boss} {wake.Event}");
        fsm!.SendEvent(wake.Event);
    }

    /// <summary>The boss this game shows: its own (it runs him) or SSMP's copy.</summary>
    private static GameObject? Body(string name) => GameObject.Find(name) ?? GameObject.Find(name + "(Clone)");
}
