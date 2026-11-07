using System;
using SSMP.Game.Client.Entity;
using Xunit;

public class EntityParentingTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Only_registered_children_send_reliable_clips(bool hasParent, bool reliable) =>
        Assert.Equal(reliable, EntityParenting.SendsChildAnimationReliably(hasParent));

    [Theory]
    [InlineData(4u, 6, 4u, 5, false)]
    [InlineData(4u, 5, 4u, 5, true)]
    [InlineData(4u, 4, 4u, 5, true)]
    [InlineData(4u, 65535, 4u, 0, true)]
    [InlineData(4u, 0, 4u, 65535, false)]
    [InlineData(5u, 0, 4u, 5, false)]
    [InlineData(3u, 50, 4u, -1, true)]
    [InlineData(4u, 0, 4u, -1, false)]
    public void Child_clips_reject_old_updates_and_previous_room_owners(
        uint epoch, ushort order, uint currentEpoch, int currentOrder, bool stale) =>
        Assert.Equal(stale, EntityParenting.IsOldChildAnimation(epoch, order, currentEpoch, currentOrder));

    [Theory]
    [InlineData("WoodWasp", true)]
    [InlineData("ShellwoodHive", false)]
    [InlineData("GrassBall", false)]
    [InlineData("Judge", false)]
    [InlineData("MossBoneCocoon", false)]
    public void Only_independent_wasp_copies_stay_out_of_the_native_hive(string type, bool expected) =>
        Assert.Equal(expected, EntityParenting.KeepsWaspDetached(Enum.Parse<EntityType>(type)));
}
