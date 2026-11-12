using SSMP.Networking.Packet;
using SSMP.Networking.Packet.Data;

namespace BossRule;

/// <summary>
/// BossRule's packets between the players' games and the server.
/// </summary>
internal enum BossPacketId {
    WaitStatus,
    FightStarted,
    Downed,
    BossDefeated,
    Outcome,
    ArenaFollow,
    Mark,
    CutsceneState,
    BossReady,
    Dialogue,
    DropTaken,
    TollCoins
}

internal abstract class BossPacket : IPacketData {
    public bool IsReliable => true;

    public virtual bool DropReliableDataIfNewerExists => false;

    public abstract void WriteData(IPacket packet);

    public abstract void ReadData(IPacket packet);

    public static IPacketData Create(BossPacketId id) => id switch {
        BossPacketId.WaitStatus => new WaitStatus(),
        BossPacketId.FightStarted => new FightStarted(),
        BossPacketId.Downed => new Downed(),
        BossPacketId.BossDefeated => new BossDefeated(),
        // (0.3.21) a collection: two words in one update (a faller's two platforms' resets) both arrive; as single data
        // the second replaced the first (crumble_reset, 2026-11-10_183308)
        BossPacketId.ArenaFollow => new PacketDataCollection<ArenaFollow>(),
        BossPacketId.Mark => new Mark(),
        BossPacketId.CutsceneState => new PacketDataCollection<CutsceneState>(),
        BossPacketId.BossReady => new BossReady(),
        BossPacketId.Dialogue => new PacketDataCollection<DialogueMessage>(),
        BossPacketId.DropTaken => new DropTaken(),
        BossPacketId.TollCoins => new TollCoins(),
        _ => new Outcome()
    };

    protected static void WriteIds(IPacket packet, ushort[] ids) {
        packet.Write((byte) ids.Length);
        foreach (var id in ids) packet.Write(id);
    }

    protected static ushort[] ReadIds(IPacket packet) {
        var ids = new ushort[packet.ReadByte()];
        for (var i = 0; i < ids.Length; i++) ids[i] = packet.ReadUShort();
        return ids;
    }
}

/// <summary>
/// The scene host's waiting line for a boss room (empty once nobody is missing), shown to the players in that room.
/// </summary>
internal class WaitStatus : BossPacket {
    public string Scene = "";
    public string Message = "";

    // Only the newest line matters
    public override bool DropReliableDataIfNewerExists => true;

    public override void WriteData(IPacket packet) {
        packet.Write(Scene);
        packet.Write(Message);
    }

    public override void ReadData(IPacket packet) {
        Scene = packet.ReadString();
        Message = packet.ReadString();
    }
}

/// <summary>
/// A listed boss fight started in a room, with the players in its arena at that moment. From the scene host:
/// the other players in the arena, and whether the sender is in it. From the server, to each player in the room: all
/// fighters, and the receiver's own id (SSMP's client API does not tell a player their id).
/// </summary>
internal class FightStarted : BossPacket {
    public string Scene = "";
    public string Flag = "";
    public ushort[] Fighters = [];
    public bool SenderFights;
    public ushort Self;

    public override void WriteData(IPacket packet) {
        packet.Write(Scene);
        packet.Write(Flag);
        WriteIds(packet, Fighters);
        packet.Write(SenderFights);
        packet.Write(Self);
    }

    public override void ReadData(IPacket packet) {
        Scene = packet.ReadString();
        Flag = packet.ReadString();
        Fighters = ReadIds(packet);
        SenderFights = packet.ReadBool();
        Self = packet.ReadUShort();
    }
}

/// <summary>
/// (coop) A player's game has a listed boss room's boss: its start gate is armed and every body is there. From a game:
/// the room; from the server, to the other players in the room: with the sender's id. The game that runs the boss holds
/// the fight's start for it (StartGate, ReadyWait).
/// </summary>
internal class BossReady : BossPacket {
    public string Scene = "";
    public ushort Player;

    public override void WriteData(IPacket packet) {
        packet.Write(Scene);
        packet.Write(Player);
    }

    public override void ReadData(IPacket packet) {
        Scene = packet.ReadString();
        Player = packet.ReadUShort();
    }
}

/// <summary>
/// The sending player went down in the boss fight of a room.
/// </summary>
internal class Downed : BossPacket {
    public string Scene = "";

    public override void WriteData(IPacket packet) => packet.Write(Scene);

    public override void ReadData(IPacket packet) => Scene = packet.ReadString();
}

