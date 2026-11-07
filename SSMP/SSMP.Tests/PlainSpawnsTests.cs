using SSMP.Game.Client.Entity.Action;
using Xunit;

namespace SSMP.Tests;

public class PlainSpawnsTests {
    [Theory]
    [InlineData(true, false, true)] // an enemy's own object: another entity, its actions are its own
    [InlineData(true, true, false)] // the Last Judge's stomp wave: a pool spawn whose name and FSM match her entry
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void A_plain_spawn_is_not_another_entity(bool registryMatch, bool plainSpawn, bool expected) =>
        Assert.Equal(expected, PlainSpawns.IsEntity(registryMatch, plainSpawn));

    [Fact]
    public void Remembers_each_plain_spawn_by_its_object() {
        var spawns = new PlainSpawns.Spawned();
        spawns.Add(101);
        Assert.True(spawns.Has(101));
        Assert.False(spawns.Has(102));
        spawns.Add(101);
        Assert.True(spawns.Has(101));
    }
}
