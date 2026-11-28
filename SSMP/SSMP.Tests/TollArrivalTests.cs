using SSMP.Game.Client.Save;
using Xunit;

namespace SSMP.Tests;

public class TollArrivalTests {
    // Bellway_03: the host paid the toll, and the friend's game only set the value: its machine stayed in Inert and its
    // floor gate shut, as Unlock Behaviour reads the value only as the room loads (probe_bellway_gate, NOTES 619)
    [Theory]
    [InlineData(true, "UnlockedBoneforestEastStation", "Inert", true)]
    [InlineData(true, "UnlockedBoneforestEastStation", "Map Up", false)]
    [InlineData(true, "UnlockedBoneforestEastStation", "Prompt", false)]
    [InlineData(true, "UnlockedBoneforestEastStation", "Sequence Animation", false)]
    [InlineData(true, "UnlockedBoneforestEastStation", null, false)]
    [InlineData(true, "AnotherStationValue", "Inert", false)]
    [InlineData(false, "UnlockedBoneforestEastStation", "Inert", false)]
    public void A_toll_paid_elsewhere_opens_this_station_once(bool value, string machineValue, string? state,
        bool plays) {
        Assert.Equal(plays, TollArrival.Plays(value, "UnlockedBoneforestEastStation", machineValue, state));
    }
}
