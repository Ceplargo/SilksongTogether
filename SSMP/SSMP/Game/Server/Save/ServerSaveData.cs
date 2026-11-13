using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SSMP.Game.Client.Save;
using SSMP.Game.Client.Save.Complex;
using SSMP.Logging;
using SSMP.Networking.Packet.Data;
using SSMP.Util;

namespace SSMP.Game.Server.Save;

/// <summary>
/// Class that holds save data from a server. This consists of global data relating to the world and individual
/// data specific to each player. This class is only used for storing the save data while the server is running;
/// serialization of this data to the save file is done with <see cref="ModSaveFile"/>.
/// </summary>
internal class ServerSaveData {
    /// <summary>
    /// Name of the variable in PlayerData that denotes a Steel Soul save file.
    /// </summary>
    private const string SteelSoulVarName = "permadeathMode";

    /// <summary>
    /// The index that corresponds with the Steel Soul variable.
    /// </summary>
    private static readonly ushort SteelSoulIndex;

    /// <summary>
    /// Player values that a player joining for the first time does not copy from the host: their rosaries, shell
    /// shards and death cocoon start empty.
    /// </summary>
    private static readonly Dictionary<string, object> FirstJoinResetValues = new() {
        ["geo"] = 0,
        ["ShellShards"] = 0,
        ["HeroCorpseMoneyPool"] = 0,
        ["HeroCorpseScene"] = ""
    };

    /// <summary>
    /// The encoded values of <see cref="FirstJoinResetValues"/>, mapped by save data index.
    /// </summary>
    private static readonly Dictionary<ushort, byte[]> FirstJoinResets = new();

    /// <summary>The name of the item container whose delivery items are each carrier's own (DeliveryCarrier).</summary>
    public const string Collectables = "Collectables";

    /// <summary>The save data index of <see cref="Collectables"/>, or null when it is not mapped.</summary>
    public static readonly ushort? CollectablesIndex;

    /// <summary>
    /// The global save data for the server. E.g. broken walls, open doors, etc.
    /// </summary>
    public Dictionary<ushort, byte[]> GlobalSaveData { get; set; } = new();

    /// <summary>
    /// The player specific save data mapped to player's auth keys.
    /// </summary>
    public Dictionary<string, Dictionary<ushort, byte[]>> PlayerSaveData { get; set; } = new();

    /// <summary>
    /// The auth key of the player hosting this save from their game, or null on a standalone server.
    /// </summary>
    public string? HostAuthKey { get; set; }

    /// <summary>
    /// Static constructor for initializing the indices for the Steel Soul variable.
    /// </summary>
    static ServerSaveData() {
        if (!SaveDataMapping.Instance.PlayerDataIndices.TryGetValue(SteelSoulVarName, out SteelSoulIndex)) {
            Logger.Warn("Could not find index for steel soul variable");
        }

        if (SaveDataMapping.Instance.PlayerDataIndices.TryGetValue(Collectables, out var collectablesIndex)) {
            CollectablesIndex = collectablesIndex;
        }

        foreach (var (name, value) in FirstJoinResetValues) {
            if (SaveDataMapping.Instance.PlayerDataIndices.TryGetValue(name, out var index)) {
                FirstJoinResets[index] = EncodeUtil.EncodeSaveDataValue(value, name);
            } else {
                Logger.Warn($"Could not find index for first join reset variable: {name}");
            }
        }
    }

    /// <summary>
    /// Get save data that contains global save data and player specific save data for the player with the given auth
    /// key. 
    /// </summary>
    /// <param name="authKey">The auth key that corresponds to the player for the player specific data.</param>
    /// <returns>A dictionary mapping save data indices to byte encoded values.</returns>
    public CurrentSave GetCurrentSaveData(string authKey) {
        var currentSave = new CurrentSave();

        if (!PlayerSaveData.TryGetValue(authKey, out var playerSaveData)) {
            if (HostAuthKey != null && PlayerSaveData.TryGetValue(HostAuthKey, out var hostSaveData)) {
                // Continue the host's game: a new game would start this player at the opening scene, away from
                // everyone else
                playerSaveData = FirstJoinSave.FromHost(hostSaveData, FirstJoinResets);
                // (coop, 0.3.22) the host's delivery items are the host's: a new player carries none
                if (CarriedApart(out var index)) {
                    playerSaveData.Remove(index);
                }

                PlayerSaveData[authKey] = playerSaveData;

                Logger.Debug("No save data for player yet, starting from a copy of the host's");
            } else {
                playerSaveData = new Dictionary<ushort, byte[]>();
                currentSave.NewForPlayer = true;

                Logger.Debug("No save data for player yet, marking in CurrentSave");
            }
        }

        var saveData = new Dictionary<ushort, byte[]>(GlobalSaveData);
        foreach (var data in playerSaveData) {
            saveData[data.Key] = data.Value;
        }

        // (coop, 0.3.22) the shared Collectables plus the player's own delivery items, every other one at 0
        if (CarriedApart(out var collectables)) {
            saveData[collectables] = JoinedCollectables(playerSaveData);
        }

        currentSave.SaveData = saveData;

        return currentSave;
    }

