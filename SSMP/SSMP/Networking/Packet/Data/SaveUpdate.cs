using System;
using System.Collections.Generic;
using System.Linq;

namespace SSMP.Networking.Packet.Data;

/// <summary>
/// Packet data for when values in the save update.
/// </summary>
internal class SaveUpdate : IPacketData {
    /// <inheritdoc />
    public bool IsReliable => true;

    /// <inheritdoc />
    public bool DropReliableDataIfNewerExists => false;
    
    /// <summary>
    /// The index of the save data entry that got updated.
    /// </summary>
    public ushort SaveDataIndex { get; set; }

    /// <summary>
    /// The encoded value of the save data in a byte array.
    /// </summary>
    public byte[] Value { get; set; } = null!;

    /// <summary>
    /// The sending game's number for this update, so the server applies each one once (0 from the server).
    /// </summary>
    public ulong Number { get; set; }

    /// <summary>
    /// (coop) The one-of-a-kind action this update belongs to, or "" (decision "The lock"). From the server, a marked
    /// update answers this player's refused change of that action with the server's value.
    /// </summary>
    public string Claim { get; set; } = "";

    /// <inheritdoc />
    public void WriteData(IPacket packet) {
        packet.Write(SaveDataIndex);
        packet.Write(Number);
        packet.Write(Claim);

        if (Value.Length > ushort.MaxValue) {
            throw new Exception($"Number of bytes exceeds ushort max value: {Value.Length}");
        }

        var length = (ushort) Value.Length;
        packet.Write(length);
        for (var i = 0; i < length; i++) {
            packet.Write(Value[i]);
        }
    }

    /// <inheritdoc />
    public void ReadData(IPacket packet) {
        SaveDataIndex = packet.ReadUShort();
        Number = packet.ReadULong();
        Claim = packet.ReadString();

        var length = packet.ReadUShort();
        Value = new byte[length];
        for (var i = 0; i < length; i++) {
            Value[i] = packet.ReadByte();
        }
    }
}

/// <summary>
/// Packet data for when an entire save is networked.
/// </summary>
/// <summary>
/// The save updates a joining player's game sent to this server run before its connection dropped, sent with its
/// join so the server can apply those it never got before it makes the join's save data.
/// </summary>
internal class HeldSaveData : IPacketData {
    /// <inheritdoc />
    public bool IsReliable => true;

    /// <inheritdoc />
    public bool DropReliableDataIfNewerExists => false;

    /// <summary>
    /// The server run the updates were sent to.
    /// </summary>
    public ulong RunId { get; set; }

    /// <summary>
    /// The updates, oldest first.
    /// </summary>
    public List<SaveUpdate> Updates { get; set; } = [];

    /// <inheritdoc />
    public void WriteData(IPacket packet) {
        packet.Write(RunId);
        packet.Write((ushort) System.Math.Min(Updates.Count, ushort.MaxValue));
        foreach (var update in Updates.Take(ushort.MaxValue)) {
            update.WriteData(packet);
        }
    }

    /// <inheritdoc />
    public void ReadData(IPacket packet) {
        RunId = packet.ReadULong();
        var count = packet.ReadUShort();
        Updates = new List<SaveUpdate>(count);
        for (var i = 0; i < count; i++) {
            var update = new SaveUpdate();
            update.ReadData(packet);
            Updates.Add(update);
        }
    }
}

internal class CurrentSave : IPacketData {
    /// <inheritdoc />
    public bool IsReliable => true;

    /// <inheritdoc />
    public bool DropReliableDataIfNewerExists => false;

    /// <summary>
    /// Whether this save is new file the player. As in, there is no player-specific save data in the save yet.
    /// </summary>
    public bool NewForPlayer { get; set; }

    /// <summary>
    /// Dictionary mapping save data indices to encoded values in byte array form.
    /// </summary>
    public Dictionary<ushort, byte[]> SaveData { get; set; }

    public CurrentSave() {
        SaveData = new Dictionary<ushort, byte[]>();
    }

    /// <inheritdoc />
    public void WriteData(IPacket packet) {
        packet.Write(NewForPlayer);

        WriteSaveDataDict(SaveData, packet);
    }

    /// <inheritdoc />
    public void ReadData(IPacket packet) {
        NewForPlayer = packet.ReadBool();

        SaveData = ReadSaveDataDict(packet);
    }

    /// <summary>
    /// Writes a save data dictionary to the given packet.
    /// </summary>
    /// <param name="dataDict">The dictionary mapping indices to byte encoded values to write.</param>
    /// <param name="packet">The packet to write the data into.</param>
    /// <exception cref="Exception">Thrown if the number of keys in the given save data dictionary is too large to
    /// be written (> max ushort).</exception>
    public static void WriteSaveDataDict(Dictionary<ushort, byte[]> dataDict, IPacket packet) {
        var saveDataKeyCount = dataDict.Keys.Count;
        if (saveDataKeyCount > ushort.MaxValue) {
            throw new Exception("Number of keys in save data is too large");
        }

        var dataLength = (ushort) saveDataKeyCount;

        packet.Write(dataLength);

        foreach (var keyValuePair in dataDict) {
            var saveDataIndex = keyValuePair.Key;
            var value = keyValuePair.Value;
            
            packet.Write(saveDataIndex);

            var length = (ushort) System.Math.Min(value.Length, ushort.MaxValue);
            packet.Write(length);
            for (var i = 0; i < length; i++) {
                packet.Write(value[i]);
            }
        }
    }

    /// <summary>
    /// Reads a save data dictionary that maps indices to byte encoded values from the given
    /// packet.
    /// </summary>
    /// <param name="packet">The packet interface to read from.</param>
    /// <returns>A dictionary mapping save data indices to byte encoded values.</returns>
    public static Dictionary<ushort, byte[]> ReadSaveDataDict(IPacket packet) {
        var saveData = new Dictionary<ushort, byte[]>();
        var dataLength = packet.ReadUShort();

        for (var i = 0; i < dataLength; i++) {
            var saveDataIndex = packet.ReadUShort();

            var length = packet.ReadUShort();
            var value = new byte[length];
            for (var j = 0; j < length; j++) {
                value[j] = packet.ReadByte();
            }

            saveData.Add(saveDataIndex, value);
        }

        return saveData;
    }
}
