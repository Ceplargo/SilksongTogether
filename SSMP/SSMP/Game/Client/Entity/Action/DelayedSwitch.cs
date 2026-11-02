using System;

namespace SSMP.Game.Client.Entity.Action;

/// <summary>
/// (coop) A delayed ActivateGameObject's switch after its wait, Unity-free so SSMP.Tests compiles it: nothing when its
/// object was destroyed meanwhile (the soak, 2026-10-31: the friend's game, Aqueduct_01, read a destroyed object's
/// activeSelf once in 6 hours).
/// </summary>
internal static class DelayedSwitch {
    /// <summary>
    /// <paramref name="target"/> switched to <paramref name="activate"/> if <paramref name="alive"/> (in the game,
    /// Unity's null check): whether it switched, and in <paramref name="initial"/> its active flag before.
    /// </summary>
    public static bool Run<T>(T target, Func<T, bool> alive, Func<T, bool> activeSelf, Action<T, bool> setActive,
        bool activate, out bool initial) {
        initial = false;
        if (!alive(target)) return false;
        initial = activeSelf(target);
        setActive(target, activate);
        return true;
    }
}
