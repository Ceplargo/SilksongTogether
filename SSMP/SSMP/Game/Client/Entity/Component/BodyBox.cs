using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) When an enemy's body box has changed (BodyBoxComponent): the Bell Beast's body box is her tk2d sprite's own
/// box collider, sized and switched with each sprite frame, and the other games' copy plays her animation from its own
/// start, so under lag its box was another frame's (6.6 by 6.4 against 5.1 by 4.7) or not there for up to 1.3 s while
/// the host's hurt (NOTES 339). Values: enabled (1 or 0), size x, y, offset x, y, the root's turn (z, degrees).
/// Unity-free, so it is tested without the game.
/// </summary>
internal static class BodyBox {
    /// <summary>Units a size or offset must change to be sent again.</summary>
    public const float Step = 0.01f;

    /// <summary>Whether the box (enabled, size, offset) differs from the one last sent.</summary>
    public static bool Changed(float[]? before, float[] after) {
        if (before == null || before.Length != after.Length) return true;
        for (var i = 0; i < after.Length; i++) {
            if (System.Math.Abs(after[i] - before[i]) > Step) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the box numbered seq is newer than the last one applied (-1: none yet; numbers wrap at 65536). Entity data
    /// goes reliable: a lost packet's data is sent again later, after a newer box. The Bell Beast's copy took her box
    /// back on, sized as two frames before her burst's end, 0.13 s after it went off with the host's, and kept it
    /// underground for seconds while the host's stayed off (0.4 enemies A, `bellbeast_body` under lag, 3 of 5 runs).
    /// </summary>
    public static bool Newer(int last, ushort seq) => last < 0 || (ushort) (seq - last) is > 0 and < 32768;

    /// <summary>An older box is dropped. Not inlined: bellbeast_lag_body's twin skips it (every box applied).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool DropsStale() => true;

    /// <summary>The body box is sent. Not inlined: bellbeast_body's twin skips it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool Follows() => true;
}
