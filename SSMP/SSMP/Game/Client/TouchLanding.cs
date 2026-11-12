namespace SSMP.Game.Client;

/// <summary>
/// (coop) Whether another player's copy stands on a platform's top, as the game's own hero lands on one: a collision
/// whose contact normal points down (TiltPlat, DustyPlatform). The copy is a trigger the platform never collides with, so
/// RemoteTouches reads its box against the platform's. Unity-free: the boxes' edges.
/// </summary>
internal static class TouchLanding {
    /// <summary>How far the copy's feet may be from the platform's top: its place comes over the network, smoothed.</summary>
    internal const float Tolerance = 0.15f;

    /// <summary>The copy's box (bottom, left, right) on the top of a platform's (top, left, right).</summary>
    internal static bool OnTop(float copyBottom, float copyLeft, float copyRight, float top, float left, float right) =>
        copyRight > left && copyLeft < right && System.Math.Abs(copyBottom - top) <= Tolerance;
}
