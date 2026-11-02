using System.Collections.Generic;
using SSMP.Game.Client.Save.Complex;
using Xunit;

namespace SSMP.Tests;

public class ProjectionOpsTests {
    private static readonly ComplexFieldSpec Tools = ComplexFieldSpecs.All["Tools"];
    private static readonly ComplexFieldSpec Collectables = ComplexFieldSpecs.All["Collectables"];

    private static Projection P(params (string Entry, string Member, int Value)[] values) {
        var projection = new Projection();
        foreach (var (entry, member, value) in values) {
            if (!projection.TryGetValue(entry, out var members)) {
                projection[entry] = members = new Dictionary<string, int>();
            }

            members[member] = value;
        }

        return projection;
    }

    private static EntryChange C(string entry, string member, int value) => new(entry, member, value);

    [Fact]
    public void Two_concurrent_add_deltas_both_count() {
        var target = P(("Simple Key", "Amount", 1));
        ProjectionOps.Apply(target, [C("Simple Key", "Amount", 1), C("Simple Key", "Amount", 1)], Collectables, snapshot: false);
        Assert.Equal(3, target["Simple Key"]["Amount"]);
    }

    [Fact]
    public void An_add_delta_never_takes_a_count_below_zero() {
        // Two players hand in one wish at once: each game takes its 12 rags, then the other's -12 arrives
        var target = P(("Pilgrim Rag", "Amount", 0));
        ProjectionOps.Apply(target, [C("Pilgrim Rag", "Amount", -12)], Collectables, snapshot: false);
        Assert.Equal(0, target["Pilgrim Rag"]["Amount"]);
    }

    [Fact]
    public void Max_never_lowers() {
        var target = P(("Straight Pin", "IsUnlocked", 1));
        ProjectionOps.Apply(target, [C("Straight Pin", "IsUnlocked", 0)], Tools, snapshot: false);
        Assert.Equal(1, target["Straight Pin"]["IsUnlocked"]);
    }

    [Fact]
    public void Diff_ignores_personal_members() =>
        Assert.Empty(ProjectionOps.Diff(P(("Straight Pin", "AmountLeft", 5)), P(("Straight Pin", "AmountLeft", 2)), Tools));

    [Fact]
    public void Add_diff_is_a_delta() =>
        Assert.Equal(
            [C("Simple Key", "Amount", 2)],
            ProjectionOps.Diff(P(("Simple Key", "Amount", 1)), P(("Simple Key", "Amount", 3)), Collectables)
        );

    [Fact]
    public void Snapshot_sets_add_members_absolutely() {
        var target = P(("Simple Key", "Amount", 4));
        ProjectionOps.Apply(target, [C("Simple Key", "Amount", 2)], Collectables, snapshot: true);
        Assert.Equal(2, target["Simple Key"]["Amount"]);
    }

    [Fact]
    public void Snapshot_keeps_the_max_rule_and_adds_new_entries() {
        var target = P(("Straight Pin", "IsUnlocked", 1));
        ProjectionOps.Apply(target, [C("Straight Pin", "IsUnlocked", 0), C("Tacks", "IsUnlocked", 1)], Tools, snapshot: true);
        Assert.Equal((1, 1), (target["Straight Pin"]["IsUnlocked"], target["Tacks"]["IsUnlocked"]));
    }

    [Fact]
    public void Snapshot_rebuilds_the_projection() {
        var original = P(("Simple Key", "Amount", 3), ("Simple Key", "AmountWhileHidden", 1), ("Tool Metal", "Amount", 2));
        var rebuilt = new Projection();
        ProjectionOps.Apply(rebuilt, ProjectionOps.Snapshot(original), Collectables, snapshot: true);
        Assert.Equal((3, 1, 2), (rebuilt["Simple Key"]["Amount"], rebuilt["Simple Key"]["AmountWhileHidden"], rebuilt["Tool Metal"]["Amount"]));
    }

    [Fact]
    public void Absolute_diff_gives_new_values() =>
        Assert.Equal(
            [C("Simple Key", "Amount", 3)],
            ProjectionOps.Diff(P(("Simple Key", "Amount", 1)), P(("Simple Key", "Amount", 3)), Collectables, absolute: true)
        );

    [Fact]
    public void Changes_merge_into_a_stored_snapshot() {
        var stored = ProjectionOps.Encode(true, [C("Simple Key", "Amount", 1)]);
        var update = ProjectionOps.Encode(false, [C("Simple Key", "Amount", 1), C("Simple Key", "Amount", 1)]);
        var (snap, changes) = ProjectionOps.Decode(ProjectionOps.MergeIntoSnapshot(stored, update, Collectables));
        Assert.True(snap);
        Assert.Contains(C("Simple Key", "Amount", 3), changes);
    }

    [Fact]
    public void An_absolute_update_replaces_add_members_in_a_stored_snapshot() {
        var stored = ProjectionOps.Encode(true, [C("Simple Key", "Amount", 5)]);
        var update = ProjectionOps.Encode(true, [C("Simple Key", "Amount", 2)]);
        var (_, changes) = ProjectionOps.Decode(ProjectionOps.MergeIntoSnapshot(stored, update, Collectables));
        Assert.Equal([C("Simple Key", "Amount", 2)], changes);
    }

    [Fact]
    public void Changes_merge_into_nothing_stored_yet() {
        var update = ProjectionOps.Encode(false, [C("Straight Pin", "IsUnlocked", 1)]);
        var (_, changes) = ProjectionOps.Decode(ProjectionOps.MergeIntoSnapshot(null, update, Tools));
        Assert.Equal([C("Straight Pin", "IsUnlocked", 1)], changes);
    }

    [Fact]
    public void Encode_round_trips() {
        EntryChange[] changes = [C("Straight Pin", "IsUnlocked", 1), C("Simple Key", "Amount", -1)];
        var (snap, back) = ProjectionOps.Decode(ProjectionOps.Encode(false, changes));
        Assert.False(snap);
        Assert.Equal(changes, back);
    }
}
