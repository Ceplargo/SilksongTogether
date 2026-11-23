using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SSMP.Game.Client.Entity;
using Xunit;

public class FlowerSpikeRegistryTests {
    // the Flower Queen's spike balls: scene objects under Boss Scene/Spike Balls P1 and Boss Scene/Spike Balls, each
    // game throwing its own (random waits, offsets, rotation; the early boss sweep, NOTES 599 and 601)
    [Fact]
    public void Her_spike_balls_are_an_entity_kind_under_both_groups() {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "entity-registry.json")));
        var entry = Assert.Single(json.RootElement.EnumerateArray()
            .Where(e => e.GetProperty("base_object_name").GetString() == "Flower Queen Spike"));
        var parent = entry.GetProperty("parent_name").GetString()!;
        Assert.Contains(parent, "Spike Balls P1");  // matched by Contains: both groups
        Assert.Contains(parent, "Spike Balls");
        Assert.Equal(EntityType.FlowerQueenSpike, Enum.Parse<EntityType>(entry.GetProperty("type").GetString()!));
        Assert.Contains("Control", entry.GetProperty("fsm_names").EnumerateArray().Select(e => e.GetString()));
    }
}
