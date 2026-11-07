namespace SSMP.Game.Client.Entity;

internal static class ScaleRefresh {
    // A lost unreliable turn needs another current snapshot even while the native stands still.
    public static bool Due(float now, float last) => now - last >= 0.1f && KeepsScaleFresh();

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool KeepsScaleFresh() => true;
}
