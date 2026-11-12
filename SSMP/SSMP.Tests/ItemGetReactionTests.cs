using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class ItemGetReactionTests {
    [Fact]
    public void A_reaction_comes_back_as_sent() {
        var read = ItemGetReaction.Read(ItemGetReaction.Info(true, 0.25f, -1.5f));
        Assert.NotNull(read);
        Assert.True(read!.Value.small);
        Assert.Equal(0.25f, read.Value.x, 2);
        Assert.Equal(-1.5f, read.Value.y, 2);
    }

    [Fact]
    public void An_offset_too_far_is_not_sent() => Assert.Null(ItemGetReaction.Info(false, 400f, 0f));

    [Fact]
    public void Another_effects_info_is_not_read() => Assert.Null(ItemGetReaction.Read([1, 2]));
}
