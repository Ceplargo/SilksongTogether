using SSMP.Networking.Matchmaking.Protocol;

namespace SSMP.Ui.Component;

/// <summary>
/// Visibility options for lobbies.
/// </summary>
public enum LobbyVisibility {
    /// <summary>
    /// Anyone can find and join via browser.
    /// </summary>
    Public,

    /// <summary>
    /// Only friends can see and join.
    /// </summary>
    FriendsOnly,

    /// <summary>
    /// Invite-only, not discoverable.
    /// </summary>
    Private
}

/// <summary>
/// The visibility a new lobby starts on. Kept free of Unity types so it can be tested without the game.
/// </summary>
internal static class LobbyVisibilityDefault {
    /// <summary>
    /// The visibility the lobby panel starts on: Friends for a Steam lobby and Private for a Matchmaking one (which
    /// has no Friends). A public lobby is listed on SSMP's matchmaking server, where strangers can join it.
    /// </summary>
    public static LobbyVisibility For(PublicLobbyType lobbyType) =>
        lobbyType == PublicLobbyType.Steam ? LobbyVisibility.FriendsOnly : LobbyVisibility.Private;
}
