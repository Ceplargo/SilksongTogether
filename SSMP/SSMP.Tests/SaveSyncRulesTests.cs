using System;
using System.Collections.Generic;
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

    // (0.3.22) A join's save with one value that throws on apply: the values after it still land, and it is reported
    [Fact]
    public void One_bad_value_does_not_stop_the_rest() {
        var values = new Dictionary<ushort, byte[]> { [1] = new byte[] { 1 }, [2] = new byte[] { 2 }, [3] = new byte[] { 3 } };
        var applied = new List<ushort>();
        var skipped = new List<ushort>();
        SaveSyncRules.ApplyEach(values, (index, _) => {
            if (index == 2) throw new InvalidCastException("bad value");
            applied.Add(index);
        }, (index, _) => skipped.Add(index));
        Assert.Equal(new ushort[] { 1, 3 }, applied);
        Assert.Equal(new ushort[] { 2 }, skipped);
    }
}
