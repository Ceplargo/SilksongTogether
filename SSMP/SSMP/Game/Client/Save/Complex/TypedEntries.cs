using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SSMP.Game.Client.Save.Complex;

/// <summary>
/// (coop, optimization) An item container's entries read through getters compiled once for its type: its entries (a
/// SerializableNamedList's Enumerate(), or the journal's Dictionary) as (name, data) with no boxing, and each shared member
/// of the data by a compiled field read. The reflection read (ComplexFieldReader) boxed every element, read its Key and
/// Value by PropertyInfo and each member by FieldInfo: 100 to 200 bytes an entry in every no-change test, 5 times a second
/// (mini PC job 113). It reads the same projection: bools as 0 or 1, other types 0, a member the data lacks left out, an
/// entry with no name left out. Kept free of Unity types so it can be tested without the game.
/// </summary>
internal abstract class TypedEntries {
    /// <summary>The container type these getters were made for.</summary>
    public abstract Type ContainerType { get; }

    /// <summary>Whether nothing changed since <paramref name="last"/> (<see cref="ProjectionOps.Unchanged{T}"/>).</summary>
    public abstract bool Unchanged(object container, Projection last, ComplexFieldSpec spec);

    /// <summary>The shared members of every entry, as ComplexFieldReader.Read gives them.</summary>
    public abstract Projection Read(object container, ComplexFieldSpec spec);

    /// <summary>
    /// The getters for a container type, or null where it has no Enumerate() or Dictionary of (string, struct data), or
    /// they can't be made: the caller then reads by reflection, as before.
    /// </summary>
    public static TypedEntries? For(Type containerType, ComplexFieldSpec spec) {
        try {
            var container = Expression.Parameter(containerType, "container");
            Expression? source = containerType.GetMethod("Enumerate", Type.EmptyTypes) is { } enumerate
                ? Expression.Call(container, enumerate)
                : containerType.GetProperty("Dictionary") != null || containerType.GetField("Dictionary") != null
                    ? Expression.PropertyOrField(container, "Dictionary")
                    : null;
            if (source == null || DataType(source.Type) is not { IsValueType: true } dataType) {
                return null;
            }

            return (TypedEntries) Activator.CreateInstance(
                typeof(TypedEntries<,>).MakeGenericType(containerType, dataType), container, source, spec
            )!;
        } catch (Exception) {
            return null;
        }
    }

    /// <summary>X where the type is or implements IEnumerable&lt;KeyValuePair&lt;string, X&gt;&gt;.</summary>
    private static Type? DataType(Type type) =>
        new[] { type }.Concat(type.GetInterfaces())
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(t => t.GetGenericArguments()[0])
            .Where(pair => pair.IsGenericType && pair.GetGenericTypeDefinition() == typeof(KeyValuePair<,>) &&
                           pair.GetGenericArguments()[0] == typeof(string))
            .Select(pair => pair.GetGenericArguments()[1])
            .FirstOrDefault();
}

internal sealed class TypedEntries<TContainer, TData> : TypedEntries where TData : struct {
    private readonly Func<TContainer, IEnumerable<KeyValuePair<string, TData>>> _entries;
    private readonly Dictionary<string, Func<TData, int>> _members = new();
    private readonly Func<TData, string, int?> _value;

    public TypedEntries(ParameterExpression container, Expression source, ComplexFieldSpec spec) {
        _entries = Expression.Lambda<Func<TContainer, IEnumerable<KeyValuePair<string, TData>>>>(
            Expression.Convert(source, typeof(IEnumerable<KeyValuePair<string, TData>>)), container
        ).Compile();
        var data = Expression.Parameter(typeof(TData), "data");
        foreach (var member in spec.MemberNames) {
            if (typeof(TData).GetField(member) is not { } field) {
                continue;
            }

            var read = Expression.Field(data, field);
            Expression value = field.FieldType == typeof(bool)
                ? Expression.Condition(read, Expression.Constant(1), Expression.Constant(0))
                : field.FieldType == typeof(int)
                    ? read
                    : Expression.Constant(0);
            _members[member] = Expression.Lambda<Func<TData, int>>(value, data).Compile();
        }

        _value = Value;
    }

    public override Type ContainerType => typeof(TContainer);

    private int? Value(TData data, string member) => _members.TryGetValue(member, out var read) ? read(data) : null;

    private IEnumerable<(string Entry, TData Data)> Entries(object container) {
        foreach (var pair in _entries((TContainer) container)) {
            if (pair.Key is { Length: > 0 }) {
                yield return (pair.Key, pair.Value);
            }
        }
    }

    public override bool Unchanged(object container, Projection last, ComplexFieldSpec spec) =>
        ProjectionOps.Unchanged(last, spec, Entries(container), _value);

    public override Projection Read(object container, ComplexFieldSpec spec) {
        var projection = new Projection();
        foreach (var (entry, data) in Entries(container)) {
            var members = new Dictionary<string, int>();
            foreach (var member in spec.MemberNames) {
                if (Value(data, member) is { } value) {
                    members[member] = value;
                }
            }

            projection[entry] = members;
        }

        return projection;
    }
}
