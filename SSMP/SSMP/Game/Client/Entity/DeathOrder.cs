namespace SSMP.Game.Client.Entity;

internal static class DeathOrder {
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool FinishesDeathBeforeHide(bool active, bool dying) => !active && dying;
}
