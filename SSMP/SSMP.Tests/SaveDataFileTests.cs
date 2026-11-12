using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

// (0.3.21) The game saves some objects under the scene GLOBAL_SHARED, whichever room they stand in: Bone_10's Caravan
// Lech (Found persistent bool in scene: (Caravan Lech, GLOBAL_SHARED)). save-data.json had it only under its rooms'
// names, so a talk with it was never sent ("Not in persistent bool save data values"): True for the talker, False for
// the friend, and None for the friend after a save and rejoin (the world sweep, c-wsweep-host-p3-1.log).
public class SaveDataFileTests {
    [Fact]
    public void The_caravan_lech_is_synced_under_the_scene_the_game_saves_it_in() {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "save-data.json")));
        var item = Assert.Single(json.RootElement.GetProperty("persistentBoolItems").EnumerateArray().Where(e =>
            e.GetProperty("Key").GetProperty("id").GetString() == "Caravan Lech" &&
            e.GetProperty("Key").GetProperty("sceneName").GetString() == "GLOBAL_SHARED"));
        Assert.True(item.GetProperty("Value").GetProperty("Sync").GetBoolean());
        Assert.True(item.GetProperty("Value").GetProperty("IgnoreSceneHost").GetBoolean());
    }
}
