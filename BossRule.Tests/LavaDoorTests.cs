using Xunit;

namespace BossRule.Tests;

// (World A, 3c; the decision "Ordinary arenas wait up to 5 s for everyone in the room": "The Lava Challenge's door is
// World A's, with the same 5 s") the door shut on the first hero in while the other still stood outside (lavadoor4-p3)
public class LavaDoorTests {
    [Fact]
    public void The_doors_ENTER_in_Wait_For_Trigger_is_the_walk_in() =>
        Assert.True(LavaDoor.IsWalkIn("Bone_East_LavaChallenge", "entrance_door", "Wait For Trigger", "ENTER"));

    [Fact]
    public void Its_cave_in_another_state_object_or_room_is_not() {
        Assert.False(LavaDoor.IsWalkIn("Bone_East_LavaChallenge", "entrance_door", "Wait For Explosion", "CAVE IN"));
        Assert.False(LavaDoor.IsWalkIn("Bone_East_LavaChallenge", "entrance_door", "Close", "ENTER"));
        Assert.False(LavaDoor.IsWalkIn("Bone_East_LavaChallenge", "Battle Scene", "Wait For Trigger", "ENTER"));
        Assert.False(LavaDoor.IsWalkIn("Bone_East_10", "entrance_door", "Wait For Trigger", "ENTER"));
    }
}
