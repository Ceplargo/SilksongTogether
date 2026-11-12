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

    private static string? _room;
    private static float _nextSend, _nextLook;

    /// <summary>Prefix on <c>BattleScene.StartBattle</c>: the scene host's game sends the arena's start (wave 0).</summary>
    public static void StartBattlePrefix(BattleScene __instance) => SendWave(__instance, 0);

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
        if (ArenaWins.SceneHost?.Invoke() == true && Time.unscaledTime >= _nextSend) {
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
        HostWave.Clear();
        Kicked.Clear();
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