    /// <summary>
    /// (coop, 0.3.22) Whether the Collectables are shared (kept in the global save data), so each player's own delivery
    /// items are kept apart in that player's store under the same index.
    /// </summary>
    public bool CarriedApart(out ushort index) {
        index = CollectablesIndex ?? 0;
        return CollectablesIndex != null && GlobalSaveData.ContainsKey(index);
    }

    /// <summary>
    /// (coop, 0.3.22) The Collectables a player's game is given (a join, or a refused change put back): the shared
    /// container plus the player's own entries, every other own entry at 0. Only while <see cref="CarriedApart"/>.
    /// </summary>
    public byte[] JoinedCollectables(Dictionary<ushort, byte[]> playerSaveData) {
        var index = CollectablesIndex!.Value;
        try {
            return Encode(DeliveryCarrier.JoinSnapshot(Stored(GlobalSaveData, index), Stored(playerSaveData, index),
                DeliveryCarrier.OwnEntries(), ComplexFieldSpecs.All[Collectables]));
        } catch (Exception e) when (e is InvalidDataException or ArgumentOutOfRangeException) {
            Logger.Warn($"Could not add the player's own entries to the Collectables: {e.Message}");
            return GlobalSaveData[index];
        }
    }

    /// <summary>The snapshot stored under an item container's index, or null when none is stored.</summary>
    public static byte[]? Stored(Dictionary<ushort, byte[]> store, ushort index) =>
        store.TryGetValue(index, out var value) &&
        EncodeUtil.DecodeSaveDataValue(Collectables, value) is byte[] { Length: > 0 } snapshot
            ? snapshot
            : null;

    /// <summary>A Collectables snapshot encoded as a save value.</summary>
    public static byte[] Encode(byte[] snapshot) => EncodeUtil.EncodeSaveDataValue(snapshot, Collectables);

    /// <summary>
    /// (coop, 0.3.22) At host start: the host's delivery items, loaded with its save into the shared Collectables, go
    /// to the host's own store; the items left uncarried at the last save go from the other players' stores.
    /// </summary>
    /// <param name="uncarried">The items the mod save names as left uncarried, or null.</param>
    public void SeparateCarried(ICollection<string>? uncarried) {
        var own = DeliveryCarrier.OwnEntries();
        if (own.Count == 0 || CollectablesIndex is not { } index) {
            return;
        }

        var spec = ComplexFieldSpecs.All[Collectables];
        try {
            if (HostAuthKey != null && Stored(GlobalSaveData, index) is { } loaded) {
                var (shared, hostOwn) = DeliveryCarrier.Separate(loaded, own, spec);
                GlobalSaveData[index] = Encode(shared);
                PlayerSaveData[HostAuthKey][index] = Encode(hostOwn);
            }

            if (uncarried is { Count: > 0 }) {
                foreach (var (key, store) in PlayerSaveData) {
                    if (key != HostAuthKey && store.ContainsKey(index)) {
                        store[index] = Encode(DeliveryCarrier.Cleared(Stored(store, index), uncarried, spec));
                    }
                }
            }
        } catch (Exception e) when (e is InvalidDataException or ArgumentOutOfRangeException) {
            Logger.Warn($"Could not keep the host's delivery items apart: {e.Message}");
        }
    }

    /// <summary>
    /// (coop, 0.3.22) The delivery items held by a player who is not in the game, at a save of the host's: their
    /// deliveries reset at the next host start (a missed night).
    /// </summary>
    /// <param name="inGame">Whether a player's auth key is in the game.</param>
    public List<string> Uncarried(Func<string, bool> inGame) {
        // only the delivery items: a rosary set an away player holds stays theirs
        var own = DeliveryCarrier.CarriedItems();
        if (own.Count == 0 || CollectablesIndex is not { } index) {
            return [];
        }

        try {
            return DeliveryCarrier.Uncarried(
                PlayerSaveData.Where(p => p.Key != HostAuthKey && Stored(p.Value, index) != null)
                    .Select(p => new KeyValuePair<string, byte[]>(p.Key, Stored(p.Value, index)!)),
                inGame
            ).Where(own.Contains).ToList();
        } catch (Exception e) when (e is InvalidDataException or ArgumentOutOfRangeException) {
            Logger.Warn($"Could not read the delivery items left uncarried: {e.Message}");
            return [];
        }
    }

    /// <summary>
    /// (coop, 0.3.22) A player's own delivery items as forwarded to the other games ("item@player"), encoded as a
    /// Collectables update, or null when there are none.
    /// </summary>
    public byte[]? CarrierTags(string authKey, string username) {
        if (CollectablesIndex is not { } index || !PlayerSaveData.TryGetValue(authKey, out var store)) {
            return null;
        }

        var tags = DeliveryCarrier.Tags(Stored(store, index), username);
        return tags.Count == 0 ? null : Encode(ProjectionOps.Encode(false, tags));
    }

    /// <summary>
    /// Whether the global save data in this instance is for Steel Soul mode.
    /// </summary>
    /// <returns>True if the save data is for Steel Soul mode, false otherwise.</returns>
    public bool IsSteelSoul() {
        if (!GlobalSaveData.TryGetValue(SteelSoulIndex, out var value)) {
            return false;
        }

        return value.Length > 0 && value[0] != 0;
    }
}