/// <summary>
/// The boss of a room was beaten (sent by that room's scene host).
/// </summary>
internal class BossDefeated : BossPacket {
    public string Scene = "";

    public override void WriteData(IPacket packet) => packet.Write(Scene);

    public override void ReadData(IPacket packet) => Scene = packet.ReadString();
}

/// <summary>
/// What happens to players of a boss fight: watch, come back (won) or die normally (wiped); Kind is an OutcomeKind.
/// </summary>
internal class Outcome : BossPacket {
    public string Scene = "";
    public byte Kind;
    public ushort[] Players = [];

    public override void WriteData(IPacket packet) {
        packet.Write(Scene);
        packet.Write(Kind);
        WriteIds(packet, Players);
    }

    public override void ReadData(IPacket packet) {
        Scene = packet.ReadString();
        Kind = packet.ReadByte();
        Players = ReadIds(packet);
    }
}

/// <summary>
/// The scene host's arena reached the catalog's follow state of a fight (its intro runs there only); the server passes
/// it to the other players in the room.
/// </summary>
internal class ArenaFollow : BossPacket {
    public string Scene = "";
    public string Flag = "";
    public string State = "";

    public override void WriteData(IPacket packet) {
        packet.Write(Scene);
        packet.Write(Flag);
        packet.Write(State);
    }

    public override void ReadData(IPacket packet) {
        Scene = packet.ReadString();
        Flag = packet.ReadString();
        State = packet.ReadString();
    }
}

/// <summary>
/// A room's cutscene (by its catalog name) entered a state in its leader's game, the seq-th that run told: from that game
/// to the server, and from the server to the room's other players. Sent as collection data: as single data a second state
/// in the same send overwrote the first (a shrine's End and Set Finished come in one frame).
/// </summary>
internal class CutsceneState : BossPacket {
    public string Scene = "";
    public string Name = "";
    public string State = "";
    public ushort Seq;

    public override void WriteData(IPacket packet) {
        packet.Write(Scene);
        packet.Write(Name);
        packet.Write(State);
        packet.Write(Seq);
    }

    public override void ReadData(IPacket packet) {
        Scene = packet.ReadString();
        Name = packet.ReadString();
        State = packet.ReadString();
        Seq = packet.ReadUShort();
    }
}

/// <summary>
/// A player marked a moment (the mark key): from the presser's game to the server, and from the server to every other
/// connected player, whose game writes its own record under the same id.
/// </summary>
internal class Mark : BossPacket {
    public string Id = "";
    public string By = "";

    public override void WriteData(IPacket packet) {
        packet.Write(Id);
        packet.Write(By);
    }

    public override void ReadData(IPacket packet) {
        Id = packet.ReadString();
        By = packet.ReadString();
    }
}

/// <summary>
/// (coop) A page of a friend's talk, or a quest they took or finished (DialogueShare's encoding): from the talker's game to
/// the server, and from the server to every other connected player. Sent as collection data: as single data a second page
/// in the same send overwrote the first.
/// </summary>
internal class DialogueMessage : BossPacket {
    public string Body = "";

    public override void WriteData(IPacket packet) => packet.Write(Body);

    public override void ReadData(IPacket packet) => Body = packet.ReadString();
}

/// <summary>
/// (0.3.21) A take of an enemy's item drop (<see cref="DropShare"/>): from the taker's game to the server; from the server
/// to every other connected player when it is the drop's first take, or back to the taker (Lost) when another game's
/// came first.
/// </summary>
internal class DropTaken : BossPacket {
    public string Scene = "";
    public string Id = "";
    public bool Lost;

    public override void WriteData(IPacket packet) {
        packet.Write(Scene);
        packet.Write(Id);
        packet.Write(Lost);
    }

    public override void ReadData(IPacket packet) {
        Scene = packet.ReadString();
        Id = packet.ReadString();
        Lost = packet.ReadBool();
    }
}

/// <summary>
/// (0.3.21) A toll machine's payment sound (<see cref="TollFollow"/>): the payer's scene and the state of its machine's
/// payment sub-FSM that played it, from the payer's game to the server and on to every other connected player.
/// </summary>
internal class TollCoins : BossPacket {
    public string Scene = "";
    public string State = "";

    public override void WriteData(IPacket packet) {
        packet.Write(Scene);
        packet.Write(State);
    }

    public override void ReadData(IPacket packet) {
        Scene = packet.ReadString();
        State = packet.ReadString();
    }
}
