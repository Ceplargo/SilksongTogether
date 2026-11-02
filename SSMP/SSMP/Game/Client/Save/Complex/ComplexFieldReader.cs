using System.Collections;
using System.Collections.Generic;

namespace SSMP.Game.Client.Save.Complex;

/// <summary>
/// Reads the game's item containers (PlayerData fields such as Tools) into projections of their shared members.
/// </summary>
internal static class ComplexFieldReader {
    /// <summary>
    /// The shared members of every entry of the container, bools as 0 or 1.
    /// </summary>
    /// <param name="fieldName">A field name from <see cref="ComplexFieldSpecs.All"/>.</param>
    public static Projection Read(string fieldName) {
        var spec = ComplexFieldSpecs.All[fieldName];
        var projection = new Projection();
        foreach (var (entry, data) in Entries(Container(fieldName))) {
            var members = new Dictionary<string, int>();
            foreach (var member in spec.SharedMembers.Keys) {
                var field = data.GetType().GetField(member);
                if (field != null) {
                    members[member] = field.GetValue(data) switch {
                        bool flag => flag ? 1 : 0,
                        int number => number,
                        _ => 0
                    };
                }
            }

            projection[entry] = members;
        }

        return projection;
    }

    /// <summary>
    /// The container object in the current PlayerData, or null.
    /// </summary>
    public static object? Container(string fieldName) =>
        typeof(PlayerData).GetField(fieldName)?.GetValue(PlayerData.instance);

    /// <summary>
    /// The container's entries as (name, boxed data). The journal is a dictionary, the others are
    /// SerializableNamedLists with Enumerate().
    /// </summary>
    private static IEnumerable<(string Entry, object Data)> Entries(object? container) {
        switch (container) {
            case null:
                yield break;
            case EnemyJournalKillData journal:
                foreach (var pair in journal.Dictionary) {
                    yield return (pair.Key, pair.Value);
                }

                yield break;
        }

        if (container.GetType().GetMethod("Enumerate")?.Invoke(container, null) is not IEnumerable entries) {
            yield break;
        }

        foreach (var pair in entries) {
            var type = pair.GetType();
            if (type.GetProperty("Key")?.GetValue(pair) is string { Length: > 0 } name &&
                type.GetProperty("Value")?.GetValue(pair) is { } data) {
                yield return (name, data);
            }
        }
    }
}
