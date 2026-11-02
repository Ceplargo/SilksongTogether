using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SSMP.Game.Client.Save.Complex;

/// <summary>
/// An item container's entries by name, each with the values of its members as ints (bools as 0 or 1).
/// </summary>
internal sealed class Projection : Dictionary<string, Dictionary<string, int>>;

/// <summary>
/// Changes between projections of an item container, their merge and their wire format. Kept free of Unity types so
/// it can be tested without the game.
/// </summary>
internal static class ProjectionOps {
    /// <summary>
    /// The shared changes from before to after, in a stable order. Add members give deltas, the others new values;
    /// with <paramref name="absolute"/> every change gives the new value.
    /// </summary>
    public static List<EntryChange> Diff(Projection before, Projection after, ComplexFieldSpec spec, bool absolute = false) {
        var changes = new List<EntryChange>();
        foreach (var entry in before.Keys.Union(after.Keys).OrderBy(name => name, StringComparer.Ordinal)) {
            before.TryGetValue(entry, out var was);
            after.TryGetValue(entry, out var now);
            foreach (var (member, rule) in spec.SharedMembers.OrderBy(pair => pair.Key, StringComparer.Ordinal)) {
                var oldValue = Get(was, member);
                var newValue = Get(now, member);
                if (oldValue != newValue) {
                    changes.Add(new EntryChange(entry, member, rule == MergeRule.Add && !absolute ? newValue - oldValue : newValue));
                }
            }
        }

        return changes;
    }

    /// <summary>
    /// Merge changes into target by each member's rule. In a snapshot (the full projection a joining player gets),
    /// Add members are absolute values. Changes to members that are not shared are ignored.
    /// </summary>
    public static void Apply(Projection target, IEnumerable<EntryChange> changes, ComplexFieldSpec spec, bool snapshot) {
        foreach (var change in changes) {
            if (!spec.SharedMembers.TryGetValue(change.Member, out var rule)) {
                continue;
            }

            if (!target.TryGetValue(change.Entry, out var members)) {
                target[change.Entry] = members = new Dictionary<string, int>();
            }

            var current = Get(members, change.Member);
            members[change.Member] = rule switch {
                // Every Add member is a count: two players taking the same 12 items at once must leave 0, not -12
                MergeRule.Add => System.Math.Max(0, snapshot ? change.Value : current + change.Value),
                MergeRule.Max => System.Math.Max(current, change.Value),
                _ => change.Value
            };
        }
    }

    /// <summary>
    /// The whole projection as absolute changes, in a stable order; applied as a snapshot to an empty projection it
    /// gives the projection back.
    /// </summary>
    public static List<EntryChange> Snapshot(Projection projection) =>
        projection.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.OrderBy(member => member.Key, StringComparer.Ordinal)
                .Select(member => new EntryChange(entry.Key, member.Key, member.Value)))
            .ToList();

    /// <summary>
    /// Merge an encoded update (changes, or new values when its snapshot flag is set) into a stored snapshot (null
    /// when nothing is stored yet) and return the new snapshot, encoded. The server keeps each item container this way.
    /// </summary>
    /// <exception cref="InvalidDataException">The stored snapshot or the update is not a valid encoding.</exception>
    public static byte[] MergeIntoSnapshot(byte[]? storedSnapshot, byte[] update, ComplexFieldSpec spec) {
        var projection = new Projection();
        if (storedSnapshot != null) {
            Apply(projection, Decode(storedSnapshot).Changes, spec, snapshot: true);
        }

        var (absolute, changes) = Decode(update);
        Apply(projection, changes, spec, absolute);
        return Encode(true, Snapshot(projection));
    }

    /// <summary>
    /// Encode changes: one byte (1 for a full snapshot, 0 for changes), the count, then each entry, member and value.
    /// </summary>
    public static byte[] Encode(bool snapshot, IReadOnlyList<EntryChange> changes) {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8)) {
            writer.Write((byte) (snapshot ? 1 : 0));
            writer.Write(changes.Count);
            foreach (var change in changes) {
                writer.Write(change.Entry);
                writer.Write(change.Member);
                writer.Write(change.Value);
            }
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Decode what <see cref="Encode"/> wrote.
    /// </summary>
    /// <exception cref="InvalidDataException">The data is not a valid encoding.</exception>
    public static (bool Snapshot, List<EntryChange> Changes) Decode(byte[] data) {
        try {
            using var reader = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
            var snapshot = reader.ReadByte() == 1;
            var count = reader.ReadInt32();
            // Each change takes at least 6 bytes, so a larger count is corrupt data
            if (count < 0 || count > data.Length / 6) {
                throw new InvalidDataException($"invalid change count {count}");
            }

            var changes = new List<EntryChange>(count);
            for (var i = 0; i < count; i++) {
                changes.Add(new EntryChange(reader.ReadString(), reader.ReadString(), reader.ReadInt32()));
            }

            return (snapshot, changes);
        } catch (EndOfStreamException e) {
            throw new InvalidDataException("truncated item container data", e);
        }
    }

    private static int Get(Dictionary<string, int>? members, string member) =>
        members != null && members.TryGetValue(member, out var value) ? value : 0;
}
