using System.Runtime.CompilerServices;

namespace SSMP.Game.Client;

/// <summary>
/// (coop, 0.3.23 world Task 13) A silk grub cocoon (the game's SilkGrubCocoon) counts each game's own hits: on the last
/// it drops one Silk Grub (a pickup with no saved id) and saves itself broken, and every hit gives that game's hero silk
/// (HeroController.SilkGain). A remote player's copied swing struck it in the other game too, so that game's hero
/// gained silk from the other player's hits, and when its copy's last hit came before the arriving broken value it
/// dropped a second grub (probe_grub_drop, Dock_14: the shared Silk Grub count 2 for one cocoon). GamePatcher plays a
/// remote hit's look but keeps the drop and the silk the hitter's game's.
/// </summary>
internal static class GrubCocoonHits {
    /// <summary>
    /// Whether this hit's drop and silk are kept off here: a remote player's. Not inlined: grub_drop's twin skips it
    /// (false, the old rule: every game's hit drops and gives silk).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool KeepsOwn(bool remote) => remote;
}
