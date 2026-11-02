using System.Runtime.CompilerServices;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) The order of a component's numbered updates. SSMP sends entity data reliably: the data of a lost packet is
/// sent again later, after newer updates, and a copy given it went back to where the scene host's thing was a moment
/// before (with 1% of the friend's packets lost, the Vampire Gnat's sickle jumped 8 units back for a tenth of a second,
/// and its FSMs were given the older states again).
/// </summary>
internal static class UpdateOrder {
    /// <summary>
    /// How many numbers back an update is still a resend of one already overtaken; further back is another sender's
    /// numbering (a new scene host whose first update came before this game reset its last number).
    /// </summary>
    public const int Window = 64;

    /// <summary>
    /// Whether the update numbered <paramref name="number"/> is the last one applied (<paramref name="last"/>, -1 for
    /// none) or older: a resend, to drop.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool IsStale(ushort number, int last) => last >= 0 && (ushort) (last - number) < Window;
}
