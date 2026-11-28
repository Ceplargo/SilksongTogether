using SSMP.Networking.Transport.SteamP2P;
using Xunit;

// 0.4.2: the Steam host accepted every P2P session request, lobby or not (NOTES 652)
public class LobbyGateTests {
    private const ulong Friend = 76561198000000001, Stranger = 76561198000000002, Host = 76561198000000000;
    private static readonly ulong[] Lobby = { Host, Friend };

    private static LobbyVerdict Decide(LobbyGate gate, ulong id, ulong[]? members, double now, bool on = true) =>
        gate.Decide(id, members, now, on, out _);

    [Fact]
    public void A_member_of_the_lobby_is_accepted() {
        Assert.Equal(LobbyVerdict.Accept, Decide(new LobbyGate(), Friend, Lobby, 0));
    }

    [Fact]
    public void A_non_member_waits_then_is_refused_once() {
        var gate = new LobbyGate();
        Assert.Equal(LobbyVerdict.Wait, Decide(gate, Stranger, Lobby, 0));
        Assert.Equal(new[] { Stranger }, gate.Waiting());
        Assert.Equal(LobbyVerdict.Wait, Decide(gate, Stranger, Lobby, LobbyGate.WaitSeconds - 0.1));
        Assert.Equal(LobbyVerdict.Refuse, gate.Decide(Stranger, Lobby, LobbyGate.WaitSeconds + 0.1, true, out var waited));
        Assert.Equal(LobbyGate.WaitSeconds + 0.1, waited, 3);
        Assert.Empty(gate.Waiting());
        Assert.Equal(LobbyVerdict.StillRefused, Decide(gate, Stranger, Lobby, LobbyGate.WaitSeconds + 5));
    }

    [Fact]
    public void A_member_who_arrives_inside_the_wait_is_accepted() {
        var gate = new LobbyGate();
        Assert.Equal(LobbyVerdict.Wait, Decide(gate, Friend, new[] { Host }, 0));
        Assert.Equal(LobbyVerdict.Accept, gate.Decide(Friend, Lobby, 4, true, out var waited));
        Assert.Equal(4, waited, 3);
        Assert.Empty(gate.Waiting());
    }

    [Fact]
    public void A_member_who_arrives_after_the_wait_is_refused_until_a_new_request_finds_them_in_the_lobby() {
        var gate = new LobbyGate();
        Assert.Equal(LobbyVerdict.Wait, Decide(gate, Friend, new[] { Host }, 0));
        Assert.Equal(LobbyVerdict.Refuse, Decide(gate, Friend, new[] { Host }, LobbyGate.WaitSeconds + 0.1));
        Assert.Equal(LobbyVerdict.Accept, Decide(gate, Friend, Lobby, LobbyGate.WaitSeconds + 3));
    }

    [Fact]
    public void With_no_lobby_everyone_is_refused_at_once() {
        var gate = new LobbyGate();
        Assert.Equal(LobbyVerdict.Refuse, Decide(gate, Friend, null, 0));
        Assert.Empty(gate.Waiting());
    }

    [Fact]
    public void Switched_off_a_non_member_is_accepted_and_switched_on_refused() {
        var gate = new LobbyGate();
        Assert.Equal(LobbyVerdict.Accept, Decide(gate, Stranger, Lobby, 0, on: false));
        Assert.Equal(LobbyVerdict.Accept, Decide(gate, Stranger, null, 0, on: false));
        Assert.Equal(LobbyVerdict.Refuse, Decide(gate, Stranger, null, 0, on: true));
    }
}
