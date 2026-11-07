using System.Collections.Generic;
using System.Linq;
using SSMP.Api.Server;
using SSMP.Api.Server.Networking;

namespace BossRule;

/// <summary>
/// BossRule's part in the host's server: passes a boss room's waiting line to everyone in that room, and keeps each
/// room's boss fight (who fights, who watches) so it survives a scene host change.
/// </summary>
internal class BossRuleServerAddon : ServerAddon {
    private readonly FightRooms _fights = new();
    private IServerApi? _api;
    private IServerAddonNetworkSender<BossPacketId>? _sender;

    protected override string Name => "BossRule";

    protected override string Version => BossRulePlugin.Version;

    public override bool NeedsNetwork => true;

    public override uint ApiVersion => 1;

    public override void Initialize(IServerApi serverApi) {
        _api = serverApi;
        _sender = serverApi.NetServer.GetNetworkSender<BossPacketId>(this);
        var receiver = serverApi.NetServer.GetNetworkReceiver<BossPacketId>(this, BossPacket.Create);
        receiver.RegisterPacketHandler<WaitStatus>(BossPacketId.WaitStatus, (_, packet) => {
            var inRoom = InRoom(packet.Scene);
            if (inRoom.Length > 0) _sender.SendSingleData(BossPacketId.WaitStatus, packet, inRoom);
        });
        receiver.RegisterPacketHandler<FightStarted>(BossPacketId.FightStarted, OnFightStarted);
        receiver.RegisterPacketHandler<BossReady>(BossPacketId.BossReady, (id, packet) => {
            var others = InRoom(packet.Scene).Where(p => p != id).ToArray();
            if (others.Length > 0) {
                _sender.SendSingleData(BossPacketId.BossReady, new BossReady { Scene = packet.Scene, Player = id }, others);
            }
        });
        receiver.RegisterPacketHandler<Downed>(BossPacketId.Downed, (id, packet) => Tell(packet.Scene, _fights.Downed(packet.Scene, id)));
        receiver.RegisterPacketHandler<BossDefeated>(BossPacketId.BossDefeated,
            (_, packet) => Tell(packet.Scene, _fights.BossDefeated(packet.Scene)));
        receiver.RegisterPacketHandler<ArenaFollow>(BossPacketId.ArenaFollow, (id, packet) => {
            var others = InRoom(packet.Scene).Where(p => p != id).ToArray();
            if (others.Length > 0) _sender.SendSingleData(BossPacketId.ArenaFollow, packet, others);
        });
        // SSMP hands a collection's items to the handler one by one
        receiver.RegisterPacketHandler<CutsceneState>(BossPacketId.CutsceneState, (id, state) => {
            var others = InRoom(state.Scene).Where(p => p != id).ToArray();
            if (others.Length > 0) _sender.SendCollectionData(BossPacketId.CutsceneState, state, others);
        });
        // A mark goes to every other connected player, at most MarkBudget.Max of one player's a session
        var marks = new MarkBudget();
        receiver.RegisterPacketHandler<Mark>(BossPacketId.Mark, (id, packet) => {
            var others = serverApi.ServerManager.Players.Where(p => p.Id != id).Select(p => p.Id).ToArray();
            if (others.Length > 0 && marks.Take(id)) _sender.SendSingleData(BossPacketId.Mark, packet, others);
        });
        serverApi.ServerManager.PlayerDisconnectEvent += player => marks.Forget(player.Id);

        // A player who leaves the room or the game stops fighting. SSMP does not report every room change as a leave,
        // so entering another room counts too
        serverApi.ServerManager.PlayerLeaveSceneEvent += player => Left(player.Id);
        serverApi.ServerManager.PlayerEnterSceneEvent += player => Left(player.Id, player.CurrentScene);
        serverApi.ServerManager.PlayerDisconnectEvent += player => Left(player.Id);

        Logger.Info("BossRule server add-on registered");
    }

    private void OnFightStarted(ushort id, FightStarted packet) {
        var fighters = packet.Fighters.ToList();
        if (packet.SenderFights) fighters.Add(id);
        // A fight already on in that room stays as it is (a new scene host's game may report its start again)
        if (!_fights.Start(packet.Scene, packet.Flag, fighters)) {
            Logger.Info($"A start in {packet.Scene} came while its fight was on; the fight stays as it is");
            return;
        }

        // Each player in the room learns the fighters and their own id
        foreach (var player in InRoom(packet.Scene)) {
            _sender!.SendSingleData(BossPacketId.FightStarted, new FightStarted {
                Scene = packet.Scene, Flag = packet.Flag, Fighters = fighters.ToArray(), Self = player
            }, player);
        }
    }

    /// <summary>
    /// A player left the fights in every room but <paramref name="stillIn"/>.
    /// </summary>
    private void Left(ushort id, string? stillIn = null) {
        foreach (var (scene, outcome) in _fights.Left(id, stillIn)) Tell(scene, outcome);
    }

    /// <summary>
    /// Send an outcome to the players it names and to everyone in its room (they hide or show the watchers).
    /// </summary>
    private void Tell(string scene, FightOutcome? outcome) {
        if (outcome == null) return;

        var connected = _api!.ServerManager.Players.Select(p => p.Id).ToHashSet();
        var to = InRoom(scene).Union(outcome.Players).Where(connected.Contains).ToArray();
        if (to.Length == 0) return;

        _sender!.SendSingleData(BossPacketId.Outcome, new Outcome {
            Scene = scene, Kind = (byte) outcome.Kind, Players = outcome.Players.ToArray()
        }, to);
    }

    private ushort[] InRoom(string scene) =>
        _api!.ServerManager.Players.Where(p => p.CurrentScene == scene).Select(p => p.Id).ToArray();
}
