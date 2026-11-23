using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BossRule;

/// <summary>
/// (coop) An ordinary wave arena (a BattleScene in a room the boss catalog does not list) starts and moves on to each wave
/// in every game in its room as it does in the game that runs the room. Each game ran its own: in a game that does not
/// run the room the arena started only as its own hero walked in, so with the friend outside or late its gates never
/// shut there, and its waves counted SSMP's hidden originals of the enemies, so they stopped (Bone_01's waves 3 and 4,
/// Dock_08's 6 and 7, Room_Forge's 5, both ways round; the Act 1 sweep's arena rows, 0.3.21 Part C). The scene host's
/// game sends each arena's start and each wave as it starts (and the wave it is on every second while it runs, for a
/// player who comes into the room later); a following game starts its own arena (its gates shut, its music) and each
/// wave the host's reached, and its own count no longer moves its waves on. Its end stays ArenaWins'.
/// </summary>
internal static class ArenaWaves {
    /// <summary>A follow message's state that is an arena's wave: "Arena Wave: ", the arena's path, "|", the wave.</summary>
    public const string Prefix = "Arena Wave: ";

    private static readonly MethodInfo StartWave = AccessTools.Method(typeof(BattleScene), "StartWave");

    /// <summary>The scene host's wave of each arena in this room (a following game).</summary>
    private static readonly Dictionary<string, int> HostWave = new();

    /// <summary>The last wave this game started for each arena, its start being wave 0 (a following game).</summary>
    private static readonly Dictionary<string, int> Kicked = new();

    /// <summary>The waves this game started while it followed the room (their BATTLE START reached no live enemy).</summary>
    private static readonly HashSet<BattleWave> StartedFollowing = new();

    /// <summary>The wave children's FSMs given their BATTLE START again since the takeover.</summary>
    private static readonly HashSet<PlayMakerFSM> WokenAgain = new();

    /// <summary>Until when the takeover's wave children are looked at (WakeLost).</summary>
    private static float _wakeUntil = -1f;

    private static string? _room;

    /// <summary>This game followed the room's arenas (it did not run the room) since it came into it.</summary>
    private static bool _followed;
    private static float _nextSend, _nextLook;

    /// <summary>
    /// Prefix on <c>BattleScene.StartBattle</c>: the scene host's game sends the arena's start (wave 0), unless an earlier
    /// prefix held it (ArenaHold: an ordinary arena waits up to WaitRule.RoomWaitS for every hero in its room; World B,
    /// 2026-11-20).
    /// </summary>
    public static void StartBattlePrefix(BattleScene __instance, bool __runOriginal) {
        if (__runOriginal) SendWave(__instance, 0);
    }

    /// <summary>Prefix on <c>BattleScene.StartWave</c>: the scene host's game sends each wave as it starts.</summary>
    public static void StartWavePrefix(BattleScene __instance, int waveNumber) {
        // (0.3.21, print only: arena_waves' lag failure) what started each wave, in either game; StartWave is a
        // coroutine, so this runs as it is made, before its start delay sets currentWave
        var from = new System.Diagnostics.StackTrace(1, false).GetFrames()?.Skip(1).Take(5)
            .Select(f => f.GetMethod()?.Name).Where(n => n != null);
        Debug.Log($"[BossRule] Arena wave made: {PathOf(__instance.transform)} {waveNumber} (current {__instance.currentWave}, " +
                  $"from {string.Join(" < ", from ?? Enumerable.Empty<string?>())})");
        SendWave(__instance, waveNumber);
    }

    /// <summary>
    /// Prefix on <c>BattleScene.WaveEnd</c>: in a following game the arena's own count does not move its waves on (it
    /// counts SSMP's hidden originals); the scene host's waves do.
    /// </summary>
    public static bool WaveEndPrefix(BattleScene __instance) => !Following(__instance);

    /// <summary>The scene host's arena at path in room started that wave: this game's follows at the next look.</summary>
    public static void Received(string room, string text) {
        if (ArenaWave.Parse(text) is not var (path, wave)) return;
        NewRoom(room);
        if (!HostWave.TryGetValue(path, out var had) || wave > had) HostWave[path] = wave;
        _nextLook = 0f;
    }

