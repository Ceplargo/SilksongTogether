using SSMP.Game.Client.Entity;
using Xunit;

namespace SSMP.Tests;

/// <summary>(coop) RoomParts: a move's target room is no part of the room it leaves; a loader's boss scene still is.</summary>
public class RoomPartsTests {
    [Fact]
    public void A_moves_target_room_that_starts_with_the_old_rooms_name_is_no_part_of_it() {
        // Shellwood_11b's deep memory: the memory loads beside Shellwood_11b while the move into it is under way
        Assert.False(RoomParts.IsPart("Shellwood_11b_Memory", "Shellwood_11b", "Shellwood_11b_Memory"));
        Assert.False(RoomParts.IsPart("Belltown_Room_Spare", "Belltown", "Belltown_Room_Spare"));
        Assert.False(RoomParts.IsPart("Bone_East_10_Church", "Bone_East_10", "Bone_East_10_Church"));
    }

    [Fact]
    public void A_loaders_boss_scene_stays_a_part_of_its_room() {
        // loaded by the room's Boss Golem Loader, in the room or as the move into the room ends (its target the room)
        Assert.True(RoomParts.IsPart("Bone_East_08_boss_golem", "Bone_East_08", null));
        Assert.True(RoomParts.IsPart("Bone_East_08_boss_golem", "Bone_East_08", "Bone_East_08"));
        Assert.True(RoomParts.IsPart("Bonetown_boss", "Bonetown", null));
    }

    [Fact]
    public void A_scene_without_the_rooms_name_or_the_room_itself_is_no_part_as_before() {
        Assert.False(RoomParts.IsPart("Bellway_Centipede_additive", "Belltown_basement", null));
        Assert.False(RoomParts.IsPart("Bone_East_08", "Bone_East_08", "Bone_East_08"));
    }
}
