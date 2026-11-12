using System.Runtime.CompilerServices;

namespace BossRule;

/// <summary>
/// (0.3.21) A hidden area (the game's Remasker) uncovers on a trigger from the local hero only, so the area another
/// player walked into stayed covered on this screen: the doer saw it open, the other player a mask over them. Each
/// game uncovers it while another player's body is inside it, as its own hero's would, a blackout mask too (Bone_08's
/// Remasker New Sharp Ultra kept its black cover over the cave on the other screen, probe_blackout08, 2026-11-11): the
/// screen going dark around the area stays the inside hero's, as only its own Entered adds it (MaskerBlackout.AddInside).
/// Open, a blackout mask's cover turns blue, which shows the area only under that dark and without it still draws the
/// area black (probe_cave08_hide, 2026-11-11): a game whose own hero is outside it hides that cover (HidesCover).
/// </summary>
internal static class HiddenArea {
    /// <summary>Whether this game uncovers the mask for the other players: one of them is inside it.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool OpenFor(bool blackout, bool friendInside) => friendInside;

    /// <summary>Whether this game hides a blackout mask's cover sprites: open for another player while this game's own
    /// hero is outside it (inside, the game's own dark around the area needs the cover to show it).</summary>
    public static bool HidesCover(bool blackout, bool open, bool selfInside) => blackout && open && !selfInside;
}
