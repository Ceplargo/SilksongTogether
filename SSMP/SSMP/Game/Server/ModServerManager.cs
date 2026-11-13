using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SSMP.Game.Command.Server;
using SSMP.Game.Settings;
using SSMP.Networking.Packet;
using SSMP.Networking.Server;
using SSMP.Networking.Transport.Common;
using SSMP.Networking.Transport.HolePunch;
using SSMP.Networking.Transport.SteamP2P;
using SSMP.Networking.Transport.UDP;
using SSMP.Game.Server.Save;
using SSMP.Game.Client.Save;
using SSMP.Hooks;
using SSMP.Ui;
using SSMP.Util;

namespace SSMP.Game.Server;

/// <summary>
/// Specialization of <see cref="ServerManager"/> that adds handlers for the mod specific things.
/// </summary>
internal class ModServerManager : ServerManager {
    /// <summary>
    /// The UiManager instance for registering events for starting and stopping a server.
    /// </summary>
    private readonly UiManager _uiManager;

    /// <summary>
    /// The mod settings instance for retrieving the auth key of the local player to set player save data when
    /// hosting a server.
    /// </summary>
    private readonly ModSettings _modSettings;

    /// <summary>
    /// The settings command.
    /// </summary>
    private readonly SettingsCommand _settingsCommand;


    /// <summary>
    /// The NetServer instance to check whether the server is started.
    /// </summary>
    private readonly NetServer _netServer;

    /// <summary>
    /// When the host's game last saved, by the unscaled clock (any save: a bench too).
    /// </summary>
    private double _lastSave;

    /// <summary>
    /// When the autosave next looks whether it is due.
    /// </summary>
    private double _nextAutoSaveCheck;

    /// <summary>
    /// Whether a friend left since the host's game last saved (set on the network thread at a timeout).
    /// </summary>
    private volatile bool _friendLeft;

    public ModServerManager(
        NetServer netServer,
        PacketManager packetManager,
        ServerSettings serverSettings,
        UiManager uiManager,
        ModSettings modSettings
    ) : base(netServer, packetManager, serverSettings) {
        _netServer = netServer;
        _uiManager = uiManager;
        _modSettings = modSettings;
        _settingsCommand = new SettingsCommand(this, InternalServerSettings);
    }

    /// <inheritdoc />
    public override void Initialize() {
        base.Initialize();

        // Start addon loading, since all addons that are also mods should be registered during the Awake phase of
        // their MonoBehaviour
        AddonManager.LoadAddons();

        // Register handlers for UI events
        _uiManager.RequestServerStartHostEvent += (_, port, _, transportType, _) =>
            OnRequestServerStartHost(port, _modSettings.FullSynchronisation, transportType);
        _uiManager.RequestServerStopHostEvent += Stop;
        PlayerConnectEvent += _ => UpdateMatchmakingRemotePlayerCount();
        PlayerDisconnectEvent += _ => UpdateMatchmakingRemotePlayerCount();
        ServerShutdownEvent += () => UpdateMatchmakingRemotePlayerCount(0);

        EventHooks.GameManagerSaveGame += OnGameSave;
        MonoBehaviourUtil.Instance.OnUpdateEvent += AutoSaveIfDue;
        PlayerDisconnectEvent += player => {
            if (player.AuthKey != _modSettings.AuthKey) {
                _friendLeft = true;
            }
        };

        // Register application quit handler
        // ModHooks.ApplicationQuitHook += Stop;
    }

