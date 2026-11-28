using SSMP.Api.Command;
using SSMP.Api.Command.Server;
using SSMP.Game.Server.Auth;

namespace SSMP.Game.Command.Server;

/// <summary>
/// Command that turns the Steam host's lobby-member check off and on (0.4.2), as /whitelist does for the whitelist.
/// </summary>
internal class LobbyOnlyCommand : IServerCommand, ICommandWithDescription {
    /// <inheritdoc />
    public string Trigger => "/lobbyonly";

    /// <inheritdoc />
    public string[] Aliases => [];

    /// <inheritdoc />
    public string Description =>
        "Steam hosting: on (default) accepts only players in your Steam lobby; off accepts any Steam player, as before 0.4.2.";

    /// <inheritdoc />
    public bool AuthorizedOnly => true;

    /// <inheritdoc />
    public void Execute(ICommandSender commandSender, string[] args) {
        var action = args.Length < 2 ? "" : args[1].ToLower();
        if (action != "on" && action != "off") {
            commandSender.SendMessage(
                $"Invalid usage: {Trigger} <on|off> (now {(LobbyOnly.Enabled ? "on" : "off")})"
            );
            return;
        }

        var on = action == "on";
        if (LobbyOnly.Enabled == on) {
            commandSender.SendMessage($"Lobby-only was already {action}");
            return;
        }

        LobbyOnly.Enabled = on;
        commandSender.SendMessage(
            on
                ? "Lobby-only is on: only players in your Steam lobby can connect"
                : "Lobby-only is off: any Steam player can connect, as before 0.4.2"
        );
    }
}
