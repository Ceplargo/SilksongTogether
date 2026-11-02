using SSMP.Networking.Matchmaking.Protocol;
using SSMP.Ui.Component;
using Xunit;

namespace SSMP.Tests;

public class LobbyVisibilityTests {
    [Fact]
    public void A_Steam_lobby_starts_on_Friends() =>
        Assert.Equal(LobbyVisibility.FriendsOnly, LobbyVisibilityDefault.For(PublicLobbyType.Steam));

    [Fact]
    public void A_Matchmaking_lobby_starts_on_Private() =>
        Assert.Equal(LobbyVisibility.Private, LobbyVisibilityDefault.For(PublicLobbyType.Matchmaking));
}
