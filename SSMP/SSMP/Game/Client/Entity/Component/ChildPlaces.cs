using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) When an enemy's children have moved within it (ChildPlacesComponent): the Rhino's Shell, its hurt box, which
/// its FSM moves with SetPositionTemp and which also comes back after a jump without any FSM action, stayed where it
/// started in the other games, up to 2.5 units off for over a second (enemies Task 6, NOTES 333). Unity-free, so it is
/// tested without the game.
/// </summary>
internal static class ChildPlaces {
    /// <summary>Units a child must move (x or y, within its parent) to be sent again.</summary>
    public const float Step = 0.01f;

    /// <summary>Whether the children's places (x, y each) differ from those last sent.</summary>
    public static bool Moved(float[]? before, float[] after) {
        if (before == null || before.Length != after.Length) return true;
        for (var i = 0; i < after.Length; i++) {
            if (System.Math.Abs(after[i] - before[i]) > Step) return true;
        }

        return false;
    }

    /// <summary>The children's places are sent. Not inlined: rhino_shell's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Follows() => true;
}
