using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SSMP.Game.Client.Entity;
using Xunit;

public class MiteflyRegistryTests {
    [Fact]
    public void Sprite_motion_is_a_child_of_its_mitefly_not_a_separate_scene_body() {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "entity-registry.json")));
        var entries = json.RootElement.EnumerateArray().ToArray();
        var parent = Assert.Single(entries.Where(e => e.GetProperty("base_object_name").GetString() == "Mitefly"));
        Assert.True(parent.TryGetProperty("children", out var children), "Mitefly Sprite Motion is not registered");
        var child = Assert.Single(children.EnumerateArray());
        Assert.Equal("Sprite", child.GetProperty("base_object_name").GetString());
        Assert.Equal("Mitefly", child.GetProperty("parent_name").GetString());
        Assert.Equal(EntityType.Fluttermite, Enum.Parse<EntityType>(child.GetProperty("type").GetString()!));
        Assert.Contains("Motion", child.GetProperty("fsm_names").EnumerateArray().Select(e => e.GetString()));
        Assert.DoesNotContain(entries, e => e.GetProperty("base_object_name").GetString() == "Sprite"
            && e.GetProperty("parent_name").GetString() == "Mitefly");
    }
}
