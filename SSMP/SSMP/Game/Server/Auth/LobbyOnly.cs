using System.IO;
using Newtonsoft.Json;
using SSMP.Util;

namespace SSMP.Game.Server.Auth;

/// <summary>
/// The host's /lobbyonly setting (0.4.2): whether the Steam host accepts a P2P session only from a member of its
/// lobby. On by default; saved beside the whitelist, in lobbyonly.json. Off means every request is accepted, as before.
/// </summary>
internal static class LobbyOnly {
    private const string FileName = "lobbyonly.json";

    private sealed class Saved {
        [JsonProperty("enabled")] public bool Enabled = true;
    }

    private static volatile bool _enabled = Load();

    /// <summary>Whether only the lobby's members are accepted. Setting it saves it.</summary>
    public static bool Enabled {
        get => _enabled;
        set {
            _enabled = value;
            FileUtil.WriteObjectToJsonFile(new Saved { Enabled = value }, FilePath);
        }
    }

    private static string FilePath => Path.Combine(FileUtil.GetCurrentPath(), FileName);

    private static bool Load() =>
        !File.Exists(FilePath) || (FileUtil.LoadObjectFromJsonFile<Saved>(FilePath)?.Enabled ?? true);
}
