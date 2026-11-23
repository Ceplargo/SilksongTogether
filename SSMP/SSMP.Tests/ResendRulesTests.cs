using System;
using System.Collections.Generic;
using System.Linq;
using SSMP.Networking;
using Xunit;

namespace SSMP.Tests;

public class ResendRulesTests {
    // An update as the builder sees it: its own data and its resends, in bytes; building it fails over the 65535 bytes
    // a packet's length holds (Packet.WriteLength)
    private class Update {
        public int Own;
        public List<int> Resends = new();
        public int Built;
    }

    private static void Build(Update u) {
        var size = u.Own + u.Resends.Sum();
        if (size > ushort.MaxValue) throw new InvalidOperationException($"Packet size ({size} bytes) exceeds the limit");
        u.Built = size;
    }

    private readonly List<string> _log = new();

    private bool Built(Update u) => ResendRules.Built(u, Build, x => x.Resends.Clear(), _log.Add);

    [Fact]
    public void An_update_that_fits_is_built_whole() {
        var u = new Update { Own = 2000, Resends = { 900, 900 } };
        Assert.True(Built(u));
        Assert.Equal(3800, u.Built);
        Assert.Empty(_log);
    }

    [Fact]
    public void An_update_too_big_with_its_resends_goes_out_without_them() {
        // the friend's channel 50 s into throw_freeze with lag: 2 KB of its own, 64 KB of resends (NOTES 535)
        var u = new Update { Own = 2000, Resends = { 30000, 34000 } };
        Assert.True(Built(u));
        Assert.Equal(2000, u.Built);
        Assert.Contains("without its resends", _log.Last());
    }

    [Fact]
    public void An_update_too_big_on_its_own_is_left_out_and_the_next_one_goes_out() {
        var big = new Update { Own = 70000 };
        Assert.False(Built(big));
        Assert.Equal(2, _log.Count);
        Assert.True(Built(new Update { Own = 2000, Resends = { 900 } }));  // the fresh update the manager starts
    }
}
