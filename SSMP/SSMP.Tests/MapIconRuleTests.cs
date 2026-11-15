using System;
using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class MapIconRuleTests {
    [Fact]
    public void With_the_default_settings_and_no_compass_no_icon_may_show() {
        // (optimization) in a game whose icon can't show, the map position was still looked up every frame: 116 MB in
        // 15 minutes of the host's game, 169 of the friend's (mini PC job 28)
        Assert.False(MapIconRule.MayShow(alwaysShow: false, onlyWithCompass: true, () => false));
        Assert.True(MapIconRule.MayShow(alwaysShow: false, onlyWithCompass: true, () => true));
    }

    [Fact]
    public void It_agrees_with_the_old_rule_in_every_case() {
        foreach (var always in new[] { false, true })
        foreach (var only in new[] { false, true })
        foreach (var compass in new[] { false, true }) {
            var old = always || (only && compass);
            Assert.Equal(old, MapIconRule.MayShow(always, only, () => compass));
        }
    }

    [Fact]
    public void The_compass_is_asked_only_when_its_answer_counts() {
        var asked = 0;
        Func<bool> compass = () => { asked++; return true; };
        MapIconRule.MayShow(alwaysShow: true, onlyWithCompass: true, compass);
        MapIconRule.MayShow(alwaysShow: false, onlyWithCompass: false, compass);
        Assert.Equal(0, asked);
        MapIconRule.MayShow(alwaysShow: false, onlyWithCompass: true, compass);
        Assert.Equal(1, asked);
    }
}