    /// <summary>
    /// Once per frame: the scene host's game sends its running arenas' waves every second; a following game's arenas take
    /// their steps every 0.2 s.
    /// </summary>
    public static void Tick() {
        var room = GameManager.SilentInstance?.sceneName;
        NewRoom(room);
        if (room == null || ArenaWins.CatalogRoom?.Invoke(room) != false) return;
        var sceneHost = ArenaWins.SceneHost?.Invoke() == true;
        if (ArenaWave.TakesOver(_followed, sceneHost)) {
            _followed = false;
            ClearGone();
            _wakeUntil = Time.unscaledTime + WakeS;
            WokenAgain.Clear();
        } else if (ArenaWins.NotSceneHost?.Invoke() == true) {
            _followed = true;
        }

        if (sceneHost && Time.unscaledTime < _wakeUntil) WakeLost();

        if (sceneHost && Time.unscaledTime >= _nextSend) {
            _nextSend = Time.unscaledTime + 1f;
            foreach (var battle in Object.FindObjectsByType<BattleScene>(FindObjectsSortMode.None)) {
                if (battle.isActiveAndEnabled && StartGate.BattleStarted(battle) && !StartGate.BattleCompleted(battle)) {
                    SendWave(battle, battle.currentWave);
                }
            }
        }

        if (ArenaWins.NotSceneHost?.Invoke() != true || Time.unscaledTime < _nextLook || !Follows()) return;
        _nextLook = Time.unscaledTime + 0.2f;
        foreach (var battle in Object.FindObjectsByType<BattleScene>(FindObjectsSortMode.None)) {
            var path = PathOf(battle.transform);
            if (!battle.isActiveAndEnabled || !HostWave.TryGetValue(path, out var hostWave)) continue;
            var started = StartGate.BattleStarted(battle);
            var kicked = Math.Max(Kicked.TryGetValue(path, out var k) ? k : -1, started ? battle.currentWave : -1);
            var step = ArenaWave.For(started, StartGate.BattleCompleted(battle), kicked, hostWave);
            if (step == WaveStep.Start) {
                Kicked[path] = 0;
                battle.StartBattle();
            } else if (step == WaveStep.Wave) {
                Kicked[path] = kicked + 1;
                battle.StartCoroutine((IEnumerator) StartWave.Invoke(battle, [kicked + 1]));
            }

            if (step != WaveStep.None) Debug.Log($"[BossRule] The arena {path} follows the room's: {step} {Kicked[path]}");
        }
    }

    /// <summary>
    /// The switch the twin turns off (a skipped bool method returns false): a following game's arena runs on its own, as
    /// before 0.3.21.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Follows() => true;

    /// <summary>
    /// The forced twin's switch (Ant_05b's lead rejoin): off, an arena taken over keeps the originals of the enemies the
    /// room's runner killed in its waves, and never ends.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ClearsGone() => true;

    /// <summary>
    /// (Ant_05b's lead rejoin; the project's ruling, 2026-11-20) This game has just taken the room over from the
    /// game that ran it (its player quit). While it followed, the enemies the runner killed died here only as SSMP's
    /// copies: their originals, hp 0 (SSMP sets it at the copy's death) and switched off at the takeover, stay children
    /// of their waves, and an arena counts its waves' children to end a wave (BattleScene.DoCheckEnemiesNew). So with the
    /// runner gone mid-wave, the wave never ended and its players stayed shut in (the old build too, held to wave 1: 3 of
    /// 3, b-w1-old-*-p4.log). Each such original leaves its wave, as a death in the game destroys the enemy, and each
    /// running arena counts again.
    /// </summary>
    private static void ClearGone() {
        if (!ClearsGone()) return;
        foreach (var battle in Object.FindObjectsByType<BattleScene>(FindObjectsSortMode.None)) {
            if (!battle.isActiveAndEnabled || !StartGate.BattleStarted(battle) || StartGate.BattleCompleted(battle)) continue;
            var gone = new List<string>();
            foreach (var wave in battle.waves.Where(w => w != null)) {
                foreach (var child in wave.transform.Cast<Transform>().ToList()) {
                    var hm = child.GetComponent<HealthManager>();
                    if (!ArenaWave.Gone(hm != null ? hm.hp : null)) continue;
                    child.SetParent(null, true);
                    gone.Add(child.name);
                }
            }

            Debug.Log($"[BossRule] Took over the arena {PathOf(battle.transform)} at wave {battle.currentWave}: the runner's " +
                      $"kills out of its waves ({string.Join(", ", gone)})");
            if (gone.Count > 0) battle.CheckEnemies();
        }
    }

