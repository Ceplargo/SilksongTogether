using BossRule;
using Xunit;

namespace BossRule.Tests;

public class CaravanRuleTests {
    private static readonly string[] Stops = ["Bone_10", "Greymoor_08", "Coral_Judge_Arena", "Aqueduct_05"];

    [Fact]
    public void A_respawn_on_the_old_stop_follows_the_caravan() =>
        Assert.Equal("Coral_Judge_Arena", CaravanRule.Moved("Greymoor_08", CaravanRule.Marker, "Coral_Judge_Arena", Stops));

    [Theory]
    [InlineData("Greymoor_08", "Death Respawn Marker", "Greymoor_08")]  // the caravan is still there
    [InlineData("Greymoor_08", "RestBench", "Coral_Judge_Arena")]  // a bench's, not the caravan's
    [InlineData("Belltown_Shrine", "Death Respawn Marker", "Coral_Judge_Arena")]  // not a caravan stop
    [InlineData("Greymoor_08", "Death Respawn Marker", null)]  // no stop read
    public void Any_other_respawn_stays(string scene, string marker, string? stop) =>
        Assert.Null(CaravanRule.Moved(scene, marker, stop, Stops));
}
