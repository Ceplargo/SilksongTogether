using Xunit;

namespace BossRule.Tests;

public class MemoryRewardTests {
    private static CutsceneEntry Heart(bool oneReward) => new("silk_heart_bellbeast", ["Bone_05"], "Boss Scene/Silk Heart",
        "Control", ["Take Control"], true, OneReward: oneReward);

    // 0.4 world Task 17 Step 1 (heart4-on-host-p3, heart4-on-friend-p3): with the follow on, each game came back through
    // its own Silk Heart Memory Return and its heart added one to the shared silkRegenMax: 0 to 2 in both games, the game
    // alone 0 to 1. A followed run of a one_reward entry holds this game's reward through the memory and the return.
    [Fact]
    public void A_followed_heart_holds_its_reward_through_its_memory_and_return() {
        var reward = new MemoryReward();
        reward.Followed(Heart(true));
        foreach (var room in new[] { "Memory_Silk_Heart_BellBeast", null, "Bone_05" }) {
            reward.Moved(room);
            Assert.Equal("silk_heart_bellbeast", reward.Held);
        }
        reward.Moved("Bone_05b");  // the hero walked on: its next heart is its own
        Assert.Null(reward.Held);
    }

    [Fact]
    public void The_leaders_run_and_an_entry_without_one_reward_hold_nothing() {
        var reward = new MemoryReward();
        reward.Moved("Bone_05");
        Assert.Null(reward.Held);  // the leader's game never followed
        reward.Followed(Heart(false));
        Assert.Null(reward.Held);
    }

    [Fact]
    public void The_catalog_reads_one_reward_false_unless_it_says_so() {
        var entries = CutsceneCatalog.Load(
            """[{"name": "a", "scenes": ["R"], "object": "O", "fsm": "F", "start": ["S"]},""" +
            """ {"name": "b", "scenes": ["R"], "object": "O", "fsm": "F", "start": ["S"], "one_reward": true}]""");
        Assert.False(entries[0].OneReward);
        Assert.True(entries[1].OneReward);
    }

    // A memory scene's end, the leader's, ends each follower's memory with it: the heart's entry names its memory's
    // Memory Control, loaded as a second followed FSM in the memory's room (heart4: the other game came back only when
    // its own hero touched its orb, 10 s after).
    [Fact]
    public void A_memory_part_loads_as_its_own_followed_fsm_in_the_memory_scene() {
        var entries = CutsceneCatalog.Load(
            """[{"name": "h", "scenes": ["Bone_05"], "object": "O", "fsm": "Control", "start": ["Take Control"],""" +
            """ "one_reward": true, "memory": {"scene": "Memory_X", "object": "Memory Control", "fsm": "Memory Control",""" +
            """ "start": ["To White"]}}]""");
        Assert.Equal(2, entries.Count);
        var m = entries[1];
        Assert.Equal(("h/memory", "Memory_X", "Memory Control", "Memory Control", "To White", true, false),
            (m.Name, m.Scenes[0], m.Object, m.Fsm, m.Start[0], m.Follow, m.OneReward));
    }
}
