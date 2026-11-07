using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity;

internal static class NativeStartup {
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool KeepsHiveRunning(EntityType type) => type == EntityType.ShellwoodHive;
}
