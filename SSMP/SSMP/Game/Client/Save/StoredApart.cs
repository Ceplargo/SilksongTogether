using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Save;

/// <summary>
/// (0.4.1) The stored entry an arriving value is written into. The game's save in a room (SSMP's autosave on the host)
/// stores each live item's own item data as the room's entry, and every read of that item's value (SSMP's poll, a load's
/// own read) writes the live value over it: an arriving value written there went back to this game's own at once
/// (0.4.1 gate, arena_after: Room_Forge's held win read "its stored win False" and the host's arena stayed shut).
/// </summary>
internal static class StoredApart {
    /// <summary>The entry to write: a copy when <paramref name="stored"/> is one of the live items' own data
    /// (<paramref name="owned"/>), which the caller stores in its place; else <paramref name="stored"/>.</summary>
    public static T Entry<T>(T stored, IEnumerable<object?> owned, Func<T, T> copy) where T : class =>
        StoresApart() && owned.Any(o => ReferenceEquals(o, stored)) ? copy(stored) : stored;

    /// <summary>The switch (its twin skips it: the value written into the item's own data, as before). Not inlined.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool StoresApart() => true;
}
