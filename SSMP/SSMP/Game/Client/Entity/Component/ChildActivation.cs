using System.Linq;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

internal static class ChildActivation {
    internal static bool NeedsUpdate(byte[]? before, byte[] after) =>
        SharesChildStates() && (before == null || !before.SequenceEqual(after));

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool SharesChildStates() => true;
}
