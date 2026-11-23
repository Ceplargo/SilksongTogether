using System;

namespace SSMP.Util;

/// <summary>
/// A helper class to cache properties of enum types to avoid repeating allocations and reflection overhead.
/// </summary>
/// <typeparam name="T">The enum type.</typeparam>
public static class EnumCache<T> where T : struct, Enum {
    /// <summary>
    /// The number of values defined in the enum.
    /// </summary>
    public static readonly int Count = Enum.GetNames(typeof(T)).Length;
}

/// <summary>
/// (coop, optimization fix H) An enum's values in Enum.GetValues' order, read once per type: Enum.GetValues made a new
/// array on every packet built or read and every bit flag written or read, and reading it as an Array boxed each value.
/// </summary>
/// <typeparam name="T">The enum type.</typeparam>
internal static class EnumValues<T> where T : Enum {
    /// <summary>The values; shared, so never written.</summary>
    public static readonly T[] All = (T[]) Enum.GetValues(typeof(T));
}
