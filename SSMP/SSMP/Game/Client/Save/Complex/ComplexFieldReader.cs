using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

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
        var container = Container(fieldName);
        if (Typed(fieldName, container, spec) is { } typed) {
            return typed.Read(container!, spec);
        }

        var projection = new Projection();
        foreach (var (entry, data) in Entries(container)) {
            var members = new Dictionary<string, int>();
            foreach (var member in spec.MemberNames) {
                if (MemberValue(data, member) is { } value) {
                    members[member] = value;
                }
            }

            projection[entry] = members;
        }

        return projection;
    }

    /// <summary>
    /// (coop, optimization) Whether the container still holds what <paramref name="last"/> read, so a read and a compare
    /// would find no change (<see cref="ProjectionOps.Unchanged{T}"/>), without building a projection: with full lists
    /// the poll's read and compare made about 2 MB of garbage a second per game, none of them finding a change (mini PC
    /// job 102).
    /// </summary>
    public static bool Unchanged(string fieldName, Projection last) {
        var spec = ComplexFieldSpecs.All[fieldName];
        var container = Container(fieldName);
        return Typed(fieldName, container, spec) is { } typed
            ? typed.Unchanged(container!, last, spec)
            : ProjectionOps.Unchanged(last, spec, Entries(container), MemberValueOf);
    }

    /// <summary>
    /// (coop, optimization) The switch the typed read's twin turns off (a skipped bool method returns false): every read
    /// and no-change test then goes by reflection, as before.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ReadsTyped() => true;

    // (coop, optimization) each container's compiled getters (TypedEntries), made at its first read; null where its type
    // has none, so it reads by reflection
    private static readonly Dictionary<string, TypedEntries?> TypedReads = new();

    private static TypedEntries? Typed(string fieldName, object? container, ComplexFieldSpec spec) {
        if (container == null || !ReadsTyped()) {
            return null;
        }

        if (!TypedReads.TryGetValue(fieldName, out var typed) || typed != null && typed.ContainerType != container.GetType()) {
            TypedReads[fieldName] = typed = TypedEntries.For(container.GetType(), spec);
        }

        return typed;
    }

    /// <summary>
    /// (coop, optimization) The switch the no-change test's twin turns off (a skipped bool method returns false): every
    /// poll then reads and compares in full, as before.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SkipsUnchanged() => true;

    /// <summary>
    /// The container object in the current PlayerData, or null.
    /// </summary>
    public static object? Container(string fieldName) =>
        FieldOf(typeof(PlayerData), fieldName)?.GetValue(PlayerData.instance);

    /// <summary>A member of an entry's data as an int (bools as 0 or 1, other types 0), or null without that member.</summary>
    private static int? MemberValue(object data, string member) =>
        FieldOf(data.GetType(), member) is { } field
            ? field.GetValue(data) switch {
                bool flag => flag ? 1 : 0,
                int number => number,
                _ => 0
            }
            : null;

    private static readonly Func<object, string, int?> MemberValueOf = MemberValue;

    // (coop, optimization) the reflection lookups, once per type (the poll's are on the main thread)
    private static readonly Dictionary<(Type, string), FieldInfo?> Fields = new();
    private static readonly Dictionary<Type, MethodInfo?> Enumerates = new();
    private static readonly Dictionary<Type, (PropertyInfo? Key, PropertyInfo? Value)> Pairs = new();

    private static FieldInfo? FieldOf(Type type, string name) {
        if (!Fields.TryGetValue((type, name), out var field)) {
            Fields[(type, name)] = field = type.GetField(name);
        }

        return field;
    }

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

        var containerType = container.GetType();
        if (!Enumerates.TryGetValue(containerType, out var enumerate)) {
            Enumerates[containerType] = enumerate = containerType.GetMethod("Enumerate");
        }

        if (enumerate?.Invoke(container, null) is not IEnumerable entries) {
            yield break;
        }

        foreach (var pair in entries) {
            var type = pair.GetType();
            if (!Pairs.TryGetValue(type, out var properties)) {
                Pairs[type] = properties = (type.GetProperty("Key"), type.GetProperty("Value"));
            }

            if (properties.Key?.GetValue(pair) is string { Length: > 0 } name &&
                properties.Value?.GetValue(pair) is { } data) {
                yield return (name, data);
            }
        }
    }
}
