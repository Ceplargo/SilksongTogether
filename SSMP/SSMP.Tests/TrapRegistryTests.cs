using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SSMP.Game.Client.Entity;
using Xunit;

public class TrapRegistryTests {
    // These are the native body names/parents/FSMs in Tut_03 and all three Shellwood hive rooms.
    [Theory]
    [InlineData("MossBoneCocoon", "MossBone Cocoon", null, "Control")]
    [InlineData("ShellwoodHive", "Control", "Shellwood Hive", "DAMAGE EFFECTS")]
    public void Native_trap_bodies_have_scoped_registry_entries(string type, string body, string? parent, string fsm) {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "entity-registry.json")));
        var entry = Assert.Single(json.RootElement.EnumerateArray().Where(e => e.GetProperty("type").GetString() == type));
        Assert.True(Enum.TryParse<EntityType>(type, out _));
        Assert.Equal(body, entry.GetProperty("base_object_name").GetString());
        Assert.Equal(parent, entry.GetProperty("parent_name").GetString());
        var fsms = entry.GetProperty("fsm_names").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("Control", fsms);
        Assert.Contains(fsm, fsms);
    }

    [Fact]
    public void Trap_types_are_appended_after_existing_wire_ids() {
        Assert.Equal((int)EntityType.GrassBall + 1, (int)Enum.Parse<EntityType>("MossBoneCocoon"));
        Assert.Equal((int)EntityType.GrassBall + 2, (int)Enum.Parse<EntityType>("ShellwoodHive"));
    }
}
