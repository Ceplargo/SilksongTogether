using SSMP.Game.Client.Save;
using Xunit;

namespace SSMP.Tests;

public class FleaArrivalTests {
    [Theory]
    [InlineData("SavedFlea_Bone_06", "Bone_06")]
    [InlineData("SavedFlea_Bone_East_10_Church", "Bone_East_10_Church")]
    [InlineData("SavedFlea_", null)]
    [InlineData("hasDash", null)]
    public void A_flea_value_names_its_room(string name, string? room) {
        Assert.Equal(room, FleaArrival.RoomOf(name));
    }

    // Bone_06: the host freed the flea, and the friend's game only set the value: its flea stayed caged and wriggling,
    // as the branches' Control reads the value only when the room loads (probe_flea_flyoff, NOTES 362)
    [Theory]
    [InlineData(true, "Bone_06", "Idle", true)]
    [InlineData(true, "Bone_06", "Hit", true)]
    [InlineData(true, "Bone_06", "Cut", false)]
    [InlineData(true, "Bone_06", "Activate", false)]
    [InlineData(true, "Bone_06", null, false)]
    [InlineData(true, "Bone_05", "Idle", false)]
    [InlineData(false, "Bone_06", "Idle", false)]
    public void A_flea_freed_elsewhere_breaks_out_here_once(bool freed, string here, string? state, bool plays) {
        Assert.Equal(plays, FleaArrival.Plays(freed, "Bone_06", here, state));
    }
}
