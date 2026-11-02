using System.IO;
using BepInEx;
using HarmonyLib;
using HutongGames.PlayMaker;
using SSMP.Api.Client;
using SSMP.Api.Server;

namespace BossRule;

/// <summary>
/// Bosses wait until every player is in the arena; a player who dies in a boss fight watches until it is won or
/// everyone is down. Registers BossRule's SSMP client and server add-ons, which every player must have.
/// </summary>
[BepInPlugin("bossrule", "BossRule", BossRulePlugin.Version)]
[BepInDependency("ssmp")]
public class BossRulePlugin : BaseUnityPlugin {
    /// <summary>
    /// The co-op zip's version (make_release.py builds no other), sent as BossRule's add-on version at login: SSMP's
    /// server refuses a friend whose version differs, since synced values travel as positions in a list built into
    /// the mod.
    /// </summary>
    internal const string Version = "0.3.17";

    private BossRuleClientAddon? _client;

    private void Awake() {
        var folder = Path.GetDirectoryName(Info.Location)!;
        var catalog = Path.Combine(folder, "bosses.json");
        var bosses = File.Exists(catalog) ? BossCatalog.Load(File.ReadAllText(catalog)) : [];
        var cutsceneCatalog = Path.Combine(folder, "cutscenes.json");
        var cutscenes = File.Exists(cutsceneCatalog) ? CutsceneCatalog.Load(File.ReadAllText(cutsceneCatalog)) : [];
        _client = new BossRuleClientAddon(bosses, cutscenes);
        ClientAddon.RegisterAddon(_client);
        ServerAddon.RegisterAddon(new BossRuleServerAddon());
        Marks.Init(Config, Logger, folder);

        var harmony = new Harmony("bossrule");
        harmony.Patch(AccessTools.Method(typeof(Fsm), nameof(Fsm.ProcessEvent), [typeof(FsmEvent), typeof(FsmEventData)]),
            prefix: new HarmonyMethod(typeof(StartGate), nameof(StartGate.ProcessEventPrefix)));
        harmony.Patch(AccessTools.Method(typeof(BattleScene), nameof(BattleScene.StartBattle)),
            prefix: new HarmonyMethod(typeof(StartGate), nameof(StartGate.StartBattlePrefix)));
        harmony.Patch(AccessTools.Method(typeof(BattleScene), nameof(BattleScene.DoEndBattle)),
            prefix: new HarmonyMethod(typeof(ArenaWins), nameof(ArenaWins.EndPrefix)));
        harmony.Patch(AccessTools.Method(typeof(HutongGames.PlayMaker.Actions.Trigger2dEvent),
                nameof(HutongGames.PlayMaker.Actions.Trigger2dEvent.DoTriggerStay2D)),
            prefix: new HarmonyMethod(typeof(GroundBells), nameof(GroundBells.TriggerStayPrefix)));
        harmony.Patch(AccessTools.Method(typeof(HutongGames.PlayMaker.Actions.SendEventByName),
                nameof(HutongGames.PlayMaker.Actions.SendEventByName.OnEnter)),
            postfix: new HarmonyMethod(typeof(MultiHits), nameof(MultiHits.SentPostfix)));
        harmony.Patch(AccessTools.Method(typeof(BellShrineGateLock), nameof(BellShrineGateLock.StartedPlaying)),
            postfix: new HarmonyMethod(typeof(DoorBells), nameof(DoorBells.StartedPostfix)));
        harmony.Patch(AccessTools.Method(typeof(BellShrineGateLock), nameof(BellShrineGateLock.StoppedPlaying)),
            postfix: new HarmonyMethod(typeof(DoorBells), nameof(DoorBells.StoppedPostfix)));
        harmony.Patch(AccessTools.Method(typeof(HeroController), "Die", [typeof(bool), typeof(bool)]),
            prefix: new HarmonyMethod(typeof(Spectate), nameof(Spectate.DiePrefix)));
        // The game heads for the main menu (the player's quit, or SSMP after a lost connection): a watcher does not die
        harmony.Patch(AccessTools.Method(typeof(UIManager), nameof(UIManager.ReturnToMainMenu)),
            prefix: new HarmonyMethod(typeof(Spectate), nameof(Spectate.LeavingPrefix)));
        harmony.Patch(AccessTools.Method(typeof(GameManager), nameof(GameManager.ReturnToMainMenu)),
            prefix: new HarmonyMethod(typeof(Spectate), nameof(Spectate.LeavingPrefix)));
        // SSMP's two copy dispatchers: a mark's record has the last 10 s of copied actions
        var ssmp = typeof(ClientAddon).Assembly;
        var dispatchers = ssmp.GetType("SSMP.Game.Client.Entity.Action.EntityFsmActions", true);
        var data = ssmp.GetType("SSMP.Networking.Packet.Data.EntityNetworkData", true);
        harmony.Patch(AccessTools.Method(dispatchers, "GetNetworkDataFromAction", [data, typeof(FsmStateAction)]),
            postfix: new HarmonyMethod(typeof(Marks), nameof(Marks.SentPostfix)));
        harmony.Patch(AccessTools.Method(dispatchers, "ApplyNetworkDataFromAction", [data, typeof(FsmStateAction)]),
            prefix: new HarmonyMethod(typeof(Marks), nameof(Marks.AppliedPrefix)));

        Logger.LogInfo($"BossRule loaded, {bosses.Count} bosses and {cutscenes.Count} cutscenes in the catalogs");
    }

    private void Update() {
        _client?.Tick();
        Marks.Tick(_client?.Connected == true);
    }
}
