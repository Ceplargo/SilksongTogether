namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) Where another game shows an object its enemy holds, predicted along x from the scene host's last place: never
/// past the copy of the enemy that catches it once it flies toward it. When the scene host's game stalled as the Dock
/// Guard Slasher's Projectile came back, the friend's was predicted on at its last speed for up to a quarter second, in a
/// gate round 8 units past his hand.
/// </summary>
internal static class CatchPoint {
    /// <summary>
    /// The x to show for a prediction from <paramref name="from"/> to <paramref name="to"/>: the catcher's once it would
    /// pass the catcher on the way to it, else where it was predicted.
    /// </summary>
    public static float Shown(float from, float to, float catcher) => (catcher - from) * (to - catcher) > 0 ? catcher : to;
}
