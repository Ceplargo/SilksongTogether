using System;
using System.Collections.Generic;

namespace SSMP.Util;

/// <summary>
/// (coop, optimization fix I) A set of an enum's values with HashSet's Add, Contains, Remove, Clear and Count, for an
/// update's types: each entity and player update made a HashSet, about 170 bytes with two types in it, and every
/// enemy that moves makes one each frame where it is built, read and passed on (a .NET harness). Values 0 to 63.
/// </summary>
/// <typeparam name="T">The enum type.</typeparam>
internal sealed class EnumSet<T> where T : unmanaged, Enum {
    private ulong _bits;

    public int Count {
        get {
            var count = 0;
            for (var bits = _bits; bits != 0; bits &= bits - 1) {
                count++;
            }

            return count;
        }
    }

    public bool Add(T value) {
        var bit = Bit(value);
        var added = (_bits & bit) == 0;
        _bits |= bit;
        return added;
    }

    public bool Contains(T value) => (_bits & Bit(value)) != 0;

    public bool Remove(T value) {
        var bit = Bit(value);
        var removed = (_bits & bit) != 0;
        _bits &= ~bit;
        return removed;
    }

    public void Clear() => _bits = 0;

    private static unsafe ulong Bit(T value) {
        var index = sizeof(T) switch {
            1 => *(byte*) &value,
            2 => *(ushort*) &value,
            _ => *(int*) &value
        };
        if (index is < 0 or > 63) {
            throw new ArgumentOutOfRangeException(nameof(value), value, "EnumSet holds values 0 to 63");
        }

        return 1UL << index;
    }
}
