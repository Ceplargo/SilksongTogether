using System;
using System.Collections.Concurrent;
using System.Linq;
using SSMP.Api.Command;
using SSMP.Api.Command.Server;
using SSMP.Game.Server;
using SSMP.Networking.Server;

namespace SSMP.Game.Command.Server;

/// <summary>
/// Command any player can use to mark the moment something went wrong in every player's log, with a short note.
/// </summary>
internal class BugCommand : IServerCommand, ICommandWithDescription {
    /// <inheritdoc />
    public string Trigger => "/bug";

    /// <inheritdoc />
    public string[] Aliases => [];

    /// <inheritdoc />
    public string Description => "Mark this moment in every player's log, with an optional short note.";

    /// <inheritdoc />
    public bool AuthorizedOnly => false;

    /// <summary>
    /// A reference to the server player data dictionary.
    /// </summary>
    private readonly ConcurrentDictionary<ushort, ServerPlayerData> _playerData;

    /// <summary>
    /// The net server instance.
    /// </summary>
    private readonly NetServer _netServer;

    public BugCommand(ConcurrentDictionary<ushort, ServerPlayerData> playerData, NetServer netServer) {
        _playerData = playerData;
        _netServer = netServer;
    }

    /// <inheritdoc />
    public void Execute(ICommandSender commandSender, string[] arguments) {
        var who = commandSender is IPlayerCommandSender sender && _playerData.TryGetValue(sender.Id, out var data)
            ? data.Username
            : "the host's console";
        var players = _playerData.Values.OrderBy(p => p.Id)
            .Select(p => (p.Username, string.IsNullOrEmpty(p.CurrentScene) ? null : p.CurrentScene));
        var line = BugMark.Line(DateTime.Now, who, string.Join(" ", arguments.Skip(1)), players);

        // Each game logs a received mark at Info (ClientManager), so this one line lands in every player's log. It
        // goes out whole: four players' rooms can pass the 255 characters the API's chat messages allow
        foreach (var id in _playerData.Keys) {
            _netServer.GetUpdateManagerForClient(id)?.AddChatMessage(line);
        }
    }
}
