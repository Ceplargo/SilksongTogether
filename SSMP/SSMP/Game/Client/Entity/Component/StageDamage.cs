using System.Collections.Generic;

namespace SSMP.Game.Client.Entity.Component;

/// <summary>
/// (coop) How much each hero damager of a followed object deals (its DamageHero components, its children's included,
/// in their order), one byte each: the scene host's game sends them with the object's parts, and the other games set
/// their copy's to match. An object's FSM can set the damage of a part it picked at random: each Skull King boulder's
/// Drop gives its picked sprite 2, and the other games' copy gave the 2 to its own pick while showing the host's, so
/// the shown sprite fell at 0 (boss round six's probe, 4 falls of 6).
/// </summary>
internal static class StageDamage {
    /// <summary>
    /// The most damagers an update carries; later ones are left as the copy's own FSM sets them.
    /// </summary>
    public const int Max = 64;

    /// <summary>
    /// Each damager's amount, in order (none while <see cref="Sends"/> is off).
    /// </summary>
    public static byte[] Pack(IReadOnlyList<int> amounts) {
        if (!Sends()) {
            return [];
        }

        var packed = new byte[amounts.Count < Max ? amounts.Count : Max];
        for (var i = 0; i < packed.Length; i++) {
            packed[i] = (byte) (amounts[i] < 0 ? 0 : amounts[i] > byte.MaxValue ? byte.MaxValue : amounts[i]);
        }

        return packed;
    }

    /// <summary>
    /// The amount the damager at this index deals in the scene host's game, or null if the update does not carry it.
    /// </summary>
    public static int? Of(byte[] amounts, int index) => index < amounts.Length ? amounts[index] : null;

    /// <summary>
    /// Whether an update's amounts pair with a copy's damagers by their order: as many as the copy has, up to Max. A copy
    /// lacking one of the scene host's damagers (a clone without an object its FSM let go of) would give every later one
    /// the next one's amount, so it takes none (the round's review; DamagePairs pairs an entity's boxes by path).
    /// </summary>
    public static bool Pairs(int sent, int damagers) => sent == (damagers < Max ? damagers : Max);

    /// <summary>
    /// The switch the twin turns off (a skipped bool method returns false): no amounts go out.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static bool Sends() => true;
}
