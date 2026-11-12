using System.Collections.Generic;
using SSMP.Game.Client;
using Xunit;

namespace SSMP.Tests;

public class GrassContactsTests {
    [Fact]
    public void Grass_the_copy_walks_into_is_entered_once_and_left_once() {
        var inside = new HashSet<int>();
        Assert.Equal("[7] []", Step(inside, [7]));
        // still in it the next frame: no second push, as the game's own trigger enters once
        Assert.Equal("[] []", Step(inside, [7]));
        Assert.Equal("[9] []", Step(inside, [7, 9]));
        Assert.Equal("[] [7]", Step(inside, [9]));
        Assert.Equal("[] [9]", Step(inside, []));
        Assert.Empty(inside);
    }

    [Fact]
    public void Grass_that_was_destroyed_while_inside_is_left() {
        var inside = new HashSet<int> { 3, 4 };
        Assert.Equal("[] [3]", Step(inside, [4]));
        Assert.Equal(new HashSet<int> { 4 }, inside);
    }

    /// <summary>"[entered] [left]", each sorted.</summary>
    private static string Step(HashSet<int> inside, IReadOnlyCollection<int> now) {
        var (entered, left) = GrassContacts.Step(inside, now);
        entered.Sort();
        left.Sort();
        return $"[{string.Join(",", entered)}] [{string.Join(",", left)}]";
    }
}