    /// <summary>
    /// Callback method for when the UI requests the server to be started as a host.
    /// </summary>
    /// <param name="port">The port to start the server on.</param>
    /// <param name="fullSynchronisation">Whether full synchronisation is enabled.</param>
    /// <param name="transportType">The type of transport to use.</param>
    private void OnRequestServerStartHost(int port, bool fullSynchronisation, TransportType transportType) {
        ItemSharingLocked = false;
        _lastSave = UnityEngine.Time.unscaledTimeAsDouble;
        _friendLeft = false;

        if (fullSynchronisation) {
            var profileId = global::GameManager.instance.profileID;
            var modSavePath = Path.Combine(FileUtil.GetConfigPath(), $"user{profileId}.modsav");

            var modSaveFile = LoadModSave(modSavePath);

            // A mod save of an earlier game in this slot: the host's save has less play time than it recorded
            if (modSaveFile != null &&
                ModSaveLifecycle.ShouldArchive(modSaveFile.HostPlayTime, PlayerData.instance.playTime)) {
                MoveAside(modSavePath, "old");
                modSaveFile = null;
            }

            // The save keeps the item sharing value it was first hosted with; it must be known before the save data
            // is split into shared and per-player values
            if (modSaveFile?.ItemSharing is { } savedItemSharing) {
                InternalServerSettings.ItemSharing = savedItemSharing;
            }

            ItemSharingLocked = true;
            Logging.Logger.Info($"Item sharing is {(InternalServerSettings.ItemSharing ? "on" : "off")} for this save");

            // (coop, 0.3.22) deliveries with no carrier on record, or whose carrier was away at the last save, start
            // over, before the loaded save is shared
            lock (DeliveryResetLines) {
                DeliveryResetLines.Clear();
                try {
                    DeliveryResetLines.AddRange(DeliveryCarrier.ResetLoaded(
                        modSaveFile != null, modSaveFile?.Carriers == true, modSaveFile?.Uncarried ?? []
                    ));
                } catch (Exception e) {
                    Logging.Logger.Error($"Could not reset the deliveries left uncarried: {e}");
                }
            }

            // Get the global save data from the save manager, which obtains the global save data from the loaded
            // save file that the user selected
            ServerSaveData.GlobalSaveData = SaveManager.GetCurrentSaveData(true, InternalServerSettings.ItemSharing);

            // Load remote players' player-specific data from disk for the current profile ID
            if (modSaveFile != null) {
                var serverSave = modSaveFile.ToServerSaveData();
                ServerSaveData.PlayerSaveData = serverSave.PlayerSaveData;
                ServerSaveData.GlobalSaveData = ModSaveLifecycle.MergeGlobalSaveData(
                    ServerSaveData.GlobalSaveData,
                    serverSave.GlobalSaveData,
                    index => SemiPersistence.AsAlone() &&
                             (SaveDataMapping.Instance.PersistentBoolIndices.ContainsSecond(index) ||
                              SaveDataMapping.Instance.PersistentIntIndices.ContainsSecond(index))
                );

                Logging.Logger.Info($"Loaded remote players' save data from: {modSavePath}");
            } else {
                ServerSaveData.PlayerSaveData = new Dictionary<string, Dictionary<ushort, byte[]>>();
                Logging.Logger.Info(
                    $"No remote player save file found at: {modSavePath}, initialized empty player save data."
                );
            }

            // Lastly, we get the player save data from the save manager, which obtains the player save data from the
            // loaded save file that the user selected. We add this data to the server save as the local player
            ServerSaveData.PlayerSaveData[_modSettings.AuthKey!] =
                SaveManager.GetCurrentSaveData(false, InternalServerSettings.ItemSharing);
            ServerSaveData.HostAuthKey = _modSettings.AuthKey;
            ServerSaveData.SeparateCarried(modSaveFile?.Uncarried);

            if (modSaveFile?.ItemSharing == null) {
                // Fix the item sharing value for this save right away
                try {
                    WriteModSave(profileId);
                } catch (Exception e) {
                    Logging.Logger.Error($"Could not write the mod save for slot {profileId}: {e}");
                }
            }
        }

        IEncryptedTransportServer transportServer = transportType switch {
            TransportType.Udp => new UdpEncryptedTransportServer(),
            TransportType.Steam => new SteamEncryptedTransportServer(),
            TransportType.HolePunch => CreateHolePunchServer(),
            _ => throw new ArgumentOutOfRangeException(nameof(transportType), transportType, null)
        };

        Start(port, fullSynchronisation, transportServer);
        UpdateMatchmakingRemotePlayerCount();
    }

    /// <summary>
    /// Creates a HolePunch server with the MmsClient for lobby cleanup on shutdown.
    /// </summary>
    private HolePunchEncryptedTransportServer CreateHolePunchServer() {
        return new HolePunchEncryptedTransportServer(_uiManager.ConnectInterface.MmsClient);
    }

    /// <inheritdoc />
    protected override void RegisterCommands() {
        base.RegisterCommands();

        CommandManager.RegisterCommand(_settingsCommand);
    }

    /// <inheritdoc />
    protected override void DeregisterCommands() {
        base.DeregisterCommands();

        CommandManager.DeregisterCommand(_settingsCommand);

        EventHooks.GameManagerSaveGame -= OnGameSave;
    }

    /// <summary>
    /// Pushes the current remote-player count to MMS heartbeat state.
    /// </summary>
    /// <param name="count">The number of players to set in the update, or -1 if the number needs to be retrieved
    /// from the server.</param>
    private void UpdateMatchmakingRemotePlayerCount(int count = -1) {
        if (count != -1) {
            _uiManager.ConnectInterface.MmsClient.SetConnectedPlayers(count);
            return;
        }

        var hostAuthKey = _modSettings.AuthKey;
        var remotePlayerCount = hostAuthKey == null
            ? 0
            : Players.Count(player => player.AuthKey != hostAuthKey);
        _uiManager.ConnectInterface.MmsClient.SetConnectedPlayers(remotePlayerCount);
    }

    /// <summary>
    /// Intercepts native save events to serialize remote player-specific save data to disk.
    /// </summary>
    /// <param name="saveSlot">The save slot index.</param>
    private void OnGameSave(int saveSlot) {
        if (!_netServer.IsStarted || !FullSynchronisation) {
            return;
        }

        _lastSave = UnityEngine.Time.unscaledTimeAsDouble;
        _friendLeft = false;

        try {
            Logging.Logger.Info($"Intercepted native save for slot {saveSlot}. Saving remote players' save data...");

            WriteModSave(saveSlot);
        } catch (Exception e) {
            Logging.Logger.Error($"Could not save remote players' save data to disk: {e}");
        }
    }

