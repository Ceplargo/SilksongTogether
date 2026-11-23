using System.Collections.Generic;
using SSMP.Game.Client.Save;
using Xunit;

namespace SSMP.Tests;

// crest-bench-p3 (0.4 World A, 2026-11-18): Chapel_Wanderer's found hidden area read False in the setter's game after the
// crest's reload and True in the other's, through a bench save and both games' rejoin
public class ReloadResendTests {
    [Fact]
    public void A_bool_the_reload_changed_is_sent_and_nothing_else() {
        var before = new Dictionary<string, bool> { ["Remasker New Sharp (1)"] = true, ["Geo Small Persistent"] = false, ["Pilgrim 01"] = true };
        var after = new List<(string, bool, bool)> {
            ("Remasker New Sharp (1)", false, false),
            ("Geo Small Persistent", false, false),
            ("Pilgrim 01", false, true), // semi-persistent: its reset travels as a reset
            ("Battle Scene", true, false), // not in the room before the reload: not the reload's change
        };
        Assert.Equal(new[] { "Remasker New Sharp (1)" }, ReloadResend.Changed(before, after));
    }

    [Fact]
    public void Nothing_changed_sends_nothing() {
        var before = new Dictionary<string, bool> { ["Remasker New Sharp (1)"] = true };
        Assert.Empty(ReloadResend.Changed(before, new List<(string, bool, bool)> { ("Remasker New Sharp (1)", true, false) }));
    }
}
