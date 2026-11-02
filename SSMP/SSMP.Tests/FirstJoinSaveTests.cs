using System.Collections.Generic;
using SSMP.Game.Server.Save;
using Xunit;

namespace SSMP.Tests;

public class FirstJoinSaveTests {
    [Fact]
    public void First_join_copies_the_hosts_values_and_applies_the_resets() {
        // 1: hasDash (true), 2: geo (263 as a little-endian int)
        var host = new Dictionary<ushort, byte[]> { [1] = [1], [2] = [7, 1, 0, 0] };
        var resets = new Dictionary<ushort, byte[]> { [2] = [0, 0, 0, 0], [9] = [0, 0, 0, 0] };

        var friend = FirstJoinSave.FromHost(host, resets);

        Assert.Equal(new byte[] { 1 }, friend[1]);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, friend[2]);
        Assert.False(friend.ContainsKey(9));
    }

    [Fact]
    public void The_host_keeps_its_own_values() {
        var host = new Dictionary<ushort, byte[]> { [2] = [7, 1, 0, 0] };

        FirstJoinSave.FromHost(host, new Dictionary<ushort, byte[]> { [2] = [0, 0, 0, 0] });

        Assert.Equal(new byte[] { 7, 1, 0, 0 }, host[2]);
    }
}
