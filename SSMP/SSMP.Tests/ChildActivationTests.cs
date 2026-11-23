using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SSMP.Game.Client.Entity.Component;
using Xunit;

namespace SSMP.Tests;

public class ChildActivationTests {
    [Fact]
    public void A_first_snapshot_carries_even_an_already_hidden_shield() {
        Assert.True(ChildActivation.NeedsUpdate(null, [0]));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 1, false)]
    [InlineData(1, 5, true)]
    [InlineData(5, 1, true)]
    public void Each_childs_change_is_sent_without_repeating_unchanged_states(byte before, byte after, bool send) {
        Assert.Equal(send, ChildActivation.NeedsUpdate([before], [after]));
    }

    [Fact]
    public void Only_the_Coral_Judge_and_the_Mite_Trap_request_child_activation_snapshots() {
        // (coop, 0.4) the Mite Trap's sprung Phys (MiteTrapRegistryTests)
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "entity-registry.json")));
        var entries = json.RootElement.EnumerateArray().Where(e =>
            e.TryGetProperty("components", out var parts) && parts.EnumerateArray().Any(c => c.GetString() == "ChildrenActivation"));
        Assert.Equal(["Coral Judge", "Mite Trap"], entries.Select(e => e.GetProperty("base_object_name").GetString()).Order());
    }
}
