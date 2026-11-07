using System;
using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Action;

internal static class ScaleTween {
    // ScaleToCurves: Linear=0, QuadraticOut=1, SinusoidalOut=2; unknown values are linear in the game.
    public static float Progress(float elapsed, float delay, float duration, int curve) {
        if (elapsed < delay) return 0f;
        if (elapsed >= delay + duration) return 1f;
        var t = System.Math.Clamp((elapsed - delay) / duration, 0f, 1f);
        return curve switch {
            1 => t * (2f - t),
            2 => MathF.Sin(t * MathF.PI * .5f),
            _ => t
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ReplaysScaleTo() => true;
}
