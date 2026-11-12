using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

public class CopyPresenceTests {
    // Bone_01: the friend's Sherma rang her bell at the gate, as the Rock Roller's copy never sent its presence
    [Theory]
    [InlineData("ROCK ROLLER PRESENT", true)]
    [InlineData("THUMP", false)]
    [InlineData(null, false)]
    public void A_copy_announces_only_presence_events(string? name, bool announces) {
        Assert.Equal(announces, CopyPresence.Announces(name));
    }
}
