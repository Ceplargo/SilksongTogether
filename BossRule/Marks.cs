using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using BepInEx.Logging;
using HutongGames.PlayMaker;
using SSMP.Api.Client;
using UnityEngine;

namespace BossRule;

/// <summary>
/// The mark key: on a friend night a player marks a moment that looks wrong, and every connected game writes what it
/// shows then (a screenshot and a record) under the same mark, in BossRule\marks\&lt;id&gt;\ in its game folder.
/// </summary>
internal static class Marks {
    /// <summary>
    /// The copied actions of the last 10 s, filled by the patches on SSMP's two copy dispatchers.
    /// </summary>
    private static readonly RecentActions Recent = new();

    private static ConfigEntry<KeyboardShortcut>? _key;
    private static ManualLogSource? _log;
    private static string _folder = "";
    private static IClientApi? _api;
    private static Action<Mark>? _send;
    private static float _lastPress = -10f;
    private static float _lastReceived = -10f;
    private static readonly MarkBudget Written = new();
    private static bool _toldFull;

    public static void Init(ConfigFile config, ManualLogSource log, string pluginFolder) {
        _key = config.Bind("Mark", "Key", new KeyboardShortcut(KeyCode.F9),
            "Marks this moment in every player's game: a screenshot and a record in BepInEx\\plugins\\BossRule\\marks");
        _log = log;
        _folder = Path.Combine(pluginFolder, "marks");
    }

    public static void Connect(IClientApi api, Action<Mark> send) {
        _api = api;
        _send = send;
    }

    /// <summary>
    /// Once per frame, from the plugin: a press of the key while connected marks this moment.
    /// </summary>
    public static void Tick(bool connected) {
        if (!connected || _api == null || _key == null || !_key.Value.IsDown()) return;
        // a second press within 1 s is the same moment
        if (Time.realtimeSinceStartup - _lastPress < 1f) return;
        _lastPress = Time.realtimeSinceStartup;

        var presser = _api.ClientManager.Username;
        var id = MarkRecord.Id(DateTime.Now, presser);
        Write(id, presser);
        SendsMark(new Mark { Id = id, By = presser });
        _api.UiManager.ChatBox.AddMessage($"Marked {id}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // a check's twin switches it off with a patch
    public static void SendsMark(Mark mark) => _send?.Invoke(mark);

    /// <summary>
    /// Another player's mark, from the server: its id and name come from another game, so are made safe as names here,
    /// and a flood of them is one moment.
    /// </summary>
    public static void Received(Mark mark) {
        if (_api == null || Time.realtimeSinceStartup - _lastReceived < 1f) return;
        _lastReceived = Time.realtimeSinceStartup;
        if (!Written.Take()) {
            if (!_toldFull) _log?.LogWarning($"[MARK] {MarkBudget.Max} other players' marks written this session: no more");
            _toldFull = true;
            return;
        }

        var id = MarkRecord.Safe(mark.Id);
        if (id.Length > 0) Write(id, MarkRecord.Safe(mark.By));
    }

    /// <summary>
    /// SSMP's copy dispatcher made data of an action this game runs (EntityFsmActions.GetNetworkDataFromAction).
    /// </summary>
    public static void SentPostfix(FsmStateAction action, bool __result) {
        if (__result) Add("sent", action);
    }

    /// <summary>
    /// SSMP's copy dispatcher applies an action on a copy here (EntityFsmActions.ApplyNetworkDataFromAction).
    /// </summary>
    public static void AppliedPrefix(FsmStateAction action) => Add("applied", action);

    private static void Add(string way, FsmStateAction action) =>
        Recent.Add(Time.realtimeSinceStartup, way, action.Owner != null ? action.Owner.name : "", action.Fsm?.Name ?? "",
            action.State?.Name ?? "", action.GetType().Name);

    private static void Write(string id, string by) {
        var scene = GameManager.SilentInstance != null ? GameManager.SilentInstance.sceneName : null;
        var player = _api!.ClientManager.Username;
        var now = Time.realtimeSinceStartup;
        try {
            var dir = Path.Combine(_folder, id);
            Directory.CreateDirectory(dir);
            var file = MarkRecord.Safe(player) is { Length: > 0 } safe ? safe : "player";
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, file + ".png"));
            File.WriteAllText(Path.Combine(dir, file + ".json"),
                MarkRecord.Json(id, by, player, DateTimeOffset.Now, scene, Players(scene), Enemies(), Recent.Since(now), now));
            _log!.LogInfo($"[MARK] {id} by {by} in {scene}");
        } catch (Exception e) {
            // a mark must not break the night (a full disk, a locked folder)
            _log!.LogWarning($"[MARK] {id} not written: {e.Message}");
        }
    }

    /// <summary>
    /// This hero and SSMP's other players: those outside this room without a scene or position.
    /// </summary>
    private static List<MarkPlayer> Players(string? scene) {
        var players = new List<MarkPlayer>();
        if (HeroController.SilentInstance != null) {
            var p = HeroController.SilentInstance.transform.position;
            players.Add(new MarkPlayer(_api!.ClientManager.Username, scene, p.x, p.y));
        }

        foreach (var other in _api!.ClientManager.Players) {
            var container = other.IsInLocalScene ? other.PlayerContainer : null;
            players.Add(container != null
                ? new MarkPlayer(other.Username, scene, container.transform.position.x, container.transform.position.y)
                : new MarkPlayer(other.Username, null, null, null));
        }

        return players;
    }

    /// <summary>
    /// Each active enemy in the loaded scenes, with its FSMs' active states.
    /// </summary>
    private static List<MarkEnemy> Enemies() =>
        UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).Select(h => {
            var fsms = new Dictionary<string, string?>();
            foreach (var fsm in h.GetComponents<PlayMakerFSM>()) fsms.TryAdd(fsm.FsmName, fsm.ActiveStateName);
            var p = h.transform.position;
            return new MarkEnemy(PathOf(h.transform), h.hp, p.x, p.y, fsms);
        }).ToList();

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
