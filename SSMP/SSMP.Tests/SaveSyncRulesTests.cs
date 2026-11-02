using SSMP.Game.Client.Save;
using Xunit;

namespace SSMP.Tests;

public class SaveSyncRulesTests {
    [Theory]
    [InlineData(SaveKind.Shared, false, false, true)]
    [InlineData(SaveKind.Personal, true, true, false)]
    [InlineData(SaveKind.Item, false, true, true)]
    [InlineData(SaveKind.Item, true, false, false)]
    public void Kind_decides_sharing(object kind, bool legacy, bool itemSharing, bool shared) =>
        Assert.Equal(shared, SaveSyncRules.IsShared((SaveKind) kind, legacy, itemSharing));

    [Fact]
    public void No_kind_falls_back_to_legacy() => Assert.True(SaveSyncRules.IsShared(null, true, false));
}
