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

    // (0.3.22 save checks Task 9 C) Bone_East_10_Church's lever saves under its id and scene swapped (Found persistent
    // bool: Bone_East_10_Church, Song_lever_side): never sent, False in the other game, and lost over a bench save and
    // rejoin with SSMP on (the host's too, the friend elsewhere) while the game alone kept it (game_alone_trip)
    [Fact]
    public void The_church_lever_is_synced_under_the_key_the_game_saves_it_with() {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "save-data.json")));
        var item = Assert.Single(json.RootElement.GetProperty("persistentBoolItems").EnumerateArray().Where(e =>
            e.GetProperty("Key").GetProperty("id").GetString() == "Bone_East_10_Church" &&
            e.GetProperty("Key").GetProperty("sceneName").GetString() == "Song_lever_side"));
        Assert.True(item.GetProperty("Value").GetProperty("Sync").GetBoolean());
        Assert.True(item.GetProperty("Value").GetProperty("IgnoreSceneHost").GetBoolean());
    }

    // (0.3.22, from the original SSMP's 4d7ca26) The game keeps these six as ulong bit masks (PlayerData's only ulong
    // fields); labelled System.Int32, a value's top 32 bits were cut off on decode and set into a ulong field
    [Theory]
    [InlineData("memoryOrbs_Clover_18_A")]
    [InlineData("memoryOrbs_Clover_18_B")]
    [InlineData("memoryOrbs_Clover_18_C")]
    [InlineData("memoryOrbs_Clover_18_D")]
    [InlineData("memoryOrbs_Clover_18_E")]
    [InlineData("memoryOrbs_Clover_19")]
    public void The_clover_memory_orbs_are_ulong(string name) {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "save-data.json")));
        Assert.Equal("System.UInt64",
            json.RootElement.GetProperty("playerData").GetProperty(name).GetProperty("VarType").GetString());
    }
}
