using System;

namespace SSMP.Game.Client;

/// <summary>
/// (coop, optimization) Unity-free: whether this player's map icon may show, from the server's settings and the
/// compass. MapManager looks up the map position, a walk over the map's scene names each frame, only when it may.
/// </summary>
internal static class MapIconRule {
    /// <summary>Always shown, or shown only with the compass and the compass equipped (asked only then).</summary>
    public static bool MayShow(bool alwaysShow, bool onlyWithCompass, Func<bool> compassEquipped) =>
        alwaysShow || onlyWithCompass && compassEquipped();
}