    /// <summary>
    /// Postfix on <c>BattleWave.WaveStarted</c>: a wave started while this game follows the room is kept (WakeLost). Its
    /// BATTLE START went to SSMP's originals, switched off here, and was lost.
    /// </summary>
    public static void WaveStartedPostfix(BattleWave __instance) {
        if (ArenaWins.NotSceneHost?.Invoke() != true) return;
        StartedFollowing.Add(__instance);
        Debug.Log($"[BossRule] A wave started while following: {PathOf(__instance.transform)}");
    }

    /// <summary>How long after the takeover a wave child that comes into its waiting state still gets its start (its
    /// original's FSM may start from its first state as it is switched on: Init Pause waits a frame).</summary>
    private const float WakeS = 1f;

    /// <summary>
    /// The switch the twin turns off (a skipped bool method returns false): a taken-over wave's children that lost their
    /// start stay waiting.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool WakesLostStarts() => true;

    /// <summary>
    /// (Dock_08's lead rejoin, 0.4) The room's runner quit in its arena's first wave's start delay, before its own start
    /// woke the wave's Dock Worker; this game, still following, started that wave itself and its BATTLE START went to
    /// SSMP's switched-off original, so after the takeover the Dock Worker stayed in Battle Dormant (no renderer, no hurt
    /// box) and its wave never ended (World B's arena pass; probe_wave_takeover: BATTLE START sent by hand woke it). Each
    /// FSM of a child of a wave started while following whose state still waits for BATTLE START gets it, once, as
    /// BattleWave.WaveStarted sends it.
    /// </summary>
    private static void WakeLost() {
        if (!WakesLostStarts()) return;
        foreach (var wave in StartedFollowing) {
            if (wave == null) continue;
            foreach (Transform child in wave.transform) {
                foreach (var fsm in child.GetComponents<PlayMakerFSM>()) {
                    var events = fsm.Fsm.ActiveState?.Transitions.Select(t => t.EventName) ?? Enumerable.Empty<string>();
                    if (WokenAgain.Contains(fsm) || !ArenaWave.WakesAgain(true, events)) continue;
                    WokenAgain.Add(fsm);
                    var was = fsm.ActiveStateName;
                    fsm.SendEvent(ArenaWave.BattleStart);
                    Debug.Log($"[BossRule] Took over the wave {PathOf(wave.transform)}: its BATTLE START sent again to " +
                              $"{child.name} {fsm.FsmName} (in {was}, now {fsm.ActiveStateName}; lost while following)");
                }
            }
        }
    }

    private static bool Following(BattleScene battle) {
        var room = GameManager.SilentInstance?.sceneName;
        return room != null && ArenaWins.NotSceneHost?.Invoke() == true && ArenaWins.CatalogRoom?.Invoke(room) == false &&
               Follows() && HostWave.ContainsKey(PathOf(battle.transform));
    }

    private static void SendWave(BattleScene battle, int wave) {
        var room = GameManager.SilentInstance?.sceneName;
        if (room == null || ArenaWins.SceneHost?.Invoke() != true || ArenaWins.CatalogRoom?.Invoke(room) != false) return;
        try {
            ArenaWins.Send?.Invoke(room, Prefix + ArenaWave.Message(PathOf(battle.transform), wave));
        } catch (InvalidOperationException e) {
            // SSMP's connection closed before its disconnect reached this frame (as ArenaWins)
            Debug.Log($"[BossRule] An arena's wave was not sent: {e.Message}");
        }
    }

    private static void NewRoom(string? room) {
        if (room == _room) return;
        _room = room;
        _followed = false;
        HostWave.Clear();
        Kicked.Clear();
        StartedFollowing.Clear();
        WokenAgain.Clear();
        _wakeUntil = -1f;
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