    /// <summary>
    /// While friends are in, save the host's game every few minutes and when a friend leaves, with the game's own save
    /// (as quitting does): its save hook writes the friends' save data in the same moment, so after a crash the world
    /// and everyone's things come back in step (upstream review #5: friends' progress reached disk only at the host's
    /// bench). Only while the host's hero is in control (not dead, watching a fight, in a cutscene, between rooms or
    /// paused); otherwise it waits. Looks once a second.
    /// </summary>
    private void AutoSaveIfDue() {
        var now = UnityEngine.Time.unscaledTimeAsDouble;
        if (now < _nextAutoSaveCheck || !_netServer.IsStarted || !FullSynchronisation) {
            return;
        }

        _nextAutoSaveCheck = now + 1;
        var gm = global::GameManager.instance;
        var hero = HeroController.SilentInstance;
        var inControl = gm != null && gm.GameState == GlobalEnums.GameState.PLAYING && !gm.IsInSceneTransition &&
                        !gm.isPaused && hero != null && hero.acceptingInput && !hero.controlReqlinquished &&
                        !hero.cState.dead && !hero.cState.transitioning && !hero.cState.hazardRespawning &&
                        PlayerData.instance.health > 0;
        var hostAuthKey = _modSettings.AuthKey;
        var friends = Players.Count(player => player.AuthKey != hostAuthKey);
        if (!CoopAutoSave.Due(now, _lastSave, friends, _friendLeft, inControl)) {
            return;
        }

        Logging.Logger.Info($"Saving the host's game with the friends' save data ({friends} in, a friend left: {_friendLeft})");
        _lastSave = now;
        _friendLeft = false;
        gm!.SaveGame(null);
    }

    /// <summary>
    /// Write the remote players' save data and the save's item sharing value to the slot's mod save.
    /// </summary>
    /// <param name="saveSlot">The save slot index.</param>
    private void WriteModSave(int saveSlot) {
        // Create a copy of ServerSaveData for serialization; network updates write it on another thread
        ModSaveFile modSaveFile;
        var inGame = new HashSet<string>(Players.Select(player => player.AuthKey));
        lock (ServerSaveData) {
            modSaveFile = ModSaveFile.FromServerSaveData(ServerSaveData);
            // (coop, 0.3.22) carriers are kept; the items of players not in the game reset at the next start
            modSaveFile.Carriers = true;
            modSaveFile.Uncarried = ServerSaveData.Uncarried(inGame.Contains);
        }

        modSaveFile.ItemSharing = InternalServerSettings.ItemSharing;
        modSaveFile.HostPlayTime = PlayerData.instance.playTime;

        // Filter out the host player's auth key to avoid duplicate/redundant data in the remote players' file
        var hostAuthKey = _modSettings.AuthKey;
        if (hostAuthKey != null) {
            modSaveFile.PlayerSaveData.Remove(hostAuthKey);
        }

        var configPath = FileUtil.GetConfigPath();
        if (!Directory.Exists(configPath)) {
            Directory.CreateDirectory(configPath);
        }

        var modSavePath = Path.Combine(configPath, $"user{saveSlot}.modsav");
        var tmpPath = modSavePath + ".tmp";
        File.WriteAllText(tmpPath, JsonConvert.SerializeObject(modSaveFile, Formatting.Indented));

        // Swap the new file in, so a crash while writing never leaves a half-written mod save behind
        if (File.Exists(modSavePath)) {
            File.Replace(tmpPath, modSavePath, null);
        } else {
            File.Move(tmpPath, modSavePath);
        }

        Logging.Logger.Info($"Remote players' save data successfully written to {modSavePath}");
    }

    /// <summary>
    /// Read the slot's mod save. A file that cannot be read is moved aside, so writing a new one never destroys it.
    /// </summary>
    /// <param name="modSavePath">The path of the mod save.</param>
    /// <returns>The mod save, or null if there is none or it could not be read.</returns>
    private static ModSaveFile? LoadModSave(string modSavePath) {
        if (!File.Exists(modSavePath)) {
            return null;
        }

        try {
            var modSaveFile = JsonConvert.DeserializeObject<ModSaveFile>(File.ReadAllText(modSavePath));
            if (modSaveFile != null) {
                return modSaveFile;
            }
        } catch (Exception e) {
            Logging.Logger.Error($"Could not load remote players' save data: {e}");
        }

        MoveAside(modSavePath, "unreadable");
        return null;
    }

    /// <summary>
    /// Rename a mod save to "[path].[reason]-[yyyyMMddHHmmss]", never over an earlier one.
    /// </summary>
    private static void MoveAside(string modSavePath, string reason) {
        var newPath = ModSaveLifecycle.ArchivePath(modSavePath, reason, DateTime.Now, File.Exists);
        File.Move(modSavePath, newPath);
        Logging.Logger.Info($"Moved mod save {modSavePath} to {newPath}");
    }
}
