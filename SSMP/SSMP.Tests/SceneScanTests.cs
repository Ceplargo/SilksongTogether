using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

// 0.3.20 Part C cause no. 2: each room change's entity registration scanned every loaded object, hidden ones too, seven
// times, the game's GlobalPool in DontDestroyOnLoad included (about 7,000 to 60,000 objects over a long session), so a
// co-op room load cost 55 to 435 ms more the longer the session ran.
public class SceneScanTests {
    [Fact]
    public void A_rooms_candidates_come_from_its_own_scene() => Assert.True(SceneScan.SceneOnly());
}
