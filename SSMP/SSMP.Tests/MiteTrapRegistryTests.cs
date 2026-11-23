using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SSMP.Game.Client.Entity;
using Xunit;

public class MiteTrapRegistryTests {
    // (coop, 0.4) A sprung Mite Trap lies on its child Phys, which only the runner's game switched on (Already Sprung);
    // a takeover carries no child's active state, so the new runner's trap came out kinematic with Phys off, and a hit
    // sent it flying through the ceiling (probe_trap_takeover_hit, 2026-11-17). Its own entry carries its children's
    // active states (ChildrenActivation), so the hidden original's Phys is on before any takeover.
    [Fact]
    public void A_mite_trap_carries_its_childrens_active_states() {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "entity-registry.json")));
        var trap = Assert.Single(json.RootElement.EnumerateArray()
            .Where(e => e.GetProperty("base_object_name").GetString() == "Mite Trap"));
        Assert.Equal(EntityType.Mite, Enum.Parse<EntityType>(trap.GetProperty("type").GetString()!));
        Assert.Contains("ChildrenActivation", trap.GetProperty("components").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("Control", trap.GetProperty("fsm_names").EnumerateArray().Select(e => e.GetString()));
    }
}
